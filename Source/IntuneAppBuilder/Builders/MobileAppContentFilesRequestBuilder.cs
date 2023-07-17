using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Graph.Beta.Models;
using Microsoft.Graph.Beta.Models.ODataErrors;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Serialization;

namespace IntuneAppBuilder.Builders
{
    public sealed class MobileAppContentFilesRequestBuilder : BaseRequestBuilder
    {
        public MobileAppContentFilesRequestBuilder(Dictionary<string, object> pathParameters, IRequestAdapter requestAdapter) : base(requestAdapter, "{+baseurl}/deviceAppManagement/mobileApps/{mobileApp%2Did}/{mobileApp%2Dtype}/contentVersions/{mobileAppContent%2Did}/files", pathParameters)
        {
        }

        public MobileAppContentFileRequestBuilder this[string position]
        {
            get
            {
                var urlTplParams = new Dictionary<string, object>(PathParameters);
                if (!string.IsNullOrWhiteSpace(position)) urlTplParams.Add("mobileAppContentFile%2Did", position);
                return new MobileAppContentFileRequestBuilder(urlTplParams, RequestAdapter);
            }
        }

#nullable enable
        public async Task<MobileAppContentFile?> PostAsync(MobileAppContentFile body, Action<MobileAppContentFilesRequestBuilderPostRequestConfiguration>? requestConfiguration = default, CancellationToken cancellationToken = default)
        {
#nullable restore
            _ = body ?? throw new ArgumentNullException(nameof(body));
            var requestInfo = ToPostRequestInformation(body, requestConfiguration);
            var errorMapping = new Dictionary<string, ParsableFactory<IParsable>>
            {
                { "4XX", ODataError.CreateFromDiscriminatorValue },
                { "5XX", ODataError.CreateFromDiscriminatorValue }
            };
            return await RequestAdapter.SendAsync(requestInfo, MobileAppContentFile.CreateFromDiscriminatorValue, errorMapping, cancellationToken);
        }

#nullable enable
        private RequestInformation ToPostRequestInformation(MobileAppContentFile body, Action<MobileAppContentFilesRequestBuilderPostRequestConfiguration>? requestConfiguration = default)
        {
#nullable restore
            _ = body ?? throw new ArgumentNullException(nameof(body));
            var requestInfo = new RequestInformation
            {
                HttpMethod = Method.POST,
                UrlTemplate = UrlTemplate,
                PathParameters = PathParameters
            };
            requestInfo.Headers.Add("Accept", "application/json");
            requestInfo.SetContentFromParsable(RequestAdapter, "application/json", body);
            if (requestConfiguration != null)
            {
                var requestConfig = new MobileAppContentFilesRequestBuilderPostRequestConfiguration();
                requestConfiguration.Invoke(requestConfig);
                requestInfo.AddRequestOptions(requestConfig.Options);
            }

            return requestInfo;
        }

        public sealed class MobileAppContentFilesRequestBuilderPostRequestConfiguration
        {
            public MobileAppContentFilesRequestBuilderPostRequestConfiguration() => Options = new List<IRequestOption>();

            public IList<IRequestOption> Options { get; }
        }
    }
}