// -----------------------------------------------------------------------
// <copyright file="SeedDataService.cs" company="Petabridge, LLC">
//      Copyright (C) 2026 - 2026 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------
using SkillServer.Models;

namespace SkillServer.Services;

public sealed class SeedDataService
{
    private readonly IConfiguration _configuration;
    private readonly SkillUploadService _skillUploadService;
    private readonly SubAgentUploadService _subAgentUploadService;
    private readonly ILogger<SeedDataService> _logger;

    public SeedDataService(
        IConfiguration configuration,
        SkillUploadService skillUploadService,
        SubAgentUploadService subAgentUploadService,
        ILogger<SeedDataService> logger)
    {
        _configuration = configuration;
        _skillUploadService = skillUploadService;
        _subAgentUploadService = subAgentUploadService;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var seedData = _configuration.GetValue<bool>("SkillServer:SeedData");
        if (!seedData)
        {
            _logger.LogDebug("SeedData is disabled; skipping seed.");
            return;
        }

        var seedPath = _configuration.GetValue<string>("SkillServer:SeedPath") ?? "./seed";

        await SeedSkillsAsync(seedPath, ct);
        await SeedSubAgentsAsync(seedPath, ct);
    }

    private async Task SeedSkillsAsync(string seedPath, CancellationToken ct)
    {
        var skillsDir = Path.Combine(seedPath, "skills");

        if (!Directory.Exists(skillsDir))
        {
            _logger.LogWarning("Seed skills directory not found: {Path}", skillsDir);
            return;
        }

        var files = Directory.GetFiles(skillsDir, "*.md");
        if (files.Length == 0)
        {
            _logger.LogDebug("No skill files found in {Path}", skillsDir);
            return;
        }

        _logger.LogInformation("Seeding {Count} skill(s) from {Path}", files.Length, skillsDir);

        foreach (var file in files)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (!SkillName.TryCreate(name, out var skillName))
            {
                _logger.LogWarning("Skipping skill file {File}: '{Name}' is not a valid skill name.", file, name);
                continue;
            }

            var version = SkillVersionString.Create("1.0.0");

            await using var stream = File.OpenRead(file);
            var result = await _skillUploadService.UploadSkillMdAsync(
                skillName.Value, version, stream, ct: ct);

            if (result.Success)
            {
                _logger.LogInformation("Seeded skill {Name} version {Version}", name, version.Value);
            }
            else if (result.IsDuplicateVersion)
            {
                _logger.LogDebug("Skill {Name} version {Version} already exists; skipping.", name, version.Value);
            }
            else
            {
                _logger.LogWarning("Failed to seed skill {Name}: {Error}", name, result.Error);
            }
        }
    }

    private async Task SeedSubAgentsAsync(string seedPath, CancellationToken ct)
    {
        var subAgentsDir = Path.Combine(seedPath, "subagents");

        if (!Directory.Exists(subAgentsDir))
        {
            _logger.LogWarning("Seed subagents directory not found: {Path}", subAgentsDir);
            return;
        }

        var files = Directory.GetFiles(subAgentsDir, "*.md");
        if (files.Length == 0)
        {
            _logger.LogDebug("No subagent files found in {Path}", subAgentsDir);
            return;
        }

        _logger.LogInformation("Seeding {Count} sub-agent(s) from {Path}", files.Length, subAgentsDir);

        foreach (var file in files)
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (!SkillName.TryCreate(name, out var skillName))
            {
                _logger.LogWarning("Skipping subagent file {File}: '{Name}' is not a valid skill name.", file, name);
                continue;
            }

            var version = SkillVersionString.Create("1.0.0");

            await using var stream = File.OpenRead(file);
            var result = await _subAgentUploadService.UploadSubAgentAsync(
                skillName.Value, version, stream, ct);

            if (result.Success)
            {
                _logger.LogInformation("Seeded sub-agent {Name} version {Version}", name, version.Value);
            }
            else if (result.IsDuplicateVersion)
            {
                _logger.LogDebug("Sub-agent {Name} version {Version} already exists; skipping.", name, version.Value);
            }
            else
            {
                _logger.LogWarning("Failed to seed sub-agent {Name}: {Error}", name, result.Error);
            }
        }
    }
}