// -----------------------------------------------------------------------
// <copyright file="ServiceCollectionExtensions.cs" company="Petabridge, LLC">
//      Copyright (C) 2026 - 2026 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------
using Microsoft.Extensions.DependencyInjection;

namespace Netclaw.SkillClient;

/// <summary>
/// Extension methods for registering SkillServerClient with DI.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSkillServerClient(
        this IServiceCollection services,
        string serverUrl)
    {
        return services.AddSkillServerClient(serverUrl, apiKey: null);
    }

    /// <summary>
    /// Adds SkillServerClient to the service collection with an API key.
    /// </summary>
    public static IServiceCollection AddSkillServerClient(
        this IServiceCollection services,
        string serverUrl,
        string? apiKey)
    {
        services.AddHttpClient<SkillServerClient>(client =>
        {
            client.BaseAddress = new Uri(serverUrl.TrimEnd('/') + "/");
            if (!string.IsNullOrEmpty(apiKey))
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        });

        return services;
    }

    /// <summary>
    /// Adds SkillServerClient to the service collection with configuration.
    /// </summary>
    public static IServiceCollection AddSkillServerClient(
        this IServiceCollection services,
        Action<HttpClient> configureClient)
    {
        services.AddHttpClient<SkillServerClient>(configureClient);
        return services;
    }
}
