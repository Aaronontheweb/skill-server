# Netclaw.SkillClient

.NET client library for [SkillServer](https://github.com/netclaw-dev/skill-server), a self-hosted skill registry for AI agents.

SkillServer implements the [Cloudflare Agent Skills Discovery RFC v0.2.0](https://github.com/cloudflare/agent-skills-spec) and the [AgentSkills.io](https://agentskills.io) SKILL.md format.

## Installation

```bash
dotnet add package Netclaw.SkillClient
```

## Usage

### Direct Instantiation

```csharp
using Netclaw.SkillClient;

// Read-only access (no auth needed for discovery and downloads)
using var client = new SkillServerClient("http://localhost:8080");

// With API key for write operations (publish, delete)
using var client = new SkillServerClient("http://localhost:8080", apiKey: "sk-your-api-key");
```

### Dependency Injection

```csharp
// Read-only
services.AddSkillServerClient("http://localhost:8080");

// With API key
services.AddSkillServerClient("http://localhost:8080", "sk-your-api-key");
```

## Discovering Skills

```csharp
// Get the RFC-compliant skill index (/.well-known/agent-skills/index.json)
var index = await client.GetRfcIndexAsync();

foreach (var skill in index.Skills)
{
    Console.WriteLine($"{skill.Name} - {skill.Description}");
    Console.WriteLine($"  URL: {skill.Url}");
    Console.WriteLine($"  Digest: {skill.Digest}");
}
```

## Listing and Browsing Skills

```csharp
// List all skills (paginated)
var skills = await client.ListSkillsAsync();
var page = await client.ListSkillsAsync(skip: 10, take: 5);

// Get all versions of a skill
var versions = await client.GetSkillVersionsAsync("my-skill");

// Get a specific version
var version = await client.GetVersionAsync("my-skill", "1.0.0");
```

## Downloading Skills

```csharp
// Download SKILL.md as a string
var content = await client.GetSkillFileAsStringAsync("my-skill", "1.0.0");

// Download SKILL.md as a stream
await using var stream = await client.GetSkillFileAsync("my-skill", "1.0.0");

// Download a resource file from a skill archive
await using var resource = await client.GetSkillFileAsync("my-skill", "1.0.0", "prompts/system.md");

// Download a blob by its SHA-256 digest
await using var blob = await client.GetBlobAsync("sha256:abc123...");
```

## Verifying Integrity

```csharp
// Verify a downloaded skill matches its published digest
var isValid = await client.VerifyDigestAsync("my-skill", "1.0.0", "sha256:abc123...");
```

## Publishing Skills

Requires an API key with write access.

```csharp
await using var file = File.OpenRead("SKILL.md");
var result = await client.UploadSkillAsync("my-skill", "1.0.0", file, category: "coding");

Console.WriteLine($"Published: {result.Url}");
Console.WriteLine($"Digest: {result.Sha256}");
```

## Deleting Skills

```csharp
await client.DeleteVersionAsync("my-skill", "1.0.0");
```

## AOT Compatibility

This library is fully AOT-compatible. All JSON serialization uses source-generated `System.Text.Json` contexts with no runtime reflection.

## License

Apache-2.0 - Copyright 2025 [Petabridge, LLC](https://petabridge.com)
