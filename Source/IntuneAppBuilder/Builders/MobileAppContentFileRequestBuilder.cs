using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Graph.Beta.Models;
using Microsoft.Graph.Beta.Models.ODataErrors;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Serialization;

namespace IntuneAppBuilder.Builders
{
    public sealed class MobileAppContentFileRequestBuilder : BaseRequestBuilder
    {
        public MobileAppContentFileRequestBuilder(Dictionary<string, object> pathParameters, IRequestAdapter requestAdapter) : base(requestAdapter, "{+baseurl}/deviceAppManagement/mobileApps/{mobileApp%2Did}/{mobileApp%2Dtype}/contentVersions/{mobileAppContent%2Did}/files/{mobileAppContentFile%2Did}", pathParameters)
        {
        }

        public MobileAppContentFileCommitRequestBuilder Commit => new(PathParameters, RequestAdapter);

#nullable enable
        public async Task<MobileAppContentFile?> GetAsync()
        {
#nullable restore
            var requestInfo = ToGetRequestInformation();
            var errorMapping = new Dictionary<string, ParsableFactory<IParsable>>
            {
                { "4XX", ODataError.CreateFromDiscriminatorValue },
                { "5XX", ODataError.CreateFromDiscriminatorValue }
            };
            return await RequestAdapter.SendAsync(requestInfo, MobileAppContentFile.CreateFromDiscriminatorValue, errorMapping);
        }

        public MobileAppContentFileRenewUploadRequestBuilder RenewUpload() => new(PathParameters, RequestAdapter);

        public RequestInformation ToGetRequestInformation()
        {
            var requestInfo = new RequestInformation
            {
                HttpMethod = Method.GET,
                UrlTemplate = UrlTemplate,
                PathParameters = PathParameters
            };
            requestInfo.Headers.Add("Accept", "application/json");

            return requestInfo;
        }
    }
}