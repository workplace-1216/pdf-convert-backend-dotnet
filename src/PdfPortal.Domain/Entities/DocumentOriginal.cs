using System.ComponentModel.DataAnnotations;

namespace PdfPortal.Domain.Entities;

public class DocumentOriginal
{
    public int Id { get; set; }
    
    [Required]
    public int UploaderUserId { get; set; }
    
    public User UploaderUser { get; set; } = null!;
    
    [Required]
    [MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(255)]
    public string OriginalFileName { get; set; } = string.Empty;
    
    public long FileSizeBytes { get; set; }
    
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    
    // Identifier for a multi-file upload session (all files sent in one click)
    [MaxLength(100)]
    public string? UploadBatchId { get; set; }
    
    public DocumentStatus Status { get; set; } = DocumentStatus.Uploaded;
}

public enum DocumentStatus
{
    Uploaded = 1,
    Processing = 2,
    ReadyForPreview = 3,
    Approved = 4,
    Rejected = 5
}
