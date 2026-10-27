using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using PTO_Management_System.Interfaces;

namespace PTO_Management_System.Services
{
    /// <summary>A Supabase login session: the tokens plus who the person is.</summary>
    public class AuthSession
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public long ExpiresAt { get; set; }   // unix seconds
        public string UserId { get; set; }
        public string Email { get; set; }
    }

    public class AuthException : Exception
    {
        public string Code { get; }
        public int Status { get; }

        public AuthException(string message, string code = null, int status = 0) : base(message)
        {
            Code = code;
            Status = status;
        }

        public bool IsRateLimited => Status == 429 || (Code ?? "").Contains("rate_limit");
    }

    public class SupabaseAuthService : ISupabaseAuth
    {
        private readonly IHttpClientFactory _factory;
        private readonly string _baseUrl;
        private readonly string _apiKey;

        public SupabaseAuthService(IHttpClientFactory factory, IConfiguration configuration)
        {
            _factory = factory;
            _baseUrl = (configuration["Supabase:Url"] ?? "").TrimEnd('/');
            _apiKey = configuration["Supabase:PublishableKey"] ?? "";
        }

        public Task<AuthSession> SignUpAsync(string email, string password)
            => GetSession(HttpMethod.Post, "/auth/v1/signup", new { email, password }, null);

        public Task<AuthSession> SignInAsync(string email, string password)
            => GetSession(HttpMethod.Post, "/auth/v1/token?grant_type=password", new { email, password }, null);

        public Task<AuthSession> RefreshAsync(string refreshToken)
            => GetSession(HttpMethod.Post, "/auth/v1/token?grant_type=refresh_token", new { refresh_token = refreshToken }, null);

        public async Task SendRecoveryCodeAsync(string email)
        {
            await Send(HttpMethod.Post, "/auth/v1/recover", new { email }, null);
        }

        public Task<AuthSession> VerifyRecoveryCodeAsync(string email, string code)
            => GetSession(HttpMethod.Post, "/auth/v1/verify", new { type = "recovery", email, token = code }, null);

        public async Task UpdatePasswordAsync(string accessToken, string newPassword)
        {
            await Send(HttpMethod.Put, "/auth/v1/user", new { password = newPassword }, accessToken);
        }

        /// <summary>Which e-mail address was this ID number registered with? (database function login_email)</summary>
        public async Task<string> EmailForIdNumberAsync(string idNumber)
        {
            var text = await Send(HttpMethod.Post, "/rest/v1/rpc/login_email", new { p_id_number = idNumber }, null);
            using (var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "null" : text))
            {
                return doc.RootElement.ValueKind == JsonValueKind.String ? doc.RootElement.GetString() : null;
            }
        }

        // ------------------------------------------------------------ helpers

        private async Task<string> Send(HttpMethod method, string path, object body, string bearer)
        {
            var client = _factory.CreateClient();
            using (var request = new HttpRequestMessage(method, _baseUrl + path))
            {
                request.Headers.Add("apikey", _apiKey);
                if (!string.IsNullOrEmpty(bearer))
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
                request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

                using (var response = await client.SendAsync(request))
                {
                    var text = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode) throw ToException((int)response.StatusCode, text);
                    return text;
                }
            }
        }

        private async Task<AuthSession> GetSession(HttpMethod method, string path, object body, string bearer)
        {
            var text = await Send(method, path, body, bearer);
            using (var doc = JsonDocument.Parse(text))
            {
                var root = doc.RootElement;
                if (!root.TryGetProperty("access_token", out var access) || access.ValueKind != JsonValueKind.String)
                    throw new AuthException(
                        "The login was created but Supabase did not return a session. In Supabase go to Authentication > Sign In / Providers > Email and switch Confirm email OFF.",
                        "no_session");

                long expiresAt;
                if (root.TryGetProperty("expires_at", out var ea) && ea.ValueKind == JsonValueKind.Number)
                    expiresAt = ea.GetInt64();
                else
                {
                    long seconds = root.TryGetProperty("expires_in", out var ei) && ei.ValueKind == JsonValueKind.Number ? ei.GetInt64() : 3600;
                    expiresAt = DateTimeOffset.UtcNow.AddSeconds(seconds).ToUnixTimeSeconds();
                }

                string userId = null, email = null;
                if (root.TryGetProperty("user", out var user) && user.ValueKind == JsonValueKind.Object)
                {
                    if (user.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String) userId = id.GetString();
                    if (user.TryGetProperty("email", out var em) && em.ValueKind == JsonValueKind.String) email = em.GetString();
                }

                return new AuthSession
                {
                    AccessToken = access.GetString(),
                    RefreshToken = root.TryGetProperty("refresh_token", out var rt) && rt.ValueKind == JsonValueKind.String ? rt.GetString() : null,
                    ExpiresAt = expiresAt,
                    UserId = userId,
                    Email = email
                };
            }
        }

        private static AuthException ToException(int status, string text)
        {
            string code = null, message = null;
            try
            {
                using (var doc = JsonDocument.Parse(text))
                {
                    var root = doc.RootElement;
                    string Read(string name) =>
                        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

                    code = Read("error_code") ?? Read("code") ?? Read("error");
                    message = Read("msg") ?? Read("message") ?? Read("error_description") ?? Read("error");
                }
            }
            catch (JsonException) { }

            if (status == 429 && code == null) code = "over_request_rate_limit";
            return new AuthException(message ?? "The request to Supabase failed (" + status + ").", code, status);
        }
    }
}
