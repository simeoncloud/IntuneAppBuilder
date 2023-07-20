using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Graph.Beta.Models;
using Microsoft.Graph.Beta.Models.ODataErrors;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Serialization;

namespace IntuneAppBuilder.Builders
{
    public sealed class ContentVersionsRequestBuilder : BaseRequestBuilder
    {
        public ContentVersionsRequestBuilder(Dictionary<string, object> pathParameters, IRequestAdapter requestAdapter) : base(requestAdapter, "{+baseurl}/deviceAppManagement/mobileApps/{mobileApp%2Did}/{mobileApp%2Dtype}/contentVersions{?%24orderby}", pathParameters)
        {
        }

        public MobileAppContentRequestBuilder this[string position]
        {
            get
            {
                var urlTplParams = new Dictionary<string, object>(PathParameters);
                if (!string.IsNullOrWhiteSpace(position)) urlTplParams.Add("mobileAppContent%2Did", position);
                return new MobileAppContentRequestBuilder(urlTplParams, RequestAdapter);
            }
        }

        public async Task<MobileAppContentCollectionResponse> GetAsync(Action<ContentVersionsRequestBuilderGetRequestConfiguration> requestConfiguration = default)
        {
            var requestInfo = ToGetRequestInformation(requestConfiguration);
            var errorMapping = new Dictionary<string, ParsableFactory<IParsable>>
            {
                { "4XX", ODataError.CreateFromDiscriminatorValue },
                { "5XX", ODataError.CreateFromDiscriminatorValue }
            };
            return await RequestAdapter.SendAsync(requestInfo, MobileAppContentCollectionResponse.CreateFromDiscriminatorValue, errorMapping);
        }

#nullable enable
        public async Task<MobileAppContent?> PostAsync(MobileAppContent body)
        {
#nullable restore
            _ = body ?? throw new ArgumentNullException(nameof(body));
            var requestInfo = ToPostRequestInformation(body);
            var errorMapping = new Dictionary<string, ParsableFactory<IParsable>>
            {
                { "4XX", ODataError.CreateFromDiscriminatorValue },
                { "5XX", ODataError.CreateFromDiscriminatorValue }
            };
            return await RequestAdapter.SendAsync(requestInfo, MobileAppContent.CreateFromDiscriminatorValue, errorMapping);
        }

        private RequestInformation ToGetRequestInformation(Action<ContentVersionsRequestBuilderGetRequestConfiguration> requestConfiguration = default)
        {
            var requestInfo = new RequestInformation
            {
                HttpMethod = Method.GET,
                UrlTemplate = UrlTemplate,
                PathParameters = PathParameters
            };
            requestInfo.Headers.Add("Accept", "application/json");
            if (requestConfiguration != null)
            {
                var requestConfig = new ContentVersionsRequestBuilderGetRequestConfiguration();
                requestConfiguration.Invoke(requestConfig);
                requestInfo.AddQueryParameters(requestConfig.QueryParameters);
            }

            return requestInfo;
        }

        private RequestInformation ToPostRequestInformation(MobileAppContent body)
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

        public sealed class ContentVersionsRequestBuilderGetRequestConfiguration
        {
            public ContentVersionsRequestBuilderGetQueryParameters QueryParameters { get; } = new();
        }

        public sealed class ContentVersionsRequestBuilderGetQueryParameters
        {
#nullable enable
            [QueryParameter("%24orderby")]
            public string[]? Orderby { get; set; }
#nullable restore
        }
    }
}