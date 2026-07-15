namespace WhyNoPower.Core.Entities;

/// <summary>
/// A user's solar installation. Named PvSystem (not System) to avoid colliding with the
/// .NET root namespace; maps to the SYSTEMS table.
/// </summary>
public class PvSystem
{
    public long Id { get; set; }
    public long UserProfileId { get; set; }
    public long SuburbId { get; set; }
    public string Name { get; set; } = null!;

    public int InverterMaxW { get; set; }
    public InverterType InverterType { get; set; }

    public int? BatteryCapacityWh { get; set; }
    public int BatteryUsablePct { get; set; } = 80;

    /// <summary>Average essential load during an outage (W) — captured on the power page.</summary>
    public int EssentialLoadW { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public UserProfile UserProfile { get; set; } = null!;
    public Suburb Suburb { get; set; } = null!;
    public List<PanelGroup> PanelGroups { get; set; } = new();
    public List<Tariff> Tariffs { get; set; } = new();
    public InverterConnection? InverterConnection { get; set; }

    public decimal TotalKwp() => PanelGroups.Sum(g => g.GroupKwp());

    public int UsableBatteryWh() =>
        BatteryCapacityWh is int cap ? (int)(cap * (BatteryUsablePct / 100m)) : 0;
}

public class PanelGroup
{
    public long Id { get; set; }
    public long PvSystemId { get; set; }
    public int PanelCount { get; set; }
    public int PanelWatt { get; set; }

    /// <summary>Null = user skipped the optional field. Defaults are applied by behaviour,
    /// never written back — preserving "user said 30°" vs "we assumed 26°".</summary>
    public int? AzimuthDeg { get; set; }
    public int? TiltDeg { get; set; }

    public PvSystem PvSystem { get; set; } = null!;

    public decimal GroupKwp() => PanelCount * PanelWatt / 1000m;

    /// <summary>Default: due north (0° in our convention).</summary>
    public int EffectiveAzimuth() => AzimuthDeg ?? 0;

    /// <summary>Default: |latitude| rounded — ≈26° for Johannesburg.</summary>
    public int EffectiveTilt(decimal latitude) => TiltDeg ?? (int)Math.Round(Math.Abs(latitude));
}

/// <summary>Effective-dated tariff history — never a mutable column (NERSA increases land every July).</summary>
public class Tariff
{
    public long Id { get; set; }
    public long PvSystemId { get; set; }
    public int RateCentsPerKwh { get; set; }
    public DateOnly EffectiveFrom { get; set; }

    public PvSystem PvSystem { get; set; } = null!;
}

public class InverterConnection
{
    public long Id { get; set; }
    public long PvSystemId { get; set; }
    public string Provider { get; set; } = "growatt";
    public string Username { get; set; } = null!;
    /// <summary>Encrypted at rest. Skeleton uses ASP.NET Data Protection; production swaps to a
    /// Key Vault-wrapped key (ADR-005). Never logged, never returned by any endpoint.</summary>
    public byte[] CredentialsEncrypted { get; set; } = Array.Empty<byte>();
    public string? DataloggerSn { get; set; }
    public ConnectionStatus Status { get; set; } = ConnectionStatus.Unverified;
    public DateTime? LastSyncedAtUtc { get; set; }

    public PvSystem PvSystem { get; set; } = null!;
}
