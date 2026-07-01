using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Threading.RateLimiting;
using ImperadorBarberShop.Api.Middleware;
using ImperadorBarberShop.Application;
using ImperadorBarberShop.Infrastructure;
using ImperadorBarberShop.Infrastructure.Persistence;
using ImperadorBarberShop.Infrastructure.Services;
using ImperadorBarberShop.Infrastructure.Settings;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

// Keep JWT claim names as-is (no mapping to CLR claim types)
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers()
    .AddJsonOptions(opts =>
        opts.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "O Imperador Barber Shop API",
        Version = "v1",
        Description = "API para agendamento de serviços na barbearia O Imperador."
    });
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header. Exemplo: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// JWT Authentication
var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
    ?? throw new InvalidOperationException("JWT settings are not configured.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("RequireBarberRole", policy => policy.RequireClaim("role", "Barber"));
    options.AddPolicy("RequireAdminRole", policy => policy.RequireClaim("role", "Admin"));
});

// CORS — allow only configured frontend URL
var frontendUrl = builder.Configuration["FrontendUrl"] ?? "http://localhost:3000";
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
        policy.WithOrigins(frontendUrl)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// Anti-spam: cap public appointment creation per client IP. A second,
// phone-number-based check lives in CreateAppointmentCommandHandler since the
// rate limiter's partition key cannot see the deserialized request body.
builder.Services.AddRateLimiter(options =>
{
    // Default RejectionStatusCode for AddFixedWindowLimiter is 503 — override to the
    // semantically correct 429 so clients (and tests) can distinguish "rate limited"
    // from "server unavailable".
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("appointment-creation", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromHours(1),
                PermitLimit = 5,
                QueueLimit = 0
            }));
});

var app = builder.Build();

// Auto-migrate and seed on startup (all environments)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    var seeder = scope.ServiceProvider.GetRequiredService<AdminSeedService>();
    await seeder.SeedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "O Imperador API v1"));
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseHttpsRedirection();
app.UseCors("FrontendPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();

app.Run();

// Needed for WebApplicationFactory in integration tests
public partial class Program { }
