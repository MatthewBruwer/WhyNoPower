using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WhyNoPower.Api.Auth;
using WhyNoPower.Api.Contracts;
using WhyNoPower.Core.Entities;
using WhyNoPower.Infrastructure;
using WhyNoPower.Infrastructure.Identity;

namespace WhyNoPower.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<AppUser> _users;
    private readonly ITokenService _tokens;
    private readonly JwtOptions _jwtOpts;
    private readonly AppDbContext _db;

    public AuthController(UserManager<AppUser> users, ITokenService tokens,
        IOptions<JwtOptions> jwtOpts, AppDbContext db)
    {
        _users = users;
        _tokens = tokens;
        _jwtOpts = jwtOpts.Value;
        _db = db;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest req)
    {
        var user = new AppUser { UserName = req.Email, Email = req.Email };
        var result = await _users.CreateAsync(user, req.Password);
        if (!result.Succeeded)
            return BadRequest(result.Errors.Select(e => e.Description));

        _db.UserProfiles.Add(new UserProfile
        {
            AspNetUserId = user.Id,
            DisplayName = req.DisplayName,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        return await IssueTokens(user);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest req)
    {
        var user = await _users.FindByEmailAsync(req.Email);
        if (user is null || !await _users.CheckPasswordAsync(user, req.Password))
            return Unauthorized(); // deliberately generic — don't reveal which field was wrong

        return await IssueTokens(user);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest req)
    {
        var incomingHash = _tokens.HashToken(req.RefreshToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == incomingHash);

        if (stored is null)
            return Unauthorized();

        if (!stored.IsActive)
        {
            // Reuse of an already-rotated/revoked token: treat as compromise, revoke the chain.
            if (stored.RevokedAtUtc is not null)
                await RevokeAllForUser(stored.AspNetUserId);
            return Unauthorized();
        }

        var user = await _users.FindByIdAsync(stored.AspNetUserId);
        if (user is null) return Unauthorized();

        stored.RevokedAtUtc = DateTime.UtcNow;
        var response = await IssueTokens(user);
        stored.ReplacedByTokenHash = _tokens.HashToken(response.Value!.RefreshToken);
        await _db.SaveChangesAsync();

        return response;
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest req)
    {
        var hash = _tokens.HashToken(req.RefreshToken);
        var stored = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);
        if (stored is not null)
        {
            stored.RevokedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
        return NoContent();
    }

    private async Task RevokeAllForUser(string userId)
    {
        var active = await _db.RefreshTokens
            .Where(t => t.AspNetUserId == userId && t.RevokedAtUtc == null)
            .ToListAsync();
        foreach (var t in active) t.RevokedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    private async Task<ActionResult<AuthResponse>> IssueTokens(AppUser user)
    {
        var roles = await _users.GetRolesAsync(user);
        var access = _tokens.CreateAccessToken(user.Id, user.Email!, roles);
        var (raw, hash) = _tokens.CreateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            AspNetUserId = user.Id,
            TokenHash = hash,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(_jwtOpts.RefreshTokenDays),
        });
        await _db.SaveChangesAsync();

        return new AuthResponse(access, raw, DateTime.UtcNow.AddMinutes(_jwtOpts.AccessTokenMinutes));
    }
}
