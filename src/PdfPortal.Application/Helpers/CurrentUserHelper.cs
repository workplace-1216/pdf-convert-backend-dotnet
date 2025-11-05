using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace PdfPortal.Application.Helpers;

public static class CurrentUserHelper
{
    public static int GetCurrentUserId(HttpContext httpContext)
    {
        var userIdClaim = httpContext.User.FindFirst(ClaimTypes.NameIdentifier) ?? 
                         httpContext.User.FindFirst("sub");
        
        if (userIdClaim != null && int.TryParse(userIdClaim.Value, out var userId))
        {
            return userId;
        }
        
        throw new UnauthorizedAccessException("User ID not found in token");
    }

    public static string GetCurrentUserRole(HttpContext httpContext)
    {
        var roleClaim = httpContext.User.FindFirst(ClaimTypes.Role) ?? 
                       httpContext.User.FindFirst("role");
        
        if (roleClaim != null)
        {
            return roleClaim.Value;
        }
        
        throw new UnauthorizedAccessException("User role not found in token");
    }

    public static string GetCurrentUserEmail(HttpContext httpContext)
    {
        var emailClaim = httpContext.User.FindFirst(ClaimTypes.Email);
        
        if (emailClaim != null)
        {
            return emailClaim.Value;
        }
        
        throw new UnauthorizedAccessException("User email not found in token");
    }

    public static bool IsAdmin(HttpContext httpContext)
    {
        try
        {
            return GetCurrentUserRole(httpContext) == "Admin";
        }
        catch
        {
            return false;
        }
    }

    public static bool IsVendor(HttpContext httpContext)
    {
        try
        {
            return GetCurrentUserRole(httpContext) == "Vendor";
        }
        catch
        {
            return false;
        }
    }

    public static bool IsClient(HttpContext httpContext)
    {
        try
        {
            return GetCurrentUserRole(httpContext) == "Client";
        }
        catch
        {
            return false;
        }
    }
}
