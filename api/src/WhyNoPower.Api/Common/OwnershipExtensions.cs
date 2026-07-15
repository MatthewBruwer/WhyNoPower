using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using WhyNoPower.Core.Entities;
using WhyNoPower.Infrastructure;

namespace WhyNoPower.Api.Common;

/// <summary>
/// Ownership resolution helper. Deliberately returns 404 (not 403) when a system exists but
/// belongs to someone else — a 403 confirms the id exists, which aids enumeration. Small,
/// intentional deviation from the plain "403" note in system-design.md §10; worth a doc sync.
/// </summary>
public static class OwnershipExtensions
{
    public static string RequireUserId(this ControllerBase c) =>
        c.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? c.User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)
        ?? throw new InvalidOperationException("No user id claim on an authenticated request.");

    public static async Task<PvSystem?> GetOwnedSystemOrNullAsync(
        this AppDbContext db, long systemId, string aspNetUserId)
    {
        return await db.PvSystems
            .Include(s => s.PanelGroups)
            .Include(s => s.Tariffs)
            .Include(s => s.InverterConnection)
            .Include(s => s.UserProfile)
            .FirstOrDefaultAsync(s => s.Id == systemId && s.UserProfile.AspNetUserId == aspNetUserId);
    }
}
