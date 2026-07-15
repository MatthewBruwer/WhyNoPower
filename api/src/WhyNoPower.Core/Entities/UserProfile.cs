namespace WhyNoPower.Core.Entities;

/// <summary>App-specific user data, 1:1 with the ASP.NET Identity user (kept separate by design).</summary>
public class UserProfile
{
    public long Id { get; set; }
    public string AspNetUserId { get; set; } = null!;
    public string DisplayName { get; set; } = null!;

    /// <summary>Entire storage footprint of the feature-flagged ad/reward mechanic.</summary>
    public DateTime? AdFreeUntilUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public List<PvSystem> Systems { get; set; } = new();
    public List<UserSuburb> Suburbs { get; set; } = new();

    public void GrantAdFreeDay(DateTime utcNow) => AdFreeUntilUtc = utcNow.AddHours(24);
}

public class Suburb
{
    public long Id { get; set; }
    public string Name { get; set; } = null!;
    public string Municipality { get; set; } = null!;
    public string Province { get; set; } = null!;
    /// <summary>Suburb centroid — the POPIA-safe weather query point. Never a user address.</summary>
    public decimal Lat { get; set; }
    public decimal Lng { get; set; }
    /// <summary>Nullable until Phase 2 ESP area mapping.</summary>
    public long? EspAreaId { get; set; }
}

public class UserSuburb
{
    public long Id { get; set; }
    public long UserProfileId { get; set; }
    public long SuburbId { get; set; }
    public string Label { get; set; } = "home";
    public bool IsPrimary { get; set; }

    public UserProfile UserProfile { get; set; } = null!;
    public Suburb Suburb { get; set; } = null!;
}
