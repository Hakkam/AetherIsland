using AetherIsland.Core.Logic;
using AetherIsland.Core.Models;
using AetherIsland.Integrations.AI;
using Xunit;

namespace AetherIsland.Tests;

public class UsageAggregatorAlertTests
{
    private static UsageSnapshot Snapshot(params QuotaWindow[] windows)
    {
        return new UsageSnapshot
        {
            ProviderId = "test",
            Models = { new ModelUsage { ModelId = "m", EstimatedCost = 4.0 } },
            QuotaWindows = windows.ToList()
        };
    }

    [Fact]
    public void ManualBudget_TriggersAlert_WhenEstimatedCostCrosses()
    {
        var snap = Snapshot(new QuotaWindow
        {
            Label = "Manual monthly budget",
            Dimension = QuotaDimension.Dollars,
            Limit = 5,
            Source = UsageSource.ManualBudget
        });
        var alert = UsageAggregator.EvaluateAlerts("test", snap, new[] { 50, 80, 90, 100 });
        Assert.NotNull(alert);
        Assert.Equal(80, alert!.Threshold); // $4 of $5 = 80%
        Assert.Equal(UsageSource.ManualBudget, alert.Source);
    }

    [Fact]
    public void UnavailableQuota_NeverTriggersAlert()
    {
        var snap = Snapshot(new QuotaWindow
        {
            Label = "Subscription quota",
            Dimension = QuotaDimension.None,
            Source = UsageSource.Unavailable,
            SourceDescription = "not exposed"
        });
        Assert.Null(UsageAggregator.EvaluateAlerts("test", snap, new[] { 50, 80 }));
    }

    [Fact]
    public void EstimatedSource_NeverTriggersAlert()
    {
        var snap = Snapshot(new QuotaWindow
        {
            Label = "estimated",
            Dimension = QuotaDimension.Dollars,
            Limit = 5,
            Used = 4,
            Source = UsageSource.Estimated
        });
        Assert.Null(UsageAggregator.EvaluateAlerts("test", snap, new[] { 50, 80 }));
    }

    [Fact]
    public void VerifiedOfficial_TriggersAlert()
    {
        var snap = Snapshot(new QuotaWindow
        {
            Label = "org budget",
            Dimension = QuotaDimension.Dollars,
            Limit = 100,
            Used = 91,
            Source = UsageSource.VerifiedOfficial
        });
        var alert = UsageAggregator.EvaluateAlerts("test", snap, new[] { 50, 80, 90 });
        Assert.NotNull(alert);
        Assert.Equal(90, alert!.Threshold);
    }
}
