using Microsoft.AspNetCore.Http;
using PdfPortal.Domain.Entities;

namespace PdfPortal.Application.DTOs;

public class PagedResult<T>
{
    public IEnumerable<T> Items { get; set; } = new List<T>();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}

public class DocumentUploadRequest
{
    public IFormFile File { get; set; } = null!;
    public string TemplateId { get; set; } = string.Empty;
}

public class DocumentUploadResponse
{
    public int DocumentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string SourceDocumentTempId { get; set; } = string.Empty;
    public string PreviewPdfBase64 { get; set; } = string.Empty;
    public Dictionary<string, object> ExtractedData { get; set; } = new();
}

public class DocumentConfirmRequest
{
    public string SourceDocumentTempId { get; set; } = string.Empty;
    public string TemplateId { get; set; } = string.Empty;
    public Dictionary<string, object> ExtractedData { get; set; } = new();
    public string FinalPdfBase64 { get; set; } = string.Empty;
}

public class DocumentConfirmResponse
{
    public int ProcessedDocumentId { get; set; }
}

public class ProcessedDocumentDto
{
    public int Id { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DocumentStatus Status { get; set; }
    public DateTime UploadedAt { get; set; }
    public DateTime ProcessedAt { get; set; }
    public int VendorId { get; set; }
    public string VendorEmail { get; set; } = string.Empty;
    public int TemplateId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public Dictionary<string, object> ExtractedData { get; set; } = new();
}

public class ProcessedDocumentListResponse
{
    public List<ProcessedDocumentDto> Documents { get; set; } = new();
    public int TotalCount { get; set; }
}

public class DocumentPreviewResponse
{
    public int DocumentId { get; set; }
    public string PreviewPdfPath { get; set; } = string.Empty;
    public Dictionary<string, object> ExtractedData { get; set; } = new();
    public bool IsReadyForApproval { get; set; }
}

public class DocumentApprovalRequest
{
    public int DocumentId { get; set; }
    public bool Approved { get; set; }
    public string? Comments { get; set; }
}

public class VendorDocumentDto
{
    public int Id { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string ProcessedStatus { get; set; } = string.Empty;
    public int? ProcessedDocumentId { get; set; }
}

public class VendorDocumentListResponse
{
    public List<VendorDocumentDto> Documents { get; set; } = new();
    public int TotalCount { get; set; }
}

public class DocumentDto
{
    public int Id { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public DocumentStatus Status { get; set; }
    public DateTime UploadedAt { get; set; }
    public string? FinalPdfPath { get; set; }
    public Dictionary<string, object>? ExtractedData { get; set; }
}

// Client-specific DTOs
public class ClientReadyDocumentDto
{
    public string Id { get; set; } = string.Empty;
    public string ProveedorEmail { get; set; } = string.Empty;
    public DateTime ReadyAtUtc { get; set; }
    public DateTime? UploadedAtUtc { get; set; } // original upload timestamp for stable folder grouping
    public string? UploadBatchId { get; set; }
    public string RfcEmisor { get; set; } = string.Empty;
    public string Periodo { get; set; } = string.Empty;
    public string MontoTotalMxn { get; set; } = string.Empty;
    public string ComplianceStatus { get; set; } = string.Empty;
}

public class ClientDocumentDetailDto
{
    public string Id { get; set; } = string.Empty;
    public string ProveedorEmail { get; set; } = string.Empty;
    public DateTime ReadyAtUtc { get; set; }
    public ClientFiscalDataDto FiscalData { get; set; } = new();
    public ClientDocumentStructureDto DocumentStructure { get; set; } = new();
    public ClientAppliedMetadataDto AppliedMetadata { get; set; } = new();
    public ClientTechnicalComplianceDto TechnicalCompliance { get; set; } = new();
    public ClientDownloadLinksDto DownloadLinks { get; set; } = new();
    public ClientTransformationVerificationDto TransformationVerification { get; set; } = new();
}

public class ClientFiscalDataDto
{
    public string RfcEmisor { get; set; } = string.Empty;
    public string Periodo { get; set; } = string.Empty;
    public string MontoTotalMxn { get; set; } = string.Empty;
}

public class ClientDocumentStructureDto
{
    public bool AddedStandardCoverPage { get; set; }
    public bool AddedFooterTraceability { get; set; }
    public bool RemovedExtraPages { get; set; }
    public bool RemovedInteractiveElements { get; set; }
    public string StructureNote { get; set; } = string.Empty;
}

public class ClientAppliedMetadataDto
{
    public string Title { get; set; } = string.Empty;
    public string RfcEmisorField { get; set; } = string.Empty;
    public string PeriodoField { get; set; } = string.Empty;
    public DateTime NormalizedAtUtc { get; set; }
    public string NormalizedByEmail { get; set; } = string.Empty;
}

public class ClientTechnicalComplianceDto
{
    public bool IsPdf { get; set; }
    public bool Grayscale8bit { get; set; }
    public bool Dpi300 { get; set; }
    public bool SizeUnder3MB { get; set; }
    public bool NoInteractiveStuff { get; set; }
    public bool HasRequiredMetadata { get; set; }
}

public class ClientDownloadLinksDto
{
    public string PdfFinalUrl { get; set; } = string.Empty;
    public string DataJsonUrl { get; set; } = string.Empty;
}

// Comprehensive transformation verification DTOs
public class ClientTransformationVerificationDto
{
    public ClientMetadataTransformationDto Metadata { get; set; } = new();
    public ClientContentRestructuringDto Restructuring { get; set; } = new();
    public ClientDataExtractionDto Extraction { get; set; } = new();
    public ClientFormatNormalizationDto Normalization { get; set; } = new();
    public bool AllTransformationsApplied { get; set; }
    public string ComplianceStatus { get; set; } = string.Empty; // "COMPLIANT", "NON_COMPLIANT", "PARTIAL"
    public string ProcessingSummary { get; set; } = string.Empty;
}

public class ClientMetadataTransformationDto
{
    public Dictionary<string, string> OriginalMetadata { get; set; } = new();
    public Dictionary<string, string> InjectedMetadata { get; set; } = new();
    public Dictionary<string, string> FinalMetadata { get; set; } = new();
    public bool RfcInjected { get; set; }
    public bool PeriodoInjected { get; set; }
    public bool MontoTotalInjected { get; set; }
    public bool AuditTrailAdded { get; set; }
    public string TemplateUsed { get; set; } = string.Empty;
    public string ProcessingTimestamp { get; set; } = string.Empty;
    public string ProcessedBy { get; set; } = string.Empty;
}

public class ClientContentRestructuringDto
{
    public int OriginalPageCount { get; set; }
    public int FinalPageCount { get; set; }
    public bool CoverPageAdded { get; set; }
    public List<int> PagesRemoved { get; set; } = new();
    public List<int> PagesReordered { get; set; } = new();
    public bool FooterApplied { get; set; }
    public bool FormsStripped { get; set; }
    public bool JavaScriptRemoved { get; set; }
    public bool AttachmentsRemoved { get; set; }
    public string RestructuringSummary { get; set; } = string.Empty;
    public List<string> ContentModifications { get; set; } = new();
}

public class ClientDataExtractionDto
{
    public Dictionary<string, string> ExtractedFields { get; set; } = new();
    public Dictionary<string, double> ExtractionConfidence { get; set; } = new();
    public Dictionary<string, bool> FieldValidation { get; set; } = new();
    public string RfcExtracted { get; set; } = string.Empty;
    public string PeriodoExtracted { get; set; } = string.Empty;
    public string MontoTotalExtracted { get; set; } = string.Empty;
    public bool RfcValid { get; set; }
    public bool PeriodoValid { get; set; }
    public bool MontoTotalValid { get; set; }
    public string ExtractionMethod { get; set; } = string.Empty; // "REGEX", "AI", "MANUAL"
    public string ExtractionTimestamp { get; set; } = string.Empty;
    public List<string> ExtractionWarnings { get; set; } = new();
}

public class ClientFormatNormalizationDto
{
    public string OriginalFormat { get; set; } = string.Empty;
    public string FinalFormat { get; set; } = string.Empty;
    public bool ConvertedToGrayscale { get; set; }
    public bool ConvertedTo8Bit { get; set; }
    public bool NormalizedTo300Dpi { get; set; }
    public bool CompressedUnder3MB { get; set; }
    public bool PasswordRemoved { get; set; }
    public bool InteractiveContentRemoved { get; set; }
    public string CompressionRatio { get; set; } = string.Empty;
    public long OriginalSizeBytes { get; set; }
    public long FinalSizeBytes { get; set; }
    public string NormalizationTimestamp { get; set; } = string.Empty;
    public List<string> NormalizationSteps { get; set; } = new();
}

// Admin DTOs
public class AdminStatsDto
{
    public int TotalDocuments { get; set; }
    public int ProcessedDocuments { get; set; }
    public int PendingDocuments { get; set; }
    public int ErrorDocuments { get; set; }
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int ProcessedToday { get; set; }
}

public class AdminUserDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string LastLogin { get; set; } = string.Empty;
    public int DocumentsCount { get; set; }
}

public class AdminDocumentDto
{
    public string Id { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Uploader { get; set; } = string.Empty;
    public string UploadDate { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string FileSize { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public ExtractedDataDto ExtractedData { get; set; } = new();
}

public class ExtractedDataDto
{
    public string Rfc { get; set; } = string.Empty;
    public string Periodo { get; set; } = string.Empty;
    public string Monto { get; set; } = string.Empty;
}

public class CreateAdminRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class SendDocumentsRequest
{
    public List<int> DocumentIds { get; set; } = new();
    public string? ToEmail { get; set; }
}

public class BatchDownloadRequest
{
    public List<int> DocumentIds { get; set; } = new();
}

public class DeleteDocumentsRequest
{
    public List<int> DocumentIds { get; set; } = new();
}

// Reports & Analytics DTOs
public class ReportsAnalyticsDto
{
    public ReportsStatsDto Stats { get; set; } = new();
    public List<MonthlyTrendDto> MonthlyTrends { get; set; } = new();
    public List<UserActivityPointDto> UserActivity { get; set; } = new();
    public List<NameValueDto> DocumentTypes { get; set; } = new();
    public List<RangeCountDto> ProcessingTime { get; set; } = new();
    public List<ErrorTypeDto> ErrorTypes { get; set; } = new();
}

public class ReportsStatsDto
{
    public int TotalDocuments { get; set; }
    public int ProcessedToday { get; set; }
    public string AverageProcessingTime { get; set; } = "-";
    public double SuccessRate { get; set; }
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public double GrowthRate { get; set; }
}

public class MonthlyTrendDto
{
    public string Month { get; set; } = string.Empty; // e.g., "Ene"
    public int Documents { get; set; }
    public int Processed { get; set; }
    public int Errors { get; set; }
}

public class UserActivityPointDto
{
    public string Time { get; set; } = string.Empty; // e.g., "00:00"
    public int Users { get; set; }
    public int Documents { get; set; }
}

public class NameValueDto
{
    public string Name { get; set; } = string.Empty;
    public int Value { get; set; }
}

public class RangeCountDto
{
    public string Range { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class ErrorTypeDto
{
    public string Type { get; set; } = string.Empty;
    public int Count { get; set; }
    public int Percentage { get; set; }
}