using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using PdfPortal.Application.Interfaces;
using PdfPortal.Application.Services;
using PdfPortal.Infrastructure.Data;
using PdfPortal.Infrastructure.Repositories;
using PdfPortal.Infrastructure.Services;
using System.Text;

namespace PdfPortal.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Database - Priority: Environment Variables > appsettings.json
        var dbHost = Environment.GetEnvironmentVariable("DATABASE_HOST");
        var dbPort = Environment.GetEnvironmentVariable("DATABASE_PORT");
        var dbName = Environment.GetEnvironmentVariable("DATABASE_NAME");
        var dbUser = Environment.GetEnvironmentVariable("DATABASE_USER");
        var dbPassword = Environment.GetEnvironmentVariable("DATABASE_PASSWORD");
        
        string connectionString;
        
        // If all environment variables are set, use them to build connection string
        if (!string.IsNullOrEmpty(dbHost) && !string.IsNullOrEmpty(dbName) && 
            !string.IsNullOrEmpty(dbUser) && !string.IsNullOrEmpty(dbPassword))
        {
            connectionString = $"Host={dbHost};Port={dbPort ?? "5432"};Database={dbName};Username={dbUser};Password={dbPassword};SSL Mode=Disable";
            Console.WriteLine("✓ Using database connection from environment variables");
        }
        else
        {
            // Fall back to appsettings.json
            connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? "Host=localhost;Database=pdfportal;Username=postgres;Password=123;SSL Mode=Disable";
            Console.WriteLine("✓ Using database connection from appsettings.json");
        }
        
        services.AddDbContext<PdfPortalDbContext>(options =>
            options.UseNpgsql(connectionString));

        // Repositories
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Services
        services.AddHttpClient(); // Add HttpClient for GPT service
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPdfProcessingService, PdfProcessingService>();
        // Storage: ALWAYS use Cloudflare R2 (read credentials from .env)
        Console.WriteLine("✓ Using Cloudflare R2 storage");
        services.AddScoped<IPdfStorageService, CloudflareR2StorageService>();
        services.AddScoped<ITemplateProcessorService, TemplateProcessorService>();
        services.AddScoped<IGptService, GptService>();
        services.AddScoped<TemplateRuleParser>();
        services.AddScoped<JwtTokenService>();
        services.AddScoped<PasswordHasherService>();
        services.AddScoped<DocumentService>();
        services.AddScoped<TemplateService>();


        return services;
    }
}
