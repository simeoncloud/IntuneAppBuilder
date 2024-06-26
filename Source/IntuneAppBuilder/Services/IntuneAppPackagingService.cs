﻿using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using IntuneAppBuilder.Domain;
using IntuneAppBuilder.Util;
using Microsoft.Extensions.Logging;
using Microsoft.Graph.Beta.Models;

namespace IntuneAppBuilder.Services
{
    internal sealed class IntuneAppPackagingService : IIntuneAppPackagingService
    {
        private readonly ILogger logger;

        public IntuneAppPackagingService(ILogger<IntuneAppPackagingService> logger) => this.logger = logger;

#pragma warning disable S1541
        public async Task<IntuneAppPackage> BuildPackageAsync(string sourcePath = ".", string setupFilePath = null)
#pragma warning restore S1541
        {
            var sw = Stopwatch.StartNew();

            var originalSourcePath = Path.GetFullPath(sourcePath);
            logger.LogInformation($"Creating Intune app package from {originalSourcePath}.");

            var name = Path.GetFileNameWithoutExtension(Path.GetFullPath(sourcePath));

            var zip = ZipContent(sourcePath, setupFilePath);
            if (zip.ZipFilePath != null)
            {
                sourcePath = zip.ZipFilePath;
                setupFilePath = zip.SetupFilePath;
            }

            if (!File.Exists(sourcePath)) throw new FileNotFoundException($"Could not find source file {sourcePath}.");

            setupFilePath ??= sourcePath;

            logger.LogInformation($"Generating encrypted version of {sourcePath}.");

            var data = new FileStream(Path.GetRandomFileName(), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 4096, FileOptions.DeleteOnClose);
            var encryptionInfo = await EncryptFileAsync(sourcePath, data);
            data.Position = 0;

            var msiInfo = GetMsiInfo(setupFilePath);

            var app = msiInfo.Info != null && zip.ZipFilePath == null // a zip of a folder with an MSI is still a Win32LobApp, not a WindowsMobileMSI
                ? new WindowsMobileMSI
                {
                    ProductCode = msiInfo.Info.ProductCode,
                    ProductVersion = msiInfo.Info.ProductVersion,
                    IdentityVersion = msiInfo.Info.ProductVersion,
                    FileName = Path.GetFileName(setupFilePath)
                } as MobileLobApp
                : new Win32LobApp
                {
                    FileName = $"{name}.intunewin",
                    SetupFilePath = Path.GetFileName(setupFilePath),
                    MsiInformation = msiInfo.Info,
                    InstallExperience = new Win32LobAppInstallExperience { RunAsAccount = GetRunAsAccountType(msiInfo) }
                };

            app.DisplayName = msiInfo.Info?.ProductName ?? name;
            app.Publisher = msiInfo.Info?.Publisher;

            var file = new MobileAppContentFile
            {
                Name = app.FileName,
                Size = new FileInfo(sourcePath).Length,
                SizeEncrypted = data.Length,
                Manifest = msiInfo.Manifest?.ToByteArray()
            };

            var result = new IntuneAppPackage
            {
                Data = data,
                App = app,
                EncryptionInfo = encryptionInfo,
                File = file
            };

            if (zip.ZipFilePath != null) File.Delete(zip.ZipFilePath);

            logger.LogInformation($"Created Intune app package from {originalSourcePath} in {sw.ElapsedMilliseconds}ms.");

            return result;
        }

        public async Task BuildPackageForPortalAsync(IntuneAppPackage package, Stream outputStream)
        {
            var sw = Stopwatch.StartNew();

            logger.LogInformation($"Creating Intune portal package for {package.App.FileName}.");

            using var archive = new ZipArchive(outputStream, ZipArchiveMode.Create);

            // the portal can only read if no compression is used

            var packageEntry = archive.CreateEntry("IntuneWinPackage/Contents/IntunePackage.intunewin", CompressionLevel.NoCompression);
            package.Data.Position = 0;
            using (var dataEntryStream = packageEntry.Open())
            {
                await package.Data.CopyToAsync(dataEntryStream);
            }

            var detectionEntry = archive.CreateEntry("IntuneWinPackage/Metadata/Detection.xml", CompressionLevel.NoCompression);
            using (var detectionEntryStream = detectionEntry.Open())
            {
                using var writer = new StreamWriter(detectionEntryStream);
                await writer.WriteAsync(GetDetectionXml(package));
            }

            logger.LogInformation($"Created Intune portal package for {package.App.FileName} in {sw.ElapsedMilliseconds}ms.");
        }

