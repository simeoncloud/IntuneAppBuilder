using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Graph.Beta.Models.ODataErrors;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Serialization;

namespace IntuneAppBuilder.Builders
{
    public sealed class MobileAppContentFileRenewUploadRequestBuilder : BaseRequestBuilder
    {
        public MobileAppContentFileRenewUploadRequestBuilder(Dictionary<string, object> pathParameters, IRequestAdapter requestAdapter) : base(requestAdapter, "{+baseurl}/deviceAppManagement/mobileApps/{mobileApp%2Did}/{mobileApp%2Dtype}/contentVersions/{mobileAppContent%2Did}/files/{mobileAppContentFile%2Did}/microsoft.graph.renewUpload", pathParameters)
        {
        }

        public async Task PostAsync()
        {
            var requestInfo = new RequestInformation
            {
                HttpMethod = Method.POST,
                UrlTemplate = UrlTemplate,
                PathParameters = PathParameters
            };
            var errorMapping = new Dictionary<string, ParsableFactory<IParsable>>
            {
                { "4XX", ODataError.CreateFromDiscriminatorValue },
                { "5XX", ODataError.CreateFromDiscriminatorValue }
            };
            await RequestAdapter.SendNoContentAsync(requestInfo, errorMapping);
        }
    }
}