using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PdfPortal.Infrastructure.Data;
using PdfPortal.Application.Helpers;

namespace PdfPortal.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationController : ControllerBase
{
    private readonly PdfPortalDbContext _context;

    public NotificationController(PdfPortalDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications()
    {
        Console.WriteLine("[NotificationController] 📥 GET /api/notification called");
        // Only admin can see notifications
        var userRole = CurrentUserHelper.GetCurrentUserRole(HttpContext);
        Console.WriteLine($"[NotificationController] User role: {userRole}");
        
        if (!string.Equals(userRole, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("[NotificationController] ❌ Access denied: User is not admin");
            return Forbid();
        }

        var notifications = await _context.Notifications
            .Include(n => n.ClientUser)
            .OrderByDescending(n => n.SentAt)
            .Take(50) // Limit to last 50 notifications
            .Select(n => new
            {
                n.Id,
                n.DocumentCount,
                SentAt = n.SentAt.ToString("O"),
                ClientEmail = n.ClientUser != null ? n.ClientUser.Email : "Unknown",
                Read = n.IsRead
            })
            .ToListAsync();

        Console.WriteLine($"[NotificationController] ✅ Returning {notifications.Count} notifications");
        return Ok(notifications);
    }

    [HttpPut("{id}/read")]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var userRole = CurrentUserHelper.GetCurrentUserRole(HttpContext);
        if (!string.Equals(userRole, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        var notification = await _context.Notifications.FindAsync(id);
        if (notification == null)
        {
            return NotFound();
        }

        notification.IsRead = true;
        await _context.SaveChangesAsync();

        return Ok();
    }

    [HttpDelete]
    public async Task<IActionResult> ClearAll()
    {
        var userRole = CurrentUserHelper.GetCurrentUserRole(HttpContext);
        if (!string.Equals(userRole, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        _context.Notifications.RemoveRange(_context.Notifications);
        await _context.SaveChangesAsync();

        return Ok();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userRole = CurrentUserHelper.GetCurrentUserRole(HttpContext);
        if (!string.Equals(userRole, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        var notification = await _context.Notifications.FindAsync(id);
        if (notification == null)
        {
            return NotFound();
        }

        _context.Notifications.Remove(notification);
        await _context.SaveChangesAsync();

        return Ok();
    }
}

