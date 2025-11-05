using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PdfPortal.Infrastructure.Data;
using PdfPortal.Infrastructure.Extensions;
using System.Text;
using DotNetEnv;

// Load environment variables from .env file
// Try multiple possible locations
var possibleEnvPaths = new[]
{
    Path.Combine(Directory.GetCurrentDirectory(), ".env"),           // Same directory as executable
    Path.Combine(Directory.GetCurrentDirectory(), "../../.env"),     // backend directory
    Path.Combine(Directory.GetCurrentDirectory(), "../../../.env")   // project root
};

bool envLoaded = false;
foreach (var envPath in possibleEnvPaths)
{
    if (File.Exists(envPath))
    {
        Env.Load(envPath);
        Console.WriteLine($"✓ Loaded environment variables from {envPath}");
        envLoaded = true;
        break;
    }
}

if (!envLoaded)
{
    Console.WriteLine("⚠ No .env file found, using appsettings.json or environment variables");
    Console.WriteLine($"⚠ Current directory: {Directory.GetCurrentDirectory()}");
    Console.WriteLine($"⚠ Searched in: {string.Join(", ", possibleEnvPaths)}");
}

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "PDF Portal API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new()
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme."
    });
    c.AddSecurityRequirement(new()
    {
        {
            new()
            {
                Reference = new()
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                  "http://localhost:5173",
                  "http://localhost:5174",
                  "http://localhost:3000"
              )
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Add Infrastructure services
builder.Services.AddInfrastructure(builder.Configuration);

// Configure JWT Authentication
// Priority: Environment Variables > appsettings.json
var jwtSettings = builder.Configuration.GetSection("Jwt");
var key = Environment.GetEnvironmentVariable("JWT_SECRET_KEY") 
    ?? jwtSettings["Key"] 
    ?? "YourSuperSecretKeyThatIsAtLeast32CharactersLong!";
var issuer = Environment.GetEnvironmentVariable("JWT_ISSUER") 
    ?? jwtSettings["Issuer"] 
    ?? "PdfPortal";
var audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE") 
    ?? jwtSettings["Audience"] 
    ?? "PdfPortalUsers";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Disable HTTPS redirection in development to avoid CORS issues with preflight/redirects
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Ensure database is created and migrated
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<PdfPortalDbContext>();
    context.Database.EnsureCreated();
    
    // Seed default admin user
    await DatabaseSeeder.SeedDefaultAdminAsync(context);
    
    // NOTE: Removed local storage folder creation; PDFs are stored in Cloudflare R2.
}

app.Run();
