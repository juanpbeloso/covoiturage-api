using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SubiteAPI.Data;
using SubiteAPI.Features.Email.Options;
using SubiteAPI.Features.Email.Services;
using SubiteAPI.Features.TripPricing.Domain;
using SubiteAPI.Features.TripPricing.Infrastructure;
using SubiteAPI.Features.TripPricing.Infrastructure.Repositories;
using SubiteAPI.Features.TripPricing.Services;
using SubiteAPI.Middleware;
using SubiteAPI.Models;
using SubiteAPI.Options;
using SubiteAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// Railway (y similares) inyectan PORT; ASP.NET debe escuchar ahí.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// ========== DATABASE ==========
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(ResolveConnectionString(builder.Configuration)));

// ========== IDENTITY ==========
builder.Services.AddIdentity<User, IdentityRole<Guid>>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// ========== JWT AUTHENTICATION ==========
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new Exception("JWT Key not configured");
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.Zero
    };
});

// ========== SERVICES ==========
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPasswordResetService, PasswordResetService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IVehicleService, VehicleService>();
builder.Services.AddScoped<IRideService, RideService>();
builder.Services.AddScoped<IReservationService, ReservationService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IMercadoPagoService, MercadoPagoService>();
builder.Services.AddHostedService<PendingCheckoutExpiryService>();
builder.Services.Configure<MercadoPagoOptions>(builder.Configuration.GetSection(MercadoPagoOptions.SectionName));
builder.Services.Configure<AppOptions>(builder.Configuration.GetSection(AppOptions.SectionName));
builder.Services.Configure<DiditOptions>(builder.Configuration.GetSection(DiditOptions.SectionName));
builder.Services.AddHttpClient("Didit", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<IDiditService, DiditService>();
builder.Services.AddHttpClient("ExpoPush", client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.Configure<EnvialoSimpleOptions>(builder.Configuration.GetSection(EnvialoSimpleOptions.SectionName));
builder.Services.AddSingleton<IEmailTemplateService, EmailTemplateService>();
builder.Services.AddHttpClient<IEmailService, EnvialoSimpleEmailService>((sp, http) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<EnvialoSimpleOptions>>().Value;
    EnvialoSimpleEmailService.ConfigureHttpClient(http, options);
});
builder.Services.AddScoped<ITripPricingService, TripPricingService>();
builder.Services.AddScoped<IPricingConfigService, PricingConfigService>();
builder.Services.AddScoped<IReferencePriceService, ReferencePriceService>();
builder.Services.AddScoped<IPricingConfigRepository, PricingConfigRepository>();
builder.Services.AddScoped<IReferencePriceRepository, ReferencePriceRepository>();
builder.Services.AddSingleton<PricingCalculator>();
builder.Services.Configure<GeorefOptions>(builder.Configuration.GetSection(GeorefOptions.SectionName));
builder.Services.AddHttpClient<IGeorefService, GeorefService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});
// builder.Services.AddScoped<IMercadoPagoService, MercadoPagoService>();
// builder.Services.AddScoped<IFirebaseService, FirebaseService>();
// builder.Services.AddScoped<IStorageService, StorageService>();

// ========== CORS ==========
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// ========== CONTROLLERS ==========
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// ========== SWAGGER ==========
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "Subite API", 
        Version = "v1",
        Description = "API para la aplicación de carpooling Subite"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: 'Bearer {token}'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// ========== MIDDLEWARE ==========
app.UseGlobalExceptionHandler(); // Manejo global de excepciones

var enableSwagger =
    app.Environment.IsDevelopment()
    || app.Configuration.GetValue("EnableSwagger", false);

if (enableSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Migraciones al arranque (local Development o Railway con Database__MigrateOnStartup=true).
var migrateOnStartup =
    app.Environment.IsDevelopment()
    || app.Configuration.GetValue("Database:MigrateOnStartup", false);

if (migrateOnStartup)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync().ConfigureAwait(false);
    await PricingDataSeeder.SeedAsync(db).ConfigureAwait(false);
}

app.Run();

static string ResolveConnectionString(IConfiguration configuration)
{
    var fromConfig = configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrWhiteSpace(fromConfig)
        && !fromConfig.Contains("localhost", StringComparison.OrdinalIgnoreCase))
    {
        return fromConfig;
    }

    // Railway Postgres suele exponer DATABASE_URL (postgres://user:pass@host:port/db).
    var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
    if (string.IsNullOrWhiteSpace(databaseUrl))
    {
        return fromConfig
            ?? throw new InvalidOperationException("Connection string no configurada.");
    }

    if (databaseUrl.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
        || databaseUrl.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':', 2);
        var username = Uri.UnescapeDataString(userInfo[0]);
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
        var database = uri.AbsolutePath.Trim('/');
        return $"Host={uri.Host};Port={(uri.Port > 0 ? uri.Port : 5432)};Database={database};Username={username};Password={password};SSL Mode=Require;Trust Server Certificate=true";
    }

    return databaseUrl;
}
