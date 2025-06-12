using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace govapi.Services
{
    public interface IPolicyExemptionsService
    {
        Task<Dictionary<string, List<Dictionary<string, object?>>>?> GetPolicyExemptionsAsync(
            string subscriptionId,
            HttpContext httpContext,
            bool showAllProperties,
            bool withExpiryDate,
            string? withExpiryDateWithinDays = null);
        Task<JsonElement?> GetPolicyExemptionDetailsAsync(string subscriptionId, string exemptionName, HttpContext httpContext);

        // Add: Update a single policy exemption
        Task<JsonElement?> UpdatePolicyExemptionAsync(
            string subscriptionId,
            string exemptionName,
            string expiresOnIso8601,
            HttpContext httpContext);
    }

    public class PolicyExemptionsService : IPolicyExemptionsService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<PolicyExemptionsService> _logger;

        public PolicyExemptionsService(IHttpClientFactory httpClientFactory, ILogger<PolicyExemptionsService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<JsonElement?> UpdatePolicyExemptionAsync(
            string subscriptionId,
            string exemptionName,
            string expiresOnIso8601,
            HttpContext httpContext)
        {
            var apiVersion = "2022-07-01-preview";
            var url = $"https://management.azure.com/subscriptions/{subscriptionId}/providers/Microsoft.Authorization/policyExemptions/{exemptionName}?api-version={apiVersion}";

            var accessToken = httpContext.Items["AzureAccessToken"] as string;
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogError("Failed to acquire token for Azure Management API");
                return null;
            }

            // Get the current exemption details
            var currentExemption = await GetPolicyExemptionDetailsAsync(subscriptionId, exemptionName, httpContext);
            if (currentExemption == null)
            {
                _logger.LogError("Failed to retrieve current exemption details for update.");
                return null;
            }

            // Convert to mutable dictionary
            var exemptionDict = JsonSerializer.Deserialize<Dictionary<string, object>>(currentExemption.Value.GetRawText());
            if (exemptionDict == null)
            {
                _logger.LogError("Failed to deserialize exemption details for update.");
                return null;
            }

            // Update properties.expiresOn
            if (exemptionDict.TryGetValue("properties", out var propsObj) && propsObj is JsonElement propsElem)
            {
                var propsDict = JsonSerializer.Deserialize<Dictionary<string, object>>(propsElem.GetRawText()) ?? new Dictionary<string, object>();
                propsDict["expiresOn"] = expiresOnIso8601;
                exemptionDict["properties"] = propsDict;
            }
            else if (exemptionDict.TryGetValue("properties", out var propsDictObj) && propsDictObj is Dictionary<string, object> propsDict2)
            {
                propsDict2["expiresOn"] = expiresOnIso8601;
                exemptionDict["properties"] = propsDict2;
            }
            else
            {
                // If properties does not exist, create it
                exemptionDict["properties"] = new Dictionary<string, object> { { "expiresOn", expiresOnIso8601 } };
            }

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var jsonPayload = JsonSerializer.Serialize(exemptionDict);
            var content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");

            var response = await client.PutAsync(url, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to update policy exemption: {Status} {Content}", response.StatusCode, responseContent);
                return null;
            }

            var doc = JsonDocument.Parse(responseContent);
            return doc.RootElement.Clone();
        }

        public async Task<Dictionary<string, List<Dictionary<string, object?>>>?> GetPolicyExemptionsAsync(
            string subscriptionId,
            HttpContext httpContext,
            bool showAllProperties,
            bool withExpiryDate,
            string? withExpiryDateWithinDays = null)
        {
            var apiVersion = "2022-07-01-preview";
            var url = $"https://management.azure.com/subscriptions/{subscriptionId}/providers/Microsoft.Authorization/policyExemptions?api-version={apiVersion}";

            var accessToken = httpContext.Items["AzureAccessToken"] as string;
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogError("Failed to acquire token for Azure Management API");
                return null;
            }

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await client.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch policy exemptions: {Status} {Content}", response.StatusCode, content);
                return null;
            }

            var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            var results = new List<Dictionary<string, object?>>();
            int? expiryDays = null;
            if (!string.IsNullOrEmpty(withExpiryDateWithinDays))
            {
                if (int.TryParse(withExpiryDateWithinDays, out var days) && days >= 0 && days <= 365)
                {
                    expiryDays = days;
                    withExpiryDate = true; // override to true if set
                }
                else
                {
                    throw new ArgumentException("withExpiryDateWithinDays must be an integer between 0 and 365.");
                }
            }

            if (root.TryGetProperty("value", out var valueArray) && valueArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in valueArray.EnumerateArray())
                {
                    // Eliminate items that do not have "/subscriptions" as the first element of the id property
                    if (!item.TryGetProperty("id", out var idProp))
                        throw new InvalidOperationException("Policy exemption item is missing 'id' property.");
                    var idVal = idProp.GetString();
                    if (string.IsNullOrEmpty(idVal))
                        throw new InvalidOperationException("Policy exemption item has empty 'id' property.");
                    var segments = idVal.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    if (segments.Length == 0 || !idVal.StartsWith("/subscriptions"))
                        continue;

                    // Get expiresOn if present
                    string? expiresOn = null;
                    JsonElement? props = null;
                    if (item.TryGetProperty("properties", out var propsElem))
                    {
                        props = propsElem;
                        if (propsElem.TryGetProperty("expiresOn", out var expiresOnProp) && expiresOnProp.ValueKind != JsonValueKind.Null)
                        {
                            expiresOn = expiresOnProp.GetString();
                        }
                    }

                    // Filtering logic
                    if (expiryDays.HasValue)
                    {
                        // Only items with a valid expiresOn within the specified days
                        if (string.IsNullOrEmpty(expiresOn))
                            continue;
                        if (!DateTime.TryParse(expiresOn, out var expiresOnDate))
                            continue;
                        var now = DateTime.UtcNow.Date;
                        var threshold = now.AddDays(expiryDays.Value);
                        if (expiresOnDate.Date > threshold)
                            continue;
                    }
                    else if (withExpiryDate)
                    {
                        // Only items with a non-null, non-empty expiresOn
                        if (string.IsNullOrEmpty(expiresOn))
                            continue;
                    }
                    else
                    {
                        // Only items with missing, null, or empty expiresOn
                        if (!string.IsNullOrEmpty(expiresOn))
                            continue;
                    }

                    var result = new Dictionary<string, object?>();
                    if (showAllProperties)
                    {
                        foreach (var prop in item.EnumerateObject())
                        {
                            result[prop.Name] = prop.Value.ValueKind == JsonValueKind.Object
                                ? JsonSerializer.Deserialize<Dictionary<string, object?>>(prop.Value.GetRawText())
                                : prop.Value.ToString();
                        }
                    }
                    else
                    {
                        if (item.TryGetProperty("name", out var nameProp))
                        {
                            result["name"] = nameProp.GetString();
                        }
                        if (props.HasValue)
                        {
                            var propsVal = props.Value;
                            if (propsVal.TryGetProperty("displayName", out var displayNameProp))
                            {
                                result["displayName"] = displayNameProp.GetString();
                            }
                            if ((withExpiryDate || expiryDays.HasValue) && propsVal.TryGetProperty("expiresOn", out var expiresOnProp) && expiresOnProp.ValueKind != JsonValueKind.Null)
                            {
                                result["expiresOn"] = expiresOnProp.GetString();
                            }
                        }
                    }
                    results.Add(result);
                }

                // Group by last segment of policyAssignmentId
                var grouped = results
                    .Where(r => itemHasPolicyAssignmentId(r, valueArray))
                    .GroupBy(r =>
                    {
                        var id = getPolicyAssignmentId(r, valueArray);
                        var segs = id.Split('/', StringSplitOptions.RemoveEmptyEntries);
                        return segs.Length > 0 ? segs.Last() : "";
                    })
                    .OrderBy(g => g.Key)
                    .ToDictionary(
                        g => g.Key,
                        g => g.OrderBy(x => x.ContainsKey("name") ? x["name"] : null).ToList()
                    );

                return grouped;
            }

            return null;
        }

        public async Task<JsonElement?> GetPolicyExemptionDetailsAsync(string subscriptionId, string exemptionName, HttpContext httpContext)
        {
            var apiVersion = "2022-07-01-preview";
            var url = $"https://management.azure.com/subscriptions/{subscriptionId}/providers/Microsoft.Authorization/policyExemptions/{exemptionName}?api-version={apiVersion}";

            var accessToken = httpContext.Items["AzureAccessToken"] as string;
            if (string.IsNullOrEmpty(accessToken))
            {
                _logger.LogError("Failed to acquire token for Azure Management API");
                return null;
            }

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await client.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch policy exemption details: {Status} {Content}", response.StatusCode, content);
                return null;
            }

            var doc = JsonDocument.Parse(content);
            return doc.RootElement.Clone();
        }

        // Helper to get policyAssignmentId for a result dictionary from the original valueArray
        private static string getPolicyAssignmentId(Dictionary<string, object?> result, JsonElement valueArray)
        {
            // Find the original item in valueArray that matches the name
            if (result.TryGetValue("name", out var nameObj) && nameObj is string name)
            {
                foreach (var item in valueArray.EnumerateArray())
                {
                    if (item.TryGetProperty("name", out var nameProp) && nameProp.GetString() == name)
                    {
                        if (item.TryGetProperty("properties", out var props) &&
                            props.TryGetProperty("policyAssignmentId", out var assignmentIdProp))
                        {
                            return assignmentIdProp.GetString() ?? "";
                        }
                    }
                }
            }
            return "";
        }

        // Helper to check if a result dictionary has a policyAssignmentId in the original valueArray
        private static bool itemHasPolicyAssignmentId(Dictionary<string, object?> result, JsonElement valueArray)
        {
            return !string.IsNullOrEmpty(getPolicyAssignmentId(result, valueArray));
        }
    }
}
