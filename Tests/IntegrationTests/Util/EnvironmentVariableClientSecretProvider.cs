using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Microsoft.Graph.Authentication;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;

namespace IntuneAppBuilder.IntegrationTests.Util
{
    /// <summary>
    ///     Auth provider for Graph client that uses environment variables and the client credentials flow.
    /// </summary>
    public sealed class EnvironmentVariableClientSecretProvider : IAuthenticationProvider
    {
        private readonly Lazy<TokenCredential> tokenCredential = new(() =>
        {
            foreach (var name in new[] { "TenantId", "ClientId", "ClientSecret" })
            {
                if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable($"AadAuth:{name}")))
                    throw new InvalidOperationException($"Environment variable AadAuth:{name} is not specified.");
            }

            return new ClientSecretCredential(
                Environment.GetEnvironmentVariable("AadAuth:TenantId"),
                Environment.GetEnvironmentVariable("AadAuth:ClientId"),
                Environment.GetEnvironmentVariable("AadAuth:ClientSecret"));
        });

        private IAuthenticationProvider InnerProvider => new AzureIdentityAuthenticationProvider(tokenCredential.Value, isCaeEnabled: false, scopes: "https://graph.microsoft.com/.default");

        public async Task AuthenticateRequestAsync(RequestInformation request, Dictionary<string, object> additionalAuthenticationContext = default, CancellationToken cancellationToken = default) => await InnerProvider.AuthenticateRequestAsync(request, additionalAuthenticationContext, cancellationToken);
    }
}
