// -----------------------------------------------------------------------
// <copyright file="SkillServerIntegrationTests.cs" company="Petabridge, LLC">
//      Copyright (C) 2026 - 2026 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Netclaw.SkillClient;
using Xunit;

namespace SkillServer.Integration.Tests;

[Collection("SkillServer")]
public sealed class SkillServerIntegrationTests
{
    private readonly SkillServerFixture _fixture;

    public SkillServerIntegrationTests(SkillServerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var ct = TestContext.Current.CancellationToken;
        var response = await _fixture.HttpClient.GetAsync("/health", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetRfcIndex_ReturnsIndex()
    {
        var ct = TestContext.Current.CancellationToken;
        var index = await _fixture.Client.GetRfcIndexAsync(ct);

        Assert.NotNull(index);
    }

    [Fact]
    public async Task ListSkills_ReturnsList()
    {
        var ct = TestContext.Current.CancellationToken;
        var skills = await _fixture.Client.ListSkillsAsync(ct: ct);

        Assert.NotNull(skills);
    }

    [Fact]
    public async Task UploadAndRetrieveSkill_EndToEnd()
    {
        var ct = TestContext.Current.CancellationToken;
        var skillName = $"e2e-test-{Guid.NewGuid():N}"[..20];

        // Upload a skill
        var skillContent = $"""
            ---
            name: {skillName}
            description: A test skill for integration testing
            ---

            # Test Skill

            This is a test skill.
            """;

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(skillName), "name");
        content.Add(new StringContent("1.0.0"), "version");

        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(skillContent));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/markdown");
        content.Add(fileContent, "file", "SKILL.md");

        var uploadResponse = await _fixture.AuthenticatedHttpClient.PostAsync("/skills", content, ct);
        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);

        // List skills - should contain our skill (no auth required for reads)
        var skills = await _fixture.Client.ListSkillsAsync(ct: ct);
        Assert.Contains(skills, s => s.Name == skillName);

        // Get RFC index - should contain our skill
        var index = await _fixture.Client.GetRfcIndexAsync(ct);
        Assert.NotNull(index);
        Assert.Contains(index.Skills, s => s.Name == skillName);

        // Get skill versions
        var versions = await _fixture.Client.GetSkillVersionsAsync(skillName, ct);
        Assert.Single(versions);
        Assert.Equal("1.0.0", versions[0].Version);

        // Get specific version
        var version = await _fixture.Client.GetVersionAsync(skillName, "1.0.0", ct);
        Assert.NotNull(version);
        Assert.Equal(skillName, version.Name);

        // Download SKILL.md
        var downloadedContent = await _fixture.Client.GetSkillFileAsStringAsync(skillName, "1.0.0", ct: ct);
        Assert.Contains("# Test Skill", downloadedContent);

