using Microsoft.AspNetCore.Http;
using Azure.Core;
using System.Threading.Tasks;

namespace govapi.Middleware
{
    public class AuthTokenMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly TokenCredential _credential;

        public AuthTokenMiddleware(RequestDelegate next, TokenCredential credential)
        {
            _next = next;
            _credential = credential;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var tokenRequestContext = new TokenRequestContext(new[] { "https://management.azure.com/.default" });
            try
            {
                var token = await _credential.GetTokenAsync(tokenRequestContext, default);
                context.Items["AzureAccessToken"] = token.Token;
            }
            catch
            {
                context.Items["AzureAccessToken"] = null;
            }

            await _next(context);
        }
    }
}
