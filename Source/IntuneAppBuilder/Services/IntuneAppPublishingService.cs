using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Azure;
using IntuneAppBuilder.Builders;
using IntuneAppBuilder.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Graph.Beta;
using Microsoft.Graph.Beta.Models;
using Microsoft.Kiota.Http.HttpClientLibrary.Middleware.Options;
using Azure.Storage.Blobs.Specialized;

namespace IntuneAppBuilder.Services
{
    internal sealed class IntuneAppPublishingService : IIntuneAppPublishingService
    {
        private readonly ILogger logger;
        private readonly GraphServiceClient msGraphClient;

        public IntuneAppPublishingService(ILogger<IntuneAppPublishingService> logger, GraphServiceClient msGraphClient)
        {
            this.logger = logger;
            this.msGraphClient = msGraphClient;
        }

        public async Task PublishAsync(IntuneAppPackage package)
        {
            logger.LogInformation($"Publishing Intune app package for {package.App.DisplayName}.");

            var app = await GetAppAsync(package.App);

            var sw = Stopwatch.StartNew();

            var requestBuilder = msGraphClient.DeviceAppManagement.MobileApps[app.Id];
            var contentVersionsRequestBuilder = requestBuilder.ContentVersions(app.OdataType.TrimStart('#'));

            MobileAppContent content = null;

            // if content has never been committed, need to use last created content if one exists, otherwise an error is thrown
            if (app.CommittedContentVersion == null) content = (await contentVersionsRequestBuilder.GetAsync(requestConfiguration => requestConfiguration.QueryParameters.Orderby = new[] { "id desc" }))!.Value!.FirstOrDefault();

            content ??= await contentVersionsRequestBuilder.PostAsync(new MobileAppContent());

            // manifests are only supported if the app is a WindowsMobileMSI (not a Win32 app installing an msi)
            if (!(app is WindowsMobileMSI)) package.File.Manifest = null;

            await CreateAppContentFileAsync(contentVersionsRequestBuilder[content!.Id], package);

            var update = (MobileLobApp)Activator.CreateInstance(app.GetType());
            update!.CommittedContentVersion = content.Id;
            await msGraphClient.DeviceAppManagement.MobileApps[app.Id].PatchAsync(update);

            logger.LogInformation($"Published Intune app package for {app.DisplayName} in {sw.ElapsedMilliseconds}ms.");
        }

        private async Task<MobileAppContentFile> AddContentFileAsync(MobileAppContentRequestBuilder requestBuilder, IntuneAppPackage package) =>
            await requestBuilder.Files.PostAsync(package.File, requestConfiguration => requestConfiguration.Options.Add(new RetryHandlerOption
            {
                MaxRetry = 10,
                Delay = 30,
                ShouldRetry = (_, _, message) => message.StatusCode == HttpStatusCode.NotFound
            }));

        private async Task CreateAppContentFileAsync(MobileAppContentRequestBuilder requestBuilder, IntuneAppPackage package)
        {
            // add content file
            var contentFile = await AddContentFileAsync(requestBuilder, package);

            // refetch until we can get the uri to upload to
            contentFile = await WaitForStateAsync(requestBuilder.Files[contentFile.Id], MobileAppContentFileUploadState.AzureStorageUriRequestSuccess);

            var sw = Stopwatch.StartNew();

            await CreateBlobAsync(package, contentFile, requestBuilder.Files[contentFile.Id]);

            logger.LogInformation($"Uploaded app content file in {sw.ElapsedMilliseconds}ms.");

            // commit
            await requestBuilder.Files[contentFile.Id].Commit.PostAsync(new MobileAppContentFileCommitRequest { FileEncryptionInfo = package.EncryptionInfo });

            // refetch until has committed
            await WaitForStateAsync(requestBuilder.Files[contentFile.Id], MobileAppContentFileUploadState.CommitFileSuccess);
        }

