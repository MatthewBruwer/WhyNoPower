namespace WhyNoPower.Core.Entities;

public class GenerationSample
{
    public long Id { get; set; }
    public long PvSystemId { get; set; }
    public long? RawPayloadId { get; set; }
    public DateTime SampledAtUtc { get; set; }
    public int AcPowerW { get; set; }
    public int EnergyTodayWh { get; set; }
    public int? BatterySocPct { get; set; }
}

public class DailyGenerationRollup
{
    public long Id { get; set; }
    public long PvSystemId { get; set; }
    public DateOnly DateLocal { get; set; }
    public int TotalWh { get; set; }
    public int PeakW { get; set; }
    public RollupSource Source { get; set; }
    /// <summary>Battery-full + high sun + low output. Badged, not scored (analytics doc §3).</summary>
    public bool IsCurtailed { get; set; }
    public DateTime ComputedAtUtc { get; set; }
}

public class ManualGenerationEntry
{
    public long Id { get; set; }
    public long PvSystemId { get; set; }
    public DateOnly DateLocal { get; set; }
    public int TotalWh { get; set; }
    public DateTime EnteredAtUtc { get; set; }
}

public class GenerationForecast
{
    public long Id { get; set; }
    public long PvSystemId { get; set; }
    public long? ModelVersionId { get; set; }
    public DateTime IssuedAtUtc { get; set; }
    public DateOnly TargetDateLocal { get; set; }
    /// <summary>Null = daily total row rather than an hourly row.</summary>
    public DateTime? TargetHourUtc { get; set; }
    public int PhysicsWh { get; set; }
    public int PredictedWh { get; set; }

    public int DeviationWh(int actualWh) => actualWh - PredictedWh;
}

public class ModelVersion
{
    public long Id { get; set; }
    public string Domain { get; set; } = null!;
    public string Version { get; set; } = null!;
    public DateTime TrainedAtUtc { get; set; }
    public string MetricsJson { get; set; } = "{}";
    public string? Notes { get; set; }
}

/// <summary>Raw-first landing zone (ADR-005 generalised). Every external fetch, as received.</summary>
public class RawIngestPayload
{
    public long Id { get; set; }
    public string Source { get; set; } = null!; // growatt / open_meteo / esp / jw
    public string PayloadJson { get; set; } = null!;
    public int? HttpStatus { get; set; }
    public string ContentHash { get; set; } = null!;
    public DateTime FetchedAtUtc { get; set; }
}
