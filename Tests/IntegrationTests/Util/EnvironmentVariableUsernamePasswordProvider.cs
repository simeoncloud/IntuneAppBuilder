using System;
using System.Collections.Generic;
using System.Linq;
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
    ///     Auth provider for Graph client that uses environment variables and resource owner flow.
    /// </summary>
    public sealed class EnvironmentVariableUsernamePasswordProvider : IAuthenticationProvider
    {
        private readonly Lazy<TokenCredential> tokenCredential = new(() =>
        {
            new[] { "Username", "Password" }.Select(var => $"AadAuth:{var}").Select(Environment.GetEnvironmentVariable).Where(string.IsNullOrEmpty).ToList()
                .ForEach(missingVar => throw new InvalidOperationException($"Environment variable {missingVar} is not specified."));

            var username = Environment.GetEnvironmentVariable("AadAuth:Username");
            return new UsernamePasswordCredential(
                username,
                Environment.GetEnvironmentVariable("AadAuth:Password"),
                username?.Split('@').Last(),
                "14d82eec-204b-4c2f-b7e8-296a70dab67e"); // Microsoft Graph PowerShell well known client id
        });

        private IAuthenticationProvider InnerProvider => new AzureIdentityAuthenticationProvider(tokenCredential.Value, scopes: "DeviceManagementApps.ReadWrite.All");

        public async Task AuthenticateRequestAsync(RequestInformation request, Dictionary<string, object> additionalAuthenticationContext = default, CancellationToken cancellationToken = default) => await InnerProvider.AuthenticateRequestAsync(request, additionalAuthenticationContext, cancellationToken);
    }
}