        // Verify digest
        var verified = await _fixture.Client.VerifyDigestAsync(skillName, "1.0.0", version.Sha256, ct);
        Assert.True(verified);
    }

    [Fact]
    public async Task GetBlob_ByDigest()
    {
        var ct = TestContext.Current.CancellationToken;
        var skillName = $"blob-test-{Guid.NewGuid():N}"[..20];

        // Upload a skill first
        var skillContent = $"""
            ---
            name: {skillName}
            description: Testing blob endpoint
            ---

            # Blob Test
            """;

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(skillName), "name");
        content.Add(new StringContent("1.0.0"), "version");

        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(skillContent));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/markdown");
        content.Add(fileContent, "file", "SKILL.md");

        await _fixture.AuthenticatedHttpClient.PostAsync("/skills", content, ct);

        // Get the version to find the digest
        var version = await _fixture.Client.GetVersionAsync(skillName, "1.0.0", ct);
        Assert.NotNull(version);

        // Download via blob endpoint
        await using var blobStream = await _fixture.Client.GetBlobAsync(version.Sha256, ct);
        using var reader = new StreamReader(blobStream);
        var blobContent = await reader.ReadToEndAsync(ct);

        Assert.Contains("# Blob Test", blobContent);
    }

    [Fact]
    public async Task DeleteVersion_RemovesSkill()
    {
        var ct = TestContext.Current.CancellationToken;
        var skillName = $"del-test-{Guid.NewGuid():N}"[..20];

        // Upload a skill
        var skillContent = $"""
            ---
            name: {skillName}
            description: Testing delete
            ---

            # Delete Test
            """;

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(skillName), "name");
        content.Add(new StringContent("1.0.0"), "version");

        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(skillContent));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/markdown");
        content.Add(fileContent, "file", "SKILL.md");

        await _fixture.AuthenticatedHttpClient.PostAsync("/skills", content, ct);

        // Verify it exists
        var version = await _fixture.Client.GetVersionAsync(skillName, "1.0.0", ct);
        Assert.NotNull(version);

        // Delete it (requires auth)
        var deleteResponse = await _fixture.AuthenticatedHttpClient.DeleteAsync($"/skills/{skillName}/1.0.0", ct);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Verify it's gone
        var versions = await _fixture.Client.GetSkillVersionsAsync(skillName, ct);
        Assert.Empty(versions);
    }

    [Fact]
    public async Task Pagination_WorksCorrectly()
    {
        var ct = TestContext.Current.CancellationToken;
        var prefix = $"page-{Guid.NewGuid():N}"[..10];

        // Upload multiple skills
        for (int i = 1; i <= 3; i++)
        {
            var skillName = $"{prefix}-{i}";
            var skillContent = $"""
                ---
                name: {skillName}
                description: Pagination test skill {i}
                ---

                # Page Test {i}
                """;

            using var content = new MultipartFormDataContent();
            content.Add(new StringContent(skillName), "name");
            content.Add(new StringContent("1.0.0"), "version");

            var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(skillContent));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/markdown");
            content.Add(fileContent, "file", "SKILL.md");

            await _fixture.AuthenticatedHttpClient.PostAsync("/skills", content, ct);
        }

        // Test pagination - get all skills then verify we can paginate
        var allSkills = await _fixture.Client.ListSkillsAsync(ct: ct);
        var ourSkills = allSkills.Where(s => s.Name.StartsWith(prefix)).ToList();
        Assert.Equal(3, ourSkills.Count);

        // Test take
        var limited = await _fixture.Client.ListSkillsAsync(take: 2, ct: ct);
        Assert.Equal(2, limited.Count);
    }

    [Fact]
    public async Task UploadSkill_WithoutApiKey_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("no-auth-test"), "name");
        content.Add(new StringContent("1.0.0"), "version");

        var fileContent = new ByteArrayContent("---\nname: no-auth-test\ndescription: test\n---\n# Test"u8.ToArray());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/markdown");
        content.Add(fileContent, "file", "SKILL.md");

        var response = await _fixture.HttpClient.PostAsync("/skills", content, ct);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteSkill_WithoutApiKey_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _fixture.HttpClient.DeleteAsync("/skills/nonexistent/1.0.0", ct);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UploadSkill_WithInvalidApiKey_Returns403()
    {
        var ct = TestContext.Current.CancellationToken;

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("bad-key-test"), "name");
        content.Add(new StringContent("1.0.0"), "version");

        var fileContent = new ByteArrayContent("---\nname: bad-key-test\ndescription: test\n---\n# Test"u8.ToArray());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/markdown");
        content.Add(fileContent, "file", "SKILL.md");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/skills");
        request.Content = content;
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "sk-invalid-key");

        var response = await _fixture.HttpClient.SendAsync(request, ct);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetSkills_WithoutApiKey_Returns200()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _fixture.HttpClient.GetAsync("/skills", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ApiKeyManagement_CreateListDelete_EndToEnd()
    {
        var ct = TestContext.Current.CancellationToken;

        // Create a new key
        var createResponse = await _fixture.AuthenticatedHttpClient.PostAsJsonAsync("/api-keys",
            new { label = "test-key" }, ct);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var jsonOptions = new JsonSerializerOptions
        {
            TypeInfoResolver = SkillServerClientJsonContext.Default
        };
        var created = await createResponse.Content.ReadFromJsonAsync<Netclaw.SkillClient.CreateApiKeyResponse>(jsonOptions, ct);
        Assert.NotNull(created);
        Assert.Equal("test-key", created.Label);
        Assert.StartsWith("sk-", created.Key);

        // List keys
        var listResponse = await _fixture.AuthenticatedHttpClient.GetAsync("/api-keys", ct);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var keys = await listResponse.Content.ReadFromJsonAsync<IReadOnlyList<Netclaw.SkillClient.ApiKeySummary>>(jsonOptions, ct);
        Assert.NotNull(keys);
        Assert.Contains(keys, k => k.Label == "test-key");

        // Delete the new key (not the bootstrap key)
        var deleteResponse = await _fixture.AuthenticatedHttpClient.DeleteAsync($"/api-keys/{created.Id}", ct);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task ApiKeyManagement_WithoutAuth_Returns401()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _fixture.HttpClient.GetAsync("/api-keys", ct);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
