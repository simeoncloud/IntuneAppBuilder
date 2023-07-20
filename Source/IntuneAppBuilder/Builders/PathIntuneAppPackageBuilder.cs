using System.IO;
using System.Threading.Tasks;
using IntuneAppBuilder.Domain;
using IntuneAppBuilder.Services;
using Microsoft.Graph.Beta.Models;

namespace IntuneAppBuilder.Builders
{
    /// <summary>
    ///     Builds an app package from all files in a specified directory or from a single file.
    /// </summary>
    public class PathIntuneAppPackageBuilder : IIntuneAppPackageBuilder
    {
        private readonly IIntuneAppPackagingService packagingService;

        private readonly string path;

        public PathIntuneAppPackageBuilder(string path, IIntuneAppPackagingService packagingService)
        {
            Name = Path.GetFullPath(path);
            this.path = path;
            this.packagingService = packagingService;
        }

        public string Name { get; }

        public Task<IntuneAppPackage> BuildAsync(MobileLobApp app) => packagingService.BuildPackageAsync(path);
    }
}