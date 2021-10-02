using System;
using System.IO;
using Microsoft.Graph;
using Newtonsoft.Json;

namespace IntuneAppBuilder.Domain
{
    /// <summary>
    ///     Metadata about a package produced for an Intune mobile app.
    /// </summary>
#pragma warning disable S3881 // "IDisposable" should be implemented correctly
    public class IntuneAppPackage : IDisposable
#pragma warning restore S3881 // "IDisposable" should be implemented correctly
    {
        public MobileLobApp App { get; set; }

        /// <summary>
        ///     Data stream containing the intunewin package contents. Stream must support seek operations.
        /// </summary>
        [JsonIgnore]
        public Stream Data { get; set; }

        public FileEncryptionInfo EncryptionInfo { get; set; }

        public MobileAppContentFile File { get; set; }

        public void Dispose() => Data?.Dispose();
    }
}