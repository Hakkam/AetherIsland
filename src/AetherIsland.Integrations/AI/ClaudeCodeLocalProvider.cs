using System.Text.Json;
using AetherIsland.Core.Models;

namespace AetherIsland.Integrations.AI;

/// <summary>
/// Reads Claude Code local session transcripts (~/.claude/projects/**/*.jsonl) and
/// aggregates assistant-message usage entries. Only metadata (tokens, model, timestamp,
/// project folder name) is read — prompts and completions are never parsed or stored.
/// </summary>
public sealed class ClaudeCodeLocalProvider : IAiUsageProvider
{
    public string ProviderId => AiProviderIds.ClaudeCode;
    public string DisplayName { get; set; } = "Claude Code";

    private readonly bool _metadataConsent;
    public ClaudeCodeLocalProvider(bool metadataConsent) => _metadataConsent = metadataConsent;

    private string ProjectsDir
    {
        get
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, ".claude", "projects");
        }
    }

    public Task<ConnectionStatus> CheckConnectionAsync(CancellationToken ct)
    {
        if (!_metadataConsent)
            return Task.FromResult(new ConnectionStatus { Connected = false, State = "disconnected", Message = "Local agent metadata permission is off (Settings → Privacy)", CheckedAt = DateTime.Now });
        var found = Directory.Exists(ProjectsDir);
        var sessions = found ? SafeSessionFiles().Count() : 0;
        return Task.FromResult(new ConnectionStatus
        {
            Connected = found,
            State = found ? "connected" : "notfound",
            Message = found ? $"Claude Code detected ({sessions} session files)" : "Claude Code data not found (~/.claude/projects)",
            AccountLabel = "this machine",
            CheckedAt = DateTime.Now
        });
    }

    public Task<UsageSnapshot> GetUsageAsync(UsagePeriod period, CancellationToken ct)
    {
        var snap = new UsageSnapshot
        {
            ProviderId = ProviderId,
            AccountLabel = "this machine",
            Period = period,
            SubscriptionCategory = "Local session metadata",
            CostSource = UsageSource.VerifiedLocal,
            CostSourceDescription = "Usage fields recorded by Claude Code in ~/.claude/projects"
        };

        if (!_metadataConsent) { snap.ErrorState = "Local agent metadata permission is off"; return Task.FromResult(snap); }

        var byModel = new Dictionary<string, ModelUsage>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var file in SafeSessionFiles())
            {
                FileInfo fi;
                try { fi = new FileInfo(file); } catch { continue; }
                if (fi.LastWriteTime < period.Start) continue;
                foreach (var line in ReadLinesSafe(file))
                {
                    if (line.Length < 40) continue;
                    try
                    {
                        using var doc = JsonDocument.Parse(line);
                        var root = doc.RootElement;
                        if (!root.TryGetProperty("message", out var msg)) continue;
                        if (!msg.TryGetProperty("usage", out var usage)) continue;
                        var model = HttpJson.GetString(msg, "model") ?? "unknown";
                        var input = HttpJson.GetLong(usage, "input_tokens") ?? 0;
                        var output = HttpJson.GetLong(usage, "output_tokens") ?? 0;
                        var cacheC = HttpJson.GetLong(usage, "cache_creation_input_tokens") ?? 0;
                        var cacheR = HttpJson.GetLong(usage, "cache_read_input_tokens") ?? 0;
                        if (input == 0 && output == 0 && cacheC == 0 && cacheR == 0) continue;
                        var key = model;
                        if (!byModel.TryGetValue(key, out var mu)) { mu = new ModelUsage { ModelId = key }; byModel[key] = mu; }
                        mu.InputTokens = (mu.InputTokens ?? 0) + input;
                        mu.OutputTokens = (mu.OutputTokens ?? 0) + output;
                        mu.CachedTokens = (mu.CachedTokens ?? 0) + cacheC + cacheR;
                        mu.RequestCount = (mu.RequestCount ?? 0) + 1;
                    }
                    catch { }
                }
            }
        }
        catch { }

        snap.Models = byModel.Values.ToList();
        snap.Success = byModel.Count > 0;
        if (!snap.Success) snap.ErrorState = "No Claude Code usage entries found in the period";
        snap.LastUpdatedAt = DateTime.Now;
        return Task.FromResult(snap);
    }

    private IEnumerable<string> SafeSessionFiles()
    {
        try
        {
            if (!Directory.Exists(ProjectsDir)) return Array.Empty<string>();
            return Directory.EnumerateFiles(ProjectsDir, "*.jsonl", SearchOption.AllDirectories).ToList();
        }
        catch { return Array.Empty<string>(); }
    }

    private static IEnumerable<string> ReadLinesSafe(string path)
    {
        try { return File.ReadLines(path); }
        catch { return Array.Empty<string>(); }
    }

    public Task<IReadOnlyList<QuotaWindow>> GetQuotaWindowsAsync(CancellationToken ct)
    {
        IReadOnlyList<QuotaWindow> windows = new List<QuotaWindow>
        {
            new()
            {
                Label = "Claude subscription quota",
                Dimension = QuotaDimension.None,
                Source = UsageSource.Unavailable,
                SourceDescription = "Claude Pro/Max remaining quota is not exposed by any public interface and is not read from the browser.",
            }
        };
        return Task.FromResult(windows);
    }
}
