using System.Net.Http.Headers;
using PTO_Management_System.Interfaces;

namespace PTO_Management_System.Storage
{
    /// <summary>
    /// Stores documents in a Supabase Storage bucket (SRS 13.1 "Supabase Storage").
    /// Turn it on with  "Storage": { "Provider": "Supabase" }  in appsettings.json.
    /// Key used: Supabase:ServiceRoleKey (keep it in user-secrets, never in GitHub) when present,
    /// otherwise the publishable key - which only works if you ran the storage policies in
    /// Database/supabase_setup.sql (prototype only).
    /// </summary>
    public class SupabaseFileStorage : IFileStorage
    {
        private readonly HttpClient _http;
        private readonly string _baseUrl;
        private readonly string _bucket;
        private readonly string _key;

        public SupabaseFileStorage(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _http = httpClientFactory.CreateClient();
            _baseUrl = (configuration["Supabase:Url"] ?? "").TrimEnd('/');
            _bucket = configuration["Supabase:Bucket"] ?? "pto-documents";

            var serviceKey = configuration["Supabase:ServiceRoleKey"];
            _key = string.IsNullOrWhiteSpace(serviceKey) ? configuration["Supabase:PublishableKey"] : serviceKey;
        }

        private HttpRequestMessage NewRequest(HttpMethod method, string storedFileName)
        {
            var url = _baseUrl + "/storage/v1/object/" + _bucket + "/" + Uri.EscapeDataString(Path.GetFileName(storedFileName));
            var request = new HttpRequestMessage(method, url);
            request.Headers.Add("apikey", _key);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _key);
            return request;
        }

        public async Task SaveAsync(IFormFile file, string storedFileName)
        {
            using (var stream = file.OpenReadStream())
            using (var request = NewRequest(HttpMethod.Post, storedFileName))
            {
                request.Content = new StreamContent(stream);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType ?? "application/octet-stream");
                request.Headers.Add("x-upsert", "true");

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
            using (var request = NewRequest(HttpMethod.Get, storedFileName))
            {
                var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode) return null;
                return await response.Content.ReadAsStreamAsync();
            }
        }

        public async Task DeleteAsync(string storedFileName)
        {
            using (var request = NewRequest(HttpMethod.Delete, storedFileName))
            {
                await _http.SendAsync(request);
            }
        }
    }
}
