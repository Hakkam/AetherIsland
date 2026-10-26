using System.Text.Json;
using AetherIsland.Core.Models;

namespace AetherIsland.Integrations.AI;

/// <summary>
/// Reads OpenAI Codex CLI local session rollouts (~/.codex/sessions/**/*.jsonl) and
/// aggregates the final token_count totals per session. Metadata only.
/// </summary>
public sealed class CodexLocalProvider : IAiUsageProvider
{
    public string ProviderId => AiProviderIds.OpenAiCodex;
    public string DisplayName { get; set; } = "OpenAI Codex CLI";

    private readonly bool _metadataConsent;
    public CodexLocalProvider(bool metadataConsent) => _metadataConsent = metadataConsent;

    private string SessionsDir
    {
        get
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, ".codex", "sessions");
        }
    }

    public Task<ConnectionStatus> CheckConnectionAsync(CancellationToken ct)
    {
        if (!_metadataConsent)
            return Task.FromResult(new ConnectionStatus { Connected = false, State = "disconnected", Message = "Local agent metadata permission is off (Settings → Privacy)", CheckedAt = DateTime.Now });
        var found = Directory.Exists(SessionsDir);
        return Task.FromResult(new ConnectionStatus
        {
            Connected = found,
            State = found ? "connected" : "notfound",
            Message = found ? "Codex CLI session data found" : "Codex CLI data not found (~/.codex/sessions)",
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
            CostSourceDescription = "token_count events recorded by Codex CLI in ~/.codex/sessions"
        };

        if (!_metadataConsent) { snap.ErrorState = "Local agent metadata permission is off"; return Task.FromResult(snap); }

        var files = SafeSessionFiles();
        var total = new ModelUsage { ModelId = "gpt (codex sessions)" };
        var sessions = 0;
        try
        {
            foreach (var file in files)
            {
                FileInfo fi;
                try { fi = new FileInfo(file); } catch { continue; }
                if (fi.LastWriteTime < period.Start) continue;
                long inTok = 0, outTok = 0, cache = 0;
                foreach (var line in ReadLinesSafe(file))
                {
                    if (!line.Contains("token_count")) continue;
                    try
                    {
                        using var doc = JsonDocument.Parse(line);
                        if (!doc.RootElement.TryGetProperty("payload", out var payload)) continue;
                        if (!payload.TryGetProperty("info", out var info)) continue;
                        // prefer the running total in total_token_usage; fall back to last_token_usage
                        var src = info.TryGetProperty("total_token_usage", out var ttu) ? ttu
                                : info.TryGetProperty("last_token_usage", out var ltu) ? ltu : default;
                        if (src.ValueKind != JsonValueKind.Object) continue;
                        inTok = HttpJson.GetLong(src, "input_tokens") ?? inTok;
                        outTok = HttpJson.GetLong(src, "output_tokens") ?? outTok;
                        var cached = HttpJson.GetLong(src, "cached_input_tokens");
                        if (cached is not null) cache = cached.Value;
                    }
                    catch { }
                }
                if (inTok == 0 && outTok == 0) continue;
                sessions++;
                total.InputTokens = (total.InputTokens ?? 0) + inTok;
                total.OutputTokens = (total.OutputTokens ?? 0) + outTok;
                total.CachedTokens = (total.CachedTokens ?? 0) + cache;
            }
        }
        catch { }

        if (sessions > 0)
        {
            total.RequestCount = sessions;
            snap.Models.Add(total);
            snap.Success = true;
        }
        else
        {
            snap.Success = false;
            snap.ErrorState = "No Codex CLI sessions with token data found in the period";
        }
        snap.LastUpdatedAt = DateTime.Now;
        return Task.FromResult(snap);
    }

    private IEnumerable<string> SafeSessionFiles()
    {
        try
        {
            if (!Directory.Exists(SessionsDir)) return Array.Empty<string>();
            return Directory.EnumerateFiles(SessionsDir, "*.jsonl", SearchOption.AllDirectories).ToList();
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
                Label = "ChatGPT/Codex plan quota",
                Dimension = QuotaDimension.None,
                Source = UsageSource.Unavailable,
                SourceDescription = "The Codex CLI does not expose remaining plan quota through a documented interface.",
            }
        };
        return Task.FromResult(windows);
    }
}
