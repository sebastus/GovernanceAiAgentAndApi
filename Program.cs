using Azure.Identity;
using Microsoft.ApplicationInsights.Extensibility;
using govapi.Middleware;
using govapi.Services;

var builder = WebApplication.CreateBuilder(args);

// Set minimum log level to Information for all providers
// builder.Logging.ClearProviders();
// builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Get tenant ID from configuration
var tenantId = builder.Configuration["AZURE_TENANT_ID"];
var mi_client_id = builder.Configuration["UAMI_CLIENT_ID"];
var is_development = builder.Configuration["ASPNETCORE_ENVIRONMENT"] == "Development";

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "govapi", Version = "v1" });

    // Only require x-api-key in non-development environments
    if (!is_development)
    {
        c.AddSecurityDefinition("ApiKey", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Description = "API Key needed to access the endpoints. x-api-key: {key}",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Name = "x-api-key",
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
            Scheme = "ApiKeyScheme"
        });
        c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "ApiKey"
                    }
                },
                new string[] {}
            }
        });
    }
});
builder.Services.AddHttpClient();
builder.Services.AddScoped<IPolicyExemptionsService, PolicyExemptionsService>();
builder.Services.AddScoped<ITimeService, SystemTimeService>();
// Register DefaultAzureCredential with tenant ID if provided
builder.Services.AddSingleton<Azure.Core.TokenCredential>(sp =>
{
    var options = new DefaultAzureCredentialOptions();
    if (!string.IsNullOrEmpty(tenantId))
    {
        options.TenantId = tenantId;
        options.ExcludeSharedTokenCacheCredential = true; // Exclude shared token cache to avoid conflicts in multi-tenant scenarios
    }
    if (!string.IsNullOrEmpty(mi_client_id))
    {
        options.ManagedIdentityClientId = mi_client_id; // Use the specified managed identity client ID
    }
    return new DefaultAzureCredential(options);
});

// Add Application Insights telemetry
builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.ConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
});

var app = builder.Build();

// Enable Swagger UI and JSON endpoint in all environments
app.UseSwagger();
app.UseSwaggerUI();

// Add API key validation middleware
app.UseMiddleware<ApiKeyValidationMiddleware>();
app.UseMiddleware<AuthTokenMiddleware>(); // Add this line

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
