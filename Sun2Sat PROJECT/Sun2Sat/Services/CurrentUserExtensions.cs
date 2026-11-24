using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace Sun2Sat.Services;

public static class CurrentUserExtensions
{
    /// <summary>The signed-in user's Id (from the login cookie), or null if signed out.</summary>
    public static async Task<string?> GetUserIdAsync(this Task<AuthenticationState>? authState)
    {
        if (authState is null) return null;
        var state = await authState;
        return state.User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
