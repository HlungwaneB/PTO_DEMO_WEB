using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using PTO_Management_System.Interfaces;

namespace PTO_Management_System.Services
{
    /// <summary>
    /// Turns a Supabase login into this website's login cookie, and keeps the person's own Supabase
    /// tokens inside that (encrypted) cookie so documents can be saved to Supabase Storage as the person
    /// themselves. The tokens are refreshed automatically shortly before they expire.
    /// </summary>
    public class SessionService
    {
        private const string Scheme = CookieAuthenticationDefaults.AuthenticationScheme;
        private readonly IHttpContextAccessor _http;
        private readonly ISupabaseAuth _auth;

        public SessionService(IHttpContextAccessor http, ISupabaseAuth auth)
        {
            _http = http;
            _auth = auth;
        }

        public async Task SignInAsync(AuthSession session, string role, bool persistent)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, session.UserId),
                new Claim(ClaimTypes.Name, session.Email ?? session.UserId),
                new Claim(ClaimTypes.Email, session.Email ?? ""),
                new Claim(ClaimTypes.Role, role)
            };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme));

            var properties = new AuthenticationProperties { IsPersistent = persistent };
            properties.StoreTokens(Tokens(session));
            await _http.HttpContext.SignInAsync(Scheme, principal, properties);
        }

        public async Task SignOutAsync()
        {
            await _http.HttpContext.SignOutAsync(Scheme);
        }

        /// <summary>The signed-in person's Supabase access token (refreshed when it is about to expire), or null.</summary>
        public async Task<string> GetAccessTokenAsync()
        {
            var context = _http.HttpContext;
            var result = await context.AuthenticateAsync(Scheme);
            if (!result.Succeeded || result.Properties == null) return null;

            var access = result.Properties.GetTokenValue("access_token");
            var refresh = result.Properties.GetTokenValue("refresh_token");
            if (string.IsNullOrEmpty(access)) return null;

            if (long.TryParse(result.Properties.GetTokenValue("expires_at"), out var unix)
                && DateTimeOffset.FromUnixTimeSeconds(unix) > DateTimeOffset.UtcNow.AddSeconds(60))
                return access;

            if (string.IsNullOrEmpty(refresh)) return null;

            var renewed = await _auth.RefreshAsync(refresh);
            renewed.UserId ??= result.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
            result.Properties.StoreTokens(Tokens(renewed));
            await context.SignInAsync(Scheme, result.Principal, result.Properties);
            return renewed.AccessToken;
        }

        private static IEnumerable<AuthenticationToken> Tokens(AuthSession s)
        {
            return new[]
            {
                new AuthenticationToken { Name = "access_token", Value = s.AccessToken },
                new AuthenticationToken { Name = "refresh_token", Value = s.RefreshToken ?? "" },
                new AuthenticationToken { Name = "expires_at", Value = s.ExpiresAt.ToString() }
            };
        }
    }
}
