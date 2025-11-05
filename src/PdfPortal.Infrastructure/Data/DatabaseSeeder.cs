using Microsoft.EntityFrameworkCore;
using PdfPortal.Domain.Entities;
using System.Security.Cryptography;
using System.Text;

namespace PdfPortal.Infrastructure.Data;

public static class DatabaseSeeder
{
    public static async Task SeedDefaultAdminAsync(PdfPortalDbContext context)
    {
        // Check if admin already exists
        var existingAdmin = await context.Users.FirstOrDefaultAsync(u => u.Role == UserRole.Admin);
        if (existingAdmin != null)
        {
            Console.WriteLine("✓ Admin user already exists");
            return;
        }

        // Create default admin user
        var admin = new User
        {
            Email = "meguiazt@gmail.com",
            PasswordHash = HashPassword("pon87654321"),
            Role = UserRole.Admin,
            CreatedAt = DateTime.UtcNow
        };

        context.Users.Add(admin);
        await context.SaveChangesAsync();

        Console.WriteLine("✓ Default admin user created successfully");
        Console.WriteLine("  Email: meguiazt@gmail.com");
        Console.WriteLine("  Password: pon87654321");
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

