using System.Security.Cryptography;
using System.Text;

namespace PdfPortal.Application.Services;

public class PasswordHasherService
{
    public string HashPassword(string password)
    {
        // Using BCrypt-style hashing with salt
        var salt = GenerateSalt();
        var hash = HashWithSalt(password, salt);
        return $"{salt}:{hash}";
    }

    public bool VerifyPassword(string password, string hashedPassword)
    {
        if (string.IsNullOrEmpty(hashedPassword))
            return false;

        var parts = hashedPassword.Split(':');
        if (parts.Length != 2)
            return false;

        var salt = parts[0];
        var hash = parts[1];
        var computedHash = HashWithSalt(password, salt);

        return hash == computedHash;
    }

    private string GenerateSalt()
    {
        var saltBytes = new byte[16];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(saltBytes);
        return Convert.ToBase64String(saltBytes);
    }

    private string HashWithSalt(string password, string salt)
    {
        var saltBytes = Convert.FromBase64String(salt);
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        var combinedBytes = new byte[saltBytes.Length + passwordBytes.Length];
        
        Array.Copy(saltBytes, 0, combinedBytes, 0, saltBytes.Length);
        Array.Copy(passwordBytes, 0, combinedBytes, saltBytes.Length, passwordBytes.Length);

        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(combinedBytes);
        return Convert.ToBase64String(hashBytes);
    }
}
