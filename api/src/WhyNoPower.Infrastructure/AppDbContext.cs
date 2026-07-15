using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WhyNoPower.Core.Entities;
using WhyNoPower.Infrastructure.Identity;

namespace WhyNoPower.Infrastructure;

public class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<Suburb> Suburbs => Set<Suburb>();
    public DbSet<UserSuburb> UserSuburbs => Set<UserSuburb>();
    public DbSet<PvSystem> PvSystems => Set<PvSystem>();
    public DbSet<PanelGroup> PanelGroups => Set<PanelGroup>();
    public DbSet<Tariff> Tariffs => Set<Tariff>();
    public DbSet<InverterConnection> InverterConnections => Set<InverterConnection>();
    public DbSet<GenerationSample> GenerationSamples => Set<GenerationSample>();
    public DbSet<DailyGenerationRollup> DailyGenerationRollups => Set<DailyGenerationRollup>();
    public DbSet<ManualGenerationEntry> ManualGenerationEntries => Set<ManualGenerationEntry>();
    public DbSet<GenerationForecast> GenerationForecasts => Set<GenerationForecast>();
    public DbSet<ModelVersion> ModelVersions => Set<ModelVersion>();
    public DbSet<RawIngestPayload> RawIngestPayloads => Set<RawIngestPayload>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b); // Identity tables first

        b.Entity<UserProfile>(e =>
        {
            e.HasIndex(x => x.AspNetUserId).IsUnique();
            e.Property(x => x.DisplayName).HasMaxLength(200);
            e.HasMany(x => x.Systems).WithOne(s => s.UserProfile).HasForeignKey(s => s.UserProfileId);
        });

        b.Entity<Suburb>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Lat).HasPrecision(9, 6);
            e.Property(x => x.Lng).HasPrecision(9, 6);
        });

        b.Entity<UserSuburb>(e =>
        {
            e.HasIndex(x => new { x.UserProfileId, x.SuburbId }).IsUnique();
            e.HasOne(x => x.Suburb).WithMany().HasForeignKey(x => x.SuburbId);
        });

        b.Entity<PvSystem>(e =>
        {
            e.ToTable("Systems"); // avoid PvSystem-vs-domain-name confusion at the SQL layer
            e.HasOne(x => x.Suburb).WithMany().HasForeignKey(x => x.SuburbId);
            e.HasMany(x => x.PanelGroups).WithOne(p => p.PvSystem).HasForeignKey(p => p.PvSystemId);
            e.HasMany(x => x.Tariffs).WithOne(t => t.PvSystem).HasForeignKey(t => t.PvSystemId);
            e.HasOne(x => x.InverterConnection).WithOne(i => i.PvSystem)
                .HasForeignKey<InverterConnection>(i => i.PvSystemId);
        });

        b.Entity<Tariff>(e => e.HasIndex(x => new { x.PvSystemId, x.EffectiveFrom }));

        b.Entity<InverterConnection>(e =>
        {
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        });

        b.Entity<GenerationSample>(e =>
        {
            e.HasIndex(x => new { x.PvSystemId, x.SampledAtUtc }).IsUnique();
        });

        b.Entity<DailyGenerationRollup>(e =>
        {
            e.HasIndex(x => new { x.PvSystemId, x.DateLocal }).IsUnique();
            e.Property(x => x.Source).HasConversion<string>().HasMaxLength(20);
        });

        b.Entity<GenerationForecast>(e =>
        {
            e.HasIndex(x => new { x.PvSystemId, x.TargetDateLocal, x.IssuedAtUtc });
        });

        b.Entity<RawIngestPayload>(e =>
        {
            e.HasIndex(x => x.ContentHash);
            e.Property(x => x.PayloadJson).HasColumnType("nvarchar(max)");
        });

        b.Entity<RefreshToken>(e =>
        {
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => x.AspNetUserId);
        });
    }
}
