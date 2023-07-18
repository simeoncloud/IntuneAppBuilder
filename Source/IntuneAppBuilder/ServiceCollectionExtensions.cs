using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using Azure.Core;
using Azure.Identity;
using IntuneAppBuilder.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Graph.Beta;

namespace IntuneAppBuilder;

public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Registers required services.
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddIntuneAppBuilder(this IServiceCollection services, string token = null)
    {
        services.AddLogging();
        services.AddHttpClient();
        services.TryAddSingleton(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient());
        services.TryAddTransient<IIntuneAppPublishingService, IntuneAppPublishingService>();
        services.TryAddTransient<IIntuneAppPackagingService, IntuneAppPackagingService>();
        services.TryAddSingleton(sp => new GraphServiceClient(CreateTokenCredential(token), new[] { "DeviceManagementApps.ReadWrite.All" }));
        return services;
    }

    /// <summary>
    ///     For more granular control, register GraphServiceClient yourself.
    /// </summary>
    /// <returns></returns>
    private static TokenCredential CreateTokenCredential(string token = null)
    {
        if (token != null)
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtSecurityToken = handler.ReadJwtToken(token);
            var tokenExpirationTicks = long.Parse(jwtSecurityToken.Claims.First(claim => claim.Type.Equals("exp")).Value);
            return DelegatedTokenCredential.Create((_, _) => new AccessToken(token, DateTimeOffset.FromUnixTimeSeconds(tokenExpirationTicks).UtcDateTime));
        }

        // Microsoft Graph PowerShell well known client id
        const string microsoftGraphPowerShellClientId = "14d82eec-204b-4c2f-b7e8-296a70dab67e";

        return new DeviceCodeCredential(new DeviceCodeCredentialOptions
        {
            ClientId = microsoftGraphPowerShellClientId,
            DeviceCodeCallback = async (dcr, _) => await Console.Out.WriteLineAsync(dcr.Message),
        });
    }
}