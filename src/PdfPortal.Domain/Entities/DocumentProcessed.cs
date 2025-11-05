using System.ComponentModel.DataAnnotations;

namespace PdfPortal.Domain.Entities;

public class DocumentProcessed
{
    public int Id { get; set; }
    
    [Required]
    public int SourceDocumentId { get; set; }
    
    public DocumentOriginal SourceDocument { get; set; } = null!;
    
    public int? TemplateRuleSetId { get; set; }
    
    public TemplateRuleSet? TemplateRuleSet { get; set; }
    
    [Required]
    [MaxLength(500)]
    public string FilePathFinalPdf { get; set; } = string.Empty;
    
    [Required]
    public string ExtractedJsonData { get; set; } = string.Empty;
    
    // GPT-4 Extracted Information
    public string? GptTitle { get; set; }
    
    public string? GptSummary { get; set; }
    
    public string? GptContactInformation { get; set; }
    
    public DateTime? ApprovedAt { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public ProcessedDocumentStatus Status { get; set; } = ProcessedDocumentStatus.Pending;
    
    public bool IsDeletedByClient { get; set; } = false;
    
    // Tracks whether document has been sent to admin via "Enviar" button
    public bool IsSentToAdmin { get; set; } = false;
    
    public DateTime? SentToAdminAt { get; set; }
}

public enum ProcessedDocumentStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3
}
