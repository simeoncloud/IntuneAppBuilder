using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using Microsoft.Graph.Beta.Models;
using Microsoft.Kiota.Abstractions.Serialization;

namespace IntuneAppBuilder.Domain
{
    /// <summary>
    ///     Metadata about a package produced for an Intune mobile app.
    /// </summary>
#pragma warning disable S3881 // "IDisposable" should be implemented correctly
    public sealed class IntuneAppPackage : IDisposable, IParsable
#pragma warning restore S3881 // "IDisposable" should be implemented correctly
    {
        [JsonPropertyName("app")]
        public MobileLobApp App { get; set; }

        /// <summary>
        ///     Data stream containing the intunewin package contents. Stream must support seek operations.
        /// </summary>
        [JsonIgnore]
        public Stream Data { get; set; }

        [JsonPropertyName("encryptionInfo")]
        public FileEncryptionInfo EncryptionInfo { get; set; }

        [JsonPropertyName("file")]
        public MobileAppContentFile File { get; set; }

        public void Dispose() => Data?.Dispose();

        public IDictionary<string, Action<IParseNode>> GetFieldDeserializers() =>
            new Dictionary<string, Action<IParseNode>>
            {
                { "app", n => { App = n.GetObjectValue(MobileLobApp.CreateFromDiscriminatorValue); } },
                { "encryptionInfo", n => { EncryptionInfo = n.GetObjectValue(FileEncryptionInfo.CreateFromDiscriminatorValue); } },
                { "file", n => { File = n.GetObjectValue(MobileAppContentFile.CreateFromDiscriminatorValue); } }
            };

        public void Serialize(ISerializationWriter writer)
        {
            _ = writer ?? throw new ArgumentNullException(nameof(writer));
            writer.WriteObjectValue("app", App);
            writer.WriteObjectValue("encryptionInfo", EncryptionInfo);
            writer.WriteObjectValue("file", File);
        }

        public static IntuneAppPackage CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            _ = parseNode ?? throw new ArgumentNullException(nameof(parseNode));
            return new IntuneAppPackage();
        }
    }
}