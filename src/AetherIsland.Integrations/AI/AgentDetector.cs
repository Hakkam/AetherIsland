using System.Diagnostics;
using AetherIsland.Core.Models;

namespace AetherIsland.Integrations.AI;

/// <summary>
/// Detects local coding-agent activity without reading any content:
///  - running processes (claude, codex, opencode, zcode)
///  - recency of the agents' local session metadata (Claude Code / Codex / OpenCode data dirs)
/// Project labels are derived from the session folder names only.
/// </summary>
public sealed class AgentDetector
{
    private readonly Func<bool> _metadataConsent;

    public AgentDetector(bool metadataConsent) : this(() => metadataConsent) { }
    public AgentDetector(Func<bool> metadataConsent) => _metadataConsent = metadataConsent;

    public List<AgentInfo> Detect()
    {
        var agents = new List<AgentInfo>();
        if (!_metadataConsent()) return agents;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1) running processes
        foreach (var (procName, agentName, provider) in new[]
        {
            ("claude", "Claude Code", "Anthropic"),
            ("codex", "Codex CLI", "OpenAI"),
            ("opencode", "OpenCode", "OpenCode"),
            ("zcode", "ZCode", "Z.ai / GLM")
        })
        {
            try
            {
                foreach (var proc in Process.GetProcessesByName(procName))
                {
                    using (proc)
                    {
                        if (seen.Add(agentName))
                        {
                            agents.Add(new AgentInfo
                            {
                                AgentName = agentName,
                                State = "running",
                                Provider = provider,
                                StartedAt = TryGetStartTime(proc),
                                LastActivity = DateTime.Now,
                                Source = UsageSource.VerifiedLocal
                            });
                        }
                    }
                }
            }
            catch { }
        }

        // 2) recent local session metadata → "waiting"/"idle" states for non-running agents
        TryAddFromRecency(agents, seen, "Claude Code", "Anthropic",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects"), "*.jsonl");
        TryAddFromRecency(agents, seen, "Codex CLI", "OpenAI",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex", "sessions"), "*.jsonl");
        TryAddFromRecency(agents, seen, "OpenCode", "OpenCode",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "opencode"), "*.json");

        return agents;
    }

    private static DateTime? TryGetStartTime(Process proc)
    {
        try { return proc.StartTime; } catch { return null; }
    }

    private static void TryAddFromRecency(List<AgentInfo> agents, HashSet<string> seen, string agentName, string provider, string dir, string pattern)
    {
        try
        {
            if (!Directory.Exists(dir)) return;
            DateTime? mostRecent = null;
            string? recentProject = null;
            foreach (var file in Directory.EnumerateFiles(dir, pattern, SearchOption.AllDirectories))
            {
                DateTime write;
                try { write = File.GetLastWriteTime(file); } catch { continue; }
                if (mostRecent is null || write > mostRecent)
                {
                    mostRecent = write;
                    recentProject = Path.GetFileName(Path.GetDirectoryName(file));
                }
            }
            if (mostRecent is null || seen.Contains(agentName)) return;
            var age = DateTime.Now - mostRecent.Value;
            if (age.TotalHours > 24) return; // not "active" anymore

            const string state = "idle";
            agents.Add(new AgentInfo
            {
                AgentName = agentName,
                State = state,
                Provider = provider,
                ProjectLabel = recentProject,
                LastActivity = mostRecent,
                Source = UsageSource.VerifiedLocal
            });
            seen.Add(agentName);
        }
        catch { }
    }
}
