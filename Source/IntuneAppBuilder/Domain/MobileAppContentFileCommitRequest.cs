using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.Graph.Beta.Models;
using Microsoft.Kiota.Abstractions.Serialization;

namespace IntuneAppBuilder.Domain
{
    public sealed class MobileAppContentFileCommitRequest : IParsable
    {
        [JsonPropertyName("fileEncryptionInfo")]
        public FileEncryptionInfo FileEncryptionInfo { get; set; }

        public IDictionary<string, Action<IParseNode>> GetFieldDeserializers() =>
            new Dictionary<string, Action<IParseNode>>
            {
                { "fileEncryptionInfo", n => { FileEncryptionInfo = n.GetObjectValue(FileEncryptionInfo.CreateFromDiscriminatorValue); } }
            };

        public void Serialize(ISerializationWriter writer)
        {
            _ = writer ?? throw new ArgumentNullException(nameof(writer));
            writer.WriteObjectValue("fileEncryptionInfo", FileEncryptionInfo);
        }

        public static MobileAppContentFileCommitRequest CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            _ = parseNode ?? throw new ArgumentNullException(nameof(parseNode));
            return new MobileAppContentFileCommitRequest();
        }
    }
}