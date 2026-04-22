// -----------------------------------------------------------------------
// <copyright file="ApiKeyService.cs" company="Petabridge, LLC">
//      Copyright (C) 2026 - 2026 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System.Security.Cryptography;
using System.Text;
using SkillServer.Data;
using SkillServer.Models;

namespace SkillServer.Services;

public sealed class ApiKeyService
{
    private readonly ApiKeyRepository _repository;
    private readonly ILogger<ApiKeyService> _logger;
    // -1 = unknown, 0 = no keys, 1 = keys exist
    private volatile int _authEnabled = -1;

    public ApiKeyService(ApiKeyRepository repository, ILogger<ApiKeyService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<(string RawKey, ApiKey StoredKey)> CreateKeyAsync(
        string label, DateTimeOffset? expiresAt = null, CancellationToken ct = default)
    {
        var rawKey = GenerateRawKey();
        var keyHash = HashKey(rawKey);
        var now = DateTimeOffset.UtcNow;

        var id = await _repository.CreateKeyAsync(label, keyHash, expiresAt, ct);

        _authEnabled = 1;
        _logger.LogInformation("Created API key '{Label}' (id={Id})", label, id);

        return (rawKey, new ApiKey
        {
            Id = id,
            Label = label,
            KeyHash = keyHash,
            CreatedAt = now,
            ExpiresAt = expiresAt
        });
    }

    public async Task<bool> ValidateKeyAsync(string rawKey, CancellationToken ct = default)
    {
        var keyHashHex = HashKey(rawKey);
        var storedKey = await _repository.GetKeyByHashAsync(keyHashHex, ct);

        if (storedKey is null)
            return false;

        if (storedKey.ExpiresAt.HasValue && storedKey.ExpiresAt.Value < DateTimeOffset.UtcNow)
            return false;

        return true;
    }

    public async Task<bool> IsAuthenticationEnabledAsync(CancellationToken ct = default)
    {
        var state = _authEnabled;
        if (state >= 0)
            return state == 1;

        var result = await _repository.AnyKeysExistAsync(ct);
        _authEnabled = result ? 1 : 0;
        return result;
    }

    public async Task SeedFromEnvironmentAsync(CancellationToken ct = default)
    {
        var envKey = Environment.GetEnvironmentVariable("SKILLSERVER__APIKEY");
        if (string.IsNullOrWhiteSpace(envKey))
            return;

        if (await _repository.AnyKeysExistAsync(ct))
        {
            _logger.LogDebug("API keys already exist, skipping environment seed");
            _authEnabled = 1;
            return;
        }

        var keyHash = HashKey(envKey);
        await _repository.CreateKeyAsync("bootstrap", keyHash, expiresAt: null, ct);
        _authEnabled = 1;
        _logger.LogInformation("Seeded initial API key from SKILLSERVER__APIKEY environment variable");
    }

    public async Task<IReadOnlyList<ApiKey>> ListKeysAsync(CancellationToken ct = default)
    {
        return await _repository.GetAllKeysAsync(ct);
    }

    public async Task<bool> DeleteKeyAsync(long id, CancellationToken ct = default)
    {
        var deleted = await _repository.DeleteKeyIfNotLastAsync(id, ct);
        if (deleted)
        {
            _authEnabled = -1;
            _logger.LogInformation("Deleted API key id={Id}", id);
        }

        return deleted;
    }

    private static string GenerateRawKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return $"sk-{Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')}";
    }

    internal static string HashKey(string rawKey)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey))).ToLowerInvariant();
    }
}
