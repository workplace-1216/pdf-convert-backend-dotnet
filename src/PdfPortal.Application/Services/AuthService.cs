using PdfPortal.Application.DTOs;
using PdfPortal.Application.Interfaces;
using PdfPortal.Domain.Entities;

namespace PdfPortal.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly JwtTokenService _jwtTokenService;
    private readonly PasswordHasherService _passwordHasher;

    public AuthService(IUnitOfWork unitOfWork, JwtTokenService jwtTokenService, PasswordHasherService passwordHasher)
    {
        _unitOfWork = unitOfWork;
        _jwtTokenService = jwtTokenService;
        _passwordHasher = passwordHasher;
    }

    public async Task<LoginResponse> LoginAsync(LoginRequest request)
    {
        var user = await ValidateUserAsync(request.Email, request.Password);
        if (user == null)
            throw new UnauthorizedAccessException("Invalid email or password");

        var token = _jwtTokenService.GenerateToken(user.Id, user.Email, user.Role.ToString());
        return new LoginResponse
        {
            Token = token,
            Role = user.Role.ToString()
        };
    }

    public async Task<bool> RegisterUserAsync(string email, string password, UserRole role, string? rfc = null)
    {
        var existingUser = await _unitOfWork.Users.FindAsync(u => u.Email == email);
        if (existingUser.Any())
            return false;

        var user = new User
        {
            Email = email,
            PasswordHash = _passwordHasher.HashPassword(password),
            Role = role,
            Rfc = rfc
        };

        await _unitOfWork.Users.AddAsync(user);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public Task<string> GenerateJwtTokenAsync(User user)
    {
        return Task.FromResult(_jwtTokenService.GenerateToken(user.Id, user.Email, user.Role.ToString()));
    }

    public async Task<User?> ValidateUserAsync(string email, string password)
    {
        var user = await _unitOfWork.Users.FindAsync(u => u.Email == email);
        var foundUser = user.FirstOrDefault();
        
        if (foundUser == null)
            return null;

        return _passwordHasher.VerifyPassword(password, foundUser.PasswordHash) ? foundUser : null;
    }

    public async Task<User?> GetUserByIdAsync(int userId)
    {
        return await _unitOfWork.Users.GetByIdAsync(userId);
    }
}