using WhyNoPower.Core.Entities;
using WhyNoPower.Core.Services;
using Xunit;

namespace WhyNoPower.Tests;

public class SavingsCalculatorTests
{
    [Fact]
    public void UsesLatestEffectiveTariff_NotAFutureOne()
    {
        var tariffs = new[]
        {
            new Tariff { RateCentsPerKwh = 300, EffectiveFrom = new DateOnly(2025, 7, 1) },
            new Tariff { RateCentsPerKwh = 350, EffectiveFrom = new DateOnly(2026, 7, 1) },
        };

        // Before the July 2026 increase — must use the 300c rate, not the newer one.
        var cents = SavingsCalculator.ToCents(1000, new DateOnly(2026, 1, 15), tariffs);
        Assert.Equal(300, cents);
    }

    [Fact]
    public void ThrowsWhenNoTariffInEffectYet()
    {
        var tariffs = new[] { new Tariff { RateCentsPerKwh = 350, EffectiveFrom = new DateOnly(2026, 7, 1) } };
        Assert.Throws<InvalidOperationException>(() =>
            SavingsCalculator.ToCents(1000, new DateOnly(2020, 1, 1), tariffs));
    }
}

public class BatteryRuntimeCalculatorTests
{
    [Fact]
    public void MatchesTheRealSystem_5kWhBattery_80PctUsable_1kWLoad()
    {
        var system = new PvSystem { BatteryCapacityWh = 5000, BatteryUsablePct = 80, EssentialLoadW = 1000 };
        // 5000 * 0.8 / 1000 = 4.0 h
        Assert.Equal(4.0m, BatteryRuntimeCalculator.Hours(system));
    }

    [Fact]
    public void ReturnsZero_WhenNoEssentialLoadSet()
    {
        var system = new PvSystem { BatteryCapacityWh = 5000, BatteryUsablePct = 80, EssentialLoadW = 0 };
        Assert.Equal(0m, BatteryRuntimeCalculator.Hours(system));
    }
}

public class BestHoursAdvisorTests
{
    [Fact]
    public void FindsLongestContiguousWindowAbove70PctOfPeak()
    {
        // Peak = 3600 Wh at 13:00. Threshold = 2520. Hours 11,12,13,14 qualify contiguously.
        var hourly = new List<HourlyPoint>
        {
            new(new TimeOnly(9, 0), 1400),
            new(new TimeOnly(10, 0), 2300),
            new(new TimeOnly(11, 0), 3200),
            new(new TimeOnly(12, 0), 3600),
            new(new TimeOnly(13, 0), 3500),
            new(new TimeOnly(14, 0), 2800),
            new(new TimeOnly(15, 0), 1800),
        };

        var window = BestHoursAdvisor.PeakWindow(hourly);

        Assert.NotNull(window);
        Assert.Equal(new TimeOnly(11, 0), window!.Start);
        Assert.Equal(new TimeOnly(14, 0), window.End);
    }

    [Fact]
    public void ReturnsNull_WhenNoGeneration()
    {
        var hourly = new List<HourlyPoint> { new(new TimeOnly(9, 0), 0), new(new TimeOnly(10, 0), 0) };
        Assert.Null(BestHoursAdvisor.PeakWindow(hourly));
    }
}

public class PanelGroupTests
{
    [Fact]
    public void EffectiveTilt_DefaultsToLatitude_WhenUserSkipped()
    {
        var group = new PanelGroup { TiltDeg = null };
        // Johannesburg latitude ≈ -26.2 -> rounds to 26.
        Assert.Equal(26, group.EffectiveTilt(-26.2m));
    }

    [Fact]
    public void EffectiveTilt_PrefersUserValue_WhenProvided()
    {
        var group = new PanelGroup { TiltDeg = 80 }; // real Group B, per Architecture_Handoff.md
        Assert.Equal(80, group.EffectiveTilt(-26.2m));
    }

    [Fact]
    public void GroupKwp_MatchesRealArray_GroupA()
    {
        // 5 x 590W = 2.95 kWp, per Architecture_Handoff.md §7.1
        var group = new PanelGroup { PanelCount = 5, PanelWatt = 590 };
        Assert.Equal(2.95m, group.GroupKwp());
    }
}
