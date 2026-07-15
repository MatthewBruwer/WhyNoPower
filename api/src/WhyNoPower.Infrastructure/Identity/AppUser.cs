using Microsoft.AspNetCore.Identity;

namespace WhyNoPower.Infrastructure.Identity;

/// <summary>ASP.NET Identity owns auth. App-specific data lives in UserProfile (1:1), not here —
/// keeps auth concerns and app concerns separable (system-design doc §4.1).</summary>
public class AppUser : IdentityUser
{
}
