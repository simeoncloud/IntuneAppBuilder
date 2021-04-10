using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace IntuneAppBuilder.Domain
{
    /// <summary>
    /// A statically typed manifest for an MSI app. Gets converted to a byte array and included on MobileAppContentFile.
    /// </summary>
    [XmlRoot("MobileMsiData")]
    public class MobileMsiManifest
    {
        [XmlAttribute]
        public string MsiExecutionContext { get; set; }
        [XmlAttribute]
        public bool MsiRequiresReboot { get; set; }
        [XmlAttribute]
        public string MsiUpgradeCode { get; set; }
        [XmlAttribute]
        public bool MsiIsMachineInstall { get; set; }
        [XmlAttribute]
        public bool MsiIsUserInstall { get; set; }
        [XmlAttribute]
        public bool MsiIncludesServices { get; set; }
        [XmlAttribute]
        public bool MsiContainsSystemRegistryKeys { get; set; }
        [XmlAttribute]
        public bool MsiContainsSystemFolders { get; set; }

        public byte[] ToByteArray()
        {
            var serializer = new XmlSerializer(typeof(MobileMsiManifest));

            using var ms = new MemoryStream();
            using var writer = new XmlWriter(ms);
            serializer.Serialize(writer, this, new XmlSerializerNamespaces(new[]
            {
                new XmlQualifiedName(string.Empty, string.Empty)
            }));
            return ms.ToArray();
        }

        public static MobileMsiManifest FromByteArray(byte[] data)
        {
            var serializer = new XmlSerializer(typeof(MobileMsiManifest));

            if (data == null) return default;
            using var ms = new MemoryStream(data);
            return (MobileMsiManifest)serializer.Deserialize(ms);
        }

        private class XmlWriter : XmlTextWriter
        {
            public XmlWriter(Stream stream) : base(stream, Encoding.ASCII)
            {
            }

            public override void WriteStartDocument()
            {
                // do not write xml declaration
            }

            public override void WriteEndElement()
            {
                // do not auto-close xml tags
                base.WriteFullEndElement();
            }
        }
    }
}