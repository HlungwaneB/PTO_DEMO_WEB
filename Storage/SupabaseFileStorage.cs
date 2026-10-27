using System.Net.Http.Headers;
using PTO_Management_System.Interfaces;
using PTO_Management_System.Services;

namespace PTO_Management_System.Storage
{
    /// <summary>
    /// Stores documents in the private Supabase Storage bucket as the signed-in person
    /// (their own access token + the publishable key). No service-role key is used; the bucket
    /// policies decide who may save and read which file.
    /// </summary>
    public class SupabaseFileStorage : IFileStorage
    {
        private readonly HttpClient _http;
        private readonly SessionService _session;
        private readonly string _baseUrl;
        private readonly string _bucket;
        private readonly string _apiKey;

        public SupabaseFileStorage(IHttpClientFactory httpClientFactory, IConfiguration configuration, SessionService session)
        {
            _http = httpClientFactory.CreateClient();
            _session = session;
            _baseUrl = (configuration["Supabase:Url"] ?? "").TrimEnd('/');
            _bucket = configuration["Supabase:Bucket"] ?? "pto-documents";
            _apiKey = configuration["Supabase:PublishableKey"] ?? "";
        }

        private async Task<HttpRequestMessage> NewRequestAsync(HttpMethod method, string storedFileName)
        {
            var token = await _session.GetAccessTokenAsync();
            if (string.IsNullOrEmpty(token))
                throw new InvalidOperationException("Your login has expired. Please log out and log in again.");

            var url = _baseUrl + "/storage/v1/object/" + _bucket + "/" + Uri.EscapeDataString(Path.GetFileName(storedFileName));
            var request = new HttpRequestMessage(method, url);
            request.Headers.Add("apikey", _apiKey);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return request;
        }

        public async Task SaveAsync(IFormFile file, string storedFileName)
        {
            using (var stream = file.OpenReadStream())
            using (var request = await NewRequestAsync(HttpMethod.Post, storedFileName))
            {
                request.Content = new StreamContent(stream);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType ?? "application/octet-stream");

                var response = await _http.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    throw new InvalidOperationException("Supabase Storage upload failed (" + (int)response.StatusCode + "): " + body);
                }
            }
        }

        public async Task<Stream> OpenReadAsync(string storedFileName)
        {
            using (var request = await NewRequestAsync(HttpMethod.Get, storedFileName))
            {
                var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode) return null;
                return await response.Content.ReadAsStreamAsync();
            }
        }

        public async Task DeleteAsync(string storedFileName)
        {
            using (var request = await NewRequestAsync(HttpMethod.Delete, storedFileName))
            {
                await _http.SendAsync(request);
            }
        }
    }
}
