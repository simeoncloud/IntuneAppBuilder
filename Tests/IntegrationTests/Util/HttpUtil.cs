using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace IntuneAppBuilder.IntegrationTests.Util
{
    public static class HttpUtil
    {
        /// <summary>
        ///     Downloads a file using an HttpClient.
        /// </summary>
        public static async Task DownloadFileAsync(this HttpClient client, string uri, string filePath = null) => await client.DownloadFileAsync(new HttpRequestMessage(HttpMethod.Get, uri), filePath);

        /// <summary>
        ///     Downloads a file using an HttpClient.
        /// </summary>
        private static async Task DownloadFileAsync(this HttpClient client, HttpRequestMessage request, string filePath = null)
        {
            await using (Stream contentStream = await (await client.SendAsync(request)).EnsureSuccessStatusCode().Content.ReadAsStreamAsync(),
                fileStream = new FileStream(filePath ?? Path.GetFileName(request.RequestUri.LocalPath), FileMode.Create, FileAccess.Write, FileShare.None, 1024, true))
            {
                await contentStream.CopyToAsync(fileStream);
            }
        }
    }
}