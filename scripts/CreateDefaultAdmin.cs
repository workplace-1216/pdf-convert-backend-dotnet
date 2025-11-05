using Microsoft.EntityFrameworkCore;
using PdfPortal.Infrastructure.Data;
using PdfPortal.Application.Services;
using PdfPortal.Domain.Entities;
using System.Security.Cryptography;
using System.Text;

namespace PdfPortal.Scripts;

public class CreateDefaultAdmin
{
    public static async Task CreateAdminUser()
    {
        // Load environment variables
        DotNetEnv.Env.Load();
        
        // Build connection string from environment variables
        var host = Environment.GetEnvironmentVariable("DATABASE_HOST") ?? "localhost";
        var port = Environment.GetEnvironmentVariable("DATABASE_PORT") ?? "5432";
        var database = Environment.GetEnvironmentVariable("DATABASE_NAME") ?? "pdfportal";
        var username = Environment.GetEnvironmentVariable("DATABASE_USER") ?? "postgres";
        var password = Environment.GetEnvironmentVariable("DATABASE_PASSWORD") ?? "123";
        
        var connectionString = $"Host={host};Port={port};Database={database};Username={username};Password={password}";
        
        var options = new DbContextOptionsBuilder<PdfPortalDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        using var context = new PdfPortalDbContext(options);

        // Check if admin already exists and delete it
        var existingAdmin = await context.Users.FirstOrDefaultAsync(u => u.Role == UserRole.Admin);
        if (existingAdmin != null)
        {
            Console.WriteLine("Deleting existing admin user...");
            context.Users.Remove(existingAdmin);
            await context.SaveChangesAsync();
        }

        // Create default admin user with simple SHA256 hash (matching the old implementation)
        var admin = new User
        {
            Email = "meguiazt@gmail.com",
            PasswordHash = HashPassword("pon87654321"),
            Role = UserRole.Admin,
            CreatedAt = DateTime.UtcNow
        };

        context.Users.Add(admin);
        await context.SaveChangesAsync();

        Console.WriteLine("Default admin user created:");
        Console.WriteLine("Email: meguiazt@gmail.com");
        Console.WriteLine("Password: pon87654321");
    }

    private static string HashPassword(string password)
    {
        // Generate salt
        var saltBytes = new byte[16];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(saltBytes);
        var salt = Convert.ToBase64String(saltBytes);

        // Hash password with salt
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        var combinedBytes = new byte[saltBytes.Length + passwordBytes.Length];
        
        Array.Copy(saltBytes, 0, combinedBytes, 0, saltBytes.Length);
        Array.Copy(passwordBytes, 0, combinedBytes, saltBytes.Length, passwordBytes.Length);

        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(combinedBytes);
        var hash = Convert.ToBase64String(hashBytes);

        return $"{salt}:{hash}";
    }
}
