using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WhyNoPower.Api.Common;
using WhyNoPower.Api.Contracts;
using WhyNoPower.Core.Entities;
using WhyNoPower.Core.Services;
using WhyNoPower.Infrastructure;

namespace WhyNoPower.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _db;
    public DashboardController(AppDbContext db) => _db = db;

    /// <summary>
    /// The "what does my week look like?" screen. Past days: measured rollups.
    /// Today onward: forecast (physics if no model yet — GenerationForecast.PredictedWh
    /// already encodes that fallback). Zero external calls — degrades to staleness, never failure
    /// (system-design.md §7.3).
    /// </summary>
    [HttpGet("week")]
    public async Task<ActionResult<WeekDashboardDto>> Week([FromQuery] long systemId, [FromQuery] DateOnly? weekStart)
    {
        var userId = this.RequireUserId();
        var system = await _db.GetOwnedSystemOrNullAsync(systemId, userId);
        if (system is null) return NotFound();

        await _db.Entry(system).Reference(s => s.Suburb).LoadAsync();

        var start = weekStart ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var end = start.AddDays(6);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var rollups = await _db.DailyGenerationRollups
            .Where(r => r.PvSystemId == systemId && r.DateLocal >= start && r.DateLocal <= end)
            .ToListAsync();

        // Daily forecast rows: TargetHourUtc == null marks a daily-total row (schema doc §Generation).
        var forecasts = await _db.GenerationForecasts
            .Where(f => f.PvSystemId == systemId && f.TargetDateLocal >= start && f.TargetDateLocal <= end
                        && f.TargetHourUtc == null)
            .ToListAsync();

        // Day-ahead snapshot only — the forecast graded against measured data is the one issued
        // before the day began; later re-issues never rewrite the score (analytics doc §5.4).
        GenerationForecast? DayAheadForecast(DateOnly d) => forecasts
            .Where(f => f.TargetDateLocal == d && f.IssuedAtUtc < d.ToDateTime(TimeOnly.MinValue))
            .OrderByDescending(f => f.IssuedAtUtc)
            .FirstOrDefault();

        GenerationForecast? LatestForecast(DateOnly d) => forecasts
            .Where(f => f.TargetDateLocal == d)
            .OrderByDescending(f => f.IssuedAtUtc)
            .FirstOrDefault();

        var days = new List<DayPointDto>();
        var weekWh = 0;
        long weekCents = 0;

        for (var d = start; d <= end; d = d.AddDays(1))
        {
            var rollup = rollups.FirstOrDefault(r => r.DateLocal == d);

            if (rollup is not null)
            {
                var cents = SavingsCalculator.ToCents(rollup.TotalWh, d, system.Tariffs);
                var dayAhead = DayAheadForecast(d);
                int? devWh = null;
                long? devCents = null;
                if (dayAhead is not null && !rollup.IsCurtailed)
                {
                    devWh = rollup.TotalWh - dayAhead.PredictedWh;
                    devCents = SavingsCalculator.ToCents(Math.Abs(devWh.Value), d, system.Tariffs) * Math.Sign(devWh.Value);
                }

                days.Add(new DayPointDto(d, "measured", rollup.TotalWh, cents, devWh, devCents, rollup.IsCurtailed));
                weekWh += rollup.TotalWh;
                weekCents += cents;
            }
            else
            {
                var forecast = LatestForecast(d);
                var wh = forecast?.PredictedWh ?? 0;
                var cents = forecast is null ? 0 : SavingsCalculator.ToCents(wh, d, system.Tariffs);
                days.Add(new DayPointDto(d, "forecast", wh, cents, null, null, false));
                weekWh += wh;
                weekCents += cents;
            }
        }

        decimal? battery = system.EssentialLoadW > 0 ? BatteryRuntimeCalculator.Hours(system) : null;

        // Best-hours window over today's hourly potential curve.
        var todayHourly = await _db.GenerationForecasts
            .Where(f => f.PvSystemId == systemId && f.TargetDateLocal == today && f.TargetHourUtc != null)
            .OrderBy(f => f.TargetHourUtc)
            .Select(f => new HourlyPoint(TimeOnly.FromDateTime(f.TargetHourUtc!.Value), f.PhysicsWh))
            .ToListAsync();
        var window = BestHoursAdvisor.PeakWindow(todayHourly);

        // Rolling accuracy: MdAPE of daily totals, non-curtailed days, trailing 14 days
        // (analytics doc §4.5) — kept simple here; a dedicated stats service can replace this
        // once more history exists.
        var accuracyWindow = await _db.DailyGenerationRollups
            .Where(r => r.PvSystemId == systemId && !r.IsCurtailed
                        && r.DateLocal >= today.AddDays(-14) && r.DateLocal < today)
            .ToListAsync();
        decimal? rollingAccuracy = null;
        if (accuracyWindow.Count > 0)
        {
            var errors = new List<decimal>();
            foreach (var r in accuracyWindow)
            {
                var f = DayAheadForecast(r.DateLocal);
                if (f is null || f.PredictedWh == 0) continue;
                errors.Add(Math.Abs(r.TotalWh - f.PredictedWh) / (decimal)f.PredictedWh * 100m);
            }
            if (errors.Count > 0)
            {
                errors.Sort();
                rollingAccuracy = errors[errors.Count / 2]; // median
            }
        }

        return new WeekDashboardDto(
            system.Suburb.Name,
            (system.InverterConnection?.Status ?? ConnectionStatus.Unverified).ToString(),
            system.InverterConnection?.LastSyncedAtUtc,
            weekWh,
            weekCents,
            days,
            battery,
            window?.Start.ToString("HH:mm"),
            window?.End.ToString("HH:mm"),
            rollingAccuracy);
    }
}
