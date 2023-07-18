using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.Kiota.Abstractions.Serialization;

namespace IntuneAppBuilder.Domain
{
    public sealed class FileEncryptionInfo : IParsable
    {
        [JsonExtensionData]
# pragma warning disable S4004
        public IDictionary<string, object> AdditionalData { get; set; }
# pragma warning restore S4004

        [JsonPropertyName("encryptionKey")]
        public byte[] EncryptionKey { get; set; }

        [JsonPropertyName("fileDigest")]
        public byte[] FileDigest { get; set; }

        [JsonPropertyName("fileDigestAlgorithm")]
        public string FileDigestAlgorithm { get; set; }

        [JsonPropertyName("initializationVector")]
        public byte[] InitializationVector { get; set; }

        [JsonPropertyName("mac")]
        public byte[] Mac { get; set; }

        [JsonPropertyName("macKey")]
        public byte[] MacKey { get; set; }

        [JsonPropertyName("@odata.type")]
        public string ODataType { get; set; }

        [JsonPropertyName("profileIdentifier")]
        public string ProfileIdentifier { get; set; }

        public IDictionary<string, Action<IParseNode>> GetFieldDeserializers() =>
            new Dictionary<string, Action<IParseNode>>
            {
                { "encryptionKey", n => { EncryptionKey = n.GetByteArrayValue(); } },
                { "fileDigest", n => { FileDigest = n.GetByteArrayValue(); } },
                { "fileDigestAlgorithm", n => { FileDigestAlgorithm = n.GetStringValue(); } },
                { "initializationVector", n => { InitializationVector = n.GetByteArrayValue(); } },
                { "mac", n => { Mac = n.GetByteArrayValue(); } },
                { "macKey", n => { MacKey = n.GetByteArrayValue(); } },
                { "@odata.type", n => { ODataType = n.GetStringValue(); } },
                { "profileIdentifier", n => { ProfileIdentifier = n.GetStringValue(); } }
            };

        public void Serialize(ISerializationWriter writer)
        {
            _ = writer ?? throw new ArgumentNullException(nameof(writer));
            writer.WriteByteArrayValue("encryptionKey", EncryptionKey);
            writer.WriteByteArrayValue("fileDigest", FileDigest);
            writer.WriteStringValue("fileDigestAlgorithm", FileDigestAlgorithm);
            writer.WriteByteArrayValue("initializationVector", InitializationVector);
            writer.WriteByteArrayValue("mac", Mac);
            writer.WriteByteArrayValue("macKey", MacKey);
            writer.WriteStringValue("@odata.type", ODataType);
            writer.WriteStringValue("profileIdentifier", ProfileIdentifier);
            writer.WriteAdditionalData(AdditionalData);
        }

        public static FileEncryptionInfo CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            _ = parseNode ?? throw new ArgumentNullException(nameof(parseNode));
            return new FileEncryptionInfo();
        }
    }
}