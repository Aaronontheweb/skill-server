// -----------------------------------------------------------------------
// <copyright file="ApiKeyEndpointFilter.cs" company="Petabridge, LLC">
//      Copyright (C) 2026 - 2026 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using SkillServer.Models;
using SkillServer.Services;

namespace SkillServer;

public sealed class ApiKeyEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var apiKeyService = context.HttpContext.RequestServices.GetRequiredService<ApiKeyService>();

        if (!await apiKeyService.IsAuthenticationEnabledAsync(context.HttpContext.RequestAborted))
            return await next(context);

        var authHeader = context.HttpContext.Request.Headers.Authorization.ToString();

        if (string.IsNullOrEmpty(authHeader))
        {
            return Results.Json(
                new ErrorResponse
                {
                    Error = "unauthorized",
                    Message = "API key required. Provide via 'Authorization: Bearer <key>' header."
                },
                SkillServerJsonContext.Default.ErrorResponse,
                statusCode: 401);
        }

        if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Json(
                new ErrorResponse
                {
                    Error = "unauthorized",
                    Message = "Unsupported authorization scheme. Use 'Authorization: Bearer <key>'."
                },
                SkillServerJsonContext.Default.ErrorResponse,
                statusCode: 401);
        }

        var rawKey = authHeader["Bearer ".Length..].Trim();

        if (string.IsNullOrEmpty(rawKey))
        {
            return Results.Json(
                new ErrorResponse
                {
                    Error = "unauthorized",
                    Message = "API key required. Provide via 'Authorization: Bearer <key>' header."
                },
                SkillServerJsonContext.Default.ErrorResponse,
                statusCode: 401);
        }

        if (!await apiKeyService.ValidateKeyAsync(rawKey, context.HttpContext.RequestAborted))
        {
            return Results.Json(
                new ErrorResponse
                {
                    Error = "forbidden",
                    Message = "Invalid or expired API key."
                },
                SkillServerJsonContext.Default.ErrorResponse,
                statusCode: 403);
        }

        return await next(context);
    }
}
