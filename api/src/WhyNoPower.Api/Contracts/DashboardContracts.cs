namespace WhyNoPower.Api.Contracts;

public record DayPointDto(
    DateOnly Date,
    string Source,          // "measured" or "forecast"
    int TotalWh,
    long Cents,
    int? DeviationWh,       // measured - day-ahead forecast, null for future days
    long? DeviationCents,
    bool IsCurtailed);

public record WeekDashboardDto(
    string SuburbName,
    string ConnectionStatus,
    DateTime? LastSyncedAtUtc,
    int WeekTotalWh,
    long WeekTotalCents,
    List<DayPointDto> Days,
    decimal? BatteryRuntimeHours,
    string? BestHoursStart,
    string? BestHoursEnd,
    decimal? RollingAccuracyPct);
