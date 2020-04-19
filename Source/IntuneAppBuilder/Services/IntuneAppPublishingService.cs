using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using IntuneAppBuilder.Domain;
using IntuneAppBuilder.Util;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace IntuneAppBuilder.Services
{
    /// <inheritdoc />
    internal class IntuneAppPublishingService : IIntuneAppPublishingService
    {
        private readonly ILogger logger;
        private readonly IGraphServiceClient msGraphClient;

        public IntuneAppPublishingService(ILogger<IntuneAppPublishingService> logger, IGraphServiceClient msGraphClient)
        {
            this.logger = logger;
            this.msGraphClient = msGraphClient;
        }

        /// <inheritdoc />
        public async Task PublishAsync(IntuneAppPackage package)
        {
            logger.LogInformation($"Publishing Intune app package for {package.App.DisplayName}.");

            var app = await GetAppAsync(package.App);

            var sw = Stopwatch.StartNew();

            var requestBuilder = new MobileLobAppRequestBuilder(msGraphClient.DeviceAppManagement.MobileApps[app.Id]
                .AppendSegmentToRequestUrl(app.ODataType.TrimStart('#')), msGraphClient);

            MobileAppContent content = null;

            // if content has never been committed, need to use last created content if one exists, otherwise an error is thrown
            if (app.CommittedContentVersion == null) content = (await requestBuilder.ContentVersions.Request().OrderBy("id desc").GetAsync()).FirstOrDefault();

            if (content == null) content = await requestBuilder.ContentVersions.Request().AddAsync(new MobileAppContent());
            else if ((await requestBuilder.ContentVersions[content.Id].Files.Request().Filter("isCommitted ne true").GetAsync()).Any())
                // partially committed content - delete that content version
                await requestBuilder.ContentVersions[content.Id].Request().DeleteAsync();

            // manifests are only supported if the app is a WindowsMobileMSI (not a Win32 app installing an msi)
            if (!(app is WindowsMobileMSI)) package.File.Manifest = null;

            await CreateAppContentFileAsync(requestBuilder.ContentVersions[content.Id], package);

            MobileLobApp update = (MobileLobApp)Activator.CreateInstance(package.App.GetType());
            update.CommittedContentVersion = content.Id;
            await msGraphClient.DeviceAppManagement.MobileApps[app.Id].Request().UpdateAsync(update);

            logger.LogInformation($"Published Intune app package for {app.DisplayName} in {sw.ElapsedMilliseconds}ms.");
        }

        /// <summary>
        /// Gets an existing or creates a new app.
        /// </summary>
        /// <param name="app"></param>
        /// <returns></returns>
        private async Task<MobileLobApp> GetAppAsync(MobileLobApp app)
        {
            MobileLobApp result;
            if (Guid.TryParse(app.Id, out var _))
                // resolve from id
                result = await msGraphClient.DeviceAppManagement.MobileApps[app.Id].Request().GetAsync() as MobileLobApp ?? throw new ArgumentException($"App {app.Id} should be a {nameof(MobileLobApp)}.", nameof(app));
            else
            {
                // resolve from name
                result = (await msGraphClient.DeviceAppManagement.MobileApps.Request().Filter($"displayName eq '{app.DisplayName}'").GetAsync()).OfType<MobileLobApp>().FirstOrDefault();
            }

            if (result == null)
            {
                SetDefaults(app);
                // create new
                logger.LogInformation($"App {app.DisplayName} does not exist - creating new app.");
                result = (MobileLobApp)await msGraphClient.DeviceAppManagement.MobileApps.Request().AddAsync(app);
            }

            if (app.ODataType.TrimStart('#') != result.ODataType.TrimStart('#'))
            {
                throw new NotSupportedException($"Found existing application {result.DisplayName}, but it of type {result.ODataType.TrimStart('#')} and the app being deployed is of type {app.ODataType.TrimStart('#')} - delete the existing app and try again.");
            }

            logger.LogInformation($"Using app {result.Id} ({result.DisplayName}).");

            return result;
        }

        /// <summary>
        /// Gets a copy of the app with default values for null properties that are required.
        /// </summary>
        /// <param name="app"></param>
        private static void SetDefaults(MobileLobApp app)
        {
            if (app is Win32LobApp win32)
            {
                // set required properties with default values if not already specified - can be changed later in the portal
                win32.InstallExperience ??= new Win32LobAppInstallExperience { RunAsAccount = RunAsAccountType.System };
                win32.InstallCommandLine ??= win32.MsiInformation == null ? win32.SetupFilePath : $"msiexec /i \"{win32.SetupFilePath}\"";
                win32.UninstallCommandLine ??= win32.MsiInformation == null ? "echo Not Supported" : $"msiexec /x \"{win32.MsiInformation.ProductCode}\"";
                win32.Publisher ??= "-";
                win32.ApplicableArchitectures = WindowsArchitecture.X86 | WindowsArchitecture.X64;
                win32.MinimumSupportedOperatingSystem ??= new WindowsMinimumOperatingSystem { V10_1607 = true };
                if (win32.DetectionRules == null)
                {
                    if (win32.MsiInformation == null)
                    {
                        // no way to infer - use empty PS script
                        win32.DetectionRules = new[]
                        {
                            new Win32LobAppPowerShellScriptDetection
                            {
                                ScriptContent = Convert.ToBase64String(new byte[0])
                            }
                        };
                    }
                    else
                    {
                        win32.DetectionRules = new[]
                        {
                            new Win32LobAppProductCodeDetection
                            {
                                ProductCode = win32.MsiInformation.ProductCode
                            }
                        };
                    }
                }
            }
        }

        private async Task CreateAppContentFileAsync(IMobileAppContentRequestBuilder requestBuilder, IntuneAppPackage package)
        {
            // add content file
            var contentFile = await AddContentFileAsync(requestBuilder, package);

            // waits for the desired status, refreshing the file along the way
            async Task WaitForStateAsync(MobileAppContentFileUploadState state)
            {
                logger.LogInformation($"Waiting for app content file to have a state of {state}.");

                // ReSharper disable AccessToModifiedClosure - intended

                var waitStopwatch = Stopwatch.StartNew();

                while (true)
                {
                    contentFile = await requestBuilder.Files[contentFile.Id].Request().GetAsync();

                    if (contentFile.UploadState == state)
                    {
                        logger.LogInformation($"Waited {waitStopwatch.ElapsedMilliseconds}ms for app content file to have a state of {state}.");
                        return;
                    }

                    var failedStates = new[]
                    {
                        MobileAppContentFileUploadState.AzureStorageUriRequestFailed,
                        MobileAppContentFileUploadState.AzureStorageUriRenewalFailed,
                        MobileAppContentFileUploadState.CommitFileFailed
                    };

                    if (failedStates.Contains(contentFile.UploadState.GetValueOrDefault())) throw new InvalidOperationException($"{nameof(contentFile.UploadState)} is in a failed state of {contentFile.UploadState}.");
                    const int waitTimeout = 240000;
                    const int testInterval = 2000;
                    if (waitStopwatch.ElapsedMilliseconds > waitTimeout) throw new InvalidOperationException($"Timed out waiting for {nameof(contentFile.UploadState)} of {state} - current state is {contentFile.UploadState}.");
                    await Task.Delay(testInterval);
                }
                // ReSharper restore AccessToModifiedClosure
            }

            // refetch until we can get the uri to upload to
            await WaitForStateAsync(MobileAppContentFileUploadState.AzureStorageUriRequestSuccess);

            var sw = Stopwatch.StartNew();

            await CreateBlobAsync(package, contentFile);

            logger.LogInformation($"Uploaded app content file in {sw.ElapsedMilliseconds}ms.");

            // commit
            await requestBuilder.Files[contentFile.Id].Commit(package.EncryptionInfo).Request().PostAsync();

            // refetch until has committed
            await WaitForStateAsync(MobileAppContentFileUploadState.CommitFileSuccess);
        }

        private async Task CreateBlobAsync(IntuneAppPackage package, MobileAppContentFile contentFile)
        {
            var blockCount = 0;
            var blockIds = new List<string>();

            const int chunkSize = 5 * 1024 * 1024;
            package.Data.Seek(0, SeekOrigin.Begin);
            var lastBlockId = (Math.Ceiling((double)package.Data.Length / chunkSize) - 1).ToString("0000");
            foreach (var chunk in Chunk(package.Data, chunkSize, false))
            {
                var blockId = blockCount++.ToString("0000");
                logger.LogInformation($"Uploading block {blockId} of {lastBlockId} to {contentFile.AzureStorageUri}.");

                await using (var ms = new MemoryStream(chunk))
                {
                    await TryPutBlockAsync(contentFile, blockId, ms);
                }

                blockIds.Add(blockId);
            }

            await new CloudBlockBlob(new Uri(contentFile.AzureStorageUri)).PutBlockListAsync(blockIds);
        }

        private static async Task TryPutBlockAsync(MobileAppContentFile contentFile, string blockId, Stream stream)
        {
            var attemptCount = 0;
            var position = stream.Position;
            while (true)
                try
                {
                    await new CloudBlockBlob(new Uri(contentFile.AzureStorageUri)).PutBlockAsync(blockId, stream, null);
                    break;
                }
                catch (StorageException ex)
                {
                    if (!new[] { 307, 403, 400 }.Contains(ex.RequestInformation.HttpStatusCode) || attemptCount++ > 30) throw;
                    stream.Position = position;
                    await Task.Delay(10000);
                }
        }

        private async Task<MobileAppContentFile> AddContentFileAsync(IMobileAppContentRequestBuilder requestBuilder, IntuneAppPackage package)
        {
            return await requestBuilder.Files.Request()
                .WithMaxRetry(10)
                .WithRetryDelay(30)
                .WithShouldRetry((delay, count, r) => r.StatusCode == HttpStatusCode.NotFound)
                .AddAsync(package.File);
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
    }
}