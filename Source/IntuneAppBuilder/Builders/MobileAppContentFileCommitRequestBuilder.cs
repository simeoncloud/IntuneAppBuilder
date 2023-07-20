using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IntuneAppBuilder.Domain;
using Microsoft.Graph.Beta.Models.ODataErrors;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Serialization;

namespace IntuneAppBuilder.Builders
{
    public sealed class MobileAppContentFileCommitRequestBuilder : BaseRequestBuilder
    {
        public MobileAppContentFileCommitRequestBuilder(Dictionary<string, object> pathParameters, IRequestAdapter requestAdapter) : base(requestAdapter, "{+baseurl}/deviceAppManagement/mobileApps/{mobileApp%2Did}/{mobileApp%2Dtype}/contentVersions/{mobileAppContent%2Did}/files/{mobileAppContentFile%2Did}/microsoft.graph.commit", pathParameters)
        {
        }

        public async Task PostAsync(MobileAppContentFileCommitRequest body = null)
        {
            var requestInfo = ToPostRequestInformation(body);
            var errorMapping = new Dictionary<string, ParsableFactory<IParsable>>
            {
                { "4XX", ODataError.CreateFromDiscriminatorValue },
                { "5XX", ODataError.CreateFromDiscriminatorValue }
            };
            await RequestAdapter.SendAsync(requestInfo, MobileAppContentFileCommitRequest.CreateFromDiscriminatorValue, errorMapping);
        }

        private RequestInformation ToPostRequestInformation(MobileAppContentFileCommitRequest body)
        {
            _ = body ?? throw new ArgumentNullException(nameof(body));
            var requestInfo = new RequestInformation
            {
                HttpMethod = Method.POST,
                UrlTemplate = UrlTemplate,
                PathParameters = PathParameters
            };
            requestInfo.Headers.Add("Accept", "application/json");
            requestInfo.SetContentFromParsable(RequestAdapter, "application/json", body);

            return requestInfo;
        }
    }
}