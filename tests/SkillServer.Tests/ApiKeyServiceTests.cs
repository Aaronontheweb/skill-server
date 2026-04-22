// -----------------------------------------------------------------------
// <copyright file="ApiKeyServiceTests.cs" company="Petabridge, LLC">
//      Copyright (C) 2026 - 2026 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using SkillServer.Services;
using Xunit;

namespace SkillServer.Tests;

public sealed class ApiKeyHashTests
{
    [Fact]
    public void HashKey_ProducesDeterministicResult()
    {
        var hash1 = ApiKeyService.HashKey("test-key-123");
        var hash2 = ApiKeyService.HashKey("test-key-123");
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void HashKey_DifferentInputs_ProduceDifferentHashes()
    {
        var hash1 = ApiKeyService.HashKey("key-one");
        var hash2 = ApiKeyService.HashKey("key-two");
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void HashKey_Returns64CharLowercaseHex()
    {
        var hash = ApiKeyService.HashKey("test-key");
        Assert.Equal(64, hash.Length);
        Assert.Equal(hash, hash.ToLowerInvariant());
        Assert.Matches("^[a-f0-9]{64}$", hash);
    }
}
