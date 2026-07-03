// -----------------------------------------------------------------------
// <copyright file="ClientForwardCompatTests.cs" company="Petabridge, LLC">
//      Copyright (C) 2026 - 2026 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------
using System.Net;
using System.Text;
using Netclaw.SkillClient;
using Xunit;

namespace SkillServer.Integration.Tests;

/// <summary>
/// Forward-compatibility hardening for the native manifest client (issue #93):
/// a client built against today's schema must tolerate unknown resource kinds,
/// unknown artifact types, and unrecognized fields that a newer server may emit,
/// rather than failing to parse them.
/// </summary>
public sealed class ClientForwardCompatTests
{
    private sealed class CannedJsonHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    private static SkillServerClient CreateClient(string json)
    {
        var httpClient = new HttpClient(new CannedJsonHandler(json))
        {
            BaseAddress = new Uri("http://localhost/")
        };
        return new SkillServerClient(httpClient);
    }

    [Fact]
    public async Task GetManifest_WithUnknownLinkAndTopLevelField_IsTolerated()
    {
        const string json = """
            {
              "$schema": "https://netclaw.dev/manifest/v1",
              "generatedAt": "2026-07-03T00:00:00Z",
              "futureTopLevelField": { "anything": [1, 2, 3] },
              "links": {
                "self": { "href": "/manifest.json" },
                "rfcSkills": { "href": "/.well-known/agent-skills/index.json" },
                "skills": { "href": "/manifest/skills/index.json" },
                "subagents": { "href": "/manifest/subagents/index.json" },
                "futureCollection": { "href": "/manifest/future/index.json" }
              }
            }
            """;

        using var client = CreateClient(json);
        var ct = TestContext.Current.CancellationToken;

        var manifest = await client.GetManifestAsync(ct);

        Assert.NotNull(manifest);
        Assert.Equal("/manifest/skills/index.json", manifest.Links.Skills.Href);
        Assert.Equal("/manifest/subagents/index.json", manifest.Links.SubAgents.Href);
    }

    [Fact]
    public async Task GetNativeSkillPage_WithUnknownKindAndExtraFields_IsTolerated()
    {
        const string json = """
            {
              "kind": "future-skill-page-kind",
              "range": "0-1",
              "unexpectedCollectionField": ["ignore", "me"],
              "items": [
                {
                  "name": "example-skill",
                  "latestVersion": "1.0.0",
                  "versionRange": { "min": "1.0.0", "max": "1.0.0", "count": 1 },
                  "href": "/manifest/skills/example-skill/index.json",
                  "futureItemField": "ignored"
                }
              ],
              "links": { "self": { "href": "/manifest/skills/pages/0.json" } }
            }
            """;

        using var client = CreateClient(json);
        var ct = TestContext.Current.CancellationToken;

        var page = await client.GetNativeSkillPageAsync("manifest/skills/pages/0.json", ct);

        Assert.NotNull(page);
        // Unknown kind values round-trip as opaque strings instead of failing.
        Assert.Equal("future-skill-page-kind", page.Kind);
        var item = Assert.Single(page.Items);
        Assert.Equal("example-skill", item.Name);
        Assert.Equal("/manifest/skills/example-skill/index.json", item.Href);
    }

    [Fact]
    public async Task GetNativeSkillVersion_WithUnknownArtifactType_IsTolerated()
    {
        const string json = """
            {
              "kind": "skill-version",
              "artifact": {
                "name": "example-skill",
                "version": "1.0.0",
                "type": "oci-image",
                "description": "A future artifact type older clients do not understand.",
                "url": "/skills/example-skill/1.0.0/artifact.oci",
                "digest": "sha256:1111111111111111111111111111111111111111111111111111111111111111",
                "futureArtifactField": 42
              },
              "routesToSubagent": null,
              "links": { "self": { "href": "/manifest/skills/example-skill/versions/1.0.0.json" } }
            }
            """;

        using var client = CreateClient(json);
        var ct = TestContext.Current.CancellationToken;

        var detail = await client.GetNativeSkillVersionByHrefAsync(
            "manifest/skills/example-skill/versions/1.0.0.json", ct);

        Assert.NotNull(detail);
        // An unrecognized artifact type is surfaced verbatim so a caller can skip
        // it, rather than causing deserialization to throw.
        Assert.Equal("oci-image", detail.Artifact.Type);
        Assert.Null(detail.RoutesToSubagent);
    }
}
