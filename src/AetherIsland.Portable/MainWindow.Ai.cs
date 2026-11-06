using System.Windows;
using System.Windows.Controls;
using AetherIsland.Core.Models;
using AetherIsland.Integrations.AI;

namespace AetherIsland.Portable;

public partial class MainWindow
{
    private static readonly System.Threading.SemaphoreSlim AiGate = new(1, 1);

    private static UIElement RenderAi()
    {
        var stack = new StackPanel { Margin = new Thickness(16, 6, 12, 2), VerticalAlignment = VerticalAlignment.Center };

        stack.Children.Add(Ui.SectionTitle("ai", "AI usage · verified quotas only"));
        stack.Children.Add(Ui.Spacer(0, 8));
        stack.Children.Add(Ui.Text(
            "Only the OpenAI organization API monthly hard spend limit is supported — it is the only quota this app can verify against official endpoints.",
            10.5, false, Theme.Dim, wrap: true));
        stack.Children.Add(Ui.Text(
            "ChatGPT Plus/Pro, Claude, Codex, Z.ai and OpenCode subscriptions expose no documented quota API, so they are never shown.",
            9.5, false, Theme.Faint, wrap: true));
        stack.Children.Add(Ui.Spacer(0, 10));

        var key = Ui.Input("sk-…", 330);
        var status = Ui.Text("Add an organization Admin API key to begin.", 10, false, Theme.Dim, wrap: true);
        var cards = new StackPanel();

        var entry = Ui.H(key, Ui.TextButton("Save key", () =>
        {
            if (string.IsNullOrWhiteSpace(key.Text) || Ui.IsPlaceholder(key)) return;
            PortableData.Credentials.SetSecret("openai", key.Text.Trim());
            key.Clear();
            status.Text = "Key encrypted with Windows DPAPI for this account. Press Refresh.";
        }, "Store encrypted", 10, background: Theme.Chip, foreground: Theme.Text));
        stack.Children.Add(entry);
        stack.Children.Add(Ui.Spacer(0, 6));

        var controls = Ui.H(
            Ui.TextButton("Refresh verified quota", async () => await Refresh(), "Fetch from OpenAI", 10,
                background: Theme.Accent, foreground: Ui.Solid("#0B0E12")),
            Ui.TextButton("Remove key", () =>
            {
                PortableData.Credentials.RemoveSecret("openai");
                cards.Children.Clear();
                status.Text = "Encrypted key removed. No provider is connected.";
            }, "Forget stored key", 10, background: Theme.Chip, foreground: Theme.Bad));
        stack.Children.Add(controls);
        stack.Children.Add(Ui.Spacer(0, 6));
        stack.Children.Add(status);
        stack.Children.Add(Ui.Spacer(0, 4));
        stack.Children.Add(cards);

        async Task Refresh()
        {
            if (!await AiGate.WaitAsync(0))
            {
                status.Text = "A refresh is already running.";
                return;
            }
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
                    status.Text = "No verified, enforcing monthly organization hard limit available. No quota is shown — by design.";
                    return;
                }
                status.Text = $"Verified with the official OpenAI API · {DateTime.Now:HH:mm}";
                foreach (var q in verified)
                {
                    var used = q.Used!.Value;
                    var limit = q.Limit!.Value;
                    var card = Ui.V(
                        Ui.H(
                            Ui.Text($"{q.Label}", 11.5, true),
                            Ui.Text($"   ${used:0.00} / ${limit:0.00} USD", 11.5, true, Theme.Text)),
                        Ui.Spacer(0, 5),
                        Ui.Bar(used / limit, 430, 5),
                        Ui.Spacer(0, 4),
                        Ui.Text($"Remaining ${Math.Max(0, limit - used):0.00} · resets {q.ResetAt:yyyy-MM-dd} · {q.SourceDescription}",
                            9, false, Theme.Dim));
                    cards.Children.Add(card);
                    cards.Children.Add(Ui.Spacer(0, 10));
                }
            }
            catch (Exception ex)
            {
                // Never log HTTP bodies or key material.
                PortableData.Log("AI", ex.GetType().Name + ": " + ex.Message.Split('\n')[0][..Math.Min(120, ex.Message.Length)]);
                status.Text = "Unable to verify the quota — check the key, network, or OpenAI org access.";
                cards.Children.Clear();
            }
            finally { AiGate.Release(); }
        }

        return Ui.WrapPage(stack);
    }
}