        /// <summary>
        ///     Algorithm to encrypt file for upload to Intune as intunewin.
        /// </summary>
        private async Task<FileEncryptionInfo> EncryptFileAsync(string sourceFilePath, Stream outputStream)
        {
            byte[] CreateIVEncryptionKey()
            {
                using (var aes = Aes.Create())
                {
                    return aes.IV;
                }
            }

            byte[] CreateEncryptionKey()
            {
                using var provider = Aes.Create();
                provider.GenerateKey();
                return provider.Key;
            }

            var encryptionKey = CreateEncryptionKey();
            var hmacKey = CreateEncryptionKey();
            var initializationVector = CreateIVEncryptionKey();

            async Task<byte[]> EncryptFileWithIVAsync()
            {
                using (var aes = Aes.Create())
                using (var hmacSha256 = new HMACSHA256 { Key = hmacKey })
                {
                    var hmacLength = hmacSha256.HashSize / 8;
                    const int bufferBlockSize = 1024 * 4;
                    var buffer = new byte[bufferBlockSize];

                    await outputStream.WriteAsync(buffer, 0, hmacLength + initializationVector.Length);
                    using (var sourceStream = File.Open(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var encryptor = aes.CreateEncryptor(encryptionKey, initializationVector))
                    using (var cryptoStream = new CryptoStream(outputStream, encryptor, CryptoStreamMode.Write, true))
                    {
                        int bytesRead;
                        while ((bytesRead = sourceStream.Read(buffer, 0, bufferBlockSize)) > 0)
                        {
                            await cryptoStream.WriteAsync(buffer, 0, bytesRead);
                            cryptoStream.Flush();
                        }

                        cryptoStream.FlushFinalBlock();
                    }

                    outputStream.Seek(hmacLength, SeekOrigin.Begin);
                    await outputStream.WriteAsync(initializationVector, 0, initializationVector.Length);
                    outputStream.Seek(hmacLength, SeekOrigin.Begin);

                    var hmac = hmacSha256.ComputeHash(outputStream);

                    outputStream.Seek(0, SeekOrigin.Begin);
                    await outputStream.WriteAsync(hmac, 0, hmac.Length);

                    return hmac;
                }
            }

            // Create the encrypted target file and compute the HMAC value.
            var mac = await EncryptFileWithIVAsync();

            // Compute the SHA256 hash of the source file and convert the result to bytes.
            using (var sha256 = SHA256.Create())
            using (var fs = File.OpenRead(sourceFilePath))
            {
                return new FileEncryptionInfo
                {
                    EncryptionKey = encryptionKey,
                    MacKey = hmacKey,
                    InitializationVector = initializationVector,
                    Mac = mac,
                    ProfileIdentifier = "ProfileVersion1",
                    FileDigest = sha256.ComputeHash(fs),
                    FileDigestAlgorithm = "SHA256"
                };
            }
        }

        /// <summary>
        ///     This file is included in the zip file the portal expects.
        ///     It is essentially a collection of metadata, used specifically by the portal javascript to patch data on the mobile
        ///     app and its content (e.g. the Manifest of the content file).
        /// </summary>
        private string GetDetectionXml(IntuneAppPackage package)
        {
            var xml = new XmlDocument();

            XmlElement AppendElement(XmlNode parent, string name, object value = null)
            {
                var e = xml.CreateElement(name);
                if (value != null) e.InnerText = value.ToString();
                parent.AppendChild(e);
                return e;
            }

            var infoElement = AppendElement(xml, "ApplicationInfo");
            xml.DocumentElement?.SetAttribute("ToolVersion", "1.4.0.0");
            AppendElement(infoElement, "Name", package.App.DisplayName);
            AppendElement(infoElement, "UnencryptedContentSize", package.File.Size);
            AppendElement(infoElement, "FileName", "IntunePackage.intunewin");
            AppendElement(infoElement, "SetupFile", package.App is Win32LobApp win32 ? win32.SetupFilePath : package.App.FileName);

            var namespaces = new XmlSerializerNamespaces(new[]
            {
                new XmlQualifiedName(string.Empty, string.Empty)
            });

            using (var writer = infoElement.CreateNavigator().AppendChild())
            {
                writer.WriteWhitespace("");

                var overrides = new XmlAttributeOverrides();
                overrides.Add(typeof(FileEncryptionInfo), nameof(FileEncryptionInfo.BackingStore), new XmlAttributes { XmlIgnore = true });
                overrides.Add(typeof(FileEncryptionInfo), nameof(FileEncryptionInfo.AdditionalData), new XmlAttributes { XmlIgnore = true });
                overrides.Add(typeof(FileEncryptionInfo), nameof(FileEncryptionInfo.OdataType), new XmlAttributes { XmlIgnore = true });

                new XmlSerializer(typeof(FileEncryptionInfo), overrides, new Type[0],
                        new XmlRootAttribute("EncryptionInfo"), null)
                    .Serialize(writer, package.EncryptionInfo, namespaces);
            }

            if (package.File.Manifest != null)
                using (var writer = infoElement.CreateNavigator().AppendChild())
                {
                    writer.WriteWhitespace("");

                    var overrides = new XmlAttributeOverrides();
                    typeof(MobileMsiManifest).GetProperties().ToList().ForEach(p =>
                    {
                        if (p.DeclaringType != null)
                            overrides.Add(p.DeclaringType, p.Name, new XmlAttributes());
                    });
                    new XmlSerializer(typeof(MobileMsiManifest), overrides, new Type[0], new XmlRootAttribute("MsiInfo"), string.Empty)
                        .Serialize(writer, MobileMsiManifest.FromByteArray(package.File.Manifest), namespaces);
                }

            return FormatXml(xml);
        }

        private (Win32LobAppMsiInformation Info, MobileMsiManifest Manifest) GetMsiInfo(string setupFilePath)
        {
            if (OperatingSystem.IsWindows() && ".msi".Equals(Path.GetExtension(setupFilePath), StringComparison.OrdinalIgnoreCase))
            {
                using (var util = new MsiUtil(setupFilePath, logger))
                {
                    return util.ReadMsiInfo();
                }
            }

            return default;
        }

        private (string ZipFilePath, string SetupFilePath) ZipContent(string sourcePath, string setupFilePath)
        {
            string zipFilePath = null;
            if (Directory.Exists(sourcePath))
            {
                sourcePath = Path.GetFullPath(sourcePath);
                zipFilePath = Path.Combine(Path.GetTempPath(), $"{Path.GetRandomFileName()}.{Path.GetFileNameWithoutExtension(Path.GetFullPath(sourcePath))}.intunewin.zip");
                if (File.Exists(zipFilePath)) File.Delete(zipFilePath);
                logger.LogInformation($"Creating intermediate zip of {sourcePath} at {zipFilePath}.");
                ZipFile.CreateFromDirectory(sourcePath, zipFilePath, CompressionLevel.Optimal, false);
                if (setupFilePath == null) setupFilePath = Directory.GetFiles(sourcePath, "*.msi").FirstOrDefault() ?? Directory.GetFiles(sourcePath, "*.exe").FirstOrDefault();
            }

            return (zipFilePath, setupFilePath);
        }

        private static string FormatXml(XmlDocument doc)
        {
            var sb = new StringBuilder();
            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                NewLineChars = "\r\n",
                NewLineHandling = NewLineHandling.Replace,
                OmitXmlDeclaration = true
            };
            using (var writer = XmlWriter.Create(sb, settings))
            {
                doc.Save(writer);
            }

            return sb.ToString();
        }

        private static RunAsAccountType GetRunAsAccountType((Win32LobAppMsiInformation Info, MobileMsiManifest Manifest) msiInfo) => msiInfo.Info?.PackageType == Win32LobAppMsiPackageType.PerUser ? RunAsAccountType.User : RunAsAccountType.System;
    }
}