using PdfPortal.Application.DTOs;
using PdfPortal.Application.Interfaces;
using PdfPortal.Domain.Entities;

namespace PdfPortal.Application.Services;

public class DocumentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPdfProcessingService _pdfProcessingService;

    public DocumentService(IUnitOfWork unitOfWork, IPdfProcessingService pdfProcessingService)
    {
        _unitOfWork = unitOfWork;
        _pdfProcessingService = pdfProcessingService;
    }

    public async Task<DocumentUploadResponse> UploadDocumentAsync(DocumentUploadRequest request, int userId)
    {
        var document = new DocumentOriginal
        {
            UploaderUserId = userId,
            FilePath = $"uploads/{Guid.NewGuid()}_{request.File.FileName}",
            OriginalFileName = request.File.FileName,
            FileSizeBytes = request.File.Length,
            Status = DocumentStatus.Uploaded
        };

        await _unitOfWork.DocumentOriginals.AddAsync(document);
        await _unitOfWork.SaveChangesAsync();

        return new DocumentUploadResponse
        {
            DocumentId = document.Id,
            Status = "Uploaded",
            Message = "Document uploaded successfully"
        };
    }

    public async Task<DocumentPreviewResponse> PreviewDocumentAsync(int documentId, int? templateId)
    {
        var document = await _unitOfWork.DocumentOriginals.GetByIdAsync(documentId);
        if (document == null)
            throw new ArgumentException("Document not found");

        var template = templateId.HasValue 
            ? await _unitOfWork.TemplateRuleSets.GetByIdAsync(templateId.Value)
            : await GetDefaultTemplateAsync();

        if (template == null)
            throw new ArgumentException("Template not found");

        var processedPdfPath = await _pdfProcessingService.ProcessPdfAsync(document.FilePath, template.JsonDefinition);
        var extractedData = await _pdfProcessingService.ExtractDataFromPdfAsync(processedPdfPath);

        return new DocumentPreviewResponse
        {
            DocumentId = documentId,
            PreviewPdfPath = processedPdfPath,
            ExtractedData = extractedData,
            IsReadyForApproval = true
        };
    }

    public async Task<bool> ApproveDocumentAsync(int documentId, bool approved, string? comments)
    {
        var document = await _unitOfWork.DocumentOriginals.GetByIdAsync(documentId);
        if (document == null)
            return false;

        document.Status = approved ? DocumentStatus.Approved : DocumentStatus.Rejected;

        if (approved)
        {
            var processedDocument = new DocumentProcessed
            {
                SourceDocumentId = documentId,
                TemplateRuleSetId = 1, // Default template for now
                FilePathFinalPdf = document.FilePath,
                ExtractedJsonData = "{}", // Will be populated with actual data
                Status = ProcessedDocumentStatus.Approved,
                ApprovedAt = DateTime.UtcNow
            };

            await _unitOfWork.DocumentProcessed.AddAsync(processedDocument);
        }

        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<DocumentDto>> GetUserDocumentsAsync(int userId)
    {
        var documents = await _unitOfWork.DocumentOriginals.FindAsync(d => d.UploaderUserId == userId);
        
        return documents.Select(d => new DocumentDto
        {
            Id = d.Id,
            OriginalFileName = d.OriginalFileName,
            FileSizeBytes = d.FileSizeBytes,
            Status = d.Status,
            UploadedAt = d.UploadedAt
        });
    }

    private async Task<TemplateRuleSet?> GetDefaultTemplateAsync()
    {
        var templates = await _unitOfWork.TemplateRuleSets.FindAsync(t => t.IsActive);
        return templates.FirstOrDefault();
    }
}
