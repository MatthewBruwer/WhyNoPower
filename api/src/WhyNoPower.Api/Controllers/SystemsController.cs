using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhyNoPower.Api.Common;
using WhyNoPower.Api.Contracts;
using WhyNoPower.Core.Entities;
using WhyNoPower.Infrastructure;

namespace WhyNoPower.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/systems")]
public class SystemsController : ControllerBase
{
    private readonly AppDbContext _db;
    public SystemsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<List<SystemDto>>> List()
    {
        var userId = this.RequireUserId();
        var systems = await _db.PvSystems
            .Include(s => s.PanelGroups).Include(s => s.Tariffs)
            .Include(s => s.InverterConnection).Include(s => s.Suburb)
            .Include(s => s.UserProfile)
            .Where(s => s.UserProfile.AspNetUserId == userId)
            .ToListAsync();

        return systems.Select(ToDto).ToList();
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<SystemDto>> Get(long id)
    {
        var userId = this.RequireUserId();
        var system = await _db.GetOwnedSystemOrNullAsync(id, userId);
        if (system is null) return NotFound(); // 404, not 403 — see OwnershipExtensions

        await _db.Entry(system).Reference(s => s.Suburb).LoadAsync();
        return ToDto(system);
    }

    [HttpPost]
    public async Task<ActionResult<SystemDto>> Create(CreateSystemRequest req)
    {
        var userId = this.RequireUserId();
        var profile = await _db.UserProfiles.FirstOrDefaultAsync(p => p.AspNetUserId == userId);
        if (profile is null) return Problem("User profile missing.", statusCode: 500);

        if (!Enum.TryParse<InverterType>(req.InverterType, ignoreCase: true, out var invType))
            return BadRequest($"Unknown inverter type '{req.InverterType}'.");

        if (req.PanelGroups is null || req.PanelGroups.Count == 0)
            return BadRequest("At least one panel group is required.");

        // MVP: find-or-create suburb by exact name. A dedicated suburb-search endpoint with
        // fuzzy matching is a natural follow-up once the seed list (schema doc §6 open item 1) lands.
        var suburb = await _db.Suburbs.FirstOrDefaultAsync(s => s.Name == req.Suburb)
            ?? new Suburb { Name = req.Suburb, Municipality = "Unknown", Province = "Gauteng", Lat = 0, Lng = 0 };
        if (suburb.Id == 0) _db.Suburbs.Add(suburb);

        var system = new PvSystem
        {
            UserProfileId = profile.Id,
            Suburb = suburb,
            Name = req.Name,
            InverterMaxW = req.InverterMaxW,
            InverterType = invType,
            BatteryCapacityWh = req.BatteryCapacityWh,
            BatteryUsablePct = req.BatteryUsablePct,
            EssentialLoadW = req.EssentialLoadW,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            PanelGroups = req.PanelGroups.Select(g => new PanelGroup
            {
                PanelCount = g.PanelCount,
                PanelWatt = g.PanelWatt,
                AzimuthDeg = g.AzimuthDeg,
                TiltDeg = g.TiltDeg,
            }).ToList(),
            Tariffs = new List<Tariff>
            {
                new() { RateCentsPerKwh = req.TariffRateCentsPerKwh, EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow) }
            },
        };

        _db.PvSystems.Add(system);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(Get), new { id = system.Id }, ToDto(system));
    }

    /// <summary>Adds a new effective-dated tariff row rather than mutating one — history stays intact.</summary>
    [HttpPost("{id:long}/tariff")]
    public async Task<ActionResult<SystemDto>> UpdateTariff(long id, [FromBody] int rateCentsPerKwh)
    {
        var userId = this.RequireUserId();
        var system = await _db.GetOwnedSystemOrNullAsync(id, userId);
        if (system is null) return NotFound();

        _db.Tariffs.Add(new Tariff
        {
            PvSystemId = system.Id,
            RateCentsPerKwh = rateCentsPerKwh,
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
        });
        await _db.SaveChangesAsync();

        var refreshed = await _db.GetOwnedSystemOrNullAsync(id, userId);
        return ToDto(refreshed!);
    }

    private static SystemDto ToDto(PvSystem s) => new(
        s.Id, s.Name, s.Suburb?.Name ?? "", s.InverterMaxW, s.InverterType.ToString(),
        s.BatteryCapacityWh, s.BatteryUsablePct, s.EssentialLoadW,
        s.Tariffs.OrderByDescending(t => t.EffectiveFrom).FirstOrDefault()?.RateCentsPerKwh ?? 0,
        s.TotalKwp(),
        (s.InverterConnection?.Status ?? ConnectionStatus.Unverified).ToString(),
        s.PanelGroups.Select(g => new PanelGroupDto(g.Id, g.PanelCount, g.PanelWatt, g.AzimuthDeg, g.TiltDeg, g.GroupKwp())).ToList());
}
