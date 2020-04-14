﻿using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using IntuneAppBuilder.Builders;
using IntuneAppBuilder.Domain;
using IntuneAppBuilder.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace IntuneAppBuilder
{
    internal static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            if (!args.Any()) args = new[] { "--help" };

            var pack = new Command("pack")
            {
                new Option<FileSystemInfo[]>(new[] {"--source", "-s"},
                        "Specifies a source to package. May be a directory with files for a Win32 app or a single msi file. May be specified multiple times.")
                    {Name = "sources", Required = true},
                new Option<string>(new[] {"--output", "-o"},
                    "Specifies an output directory for packaging artifacts. Each packaged application will exist as a raw intunewin file, a portal-ready portal.intunewin file, and an intunewin.json file containing metadata. Defaults to the working directory.")
            };
            ((Option)pack.Children["output"]).Argument.SetDefaultValue(".");
            pack.Handler = CommandHandler.Create(typeof(Program).GetMethod(nameof(PackAsync), BindingFlags.Static | BindingFlags.NonPublic));

            var publish = new Command("publish")
            {
                new Option<string[]>(new[] {"--source", "-s"},
                    "Specifies a source to publish. May be a directory with *.intunewin.json files or a single json file")
                    {Name = "sources", Required = true}
            };
            publish.Handler = CommandHandler.Create(typeof(Program).GetMethod(nameof(PublishAsync), BindingFlags.Static | BindingFlags.NonPublic));

            var root = new RootCommand
            {
                pack,
                publish
            };
            root.TreatUnmatchedTokensAsErrors = true;

            return await root.InvokeAsync(args);
        }

        internal static async Task PackAsync(FileSystemInfo[] sources, string output, IServiceCollection services = null)
        {
            services ??= GetServices();

            output = Path.GetFullPath(output);

            AddBuilders(sources, services);

            var sp = services.BuildServiceProvider();
            foreach (var builder in sp.GetRequiredService<IEnumerable<IIntuneAppContentBuilder>>()) await BuildAsync(builder, sp.GetRequiredService<IIntuneAppPackagingService>(), output, GetLogger(sp));
        }

        /// <summary>
        ///     Invokes the builder in a dedicated working directory.
        /// </summary>
        private static async Task BuildAsync(IIntuneAppContentBuilder builder, IIntuneAppPackagingService packagingService, string output, ILogger logger)
        {
            logger.LogInformation($"Building app content for {builder.Name}.");
            var cd = Environment.CurrentDirectory;
            Environment.CurrentDirectory = output;
            try
            {
                var package = await builder.BuildAsync();
                package.Data.Position = 0;

                File.WriteAllText($"{package.App.FileName}.json", JsonConvert.SerializeObject(package, Formatting.Indented));

                await using (var fs = File.Open(package.App.FileName, FileMode.Create, FileAccess.Write, FileShare.Read))
                    await package.Data.CopyToAsync(fs);

                await using (var fs = File.Open($"{Path.GetFileNameWithoutExtension(package.App.FileName)}.portal.intunewin", FileMode.Create, FileAccess.Write, FileShare.Read))
                    await packagingService.BuildPackageForPortalAsync(package, fs);
            }
            finally
            {
                Environment.CurrentDirectory = cd;
            }
        }

        /// <summary>
        ///     Registers the correct builder type for each source from the command line.
        /// </summary>
        /// <param name="sources"></param>
        /// <param name="services"></param>
        internal static void AddBuilders(IEnumerable<FileSystemInfo> sources, IServiceCollection services)
        {
            var logger = GetLogger(services.BuildServiceProvider());
            foreach (var source in sources)
            {
                if (!source.Exists) throw new InvalidOperationException($"{source.FullName} does not exist.");

                if (new[] { ".dll", ".exe" }.Contains(source.Extension.ToLower()) && source.Exists)
                {
                    // assembly source
                    logger.LogInformation($"Loading assembly {source}.");
                    var a = Assembly.LoadFrom(source.FullName);
                    foreach (var type in a.GetTypes().Where(t => !t.IsAbstract && typeof(IIntuneAppContentBuilder).IsAssignableFrom(t)))
                    {
                        logger.LogInformation($"Registering {nameof(IIntuneAppContentBuilder)} {type.Name}.");
                        services.AddTransient(typeof(IIntuneAppContentBuilder), type);
                    }
                }
                else if (source.Extension.Equals(".msi", StringComparison.OrdinalIgnoreCase) || source is DirectoryInfo)
                {
                    services.AddSingleton<IIntuneAppContentBuilder>(sp => ActivatorUtilities.CreateInstance<PathIntuneAppContentBuilder>(sp, source.FullName));
                }
                else
                {
                    throw new InvalidOperationException($"{source} is not a supported packaging source.");
                }
            }
        }

        internal static async Task PublishAsync(FileSystemInfo[] sources, IServiceCollection services = null)
        {
            services ??= GetServices();
            var sp = services.BuildServiceProvider();
            var publishingService = sp.GetRequiredService<IIntuneAppPublishingService>();
            var logger = GetLogger(sp);

            var sourceFiles = new List<FileInfo>(sources.OfType<FileInfo>());
            sourceFiles.AddRange(sources.OfType<DirectoryInfo>().SelectMany(di => di.EnumerateFiles("*.intunewin.json", SearchOption.AllDirectories)));
            foreach (var file in sourceFiles)
            {
                logger.LogInformation($"Publishing package from file {file.FullName}.");
                using var package = ReadPackage(file, logger);
                await publishingService.PublishAsync(package);
                logger.LogInformation("Done publishing package.");
            }
        }

        private static MobileLobAppContentFilePackage ReadPackage(FileInfo file, ILogger logger)
        {
            var package = JsonConvert.DeserializeObject<MobileLobAppContentFilePackage>(File.ReadAllText(file.FullName));
            var dataPath = Path.Combine(file.DirectoryName, package.App.FileName);
            if (!File.Exists(dataPath)) throw new FileNotFoundException($"Could not find data file at {dataPath}.");
            logger.LogInformation($"Using package data file {dataPath}");
            package.Data = File.Open(package.App.FileName, FileMode.Open, FileAccess.Read, FileShare.Read);
            return package;
        }

        internal static IServiceCollection GetServices()
        {
            var services = new ServiceCollection();
            services.AddIntuneAppBuilder();
            services.AddLogging(builder =>
            {
                // don't write info for HttpClient
                builder.AddFilter((category, level) => category.StartsWith("System.Net.Http.HttpClient") ? level >= LogLevel.Warning : level >= LogLevel.Information);
                builder.AddConsole();
            });
            return services;
        }

        private static ILogger GetLogger(IServiceProvider sp) => sp.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(Program));

    }
}