        private async Task CreateBlobAsync(IntuneAppPackage package, MobileAppContentFile contentFile, MobileAppContentFileRequestBuilder contentFileRequestBuilder)
        {
            var blockCount = 0;
            var blockIds = new List<string>();

            const int chunkSize = 25 * 1024 * 1024;
            package.Data.Seek(0, SeekOrigin.Begin);
            var lastBlockId = (Math.Ceiling((double)package.Data.Length / chunkSize) - 1).ToString("0000");
            var sw = Stopwatch.StartNew();
            foreach (var chunk in Chunk(package.Data, chunkSize, false))
            {
                if (sw.ElapsedMilliseconds >= 450000)
                {
                    contentFile = await RenewStorageUri(contentFileRequestBuilder);
                    sw.Restart();
                }

                var blockId = blockCount++.ToString("0000");
                logger.LogInformation($"Uploading block {blockId} of {lastBlockId} to {contentFile.AzureStorageUri}.");

                await using (var ms = new MemoryStream(chunk))
                {
                    try
                    {
                        await TryPutBlockAsync(contentFile, blockId, ms);
                    }
                    catch (RequestFailedException ex) when (ex.Status == 403)
                    {
                        // normally the timer should account for renewing upload URIs, but the Intune APIs are fundamentally unstable and sometimes 403s will be encountered randomly
                        contentFile = await RenewStorageUri(contentFileRequestBuilder);
                        sw.Restart();
                        await TryPutBlockAsync(contentFile, blockId, ms);
                    }
                }

                blockIds.Add(blockId);
            }

            await new BlockBlobClient(new Uri(contentFile.AzureStorageUri)).CommitBlockListAsync(blockIds);
        }

        /// <summary>
        ///     Gets an existing or creates a new app.
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        private async Task<MobileLobApp> GetAppAsync(MobileLobApp app)
        {
            MobileLobApp result;
            if (Guid.TryParse(app.Id, out var _))
                // resolve from id
            {
                result = await msGraphClient.DeviceAppManagement.MobileApps[app.Id].GetAsync() as MobileLobApp ?? throw new ArgumentException($"App {app.Id} should be a {nameof(MobileLobApp)}.", nameof(app));
            }
            else
            {
                // resolve from name
                result = (await msGraphClient.DeviceAppManagement.MobileApps.GetAsync(requestConfiguration => requestConfiguration.QueryParameters.Filter = $"displayName eq '{app.DisplayName}'"))?.Value?.OfType<MobileLobApp>().FirstOrDefault();
            }

            if (result == null)
            {
                SetDefaults(app);
                // create new
                logger.LogInformation($"App {app.DisplayName} does not exist - creating new app.");
                result = (MobileLobApp)await msGraphClient.DeviceAppManagement.MobileApps.PostAsync(app);
            }

            if (app.OdataType.TrimStart('#') != result.OdataType.TrimStart('#'))
            {
                throw new NotSupportedException($"Found existing application {result.DisplayName}, but it of type {result.OdataType.TrimStart('#')} and the app being deployed is of type {app.OdataType.TrimStart('#')} - delete the existing app and try again.");
            }

            logger.LogInformation($"Using app {result.Id} ({result.DisplayName}).");

            return result;
        }

        private async Task<MobileAppContentFile> RenewStorageUri(MobileAppContentFileRequestBuilder contentFileRequestBuilder)
        {
            logger.LogInformation($"Renewing SAS URI for {contentFileRequestBuilder.ToGetRequestInformation().URI}.");
            await contentFileRequestBuilder.RenewUpload.PostAsync();
            return await WaitForStateAsync(contentFileRequestBuilder, MobileAppContentFileUploadState.AzureStorageUriRenewalSuccess);
        }

