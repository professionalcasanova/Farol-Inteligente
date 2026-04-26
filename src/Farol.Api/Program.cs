using System.Text;
using System.Threading.RateLimiting;
using Farol.Api.Common;
using Farol.Api.Modules.Bills;
using Farol.Api.Modules.Imports;
using Farol.Api.Modules.Insights;
using Farol.Infrastructure.Auth;
using Farol.Infrastructure.Persistence;
using Farol.Infrastructure.Seeding;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services
    .AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = ApiValidationErrorFactory.Create;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddRateLimiter(options =>
{
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsJsonAsync(
            new ErrorResponse("Muitas tentativas. Tente novamente em alguns instantes.", "rate_limited"),
            cancellationToken);
    };

    options.AddPolicy("auth-login", httpContext =>
    {
        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].ToString();
        var partitionKey = string.IsNullOrWhiteSpace(forwardedFor)
            ? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"
            : forwardedFor.Split(',')[0].Trim();

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
});
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("FarolWeb", policy =>
    {
        if (allowedOrigins.Length == 0)
        {
            return;
        }

        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Farol API",
        Version = "v1"
    });

    var securityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Bearer token no formato: Bearer {token}",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    };

    options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, securityScheme);
    options.OperationFilter<AuthorizeOperationFilter>();
});

var jwtSection = builder.Configuration.GetSection("Jwt");
var signingKey = jwtSection["SigningKey"]
    ?? throw new InvalidOperationException("Jwt:SigningKey configuration is required.");
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection configuration is required.");
var financialIntelligenceOptions = builder.Configuration
    .GetSection(FinancialIntelligenceOptions.SectionName)
    .Get<FinancialIntelligenceOptions>()
    ?? new FinancialIntelligenceOptions();
var internalApiKey = builder.Configuration[financialIntelligenceOptions.InternalApiKeyEnvironmentVariable];

if (!builder.Environment.IsDevelopment() &&
    !builder.Environment.IsEnvironment("Testing") &&
    string.IsNullOrWhiteSpace(internalApiKey))
{
    throw new InvalidOperationException(
        $"{financialIntelligenceOptions.InternalApiKeyEnvironmentVariable} configuration is required.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ClockSkew = TimeSpan.Zero
        };
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new ErrorResponse("Invalid access token.", "unauthorized"));
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new ErrorResponse("Access is forbidden.", "forbidden"));
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddDbContext<FarolDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddScoped<PasswordService>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<PasswordResetService>();
builder.Services.AddScoped<RefreshTokenService>();
builder.Services.AddScoped<BillSeriesExpansionService>();
builder.Services.AddScoped<BillPaymentService>();
builder.Services.AddScoped<BillSeriesUpdateService>();
builder.Services.AddScoped<MonthlyInsightsService>();
builder.Services.AddScoped<FinancialIntelligenceService>();
builder.Services.AddScoped<CsvImportFileReader>();
builder.Services.AddScoped<CsvImportParser>();
builder.Services.AddScoped<TransactionCsvImportProcessor>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.Configure<FinancialIntelligenceOptions>(
    builder.Configuration.GetSection(FinancialIntelligenceOptions.SectionName));
builder.Services.AddHttpClient<IFinancialIntelligenceClient, HttpFinancialIntelligenceClient>((serviceProvider, client) =>
{
    var options = serviceProvider
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<FinancialIntelligenceOptions>>()
        .Value;

    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing"))
{
    if (builder.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
    {
        using var migrationScope = app.Services.CreateScope();
        var dbContext = migrationScope.ServiceProvider.GetRequiredService<FarolDbContext>();
        await dbContext.Database.MigrateAsync(app.Lifetime.ApplicationStopping);
    }

    await DatabaseSeeder.SeedSystemCategoriesAsync(app.Services, app.Lifetime.ApplicationStopping);

    if (app.Environment.IsDevelopment() &&
        builder.Configuration.GetValue<bool>("DemoScenarios:Enabled"))
    {
        await DemoScenarioSeeder.SeedDemoScenariosAsync(app.Services, app.Lifetime.ApplicationStopping);
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsJsonAsync(
            new ErrorResponse("An unexpected error occurred.", "internal_error"));
    });
});

if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseForwardedHeaders(new ForwardedHeadersOptions
    {
        ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
    });
    app.UseHttpsRedirection();
}

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "farol-api"
}));

app.UseCors("FarolWeb");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program;
