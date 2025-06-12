using Microsoft.AspNetCore.Mvc;
using govapi.Services;

namespace govapi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class PolicyExemptionsController : ControllerBase
    {
        private readonly IPolicyExemptionsService _service;
        private readonly ILogger<PolicyExemptionsController> _logger;

        public PolicyExemptionsController(
            IPolicyExemptionsService service,
            ILogger<PolicyExemptionsController> logger)
        {
            _service = service;
            _logger = logger;
        }

        // GET /policyexemptions/{subscriptionId}
        [HttpGet("{subscriptionId}")]
        public async Task<IActionResult> GetPolicyExemptions(
            string subscriptionId,
            [FromQuery] bool showAllProperties = false,
            [FromQuery] bool withExpiryDate = false,
            [FromQuery] string? withExpiryDateWithinDays = null)
        {
            _logger.LogInformation("Fetching policy exemptions for subscription: {SubscriptionId}", subscriptionId);

            // Validate withExpiryDateWithinDays if set
            if (!string.IsNullOrEmpty(withExpiryDateWithinDays))
            {
                if (!int.TryParse(withExpiryDateWithinDays, out var days) || days < 0 || days > 365)
                {
                    return BadRequest("withExpiryDateWithinDays must be an integer between 0 and 365.");
                }
            }

            var result = await _service.GetPolicyExemptionsAsync(
                subscriptionId,
                HttpContext,
                showAllProperties,
                withExpiryDate,
                withExpiryDateWithinDays);

            if (result == null)
                return Ok(null);

            return new JsonResult(result);
        }
        
        // GET /policyexemptions/{subscriptionId}/{exemptionName}
        [HttpGet("{subscriptionId}/{exemptionName}")]
        public async Task<IActionResult> GetPolicyExemptionByName(string subscriptionId, string exemptionName)
        {
            _logger.LogInformation("Fetching policy exemption details for subscription: {SubscriptionId}, exemption: {ExemptionName}", subscriptionId, exemptionName);

            var details = await _service.GetPolicyExemptionDetailsAsync(subscriptionId, exemptionName, HttpContext);

            if (details == null)
                return NotFound("Exemption details not found.");

            return new JsonResult(details);
        }

        // PUT /policyexemptions/{subscriptionId}/{exemptionName}/expiresOn
        [HttpPut("{subscriptionId}/{exemptionName}/expiresOn")]
        public async Task<IActionResult> UpdatePolicyExemptionExpiresOn(
            string subscriptionId,
            string exemptionName,
            [FromQuery] string expiresOnIso8601)
        {
            _logger.LogInformation("Updating expiresOn for exemption {ExemptionName} in subscription {SubscriptionId} to {ExpiresOn}", exemptionName, subscriptionId, expiresOnIso8601);

            if (string.IsNullOrWhiteSpace(expiresOnIso8601))
                return BadRequest("expiresOn value must be provided as a query parameter.");

            var result = await _service.UpdatePolicyExemptionAsync(subscriptionId, exemptionName, expiresOnIso8601, HttpContext);

            if (result == null)
                return StatusCode(500, "Failed to update policy exemption.");

            return new JsonResult(result);
        }
    }
}
