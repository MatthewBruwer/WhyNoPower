using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhyNoPower.Api.Common;
using WhyNoPower.Core.Entities;
using WhyNoPower.Infrastructure;

namespace WhyNoPower.Api.Controllers;

public record ManualEntryRequest(DateOnly Date, int TotalWh);
public record ManualEntryResponse(long RollupId, DateTime? AdFreeUntilUtc);

[Authorize]
[ApiController]
[Route("api/systems/{systemId:long}/manual-entries")]
public class ManualEntriesController : ControllerBase
{
    private readonly AppDbContext _db;
    public ManualEntriesController(AppDbContext db) => _db = db;

    [HttpPost]
    public async Task<ActionResult<ManualEntryResponse>> Create(long systemId, ManualEntryRequest req)
    {
        var userId = this.RequireUserId();
        var system = await _db.GetOwnedSystemOrNullAsync(systemId, userId);
        if (system is null) return NotFound();

        if (req.Date > DateOnly.FromDateTime(DateTime.UtcNow))
            return BadRequest("Cannot log generation for a future date.");

        // Plausibility bound: no more than 8 sun-hours' worth of the system's rated capacity.
        var plausibleMaxWh = (int)(system.TotalKwp() * 1000 * 8);
        if (req.TotalWh < 0 || req.TotalWh > plausibleMaxWh)
            return BadRequest($"Value outside plausible range for a {system.TotalKwp()} kWp system.");

        var existing = await _db.DailyGenerationRollups
            .FirstOrDefaultAsync(r => r.PvSystemId == systemId && r.DateLocal == req.Date);

        if (existing is not null && existing.Source == RollupSource.Measured)
            return Conflict("Measured data already exists for this date — manual entry rejected.");

        _db.ManualGenerationEntries.Add(new ManualGenerationEntry
        {
            PvSystemId = systemId,
            DateLocal = req.Date,
            TotalWh = req.TotalWh,
            EnteredAtUtc = DateTime.UtcNow,
        });

        if (existing is null)
        {
            existing = new DailyGenerationRollup
            {
                PvSystemId = systemId,
                DateLocal = req.Date,
                Source = RollupSource.Manual,
            };
            _db.DailyGenerationRollups.Add(existing);
        }
        existing.TotalWh = req.TotalWh;
        existing.Source = RollupSource.Manual;
        existing.ComputedAtUtc = DateTime.UtcNow;

        var profile = await _db.UserProfiles.FirstAsync(p => p.AspNetUserId == userId);
        profile.GrantAdFreeDay(DateTime.UtcNow);

        await _db.SaveChangesAsync();

        return new ManualEntryResponse(existing.Id, profile.AdFreeUntilUtc);
    }
}
