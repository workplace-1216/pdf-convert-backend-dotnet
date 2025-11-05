using System.ComponentModel.DataAnnotations;

namespace PdfPortal.Domain.Entities;

public class User
{
    public int Id { get; set; }
    
    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; set; } = string.Empty;
    
    [MaxLength(13)]
    public string? Rfc { get; set; }
    
    [Required]
    [MaxLength(255)]
    public string PasswordHash { get; set; } = string.Empty;
    
    [Required]
    public UserRole Role { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public enum UserRole
{
    Admin = 1,
    Client = 2
}
