namespace WhyNoPower.Core.Entities;

/// <summary>
/// Schema addendum (not in original ERD): rotating refresh tokens for JWT auth.
/// Only the SHA-256 hash is stored — the raw token exists only in the client's cookie/storage.
/// </summary>
public class RefreshToken
{
    public long Id { get; set; }
    public string AspNetUserId { get; set; } = null!;
    public string TokenHash { get; set; } = null!;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    /// <summary>Set when this token is rotated out, pointing at its replacement's hash — lets us
    /// detect reuse of a stolen, already-rotated token and revoke the whole chain.</summary>
    public string? ReplacedByTokenHash { get; set; }

    public bool IsActive => RevokedAtUtc is null && DateTime.UtcNow < ExpiresAtUtc;
}