        private async Task TryPutBlockAsync(MobileAppContentFile contentFile, string blockId, Stream stream)
        {
            var attemptCount = 0;
            var position = stream.Position;
            while (true)
                try
                {
                    await new BlockBlobClient(new Uri(contentFile.AzureStorageUri)).StageBlockAsync(blockId, stream, null);
                    break;
                }
                catch (RequestFailedException ex)
                {
                    if (!new[] { 307, 403, 400 }.Contains(ex.Status) || attemptCount++ > 30) throw;
                    logger.LogInformation($"Encountered retryable error ({ex.Status}) uploading blob to {contentFile.AzureStorageUri} - will retry in 10 seconds.");
                    stream.Position = position;
                    await Task.Delay(10000);
                }
        }

        // waits for the desired status, refreshing the file along the way
        private async Task<MobileAppContentFile> WaitForStateAsync(MobileAppContentFileRequestBuilder contentFileRequestBuilder, MobileAppContentFileUploadState state)
        {
            logger.LogInformation($"Waiting for app content file to have a state of {state}.");

            var waitStopwatch = Stopwatch.StartNew();

            while (true)
            {
                var contentFile = await contentFileRequestBuilder.GetAsync();

                if (contentFile.UploadState == state)
                {
                    logger.LogInformation($"Waited {waitStopwatch.ElapsedMilliseconds}ms for app content file to have a state of {state}.");
                    return contentFile;
                }

                var failedStates = new[]
                {
                    MobileAppContentFileUploadState.AzureStorageUriRequestFailed,
                    MobileAppContentFileUploadState.AzureStorageUriRenewalFailed,
                    MobileAppContentFileUploadState.CommitFileFailed
                };

                if (failedStates.Contains(contentFile.UploadState.GetValueOrDefault())) throw new InvalidOperationException($"{nameof(contentFile.UploadState)} is in a failed state of {contentFile.UploadState} - was waiting for {state}.");
                const int waitTimeout = 600000;
                const int testInterval = 2000;
                if (waitStopwatch.ElapsedMilliseconds > waitTimeout) throw new InvalidOperationException($"Timed out waiting for {nameof(contentFile.UploadState)} of {state} - current state is {contentFile.UploadState}.");
                await Task.Delay(testInterval);
            }
        }

        /// <summary>
        ///     Chunks a stream into buffers.
        /// </summary>
        private static IEnumerable<byte[]> Chunk(Stream source, int chunkSize, bool disposeSourceStream = true)
        {
            var buffer = new byte[chunkSize];

            try
            {
                int bytesRead;
                while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
                {
                    var chunk = new byte[bytesRead];
                    Array.Copy(buffer, chunk, chunk.Length);
                    yield return chunk;
                }
            }
            finally
            {
                if (disposeSourceStream) source.Dispose();
            }
        }

        /// <summary>
        ///     Gets a copy of the app with default values for null properties that are required.
        /// </summary>
        /// <param name="app"></param>
        private static void SetDefaults(MobileLobApp app)
        {
            if (app is Win32LobApp win32)
            {
                SetDefaults(win32);
            }
        }

        private static void SetDefaults(Win32LobApp app)
        {
            // set required properties with default values if not already specified - can be changed later in the portal
            app.InstallExperience ??= new Win32LobAppInstallExperience { RunAsAccount = RunAsAccountType.System };
            app.InstallCommandLine ??= app.MsiInformation == null ? app.SetupFilePath : $"msiexec /i \"{app.SetupFilePath}\"";
            app.UninstallCommandLine ??= app.MsiInformation == null ? "echo Not Supported" : $"msiexec /x \"{app.MsiInformation.ProductCode}\"";
            if (app.DetectionRules == null)
            {
                if (app.MsiInformation == null)
                {
                    // no way to infer - use empty PS script
                    app.DetectionRules = new List<Win32LobAppDetection>
                    {
                        new Win32LobAppPowerShellScriptDetection
                        {
                            ScriptContent = Convert.ToBase64String(new byte[0])
                        }
                    };
                }
                else
                {
                    app.DetectionRules = new List<Win32LobAppDetection>
                    {
                        new Win32LobAppProductCodeDetection
                        {
                            ProductCode = app.MsiInformation.ProductCode
                        }
                    };
                }
            }
        }
    }
}