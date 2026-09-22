using Kayane.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Kayane.Controllers
{
    [Route("api/webhooks")]
    [ApiController]
    public class WebhooksController : ControllerBase
    {
        private readonly PaymentVerificationService _verifier;
        private readonly IConfiguration _config;
        private readonly ILogger<WebhooksController> _logger;

        public WebhooksController(
            PaymentVerificationService verifier,
            IConfiguration config,
            ILogger<WebhooksController> logger)
        {
            _verifier = verifier;
            _config = config;
            _logger = logger;
        }

        // POST: /api/webhooks/paystack
        // POST: /api/webhooks/9psb
        // POST: /api/webhooks/payment
        [HttpPost("paystack")]
        [HttpPost("9psb")]
        [HttpPost("payment")]
        public async Task<IActionResult> HandlePaymentWebhook()
        {
            Request.EnableBuffering();
            using var reader = new StreamReader(Request.Body, leaveOpen: true);
            var rawBody = await reader.ReadToEndAsync();
            Request.Body.Position = 0;

            var signature = Request.Headers["x-paystack-signature"].FirstOrDefault()
                         ?? Request.Headers["x-9psb-signature"].FirstOrDefault()
                         ?? Request.Headers["x-webhook-signature"].FirstOrDefault();

            var secret = _config["Psb:WebhookSecret"] ?? string.Empty;

            if (!PsbService.VerifyWebhookSignature(rawBody, signature ?? string.Empty, secret))
            {
                _logger.LogWarning("Rejected webhook — bad or missing signature.");
                return Unauthorized();
            }

            try
            {
                using var doc = JsonDocument.Parse(rawBody);
                var root = doc.RootElement;

                var eventType = root.TryGetProperty("event", out var ev)
                    ? ev.GetString()
                    : null;

                if (eventType != "charge.success")
                {
                    // Acknowledge everything else so the gateway doesn't retry forever.
                    return Ok();
                }

                var data = root.GetProperty("data");
                var reference = data.GetProperty("reference").GetString();

                if (string.IsNullOrWhiteSpace(reference))
                {
                    _logger.LogWarning("Webhook charge.success with no reference.");
                    return BadRequest();
                }

                var result = await _verifier.VerifyAndFulfillAsync(reference);

                _logger.LogInformation(
                    "Webhook handled reference {Ref} → {Result}", reference, result);

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payment webhook.");
                return BadRequest();
            }
        }
    }
}