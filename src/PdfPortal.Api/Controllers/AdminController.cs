using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PdfPortal.Application.DTOs;
using PdfPortal.Application.Helpers;
using PdfPortal.Application.Interfaces;
using PdfPortal.Domain.Entities;
using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PdfPortal.Infrastructure.Data;

namespace PdfPortal.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuthService _authService;
    private readonly PdfPortalDbContext _dbContext;

    public AdminController(IUnitOfWork unitOfWork, IAuthService authService, PdfPortalDbContext dbContext)
    {
        _unitOfWork = unitOfWork;
        _authService = authService;
        _dbContext = dbContext;
    }

    // Dashboard Stats
    [HttpGet("stats")]
    public async Task<ActionResult<AdminStatsDto>> GetDashboardStats()
    {
        try
        {
            // Admin stats should only count documents that have been sent by clients
            var totalDocuments = await _unitOfWork.DocumentProcessed.CountAsync(d => d.IsSentToAdmin);
            var processedDocuments = await _unitOfWork.DocumentProcessed.CountAsync(d => 
                d.IsSentToAdmin && d.Status == ProcessedDocumentStatus.Approved);
            var pendingDocuments = await _unitOfWork.DocumentProcessed.CountAsync(d => 
                d.IsSentToAdmin && d.Status == ProcessedDocumentStatus.Pending);
            var errorDocuments = await _unitOfWork.DocumentProcessed.CountAsync(d => 
                d.IsSentToAdmin && d.Status == ProcessedDocumentStatus.Rejected);
            
            var totalUsers = await _unitOfWork.Users.CountAsync(u => true);
            var activeUsers = await _unitOfWork.Users.CountAsync(u => u.Role == UserRole.Client);
            
            var stats = new AdminStatsDto
            {
                TotalDocuments = totalDocuments,
                ProcessedDocuments = processedDocuments,
                PendingDocuments = pendingDocuments,
                ErrorDocuments = errorDocuments,
                TotalUsers = totalUsers,
                ActiveUsers = activeUsers,
                ProcessedToday = await GetTodayProcessedCount()
            };

            return Ok(stats);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }

    // Get all users with pagination
    [HttpGet("users")]
    public async Task<ActionResult<PagedResult<AdminUserDto>>> GetUsers(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? role = null,
        [FromQuery] string? status = null)
    {
        try
        {
            Expression<Func<User, bool>> predicate = u => true;

            if (!string.IsNullOrEmpty(search))
            {
                predicate = u => u.Email.Contains(search);
            }

            if (!string.IsNullOrEmpty(role) && role != "All")
            {
                var roleEnum = Enum.Parse<UserRole>(role);
                var basePredicate = predicate;
                predicate = u => basePredicate.Compile()(u) && u.Role == roleEnum;
            }

            var totalCount = await _unitOfWork.Users.CountAsync(predicate);
            var users = await _unitOfWork.Users.FindAsync(
                predicate,
                skip: (page - 1) * pageSize,
                take: pageSize,
                orderBy: u => u.CreatedAt,
                orderByDescending: true);

            var userDtos = new List<AdminUserDto>();
            foreach (var user in users)
            {
                // Get document count by userId
                var documentCount = await _unitOfWork.DocumentOriginals.CountAsync(d => d.UploaderUserId == user.Id);
                userDtos.Add(new AdminUserDto
                {
                    Id = user.Id.ToString(),
                    Name = user.Email.Split('@')[0], // Use email prefix as name for now
                    Email = user.Email,
                    Role = user.Role.ToString(),
                    Status = "Activo", // TODO: Add status to User entity
                    LastLogin = user.CreatedAt.ToString("yyyy-MM-dd"),
                    DocumentsCount = documentCount
                });
            }

            return Ok(new PagedResult<AdminUserDto>
            {
                Items = userDtos,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }

    // Get all documents with pagination
    [HttpGet("documents")]
    public async Task<ActionResult<PagedResult<AdminDocumentDto>>> GetDocuments(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null)
    {
        try
        {
            // Admin should ONLY see documents that clients have sent
            Expression<Func<DocumentProcessed, bool>> predicate = d => d.IsSentToAdmin;
            
            Console.WriteLine($"[AdminController] GetDocuments - Page: {page}, PageSize: {pageSize}, Status: {status}");

            if (!string.IsNullOrEmpty(status) && status != "All")
            {
                var statusEnum = Enum.Parse<ProcessedDocumentStatus>(status);
                var basePredicate = predicate;
                predicate = d => basePredicate.Compile()(d) && d.Status == statusEnum;
            }

            var totalCount = await _unitOfWork.DocumentProcessed.CountAsync(predicate);
            var documents = await _unitOfWork.DocumentProcessed.FindAsync(
                predicate,
                skip: (page - 1) * pageSize,
                take: pageSize,
                orderBy: d => d.CreatedAt,
                orderByDescending: true);

            var documentDtos = new List<AdminDocumentDto>();
            foreach (var doc in documents)
            {
                // Get source document and uploader info first
                var sourceDoc = await _unitOfWork.DocumentOriginals.GetByIdAsync(doc.SourceDocumentId);
                var uploaderEmail = "Unknown";
                string uploaderRfc = "No registrado";
                
                if (sourceDoc != null)
                {
                    var uploader = await _unitOfWork.Users.GetByIdAsync(sourceDoc.UploaderUserId);
                    if (uploader != null)
                    {
                        uploaderEmail = uploader.Email ?? "Unknown";
                        uploaderRfc = uploader.Rfc ?? "No registrado";  // Use user's registered RFC
                    }
                }

                // Parse extracted data for other fields (using user's RFC, not extracted)
                var extractedData = new ExtractedDataDto { Rfc = uploaderRfc, Periodo = "N/A", Monto = "0" };
                try
                {
                    if (!string.IsNullOrEmpty(doc.ExtractedJsonData))
                    {
                        var jsonData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(doc.ExtractedJsonData);
                        if (jsonData != null)
                        {
                            // Extract periodo and monto (case-insensitive)
                            if (jsonData.ContainsKey("PERIODO")) extractedData.Periodo = jsonData["PERIODO"].GetString() ?? "N/A";
                            if (jsonData.ContainsKey("periodo")) extractedData.Periodo = jsonData["periodo"].GetString() ?? extractedData.Periodo;
                            if (jsonData.ContainsKey("MONTO_TOTAL")) extractedData.Monto = jsonData["MONTO_TOTAL"].GetString() ?? "0";
                            if (jsonData.ContainsKey("monto_total")) extractedData.Monto = jsonData["monto_total"].GetString() ?? extractedData.Monto;
                        }
                    }
                }
                catch { /* ignore JSON parse errors */ }

                documentDtos.Add(new AdminDocumentDto
                {
                    Id = doc.Id.ToString(),
                    FileName = sourceDoc?.OriginalFileName ?? "N/A",
                    Uploader = uploaderEmail,
                    UploadDate = doc.CreatedAt.ToString("yyyy-MM-dd"),
                    Status = GetDocumentStatusString(doc.Status),
                    FileSize = sourceDoc != null ? FormatFileSize(sourceDoc.FileSizeBytes) : "N/A",
                    DocumentType = "Factura", // TODO: Add document type
                    ExtractedData = extractedData
                });
            }

            return Ok(new PagedResult<AdminDocumentDto>
            {
                Items = documentDtos,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }

    // Create admin user
    [HttpPost("users")]
    public async Task<ActionResult<AdminUserDto>> CreateAdmin([FromBody] CreateAdminRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Email) ||
                !request.Email.EndsWith("@admin.com", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Email domain must be '@admin.com'");
            }

            var success = await _authService.RegisterUserAsync(request.Email, request.Password, UserRole.Admin);
            if (!success)
            {
                return BadRequest("Email already exists");
            }

            var user = await _authService.ValidateUserAsync(request.Email, request.Password);
            if (user == null)
            {
                return StatusCode(500, "User creation failed");
            }

            return Ok(new AdminUserDto
            {
                Id = user.Id.ToString(),
                Name = request.Name,
                Email = user.Email,
                Role = "Admin",
                Status = "Activo",
                LastLogin = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                DocumentsCount = 0
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }

    // Delete user
    [HttpDelete("users/{userId}")]
    public async Task<ActionResult> DeleteUser(string userId)
    {
        try
        {
            if (!int.TryParse(userId, out var userIdInt))
            {
                return BadRequest("Invalid user ID");
            }

            var user = await _unitOfWork.Users.GetByIdAsync(userIdInt);
            if (user == null)
            {
                return NotFound("User not found");
            }

            await _unitOfWork.BeginTransactionAsync();
            try
            {
                // Remove notifications for this user to avoid FK issues
                var notifications = await _dbContext.Notifications
                    .Where(n => n.ClientUserId == userIdInt)
                    .ToListAsync();
                if (notifications.Count > 0)
                {
                    _dbContext.Notifications.RemoveRange(notifications);
                }

                // Reassign any template rule sets created by this user to current admin (to avoid FK restrict)
                var currentAdminId = CurrentUserHelper.GetCurrentUserId(HttpContext);
                var templates = await _unitOfWork.TemplateRuleSets.FindAsync(t => t.CreatedByUserId == userIdInt);
                foreach (var t in templates)
                {
                    t.CreatedByUserId = currentAdminId;
                    await _unitOfWork.TemplateRuleSets.UpdateAsync(t);
                }

                // Delete documents belonging to this user
                var originals = await _unitOfWork.DocumentOriginals.FindAsync(d => d.UploaderUserId == userIdInt);
                var originalIds = originals.Select(o => o.Id).ToHashSet();
                if (originalIds.Count > 0)
                {
                    var processed = await _unitOfWork.DocumentProcessed.FindAsync(d => originalIds.Contains(d.SourceDocumentId));
                    foreach (var p in processed)
                    {
                        await _unitOfWork.DocumentProcessed.DeleteAsync(p);
                    }
                    foreach (var o in originals)
                    {
                        await _unitOfWork.DocumentOriginals.DeleteAsync(o);
                    }
                }

                // Finally delete the user
                await _unitOfWork.Users.DeleteAsync(user);

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransactionAsync();
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync();
                throw;
            }

            return Ok(new { message = "User deleted successfully" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }

    // Reports & Analytics - combined payload
    [HttpGet("analytics")]
    public async Task<ActionResult<ReportsAnalyticsDto>> GetAnalytics([FromQuery] string period = "30d")
    {
        try
        {
            int days = period.EndsWith("d") && int.TryParse(period[..^1], out var d)
                ? d
                : period.Equals("1y", StringComparison.OrdinalIgnoreCase) ? 365 : 30;

            var since = DateTime.UtcNow.Date.AddDays(-days + 1);

            // Base datasets - only count documents sent to admin
            var processedDocs = await _unitOfWork.DocumentProcessed.FindAsync(d => d.IsSentToAdmin && d.CreatedAt >= since);
            var totalDocsCount = await _unitOfWork.DocumentProcessed.CountAsync(d => d.IsSentToAdmin);
            var processedToday = await _unitOfWork.DocumentProcessed.CountAsync(d => d.IsSentToAdmin && d.CreatedAt >= DateTime.UtcNow.Date);
            var errorDocsCount = await _unitOfWork.DocumentProcessed.CountAsync(d => d.IsSentToAdmin && d.Status == ProcessedDocumentStatus.Rejected);
            var pendingDocsCount = await _unitOfWork.DocumentProcessed.CountAsync(d => d.IsSentToAdmin && d.Status == ProcessedDocumentStatus.Pending);
            var usersCount = await _unitOfWork.Users.CountAsync(u => true);
            var activeUsers = await _unitOfWork.Users.CountAsync(u => u.Role == UserRole.Client);

            // Monthly trends for last 12 months
            var startMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(-11);
            var monthly = new List<MonthlyTrendDto>();
            var monthNamesEs = new[] { "Ene", "Feb", "Mar", "Abr", "May", "Jun", "Jul", "Ago", "Sep", "Oct", "Nov", "Dic" };
            for (int i = 0; i < 12; i++)
            {
                var mStart = startMonth.AddMonths(i);
                var mEnd = mStart.AddMonths(1);
                var inMonth = processedDocs.Where(d => d.CreatedAt >= mStart && d.CreatedAt < mEnd);
                monthly.Add(new MonthlyTrendDto
                {
                    Month = monthNamesEs[mStart.Month - 1],
                    Documents = inMonth.Count(),
                    Processed = inMonth.Count(d => d.Status == ProcessedDocumentStatus.Approved),
                    Errors = inMonth.Count(d => d.Status == ProcessedDocumentStatus.Rejected)
                });
            }

            // User activity - 6 points in the last 24h (4-hour buckets)
            var activity = new List<UserActivityPointDto>();
            for (int i = 0; i < 6; i++)
            {
                var bucketStart = DateTime.UtcNow.Date.AddHours(i * 4);
                var bucketEnd = bucketStart.AddHours(4);
                var inBucket = processedDocs.Where(d => d.CreatedAt >= bucketStart && d.CreatedAt < bucketEnd).ToList();
                activity.Add(new UserActivityPointDto
                {
                    Time = bucketStart.ToString("HH:mm"),
                    Documents = inBucket.Count,
                    Users = inBucket.Count // approximation
                });
            }

            // Document types - placeholder (single type)
            var documentTypes = new List<NameValueDto>
            {
                new NameValueDto { Name = "Factura", Value = processedDocs.Count() }
            };

            // Processing time distribution - placeholder buckets
            var processingTime = new List<RangeCountDto>
            {
                new RangeCountDto { Range = "0-30s", Count = processedDocs.Count()/2 },
                new RangeCountDto { Range = "30s-1m", Count = processedDocs.Count()/4 },
                new RangeCountDto { Range = "1-2m", Count = processedDocs.Count()/8 },
                new RangeCountDto { Range = "2-5m", Count = processedDocs.Count()/16 },
                new RangeCountDto { Range = "5m+", Count = Math.Max(0, processedDocs.Count() - (processedDocs.Count()/2 + processedDocs.Count()/4 + processedDocs.Count()/8 + processedDocs.Count()/16)) }
            };

            // Error types - single bucket using total errors
            var errorTypes = new List<ErrorTypeDto>
            {
                new ErrorTypeDto { Type = "Errores", Count = errorDocsCount, Percentage = totalDocsCount == 0 ? 0 : (int)Math.Round((double)errorDocsCount * 100 / totalDocsCount) }
            };

            var successRate = totalDocsCount == 0 ? 0 : Math.Round(((double)(totalDocsCount - errorDocsCount)) * 100 / totalDocsCount, 1);

            var dto = new ReportsAnalyticsDto
            {
                Stats = new ReportsStatsDto
                {
                    TotalDocuments = totalDocsCount,
                    ProcessedToday = processedToday,
                    AverageProcessingTime = "-",
                    SuccessRate = successRate,
                    TotalUsers = usersCount,
                    ActiveUsers = activeUsers,
                    GrowthRate = 0
                },
                MonthlyTrends = monthly,
                UserActivity = activity,
                DocumentTypes = documentTypes,
                ProcessingTime = processingTime,
                ErrorTypes = errorTypes
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"An error occurred: {ex.Message}");
        }
    }

    // Helper methods
    private async Task<int> GetTodayProcessedCount()
    {
        var today = DateTime.UtcNow.Date;
        return await _unitOfWork.DocumentProcessed.CountAsync(d => 
            d.IsSentToAdmin && d.CreatedAt >= today && d.Status == ProcessedDocumentStatus.Approved);
    }

    private string GetDocumentStatusString(ProcessedDocumentStatus status)
    {
        return status switch
        {
            ProcessedDocumentStatus.Approved => "Completado",
            ProcessedDocumentStatus.Pending => "Procesando",
            ProcessedDocumentStatus.Rejected => "Error",
            _ => "Pendiente de revisión"
        };
    }

    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
