using PdfPortal.Application.DTOs;
using PdfPortal.Domain.Entities;

namespace PdfPortal.Application.Interfaces;

public interface IAuthService
{
    Task<LoginResponse> LoginAsync(LoginRequest request);
    Task<string> GenerateJwtTokenAsync(User user);
    Task<User?> ValidateUserAsync(string email, string password);
    Task<User?> GetUserByIdAsync(int userId);
    Task<bool> RegisterUserAsync(string email, string password, UserRole role, string? rfc = null);
}
