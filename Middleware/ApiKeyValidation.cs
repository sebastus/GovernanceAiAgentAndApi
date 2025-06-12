using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

namespace govapi.Middleware
{
    public class ApiKeyValidationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string _apiKey;

        public ApiKeyValidationMiddleware(RequestDelegate next, IConfiguration configuration)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _apiKey = configuration["API_KEY"] ?? string.Empty;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Allow swagger and development environment without API key
            var env = context.RequestServices.GetService(typeof(IWebHostEnvironment)) as IWebHostEnvironment;
            if (env != null && env.EnvironmentName == "Development")
            {
                await _next(context);
                return;
            }
            if (context.Request.Path.StartsWithSegments("/swagger"))
            {
                await _next(context);
                return;
            }

            if (!_apiKey?.Any() ?? true)
            {
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await context.Response.WriteAsync("API key is not configured.");
                return;
            }

            if (!context.Request.Headers.TryGetValue("x-api-key", out var extractedApiKey) ||
                !string.Equals(extractedApiKey, _apiKey))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Unauthorized: Invalid or missing API key.");
                return;
            }

            await _next(context);
        }
    }
}
