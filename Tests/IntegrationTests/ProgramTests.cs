using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using IntuneAppBuilder.IntegrationTests.Util;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Graph;
using Xunit;
using Xunit.Abstractions;
using Directory = System.IO.Directory;
using File = System.IO.File;
using FileSystemInfo = System.IO.FileSystemInfo;
using Program = IntuneAppBuilder.Console.Program;

namespace IntuneAppBuilder.IntegrationTests
{
    public sealed class ProgramTests
    {
        private readonly ITestOutputHelper testOutputHelper;

        public ProgramTests(ITestOutputHelper testOutputHelper) => this.testOutputHelper = testOutputHelper;

        [Fact]
        public async Task LargeWin32() =>
            await ExecuteInDirectory($"C:\\temp\\{nameof(Win32)}", async () =>
            {
                await DeleteAppAsync("big");

                Directory.CreateDirectory("big");

                testOutputHelper.WriteLine($"Available space: {string.Join(", ", DriveInfo.GetDrives().Where(i => i.IsReady).Select(i => $"{i.Name} - {i.AvailableFreeSpace / 1024 / 1024}MB"))}.");

                var sw = Stopwatch.StartNew();
                const int sizeInMb = 1024 * 7;
                var data = new byte[8192];
                var rng = new Random();
                using (var fs = new FileStream("big/big.exe", FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    for (var i = 0; i < sizeInMb * 128; i++)
                    {
                        rng.NextBytes(data);
                        fs.Write(data, 0, data.Length);
                    }
                }

                testOutputHelper.WriteLine($"Generated {sizeInMb}MB file in {sw.ElapsedMilliseconds / 1000} seconds.");

                await Program.PackAsync(new FileSystemInfo[] { new DirectoryInfo("big") }, ".", GetServices());

                Assert.True(File.Exists("big.intunewin"));
                Assert.True(File.Exists("big.portal.intunewin"));
                Assert.True(File.Exists("big.intunewin.json"));

                await Program.PublishAsync(new FileSystemInfo[] { new FileInfo("big.intunewin.json") }, GetServices());
                // publish second time to test updating
                await Program.PublishAsync(new FileSystemInfo[] { new FileInfo("big.intunewin.json") }, GetServices());

                await DeleteAppAsync("big");
            });

        [Fact]
        public async Task Msi() =>
            await ExecuteInDirectory(nameof(Msi), async () =>
            {
                await DeleteAppAsync("Remote Desktop");

                var http = new HttpClient();
                var tempPath = Path.Combine(Path.GetTempPath(), "wvd.msi");
                if (!File.Exists(tempPath)) await http.DownloadFileAsync("https://aka.ms/wvdclient", tempPath);
                File.Copy(tempPath, "wvd.msi");

                await Program.PackAsync(new FileSystemInfo[] { new FileInfo("wvd.msi") }, ".", GetServices());

                Assert.True(File.Exists("wvd.intunewin"));
                Assert.True(File.Exists("wvd.portal.intunewin"));
                Assert.True(File.Exists("wvd.intunewin.json"));

                await Program.PublishAsync(new FileSystemInfo[] { new FileInfo("wvd.intunewin.json") }, GetServices());
                // publish second time to test udpating
                await Program.PublishAsync(new FileSystemInfo[] { new FileInfo("wvd.intunewin.json") }, GetServices());

                await DeleteAppAsync("Remote Desktop");
            });

        [Fact]
        public async Task Win32() =>
            await ExecuteInDirectory(nameof(Win32), async () =>
            {
                await DeleteAppAsync("Remote Desktop");

                var http = new HttpClient();
                var tempPath = Path.Combine(Path.GetTempPath(), "wvd.msi");
                if (!File.Exists(tempPath)) await http.DownloadFileAsync("https://aka.ms/wvdclient", tempPath);
                Directory.CreateDirectory("wvd");
                File.Copy(tempPath, "wvd/wvd.msi");

                await Program.PackAsync(new FileSystemInfo[] { new DirectoryInfo("wvd") }, ".", GetServices());

                Assert.True(File.Exists("wvd.intunewin"));
                Assert.True(File.Exists("wvd.portal.intunewin"));
                Assert.True(File.Exists("wvd.intunewin.json"));

                await Program.PublishAsync(new FileSystemInfo[] { new FileInfo("wvd.intunewin.json") }, GetServices());
                // publish second time to test udpating
                await Program.PublishAsync(new FileSystemInfo[] { new FileInfo("wvd.intunewin.json") }, GetServices());

                await DeleteAppAsync("Remote Desktop");
            });

        private async Task DeleteAppAsync(string name)
        {
            var graph = GetServices().BuildServiceProvider().GetRequiredService<GraphServiceClient>();
            var apps = (await graph.DeviceAppManagement.MobileApps.Request().Filter($"displayName eq '{name}'").GetAsync()).OfType<MobileLobApp>();
            foreach (var app in apps)
            {
                await graph.DeviceAppManagement.MobileApps[app.Id].Request()
                    .WithMaxRetry(3).WithShouldRetry((_, _, r) => !r.IsSuccessStatusCode)
                    .DeleteAsync();
            }
        }

        private IServiceCollection GetServices() =>
            Program.GetServices()
                .AddSingleton(_ => new GraphServiceClient(new EnvironmentVariableUsernamePasswordProvider()));

        private static async Task ExecuteInDirectory(string path, Func<Task> action)
        {
            new DirectoryInfo(path).CreateEmptyDirectory();

            var cd = Environment.CurrentDirectory;
            try
            {
                Environment.CurrentDirectory = path;
                await action();
            }
            finally
            {
                Environment.CurrentDirectory = cd;
                try
                {
                    Directory.Delete(path, true);
                }
                catch (IOException)
                {
                    Thread.Sleep(5000);
                    Directory.Delete(path, true);
                }
            }
        }
    }
}