using System.Text;
using Farol.Infrastructure.Auth;
using Farol.Infrastructure.Persistence;
using Farol.Infrastructure.Seeding;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
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
});

var jwtSection = builder.Configuration.GetSection("Jwt");
var signingKey = jwtSection["SigningKey"]
    ?? throw new InvalidOperationException("Jwt:SigningKey configuration is required.");
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection configuration is required.");

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
    });

builder.Services.AddAuthorization();

builder.Services.AddDbContext<FarolDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddScoped<PasswordService>();
builder.Services.AddScoped<JwtTokenService>();

var app = builder.Build();

await DatabaseSeeder.SeedSystemCategoriesAsync(app.Services, app.Lifetime.ApplicationStopping);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
