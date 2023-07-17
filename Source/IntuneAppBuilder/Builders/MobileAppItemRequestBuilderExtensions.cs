using System.Collections.Generic;
using System.Reflection;
using Microsoft.Graph.Beta.DeviceAppManagement.MobileApps.Item;
using Microsoft.Kiota.Abstractions;

namespace IntuneAppBuilder.Builders
{
    public static class MobileAppItemRequestBuilderExtensions
    {
        public static ContentVersionsRequestBuilder ContentVersions(this MobileAppItemRequestBuilder mobileAppItemRequestBuilder, string mobileAppType)
        {
#pragma warning disable S3011
            var urlTplParams = new Dictionary<string, object>((Dictionary<string, object>)mobileAppItemRequestBuilder.GetType().GetProperty("PathParameters", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(mobileAppItemRequestBuilder)!);
            if (!string.IsNullOrWhiteSpace(mobileAppType)) urlTplParams.Add("mobileApp%2Dtype", mobileAppType);
            return new ContentVersionsRequestBuilder(urlTplParams,
                (IRequestAdapter)mobileAppItemRequestBuilder.GetType().GetProperty("RequestAdapter", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(mobileAppItemRequestBuilder));
#pragma warning restore S3011
        }
    }
}