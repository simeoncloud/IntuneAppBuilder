using System;
using System.Net.Http;
using IntuneAppBuilder.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Graph;
using Microsoft.Graph.Auth;
using Microsoft.Identity.Client;

namespace IntuneAppBuilder;

public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Registers required services.
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddIntuneAppBuilder(this IServiceCollection services)
    {
        services.AddLogging();
        services.AddHttpClient();
        services.TryAddSingleton(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient());
        services.TryAddTransient<IIntuneAppPublishingService, IntuneAppPublishingService>();
        services.TryAddTransient<IIntuneAppPackagingService, IntuneAppPackagingService>();
        services.TryAddSingleton(sp => new GraphServiceClient(CreateDefaultAuthenticationProvider()));
        return services;
    }

    /// <summary>
    ///     For more granular control, register IGraphServiceClient yourself.
    /// </summary>
    /// <returns></returns>
    private static IAuthenticationProvider CreateDefaultAuthenticationProvider()
    {
        // Microsoft Graph PowerShell well known client id
        const string microsoftGraphPowerShellClientId = "14d82eec-204b-4c2f-b7e8-296a70dab67e";

        var app = PublicClientApplicationBuilder
            .Create(microsoftGraphPowerShellClientId)
            .Build();

        return new DeviceCodeProvider(app, new[] { "DeviceManagementApps.ReadWrite.All" }, async dcr => await Console.Out.WriteLineAsync(dcr.Message));
    }
}