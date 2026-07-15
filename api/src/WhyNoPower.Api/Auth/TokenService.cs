using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace WhyNoPower.Api.Auth;

public class JwtOptions
{
    public const string SectionName = "Jwt";
    public string SigningKey { get; set; } = null!;
    public string Issuer { get; set; } = "whynopower-api";
    public string Audience { get; set; } = "whynopower-spa";
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 30;
}

public interface ITokenService
{
    string CreateAccessToken(string userId, string email, IEnumerable<string> roles);
    (string RawToken, string TokenHash) CreateRefreshToken();
    string HashToken(string rawToken);
}

/// <summary>Short-lived HS256 access tokens; refresh tokens are opaque random strings whose
/// SHA-256 hash is what's persisted (RefreshToken entity) — the raw value never touches the DB.</summary>
public class TokenService : ITokenService
{
    private readonly JwtOptions _opts;
    public TokenService(Microsoft.Extensions.Options.IOptions<JwtOptions> opts) => _opts = opts.Value;

    public string CreateAccessToken(string userId, string email, IEnumerable<string> roles)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opts.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _opts.Issuer,
            audience: _opts.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_opts.AccessTokenMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public (string RawToken, string TokenHash) CreateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        var raw = Convert.ToBase64String(bytes);
        return (raw, HashToken(raw));
    }

    public string HashToken(string rawToken)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToBase64String(hash);
    }
}
