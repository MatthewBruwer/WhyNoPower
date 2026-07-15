namespace WhyNoPower.Core.Entities;

public enum InverterType { OffGrid, Hybrid, GridTied }

/// <summary>Drives the UI sync chip. Transitions per docs/architecture/system-design.md §8.</summary>
public enum ConnectionStatus { Unverified, Connected, Disconnected, AuthFailed }

public enum RollupSource { Measured, Manual }
