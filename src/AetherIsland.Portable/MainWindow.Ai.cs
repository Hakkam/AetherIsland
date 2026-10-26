using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AetherIsland.Core.Models;
using AetherIsland.Integrations.AI;

namespace AetherIsland.Portable;

public partial class MainWindow
{
    private readonly System.Threading.SemaphoreSlim _aiGate = new(1, 1);
    private UIElement RenderAi()
    {
        var key = Ui.Input("", 325);
        key.Clear();
        var status = Ui.Text("Only a verified OpenAI API organization hard spend limit is supported.",
            11, false, Ui.Dim);
        var cards = new StackPanel();
        var source = Ui.Text("Not ChatGPT Plus/Pro, Codex, Claude, OpenCode, or Z.ai subscription quotas.",
            10, false, Ui.Dim);
        var controls = Ui.H(Ui.Button("Refresh verified quota", async () => await Refresh()),
            Ui.Button("Remove API key", () =>
            {
                PortableData.Credentials.RemoveSecret("openai");
                cards.Children.Clear();
                status.Text = "Encrypted key removed. No provider is connected.";
            }));
        var entry = Ui.H(key, Ui.Button("Save OpenAI admin key", () =>
        {
            if (string.IsNullOrWhiteSpace(key.Text)) return;
            PortableData.Credentials.SetSecret("openai", key.Text.Trim());
            key.Clear();
            status.Text = "Key encrypted for this Windows account. Press Refresh.";
        }));
        var result = Ui.V(Ui.Title("AI Usage · Official hard limits only"),
            new Border { Height = 6 }, source,
            new Border { Height = 10 }, entry,
            new Border { Height = 7 }, controls,
            new Border { Height = 8 }, status,
            new Border { Height = 7 }, cards);

        async System.Threading.Tasks.Task Refresh()
        {
            if (!await _aiGate.WaitAsync(0)) return;
            cards.Children.Clear();
            try
            {
                var adminKey = PortableData.Credentials.GetSecret("openai");
                if (string.IsNullOrWhiteSpace(adminKey))
                {
                    status.Text = "Add an authorized OpenAI organization Admin API key first.";
                    return;
                }
                status.Text = "Checking official OpenAI Admin API…";
                var provider = new OpenAiProvider(new ProviderConfig
                {
                    ProviderId = AiProviderIds.OpenAI,
                    AccountLabel = "API organization",
                    GetSecret = () => PortableData.Credentials.GetSecret("openai")
                });
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(25));
                var quota = await provider.GetQuotaWindowsAsync(cts.Token);
                var verified = quota.Where(q => q.Source == UsageSource.VerifiedOfficial
                    && q.Limit is > 0 && q.Used is >= 0 && q.Dimension == QuotaDimension.Dollars).ToList();
                if (verified.Count == 0)
                {
                    status.Text = "No verified, enforcing monthly organization hard limit available. No quota will be shown.";
                    return;
                }
                status.Text = $"Verified with OpenAI API · {DateTime.Now:HH:mm}";
                foreach (var q in verified)
                {
                    var used = q.Used!.Value;
                    var limit = q.Limit!.Value;
                    cards.Children.Add(Ui.Text($"{q.Label}: ${used:0.00} / ${limit:0.00} USD", 18, true));
                    cards.Children.Add(Ui.Bar(used / limit, 420));
                    cards.Children.Add(Ui.Text($"Remaining ${Math.Max(0, limit - used):0.00} · Reset {q.ResetAt:yyyy-MM-dd} · {q.SourceDescription}",
                        10, false, Ui.Dim));
                }
            }
            catch (Exception ex)
            {
                // Do not dump HTTP bodies or credentials to logs.
                PortableData.Log("AI", ex.GetType().Name);
                status.Text = "Unable to verify the quota. No figures will be shown.";
                cards.Children.Clear();
            }
            finally { _aiGate.Release(); }
        }

        return Ui.WrapPage(result);
    }
}
