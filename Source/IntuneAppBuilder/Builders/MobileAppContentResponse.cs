using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Graph.Beta.Models;
using Microsoft.Kiota.Abstractions.Serialization;

namespace IntuneAppBuilder.Builders
{
    public sealed class MobileAppContentCollectionResponse : BaseCollectionPaginationCountResponse, IParsable
    {
#nullable enable
        public IEnumerable<MobileAppContent>? Value
# nullable disable
        {
            get => BackingStore?.Get<List<MobileAppContent>>("value");
            private set => BackingStore?.Set(nameof(value), value);
        }

        public new IDictionary<string, Action<IParseNode>> GetFieldDeserializers() =>
            new Dictionary<string, Action<IParseNode>>(base.GetFieldDeserializers())
            {
                {
                    "value",
                    n =>
                    {
                        var collectionOfObjectValues = n.GetCollectionOfObjectValues(MobileAppContent.CreateFromDiscriminatorValue);
                        Value = collectionOfObjectValues.ToList();
                    }
                }
            };

        public new void Serialize(ISerializationWriter writer)
        {
            if (writer == null)
                throw new ArgumentNullException(nameof(writer));
            base.Serialize(writer);
            writer.WriteCollectionOfObjectValues("value", Value);
        }

        public static
            new MobileAppContentCollectionResponse CreateFromDiscriminatorValue(IParseNode parseNode)
        {
            if (parseNode == null)
                throw new ArgumentNullException(nameof(parseNode));
            return new MobileAppContentCollectionResponse();
        }
    }
}