namespace WhyNoPower.Api.Contracts;

public record PanelGroupInput(int PanelCount, int PanelWatt, int? AzimuthDeg, int? TiltDeg);

public record CreateSystemRequest(
    string Name,
    string Suburb,
    int InverterMaxW,
    string InverterType,
    int? BatteryCapacityWh,
    int BatteryUsablePct,
    int EssentialLoadW,
    int TariffRateCentsPerKwh,
    List<PanelGroupInput> PanelGroups);

public record PanelGroupDto(long Id, int PanelCount, int PanelWatt, int? AzimuthDeg, int? TiltDeg, decimal GroupKwp);

public record SystemDto(
    long Id,
    string Name,
    string Suburb,
    int InverterMaxW,
    string InverterType,
    int? BatteryCapacityWh,
    int BatteryUsablePct,
    int EssentialLoadW,
    int CurrentTariffCentsPerKwh,
    decimal TotalKwp,
    string ConnectionStatus,
    List<PanelGroupDto> PanelGroups);
