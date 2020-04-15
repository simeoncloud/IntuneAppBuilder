using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using IntuneAppBuilder.IntegrationTests.Util;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Xunit;
using Xunit.Abstractions;
using Directory = System.IO.Directory;
using File = System.IO.File;
using FileSystemInfo = System.IO.FileSystemInfo;

namespace IntuneAppBuilder.IntegrationTests
{
    public class ConsoleTests
    {
        public ConsoleTests(ITestOutputHelper testOutputHelper) => this.testOutputHelper = testOutputHelper;

        private readonly ITestOutputHelper testOutputHelper;

        private IServiceCollection GetServices() =>
            Program.GetServices()
                .AddLogging(b => b.AddProvider(new XunitLoggerProvider(testOutputHelper)))
                .AddSingleton<IGraphServiceClient>(sp => new GraphServiceClient(new EnvironmentVariableUsernamePasswordProvider()));

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
                Directory.Delete(path, true);
            }
        }

        private async Task DeleteAppAsync(string name)
        {
            var graph = GetServices().BuildServiceProvider().GetRequiredService<IGraphServiceClient>();
            var apps = (await graph.DeviceAppManagement.MobileApps.Request().Filter($"displayName eq '{name}'").GetAsync()).OfType<MobileLobApp>();
            foreach (var app in apps)
            {
                await graph.DeviceAppManagement.MobileApps[app.Id].Request()
                    .WithMaxRetry(3).WithShouldRetry((d, a, r) => true)
                    .DeleteAsync();
            }
        }

        [Fact]
        public async Task Msi()
        {
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
            });
        }

        [Fact]
        public async Task Win32()
        {
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
            });
        }
    }
}