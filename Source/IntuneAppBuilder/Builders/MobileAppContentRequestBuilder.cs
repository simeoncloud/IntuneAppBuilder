using System.Collections.Generic;
using Microsoft.Kiota.Abstractions;

namespace IntuneAppBuilder.Builders
{
    public sealed class MobileAppContentRequestBuilder : BaseRequestBuilder
    {
        public MobileAppContentRequestBuilder(Dictionary<string, object> pathParameters, IRequestAdapter requestAdapter) : base(requestAdapter, "{+baseurl}/deviceAppManagement/mobileApps/{mobileApp%2Did}/{mobileApp%2Dtype}/contentVersions/{mobileAppContent%2Did}", pathParameters)
        {
        }

        public MobileAppContentFilesRequestBuilder Files => new(PathParameters, RequestAdapter);
    }
}