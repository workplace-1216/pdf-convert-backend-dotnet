using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PdfPortal.Application.DTOs;
using PdfPortal.Application.Helpers;
using PdfPortal.Application.Interfaces;
using PdfPortal.Domain.Entities;

namespace PdfPortal.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            var response = await _authService.LoginAsync(request);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }

    [HttpPost("register")]
    public async Task<ActionResult<RegisterVendorResponse>> Register([FromBody] RegisterVendorRequest request)
    {
        try
        {
            // Validate RFC if provided
            if (!string.IsNullOrEmpty(request.Rfc))
            {
                var rfcPattern = @"^[A-Z]{4}[0-9]{6}[A-Z0-9]{3}$";
                if (!System.Text.RegularExpressions.Regex.IsMatch(request.Rfc, rfcPattern))
                {
                    return BadRequest("RFC inválido. Formato: 4 letras, 6 números, 3 alfanuméricos (Ej: AAAA123456ABC)");
                }
            }

            // Create Client users by default
            var success = await _authService.RegisterUserAsync(request.Email, request.TempPassword, UserRole.Client, request.Rfc);
            if (!success)
            {
                return BadRequest("Email already exists");
            }

            // Get the created user to return proper response
            var user = await _authService.ValidateUserAsync(request.Email, request.TempPassword);
            if (user == null)
            {
                return StatusCode(500, "User creation failed");
            }

            var response = new RegisterVendorResponse
            {
                UserId = user.Id,
                Email = user.Email,
                Role = user.Role.ToString(),
                CreatedAt = user.CreatedAt
            };

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> GetCurrentUser()
    {
        try
        {
            var userId = CurrentUserHelper.GetCurrentUserId(HttpContext);
            var user = await _authService.GetUserByIdAsync(userId);
            if (user == null)
            {
                return NotFound();
            }

            var userDto = new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                Role = user.Role.ToString(),
                CreatedAt = user.CreatedAt
            };

            return Ok(userDto);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(ex.Message);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }
}
