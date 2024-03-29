﻿using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using IntuneAppBuilder.Builders;
using IntuneAppBuilder.Domain;
using IntuneAppBuilder.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Kiota.Serialization.Json;

namespace IntuneAppBuilder.Console
{
    internal static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            if (!args.Any()) args = new[] { "--help" };

            var pack = new Command("pack")
            {
                new Option<FileSystemInfo[]>(new[] { "--source", "-s" },
                        "Specifies a source to package. May be a directory with files for a Win32 app or a single msi file. May be specified multiple times.")
                    { Name = "sources", IsRequired = true },
                new Option<string>(new[] { "--output", "-o" }, () => ".",
                    "Specifies an output directory for packaging artifacts. Each packaged application will exist as a raw intunewin file, a portal-ready portal.intunewin file, and an intunewin.json file containing metadata. Defaults to the working directory.")
            };
#pragma warning disable S3011
            pack.Handler = CommandHandler.Create(typeof(Program).GetMethod(nameof(PackAsync), BindingFlags.Static | BindingFlags.NonPublic)!);
#pragma warning restore S3011

            var publish = new Command("publish")
            {
                new Option<string[]>(new[] { "--source", "-s" },
                        "Specifies a source to publish. May be a directory with *.intunewin.json files or a single json file")
                    { Name = "sources", IsRequired = true },
                new Option<string>(new[] { "--token", "-t" },
                        "Specifies an access token to use when publishing.")
                    { Name = "token", IsRequired = false }
            };
#pragma warning disable S3011
            publish.Handler = CommandHandler.Create(typeof(Program).GetMethod(nameof(PublishAsync), BindingFlags.Static | BindingFlags.NonPublic)!);
#pragma warning restore S3011

            var root = new RootCommand
            {
                pack,
                publish
            };
            root.TreatUnmatchedTokensAsErrors = true;

            return await root.InvokeAsync(args);
        }

        internal static IServiceCollection GetServices(string token = null)
        {
            var services = new ServiceCollection();
            services.AddIntuneAppBuilder(token);
            services.AddLogging(builder =>
            {
                // don't write info for HttpClient
                builder.AddFilter((category, level) => category.StartsWith("System.Net.Http.HttpClient") ? level >= LogLevel.Warning : level >= LogLevel.Information);
                builder.AddConsole();
            });
            return services;
        }

        internal static async Task PackAsync(FileSystemInfo[] sources, string output, IServiceCollection services = null)
        {
            services ??= GetServices();

            output = Path.GetFullPath(output);

            AddBuilders(sources, services);

            var sp = services.BuildServiceProvider();
            foreach (var builder in sp.GetRequiredService<IEnumerable<IIntuneAppPackageBuilder>>()) await BuildAsync(builder, sp.GetRequiredService<IIntuneAppPackagingService>(), output, GetLogger(sp));
        }

        internal static async Task PublishAsync(FileSystemInfo[] sources, string token = null, IServiceCollection services = null)
        {
            if (token != null && services != null)
            {
                throw new ArgumentException($"Cannot specify both {nameof(token)} and {nameof(services)}.");
            }

            services ??= GetServices(token);
            var sp = services.BuildServiceProvider();
            var publishingService = sp.GetRequiredService<IIntuneAppPublishingService>();
            var logger = GetLogger(sp);

            var sourceFiles = new List<FileInfo>(sources.OfType<FileInfo>());
            sourceFiles.AddRange(sources.OfType<DirectoryInfo>().SelectMany(di => di.EnumerateFiles("*.intunewin.json", SearchOption.AllDirectories)));
            foreach (var file in sourceFiles)
            {
                using var package = ReadPackage(file, logger);
                await publishingService.PublishAsync(package);
            }
        }

        /// <summary>
        ///     Registers the correct builder type for each source from the command line.
        /// </summary>
        /// <param name="sources"></param>
        /// <param name="services"></param>
        private static void AddBuilders(IEnumerable<FileSystemInfo> sources, IServiceCollection services)
        {
            foreach (var source in sources)
            {
                if (!source.Exists) throw new InvalidOperationException($"{source.FullName} does not exist.");

                if (source.Extension.Equals(".msi", StringComparison.OrdinalIgnoreCase) || source is DirectoryInfo)
                {
                    services.AddSingleton<IIntuneAppPackageBuilder>(sp => ActivatorUtilities.CreateInstance<PathIntuneAppPackageBuilder>(sp, source.FullName));
                }
                else
                {
                    throw new InvalidOperationException($"{source} is not a supported packaging source.");
                }
            }
        }

        /// <summary>
        ///     Invokes the builder in a dedicated working directory.
        /// </summary>
        private static async Task BuildAsync(IIntuneAppPackageBuilder builder, IIntuneAppPackagingService packagingService, string output, ILogger logger)
        {
            var cd = Environment.CurrentDirectory;
            Environment.CurrentDirectory = output;
            try
            {
                var package = await builder.BuildAsync(null);
                package.Data.Position = 0;

                var baseFileName = Path.GetFileNameWithoutExtension(package.App.FileName);

                using (var jsonSerializerWriter = new JsonSerializationWriter())
                {
                    jsonSerializerWriter.WriteObjectValue(string.Empty, package);
                    var serializedStream = jsonSerializerWriter.GetSerializedContent();
                    using (var reader = new StreamReader(serializedStream, Encoding.UTF8))
                    {
                        var packageJsonString = await reader.ReadToEndAsync();
                        File.WriteAllText($"{baseFileName}.intunewin.json", packageJsonString);
                    }
                }

                await using (var fs = File.Open($"{baseFileName}.intunewin", FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    await package.Data.CopyToAsync(fs);
                }

                await using (var fs = File.Open($"{baseFileName}.portal.intunewin", FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    await packagingService.BuildPackageForPortalAsync(package, fs);
                }

                logger.LogInformation($"Finished writing {baseFileName} package files to {output}.");
            }
            finally
            {
                Environment.CurrentDirectory = cd;
            }
        }

        private static ILogger GetLogger(IServiceProvider sp) => sp.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(Program));

        private static IntuneAppPackage ReadPackage(FileInfo file, ILogger logger)
        {
            logger.LogInformation($"Loading package from file {file.FullName}.");

            var jsonParseNode = new JsonParseNode(JsonDocument.Parse(File.ReadAllText(file.FullName)).RootElement);
            var package = jsonParseNode.GetObjectValue(IntuneAppPackage.CreateFromDiscriminatorValue);
            var dataPath = Path.Combine(file.DirectoryName!, Path.GetFileNameWithoutExtension(file.FullName));
            if (!File.Exists(dataPath)) throw new FileNotFoundException($"Could not find data file at {dataPath}.");
            logger.LogInformation($"Using package data file {dataPath}");
            package!.Data = File.Open(dataPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return package;
        }
    }
}