using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Kayane.Services
{
    public class PsbService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public PsbService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;

            var apiKey = _configuration["Psb:ApiKey"] ?? "your_9psb_api_key";
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            _httpClient.BaseAddress = new Uri(_configuration["Psb:BaseUrl"] ?? "https://api.9psb.com.ng/");
        }

        /// <summary>Convert Naira to kobo (integer minor units).</summary>
        private static long ToKobo(decimal naira) => (long)Math.Round(naira * 100m, MidpointRounding.AwayFromZero);

        public async Task<PsbVirtualAccountResponse?> CreateVirtualAccountAsync(string businessName, string phoneNumber, string email)
        {
            var payload = new
            {
                account_name = businessName,
                phone = phoneNumber,
                email = email
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("api/v1/merchant/virtual-account", content);

            if (!response.IsSuccessStatusCode) return null;

            var jsonResponse = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            if (root.TryGetProperty("success", out var s) && s.GetBoolean())
            {
                var data = root.GetProperty("data");
                return new PsbVirtualAccountResponse
                {
                    AccountNumber = data.GetProperty("account_number").GetString() ?? string.Empty,
                    AccountName = data.GetProperty("account_name").GetString() ?? string.Empty,
                    Reference = data.GetProperty("reference").GetString() ?? string.Empty
                };
            }

            return null;
        }

        public async Task<bool> ProcessPayoutAsync(
            string bankCode,
            string accountNumber,
            decimal amountNaira,
            string narration,
            string reference)
        {
            var payload = new
            {
                destination_bank_code = bankCode,
                destination_account_number = accountNumber,
                amount = ToKobo(amountNaira),
                narration = narration,
                reference = reference
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("api/v1/payout/transfer", content);

            if (!response.IsSuccessStatusCode) return false;

            var jsonResponse = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonResponse);
            return doc.RootElement.TryGetProperty("success", out var s) && s.GetBoolean();
        }

        public async Task<(bool Success, string? RedirectUrl, string? Message)> InitializeTransactionAsync(
            string reference,
            decimal amountNaira,
            string email,
            string callbackUrl)
        {
            var payload = new
            {
                amount = ToKobo(amountNaira),
                email = email,
                reference = reference,
                callback_url = callbackUrl
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync("api/v1/transaction/initialize", content);

            if (!response.IsSuccessStatusCode)
            {
                return (false, null, "Failed to connect to payment gateway.");
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            if (root.TryGetProperty("success", out var s) && s.GetBoolean())
            {
                var data = root.GetProperty("data");
                var redirectUrl = data.GetProperty("authorization_url").GetString();
                return (true, redirectUrl, null);
            }

            return (false, null, "Payment initialization was unsuccessful.");
        }

        public async Task<(bool IsSuccess, string? Message)> VerifyTransactionAsync(string reference)
        {
            var response = await _httpClient.GetAsync($"api/v1/transaction/verify/{reference}");

            if (!response.IsSuccessStatusCode)
                return (false, "Transaction verification request failed.");

            var jsonResponse = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(jsonResponse);
            var root = doc.RootElement;

            if (root.TryGetProperty("success", out var s) && s.GetBoolean())
            {
                var data = root.GetProperty("data");
                var status = data.GetProperty("status").GetString();

                if (status == "success" || status == "completed")
                {
                    return (true, "Verified successfully");
                }
            }

            return (false, "Transaction was not successful.");
        }

        /// <summary>
        /// Verifies an HMAC-SHA512 signature on a webhook body.
        /// Paystack and most Nigerian gateways use HMAC-SHA512 hex-encoded.
        /// If 9PSB uses a different scheme (SHA256, base64), adjust here.
        /// </summary>
        public static bool VerifyWebhookSignature(string rawBody, string signatureHeader, string secret)
        {
            if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(secret))
                return false;

            using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
            var computed = Convert.ToHexString(hash).ToLowerInvariant();

            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(computed),
                Encoding.UTF8.GetBytes(signatureHeader.Trim().ToLowerInvariant()));
        }
    }

    public class PsbVirtualAccountResponse
    {
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
    }
}