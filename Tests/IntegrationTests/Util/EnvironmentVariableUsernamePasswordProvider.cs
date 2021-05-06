using System;
using System.Linq;
using System.Net.Http;
using System.Security;
using System.Threading.Tasks;
using Microsoft.Graph;
using Microsoft.Graph.Auth;
using Microsoft.Identity.Client;

namespace IntuneAppBuilder.IntegrationTests.Util
{
    /// <summary>
    /// Auth provider for Graph client that uses environment variables and resource owner flow.
    /// </summary>
    public class EnvironmentVariableUsernamePasswordProvider : IAuthenticationProvider
    {
        private readonly Lazy<AuthenticationProviderOption> authenticationProviderOption = new Lazy<AuthenticationProviderOption>(() =>
        {
            new[] {"Username", "Password"}.Select(Environment.GetEnvironmentVariable).Where(string.IsNullOrEmpty).ToList()
                .ForEach(missingVar => throw new InvalidOperationException($"Environment variable AadAuth:{missingVar} is not specified."));

            var option = new AuthenticationProviderOption
            {
                UserAccount = new GraphUserAccount { Email = Environment.GetEnvironmentVariable("AadAuth:Username") },
                Password = new SecureString()
            };

            Environment.GetEnvironmentVariable("AadAuth:Password")?.ToCharArray().ToList().ForEach(option.Password.AppendChar);

            return option;
        });

        private readonly IAuthenticationProvider innerProvider = new UsernamePasswordProvider(PublicClientApplicationBuilder
            .Create("14d82eec-204b-4c2f-b7e8-296a70dab67e") // Microsoft Graph PowerShell well known client id
            .WithTenantId(Environment.GetEnvironmentVariable("AadAuth:Username")?.Split('@').Last())
            .Build(), new[] { "DeviceManagementApps.ReadWrite.All" });

        public Task AuthenticateRequestAsync(HttpRequestMessage request)
        {
            request.GetRequestContext().MiddlewareOptions[typeof(AuthenticationHandlerOption).ToString()] = new AuthenticationHandlerOption
            {
                AuthenticationProviderOption = authenticationProviderOption.Value
            };
            return innerProvider.AuthenticateRequestAsync(request);
        }
    }
}