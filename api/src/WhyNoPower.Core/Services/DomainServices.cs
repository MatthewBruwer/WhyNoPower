using WhyNoPower.Core.Entities;

namespace WhyNoPower.Core.Services;

/// <summary>Wh × tariff-in-effect-on-date → cents. Pure, stateless — analytics doc §5.1.</summary>
public static class SavingsCalculator
{
    public static long ToCents(int wh, DateOnly onDate, IEnumerable<Tariff> tariffHistory)
    {
        var rate = tariffHistory
            .Where(t => t.EffectiveFrom <= onDate)
            .OrderByDescending(t => t.EffectiveFrom)
            .Select(t => (int?)t.RateCentsPerKwh)
            .FirstOrDefault();

        if (rate is null)
            throw new InvalidOperationException($"No tariff in effect on {onDate}.");

        var cents = wh * rate.Value / 1000m;
        return (long)Math.Round(cents, MidpointRounding.ToEven);
    }
}

/// <summary>usable Wh ÷ essential load W → hours. Analytics doc §5.2.</summary>
public static class BatteryRuntimeCalculator
{
    public static decimal Hours(PvSystem system)
    {
        if (system.EssentialLoadW <= 0) return 0m;
        var usableWh = system.UsableBatteryWh();
        return Math.Round(usableWh / (decimal)system.EssentialLoadW, 1);
    }
}

public record TimeRange(TimeOnly Start, TimeOnly End);

public record HourlyPoint(TimeOnly Hour, int PredictedWh);

/// <summary>Peak-window extraction over the potential-generation curve. Analytics doc §5.3.</summary>
public static class BestHoursAdvisor
{
    public static TimeRange? PeakWindow(IReadOnlyList<HourlyPoint> hourly)
    {
        if (hourly.Count == 0) return null;

        var peak = hourly.Max(h => h.PredictedWh);
        if (peak <= 0) return null;

        var threshold = 0.70m * peak;
        var eligible = hourly.Select((h, i) => (h, i))
            .Where(x => x.h.PredictedWh >= threshold)
            .Select(x => x.i)
            .ToList();

        if (eligible.Count == 0) return null;

        // Longest contiguous run; ties resolve to the earliest run.
        var bestStart = eligible[0];
        var bestLen = 1;
        var curStart = eligible[0];
        var curLen = 1;

        for (var k = 1; k < eligible.Count; k++)
        {
            if (eligible[k] == eligible[k - 1] + 1)
            {
                curLen++;
            }
            else
            {
                curStart = eligible[k];
                curLen = 1;
            }
            if (curLen > bestLen)
            {
                bestLen = curLen;
                bestStart = curStart;
            }
        }

        if (bestLen < 2)
        {
            var peakIdx = hourly.Select((h, i) => (h, i)).First(x => x.h.PredictedWh == peak).i;
            var startIdx = Math.Max(0, peakIdx - 1);
            var endIdx = Math.Min(hourly.Count - 1, peakIdx + 1);
            return new TimeRange(hourly[startIdx].Hour, hourly[endIdx].Hour);
        }

        return new TimeRange(hourly[bestStart].Hour, hourly[bestStart + bestLen - 1].Hour);
    }
}
