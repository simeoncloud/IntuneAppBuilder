﻿using System.IO;
using System.Threading.Tasks;
using IntuneAppBuilder.Domain;
using IntuneAppBuilder.Services;

namespace IntuneAppBuilder.Builders
{
    /// <summary>
    ///     Builds an app package from all files in a specified directory or from a single file.
    /// </summary>
    public class PathIntuneAppContentBuilder : IIntuneAppContentBuilder
    {
        public string Name { get; }

        private readonly string path;
        private readonly IIntuneAppPackagingService packagingService;

        public PathIntuneAppContentBuilder(string path, IIntuneAppPackagingService packagingService, string name = null)
        {
            Name = name ?? Path.GetFileNameWithoutExtension(Path.GetFullPath(path));
            this.path = path;
            this.packagingService = packagingService;
        }

        public Task<MobileLobAppContentFilePackage> BuildAsync() => packagingService.BuildPackageAsync(path);
    }
}