using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FirebirdWeb.Models;
using Microsoft.Extensions.Options;

namespace FirebirdWeb.Helpers
{
    public class KeygenService
    {
        private readonly KeygenSettings _settings;
        private readonly HttpClient _http;

        public KeygenService(IOptions<KeygenSettings> settings, HttpClient http)
        {
            _settings = settings.Value;
            _http = http;
        }

        /// <summary>
        /// Validates a license key against the Keygen API.
        /// Returns (valid: true, message) on success, (false, reason) on failure.
        /// </summary>
        public async Task<(bool Valid, string Message, string? LicenseId)> ValidateLicenseAsync(string licenseKey)
        {
            if (string.IsNullOrWhiteSpace(licenseKey))
                return (false, "License key is required.", null);

            var url = $"https://api.keygen.sh/v1/accounts/{_settings.AccountId}/licenses/actions/validate-key";

            var body = new
            {
                meta = new { key = licenseKey.Trim() }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.api+json"));
            request.Content = new StringContent(
                JsonSerializer.Serialize(body),
                Encoding.UTF8,
                "application/vnd.api+json"
            );

            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request);
            }
            catch (Exception ex)
            {
                return (false, $"Network error: {ex.Message}", null);
            }

            var json = await response.Content.ReadAsStringAsync();

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(json);
            }
            catch
            {
                return (false, "Invalid response from Keygen.", null);
            }

            using (doc)
            {
                var root = doc.RootElement;

                // Check for API-level errors
                if (root.TryGetProperty("errors", out var errors))
                {
                    var first = errors.EnumerateArray().FirstOrDefault();
                    var detail = first.TryGetProperty("detail", out var d) ? d.GetString() : "Unknown error";
                    return (false, detail ?? "Keygen API error.", null);
                }

                if (!root.TryGetProperty("meta", out var meta))
                    return (false, "Unexpected response format.", null);

                var valid = meta.TryGetProperty("valid", out var validProp) && validProp.GetBoolean();
                var code  = meta.TryGetProperty("code",  out var codeProp)  ? codeProp.GetString() : null;

                // Extract license ID if present
                string? licenseId = null;
                if (root.TryGetProperty("data", out var data) &&
                    data.ValueKind == JsonValueKind.Object &&
                    data.TryGetProperty("id", out var idProp))
                {
                    licenseId = idProp.GetString();
                }

                if (!valid)
                {
                    var reason = code switch
                    {
                        "NOT_FOUND"           => "License key not found.",
                        "SUSPENDED"           => "License is suspended.",
                        "EXPIRED"             => "License has expired.",
                        "OVERDUE_CHECK_IN"    => "License check-in is overdue.",
                        "TOO_MANY_MACHINES"   => "Too many machines activated.",
                        "TOO_MANY_CORES"      => "Too many cores activated.",
                        "TOO_MANY_PROCESSES"  => "Too many processes activated.",
                        "FINGERPRINT_SCOPE_MISMATCH" => "Machine fingerprint mismatch.",
                        "POLICY_SCOPE_MISMATCH"      => "License policy mismatch.",
                        "PRODUCT_SCOPE_MISMATCH"     => "License is for a different product.",
                        _                     => $"License invalid ({code})."
                    };
                    return (false, reason, licenseId);
                }

                return (true, "License is valid.", licenseId);
            }
        }
    }
}
