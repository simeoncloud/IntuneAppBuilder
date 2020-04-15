using System.IO;
using System.Threading.Tasks;
using IntuneAppBuilder.Domain;
using IntuneAppBuilder.Services;

namespace IntuneAppBuilder.Builders
{
    /// <summary>
    ///     Builds an app package from all files in a specified directory or from a single file.
    /// </summary>
    public class PathIntuneAppPackageBuilder : IIntuneAppPackageBuilder
    {
        public string Name { get; }

        private readonly string path;
        private readonly IIntuneAppPackagingService packagingService;

        public PathIntuneAppPackageBuilder(string path, IIntuneAppPackagingService packagingService)
        {
            Name = Path.GetFullPath(path);
            this.path = path;
            this.packagingService = packagingService;
        }

        public Task<IntuneAppPackage> BuildAsync() => packagingService.BuildPackageAsync(path);
    }
}