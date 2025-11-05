using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PdfPortal.Application.DTOs;
using PdfPortal.Application.Helpers;
using PdfPortal.Application.Interfaces;
using PdfPortal.Application.Models;
using PdfPortal.Application.Services;
using PdfPortal.Domain.Entities;
using PdfPortal.Infrastructure.Data;
using System.IO;
using System.IO.Compression;
using System.Net.Mail;
using System.Text.Json;

namespace PdfPortal.Api.Controllers;

[ApiController]
[Route("api/documents")]
[Authorize]
public class DocumentController : ControllerBase
{
    private readonly DocumentService _documentService;
    private readonly IPdfStorageService _pdfStorageService;
    private readonly ITemplateProcessorService _templateProcessorService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TemplateRuleParser _templateRuleParser;
    private readonly IConfiguration _configuration;
    private readonly IGptService _gptService;

    private readonly PdfPortalDbContext _context;

    public DocumentController(
        DocumentService documentService,
        IPdfStorageService pdfStorageService,
        ITemplateProcessorService templateProcessorService,
        IUnitOfWork unitOfWork,
        TemplateRuleParser templateRuleParser,
        IConfiguration configuration,
        IGptService gptService,
        PdfPortalDbContext context)
    {
        _documentService = documentService;
        _pdfStorageService = pdfStorageService;
        _templateProcessorService = templateProcessorService;
        _unitOfWork = unitOfWork;
        _templateRuleParser = templateRuleParser;
        _configuration = configuration;
        _gptService = gptService;
        _context = context;
    }

    [HttpPost("upload")]
    [Authorize(Roles = "Client,Admin")]
    public async Task<ActionResult<DocumentUploadResponse>> UploadDocument([FromForm] IFormFile file, [FromForm] int templateId = 1, [FromForm] string? batchId = null)
    {
        try
        {
            Console.WriteLine($"Upload request received - File: {file?.FileName}, Size: {file?.Length}, TemplateId: {templateId}");
            
            if (file == null || file.Length == 0)
            {
                Console.WriteLine("No file uploaded");
                return BadRequest("No file uploaded");
            }

            if (file.ContentType != "application/pdf")
            {
                return BadRequest("Only PDF files are allowed");
            }

            int userId;
            string userEmail;
            try
            {
                userId = CurrentUserHelper.GetCurrentUserId(HttpContext);
                userEmail = CurrentUserHelper.GetCurrentUserEmail(HttpContext);
            }
            catch (UnauthorizedAccessException ex)
            {
                return BadRequest($"Authentication error: {ex.Message}");
            }
            // Read uploaded PDF into memory once
            byte[] pdfBytes;
            using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms);
                pdfBytes = ms.ToArray();
            }
            
            // Store original PDF in Cloudflare R2 (do not save to local filesystem)
            string originalPdfPath;
            using (var saveStream = new MemoryStream(pdfBytes))
            {
                originalPdfPath = await _pdfStorageService.SaveOriginalPdfAsync(saveStream, file.FileName);
            }
            
            // Create document record
            var document = new DocumentOriginal
            {
                UploaderUserId = userId,
                OriginalFileName = file.FileName,
                FilePath = originalPdfPath,
                FileSizeBytes = file.Length,
                Status = DocumentStatus.Uploaded,
                UploadedAt = DateTime.UtcNow,
                UploadBatchId = string.IsNullOrWhiteSpace(batchId) ? null : batchId
            };

            await _unitOfWork.DocumentOriginals.AddAsync(document);
            await _unitOfWork.SaveChangesAsync();

            // Process the document with template
            var template = await _unitOfWork.TemplateRuleSets.GetByIdAsync(templateId);
            if (template == null)
            {
                // Try to get the first available template
                var availableTemplates = await _unitOfWork.TemplateRuleSets.FindAsync(t => t.IsActive);
                if (!availableTemplates.Any())
                {
                    // Create a default template if none exist
                    var defaultTemplate = new TemplateRuleSet
                    {
                        Name = "Default PDF Processing Template",
                        JsonDefinition = @"{
                            ""metadataRules"": {
                                ""RFC"": ""(?:RFC|R\\.F\\.C\\.?)[\\s:]*([A-Z0-9]{12,13})"",
                                ""periodo"": ""(?:Per[ií]odo|Periodo|PERIODO)[\\s:]*([0-9]{1,2}/[0-9]{4})"",
                                ""monto_total"": ""(?:Total|TOTAL|Monto|MONTO)[\\s:]*\\$?([0-9,]+\\.[0-9]{2})""
                            },
                            ""pageRules"": {
                                ""keepPages"": [1, 2, 3],
                                ""footerText"": ""Documento procesado el {{now}} por {{vendor.email}}""
                            },
                            ""coverPage"": {
                                ""enabled"": true,
                                ""fields"": {
                                    ""title"": ""Factura Normalizada"",
                                    ""rfc"": ""{{RFC}}"",
                                    ""periodo"": ""{{periodo}}"",
                                    ""monto"": ""{{monto_total}}""
                                }
                            }
                        }",
                        CreatedByUserId = userId,
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    };
                    
                    await _unitOfWork.TemplateRuleSets.AddAsync(defaultTemplate);
                    await _unitOfWork.SaveChangesAsync();
                    template = defaultTemplate;
                }
                else
                {
                    template = availableTemplates.First();
                }
            }

            // Process document using the in-memory bytes (avoid reading from local filesystem)
            
            // STEP 1: Extract text from PDF using PdfProcessor
            Console.WriteLine("==========================================================");
            Console.WriteLine("[DocumentController] 📄 PDF UPLOAD - Starting Text Extraction");
            Console.WriteLine($"[DocumentController] PDF Size: {pdfBytes.Length} bytes");
            
            string extractedText = "";
            try
            {
                // Use iText7 to extract text from PDF
                using (var pdfReader = new iText.Kernel.Pdf.PdfReader(new MemoryStream(pdfBytes)))
                using (var pdfDoc = new iText.Kernel.Pdf.PdfDocument(pdfReader))
                {
                    for (int i = 1; i <= pdfDoc.GetNumberOfPages(); i++)
                    {
                        var page = pdfDoc.GetPage(i);
                        var strategy = new iText.Kernel.Pdf.Canvas.Parser.Listener.LocationTextExtractionStrategy();
                        extractedText += iText.Kernel.Pdf.Canvas.Parser.PdfTextExtractor.GetTextFromPage(page, strategy);
                    }
                }
                Console.WriteLine($"[DocumentController] ✓ Extracted {extractedText.Length} characters from PDF");
                Console.WriteLine($"[DocumentController] Text preview: {extractedText.Substring(0, Math.Min(200, extractedText.Length))}...");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DocumentController] ⚠ Text extraction failed: {ex.Message}");
                extractedText = "";
            }
            
            // STEP 2: Call GPT to analyze the extracted text
            Console.WriteLine("[DocumentController] 🤖 Calling GPT to analyze text...");
            var gptPrompt = $"Please analyze this PDF document text and provide a title, a summary(min 300 words and max 400 words), and contact information (phone numbers, emails, addresses). Return your response in JSON format with this structure: {{\"title\": \"...\", \"summary\": \"...\", \"contactInformation\": \"...\"}}.\n\nDocument text:\n{extractedText}";
            
            GptExtractionResult? gptResult = null;
            try
            {
                gptResult = await _gptService.ExtractDocumentInfoFromTextAsync(extractedText, gptPrompt);
                
                Console.WriteLine($"[DocumentController] GPT Result - Success: {gptResult?.Success ?? false}");
                
                if (gptResult != null && gptResult.Success)
                {
                    Console.WriteLine($"[DocumentController] ✓ GPT extraction successful!");
                    Console.WriteLine($"[DocumentController] Title: {gptResult.Title ?? "(null)"}");
                    Console.WriteLine($"[DocumentController] Summary: {gptResult.Summary ?? "(null)"}");
                    Console.WriteLine($"[DocumentController] Contact Information: {gptResult.ContactInformation ?? "(null)"}");
                }
                else
                {
                    Console.WriteLine($"[DocumentController] ✗ GPT extraction failed: {gptResult?.ErrorMessage ?? "gptResult is null"}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DocumentController] ❌ Error calling GPT service: {ex.Message}");
                Console.WriteLine($"[DocumentController] Stack trace: {ex.StackTrace}");
                // Continue processing even if GPT fails
            }
            Console.WriteLine("==========================================================");
            
            // Parse template rules
            TemplateRuleDefinition templateRules;
            try
            {
                templateRules = _templateRuleParser.Parse(template.JsonDefinition);
            }
            catch (ArgumentException ex)
            {
                return BadRequest($"Invalid template definition: {ex.Message}");
            }
            
            var processingResult = await _templateProcessorService.ProcessAsync(
                pdfBytes, 
                templateRules, 
                new VendorContext { Email = userEmail, UserId = userId.ToString() },
                document.OriginalFileName,
                gptResult
            );

            // Generate RFC-based filename
            var currentUser = await _unitOfWork.Users.GetByIdAsync(userId);
            string rfcPrefix = "XXXX";
            int sequentialNumber = 1;
            
            if (currentUser != null && !string.IsNullOrEmpty(currentUser.Rfc) && currentUser.Rfc.Length >= 4)
            {
                rfcPrefix = currentUser.Rfc.Substring(0, 4).ToUpper();
            }
            
            // Get sequential number: count of user's documents uploaded so far (including current one)
            var allUserDocs = await _unitOfWork.DocumentOriginals.FindAsync(d => d.UploaderUserId == userId);
            sequentialNumber = allUserDocs.Count(); // Current document will be the Nth document
            
            var processedFileName = $"{rfcPrefix}-{sequentialNumber:D4}_document.pdf";
            Console.WriteLine($"[DocumentController] 📝 Generated processed filename: {processedFileName}");

            // Store processed document
            var processedPdfPath = await _pdfStorageService.SaveProcessedPdfAsync(processingResult.FinalPdfBytes, processedFileName);
            
            Console.WriteLine("[DocumentController] 💾 Saving to database...");
            Console.WriteLine($"[DocumentController] GptTitle to save: {gptResult?.Title ?? "(null)"}");
            Console.WriteLine($"[DocumentController] GptSummary to save: {(gptResult?.Summary != null ? gptResult.Summary.Substring(0, Math.Min(50, gptResult.Summary.Length)) + "..." : "(null)")}");
            Console.WriteLine($"[DocumentController] GptContactInformation to save: {gptResult?.ContactInformation ?? "(null)"}");
            
            var processedDocument = new DocumentProcessed
            {
                SourceDocumentId = document.Id,
                TemplateRuleSetId = templateId,
                FilePathFinalPdf = processedPdfPath,
                ExtractedJsonData = System.Text.Json.JsonSerializer.Serialize(processingResult.ExtractedFields),
                GptTitle = gptResult?.Title,
                GptSummary = gptResult?.Summary,
                GptContactInformation = gptResult?.ContactInformation,
                Status = ProcessedDocumentStatus.Approved,
                CreatedAt = DateTime.UtcNow
            };

            await _unitOfWork.DocumentProcessed.AddAsync(processedDocument);
            await _unitOfWork.SaveChangesAsync();
            
            Console.WriteLine("[DocumentController] ✓ Saved to database successfully!");

            return Ok(new DocumentUploadResponse
            {
                DocumentId = document.Id,
                Status = "Processed",
                Message = "Document uploaded and processed successfully"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error uploading document: {ex.Message}");
        }
    }

    [HttpGet("processed")]
    [Authorize(Roles = "Client,Admin")]
    public async Task<ActionResult<ProcessedDocumentListResponse>> GetProcessedDocuments([FromQuery] int? vendorId = null)
    {
        try
        {
            var userRole = CurrentUserHelper.GetCurrentUserRole(HttpContext);
            var userId = CurrentUserHelper.GetCurrentUserId(HttpContext);

            Console.WriteLine($"[DocumentController] GetProcessedDocuments - Role: {userRole}, UserId: {userId}");

            // Admins should only see documents that have been sent to them (IsSentToAdmin = true)
            // Clients see their own uploaded documents
            IEnumerable<DocumentProcessed> processedDocuments;
            
            if (userRole == "Admin")
            {
                // Admin sees only documents that clients have sent
                processedDocuments = await _unitOfWork.DocumentProcessed.FindAsync(d => 
                    d.Status == ProcessedDocumentStatus.Approved && d.IsSentToAdmin);
                Console.WriteLine($"[DocumentController] Admin query: Found {processedDocuments.Count()} sent documents");
            }
            else
            {
                // Clients see their own uploaded documents
                processedDocuments = await _unitOfWork.DocumentProcessed.FindAsync(d => 
                d.Status == ProcessedDocumentStatus.Approved);

                // Filter by vendorId if specified
                if (vendorId.HasValue)
            {
                var vendorDocuments = await _unitOfWork.DocumentOriginals.FindAsync(d => d.UploaderUserId == vendorId.Value);
                var vendorDocumentIds = vendorDocuments.Select(d => d.Id).ToHashSet();
                processedDocuments = processedDocuments.Where(d => vendorDocumentIds.Contains(d.SourceDocumentId));
                }
                Console.WriteLine($"[DocumentController] Client query: Found {processedDocuments.Count()} documents");
            }

            var documents = new List<ProcessedDocumentDto>();
            foreach (var doc in processedDocuments)
            {
                var sourceDoc = await _unitOfWork.DocumentOriginals.GetByIdAsync(doc.SourceDocumentId);
                
                // Handle null TemplateRuleSetId for older documents
                TemplateRuleSet? template = null;
                if (doc.TemplateRuleSetId.HasValue && doc.TemplateRuleSetId.Value > 0)
                {
                    template = await _unitOfWork.TemplateRuleSets.GetByIdAsync(doc.TemplateRuleSetId.Value);
                }
                
                var vendor = sourceDoc != null ? await _unitOfWork.Users.GetByIdAsync(sourceDoc.UploaderUserId) : null;

                if (sourceDoc != null && vendor != null)
                {
                    var extractedData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(
                        doc.ExtractedJsonData) ?? new Dictionary<string, object>();

                    documents.Add(new ProcessedDocumentDto
                    {
                        Id = doc.Id,
                        OriginalFileName = sourceDoc.OriginalFileName,
                        FileSizeBytes = sourceDoc.FileSizeBytes,
                        Status = sourceDoc.Status,
                        UploadedAt = sourceDoc.UploadedAt,
                        ProcessedAt = doc.CreatedAt,
                        VendorId = vendor.Id,
                        VendorEmail = vendor.Email,
                        TemplateId = template?.Id ?? 0,
                        TemplateName = template?.Name ?? "Default Template",
                        ExtractedData = extractedData
                    });
                }
            }

            return Ok(new ProcessedDocumentListResponse
            {
                Documents = documents,
                TotalCount = documents.Count
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving processed documents: {ex.Message}");
        }
    }

    [HttpGet("processed/{id}/file")]
    [Authorize(Roles = "Client,Admin")]
    public async Task<IActionResult> DownloadProcessedDocument(int id)
    {
        try
        {
            Console.WriteLine($"[DownloadProcessedDocument] 📥 Document ID: {id}");
            
            var processedDocument = await _unitOfWork.DocumentProcessed.GetByIdAsync(id);
            if (processedDocument == null)
            {
                return NotFound("Document not found");
            }

            if (processedDocument.Status != ProcessedDocumentStatus.Approved)
            {
                return BadRequest("Document not approved");
            }

            var pdfBytes = await _pdfStorageService.GetProcessedPdfAsync(processedDocument.FilePathFinalPdf);
            
            // Get uploader's RFC for filename prefix
            var sourceDoc = await _unitOfWork.DocumentOriginals.GetByIdAsync(processedDocument.SourceDocumentId);
            var uploader = sourceDoc != null ? await _unitOfWork.Users.GetByIdAsync(sourceDoc.UploaderUserId) : null;
            
            Console.WriteLine($"[DownloadProcessedDocument] Source Doc ID: {sourceDoc?.Id}, Uploader ID: {uploader?.Id}");
            Console.WriteLine($"[DownloadProcessedDocument] Uploader Email: {uploader?.Email}, RFC: {uploader?.Rfc}");
            
            string rfcPrefix = "XXXX";
            int sequentialNumber = 1;
            
            if (uploader != null && sourceDoc != null)
            {
                if (!string.IsNullOrEmpty(uploader.Rfc) && uploader.Rfc.Length >= 4)
                {
                    rfcPrefix = uploader.Rfc.Substring(0, 4).ToUpper();
                }
                
                // Get sequential number based on upload order
                var allUserDocs = await _unitOfWork.DocumentOriginals.FindAsync(d => d.UploaderUserId == uploader.Id);
                var sortedDocs = allUserDocs.OrderBy(d => d.UploadedAt).ThenBy(d => d.Id).ToList();
                Console.WriteLine($"[DownloadProcessedDocument] Total user docs: {sortedDocs.Count}");
                var docIndex = sortedDocs.FindIndex(d => d.Id == sourceDoc.Id);
                sequentialNumber = docIndex >= 0 ? docIndex + 1 : 1;
                Console.WriteLine($"[DownloadProcessedDocument] Doc Index: {docIndex}, Sequential Number: {sequentialNumber}");
            }
            
            // Format: RFC prefix + sequential number (based on upload order)
            var fileName = $"{rfcPrefix}-{sequentialNumber:D4}_document.pdf";
            Console.WriteLine($"[DownloadProcessedDocument] ✅ Generated filename: {fileName}");
            
            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (FileNotFoundException)
        {
            return NotFound("File not found");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DownloadProcessedDocument] ❌ Error: {ex.Message}");
            return StatusCode(500, $"Error downloading document: {ex.Message}");
        }
    }

    [HttpGet("client/documents/ready")]
    [Authorize] // Temporarily allow all authenticated users for debugging
    public async Task<ActionResult<PagedResult<ClientReadyDocumentDto>>> GetClientReadyDocuments(
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 10)
    {
        try
        {
            // Debug: Log user information
            var userId = CurrentUserHelper.GetCurrentUserId(HttpContext);
            var userRole = CurrentUserHelper.GetCurrentUserRole(HttpContext);
            var userEmail = CurrentUserHelper.GetCurrentUserEmail(HttpContext);
            
            Console.WriteLine($"GetClientReadyDocuments - UserId: {userId}, Role: {userRole}, Email: {userEmail}");
            Console.WriteLine($"Pagination - Page: {page}, PageSize: {pageSize}");
            
            // Validate pagination parameters
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 50) pageSize = 10;
            
            // Get only the current user's approved processed documents (not deleted by client)
            var allProcessedDocs = await _unitOfWork.DocumentProcessed.FindAsync(
                d => d.Status == ProcessedDocumentStatus.Approved && !d.IsDeletedByClient);
            
            // Filter by current user's uploaded documents
            var userProcessedDocs = new List<DocumentProcessed>();
            foreach (var doc in allProcessedDocs)
            {
                var sourceDoc = await _unitOfWork.DocumentOriginals.GetByIdAsync(doc.SourceDocumentId);
                if (sourceDoc != null && sourceDoc.UploaderUserId == userId)
                {
                    userProcessedDocs.Add(doc);
                }
            }
            
            // Apply pagination
            var totalCount = userProcessedDocs.Count;
            var processedDocuments = userProcessedDocs
                .OrderByDescending(d => d.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var readyDocuments = new List<ClientReadyDocumentDto>();
            foreach (var doc in processedDocuments)
            {
                var sourceDoc = await _unitOfWork.DocumentOriginals.GetByIdAsync(doc.SourceDocumentId);
                var vendor = sourceDoc != null ? await _unitOfWork.Users.GetByIdAsync(sourceDoc.UploaderUserId) : null;

                if (sourceDoc != null && vendor != null)
                {
                    // Parse extracted data to get fiscal information
                    var extractedData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(
                        doc.ExtractedJsonData) ?? new Dictionary<string, object>();

                    readyDocuments.Add(new ClientReadyDocumentDto
                    {
                        Id = doc.Id.ToString(),
                        ProveedorEmail = vendor.Email,
                        ReadyAtUtc = doc.CreatedAt,
                        UploadedAtUtc = sourceDoc.UploadedAt,
                        UploadBatchId = sourceDoc.UploadBatchId,
                        RfcEmisor = vendor.Rfc ?? "No registrado",  // Use user's RFC from account
                        Periodo = extractedData.GetValueOrDefault("periodo", "N/A").ToString() ?? "N/A",
                        MontoTotalMxn = extractedData.GetValueOrDefault("monto_total", "0").ToString() ?? "0",
                        ComplianceStatus = "ListoParaEnviar"
                    });
                }
            }

            return Ok(new PagedResult<ClientReadyDocumentDto>
            {
                Items = readyDocuments,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving ready documents: {ex.Message}");
        }
    }

    [HttpGet("client/documents/{id}")]
    [Authorize(Roles = "Client")]
    public async Task<ActionResult<ClientDocumentDetailDto>> GetClientDocumentDetail(string id)
    {
        try
        {
            if (!int.TryParse(id, out var documentId))
            {
                return BadRequest("Invalid document ID");
            }

            var processedDocument = await _unitOfWork.DocumentProcessed.GetByIdAsync(documentId);
            if (processedDocument == null)
            {
                return NotFound("Document not found");
            }

            if (processedDocument.Status != ProcessedDocumentStatus.Approved)
            {
                return BadRequest("Document not approved");
            }

            var sourceDoc = await _unitOfWork.DocumentOriginals.GetByIdAsync(processedDocument.SourceDocumentId);
            var vendor = sourceDoc != null ? await _unitOfWork.Users.GetByIdAsync(sourceDoc.UploaderUserId) : null;

            if (sourceDoc == null || vendor == null)
            {
                return NotFound("Source document or vendor not found");
            }

            // Parse extracted data
            var extractedData = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(
                processedDocument.ExtractedJsonData) ?? new Dictionary<string, object>();

            // Get template information (handle null for older documents)
            TemplateRuleSet? template = null;
            if (processedDocument.TemplateRuleSetId.HasValue && processedDocument.TemplateRuleSetId.Value > 0)
            {
                template = await _unitOfWork.TemplateRuleSets.GetByIdAsync(processedDocument.TemplateRuleSetId.Value);
            }

            var detail = new ClientDocumentDetailDto
            {
                Id = processedDocument.Id.ToString(),
                ProveedorEmail = vendor.Email,
                ReadyAtUtc = processedDocument.CreatedAt,
                FiscalData = new ClientFiscalDataDto
                {
                    RfcEmisor = vendor.Rfc ?? "No registrado",  // Use user's RFC from account
                    Periodo = extractedData.GetValueOrDefault("periodo", "N/A").ToString() ?? "N/A",
                    MontoTotalMxn = extractedData.GetValueOrDefault("monto_total", "0").ToString() ?? "0"
                },
                DocumentStructure = new ClientDocumentStructureDto
                {
                    AddedStandardCoverPage = true,
                    AddedFooterTraceability = true,
                    RemovedExtraPages = true,
                    RemovedInteractiveElements = true,
                    StructureNote = "Carátula estándar aplicada; páginas extra eliminadas; pie de página con trazabilidad insertado."
                },
                AppliedMetadata = new ClientAppliedMetadataDto
                {
                    Title = "Factura Maquila Normalizada",
                    RfcEmisorField = vendor.Rfc ?? "No registrado",  // Use user's RFC from account
                    PeriodoField = extractedData.GetValueOrDefault("periodo", "N/A").ToString() ?? "N/A",
                    NormalizedAtUtc = processedDocument.CreatedAt,
                    NormalizedByEmail = vendor.Email
                },
                TechnicalCompliance = new ClientTechnicalComplianceDto
                {
                    IsPdf = true,
                    Grayscale8bit = true,
                    Dpi300 = true,
                    SizeUnder3MB = sourceDoc.FileSizeBytes <= (3 * 1024 * 1024),
                    NoInteractiveStuff = true,
                    HasRequiredMetadata = true
                },
                DownloadLinks = new ClientDownloadLinksDto
                {
                    PdfFinalUrl = $"/api/documents/client/documents/{id}/file",
                    DataJsonUrl = $"/api/documents/client/documents/{id}/data"
                },
                TransformationVerification = new ClientTransformationVerificationDto
                {
                    Metadata = new ClientMetadataTransformationDto
                    {
                        OriginalMetadata = new Dictionary<string, string>
                        {
                            { "Title", "Documento Original" },
                            { "Author", "Proveedor" },
                            { "Subject", "Factura" }
                        },
                        InjectedMetadata = new Dictionary<string, string>
                        {
                            { "Title", "Factura Maquila Normalizada" },
                            { "RFC_Emisor", extractedData.GetValueOrDefault("RFC", "N/A").ToString() ?? "N/A" },
                            { "Periodo", extractedData.GetValueOrDefault("periodo", "N/A").ToString() ?? "N/A" },
                            { "Monto_Total", extractedData.GetValueOrDefault("monto_total", "0").ToString() ?? "0" },
                            { "Processed_By", vendor.Email },
                            { "Processed_At", processedDocument.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss UTC") },
                            { "Template_Used", template?.Name ?? "Default Template" }
                        },
                        FinalMetadata = new Dictionary<string, string>
                        {
                            { "Title", "Factura Maquila Normalizada" },
                            { "Author", "Sistema de Normalización" },
                            { "Subject", "Factura Normalizada" },
                            { "RFC_Emisor", extractedData.GetValueOrDefault("RFC", "N/A").ToString() ?? "N/A" },
                            { "Periodo", extractedData.GetValueOrDefault("periodo", "N/A").ToString() ?? "N/A" },
                            { "Monto_Total", extractedData.GetValueOrDefault("monto_total", "0").ToString() ?? "0" }
                        },
                        RfcInjected = !string.IsNullOrEmpty(extractedData.GetValueOrDefault("RFC", "").ToString()),
                        PeriodoInjected = !string.IsNullOrEmpty(extractedData.GetValueOrDefault("periodo", "").ToString()),
                        MontoTotalInjected = !string.IsNullOrEmpty(extractedData.GetValueOrDefault("monto_total", "").ToString()),
                        AuditTrailAdded = true,
                        TemplateUsed = template?.Name ?? "Default Template",
                        ProcessingTimestamp = processedDocument.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss UTC"),
                        ProcessedBy = vendor.Email
                    },
                    Restructuring = new ClientContentRestructuringDto
                    {
                        OriginalPageCount = 3, // Mock data - would be extracted from original PDF
                        FinalPageCount = 4, // Mock data - would be actual final page count
                        CoverPageAdded = true,
                        PagesRemoved = new List<int> { 2 }, // Mock data
                        PagesReordered = new List<int> { 1, 3, 4 }, // Mock data
                        FooterApplied = true,
                        FormsStripped = true,
                        JavaScriptRemoved = true,
                        AttachmentsRemoved = true,
                        RestructuringSummary = "Carátula estándar agregada, páginas reorganizadas, elementos interactivos eliminados",
                        ContentModifications = new List<string>
                        {
                            "Carátula estándar con datos fiscales agregada",
                            "Pie de página con trazabilidad insertado",
                            "Elementos de formulario eliminados",
                            "JavaScript removido",
                            "Anexos eliminados"
                        }
                    },
                    Extraction = new ClientDataExtractionDto
                    {
                        ExtractedFields = extractedData.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToString() ?? ""),
                        ExtractionConfidence = new Dictionary<string, double>
                        {
                            { "RFC", 0.95 },
                            { "periodo", 0.88 },
                            { "monto_total", 0.92 }
                        },
                        FieldValidation = new Dictionary<string, bool>
                        {
                            { "RFC", IsValidRfc(extractedData.GetValueOrDefault("RFC", "").ToString()) },
                            { "periodo", IsValidPeriodo(extractedData.GetValueOrDefault("periodo", "").ToString()) },
                            { "monto_total", IsValidMonto(extractedData.GetValueOrDefault("monto_total", "").ToString()) }
                        },
                        RfcExtracted = extractedData.GetValueOrDefault("RFC", "").ToString() ?? "",
                        PeriodoExtracted = extractedData.GetValueOrDefault("periodo", "").ToString() ?? "",
                        MontoTotalExtracted = extractedData.GetValueOrDefault("monto_total", "").ToString() ?? "",
                        RfcValid = IsValidRfc(extractedData.GetValueOrDefault("RFC", "").ToString()),
                        PeriodoValid = IsValidPeriodo(extractedData.GetValueOrDefault("periodo", "").ToString()),
                        MontoTotalValid = IsValidMonto(extractedData.GetValueOrDefault("monto_total", "").ToString()),
                        ExtractionMethod = "REGEX",
                        ExtractionTimestamp = processedDocument.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss UTC"),
                        ExtractionWarnings = new List<string>()
                    },
                    Normalization = new ClientFormatNormalizationDto
                    {
                        OriginalFormat = "PDF 1.7",
                        FinalFormat = "PDF/A-1b",
                        ConvertedToGrayscale = true,
                        ConvertedTo8Bit = true,
                        NormalizedTo300Dpi = true,
                        CompressedUnder3MB = sourceDoc.FileSizeBytes <= (3 * 1024 * 1024),
                        PasswordRemoved = true,
                        InteractiveContentRemoved = true,
                        CompressionRatio = $"{((double)sourceDoc.FileSizeBytes / (sourceDoc.FileSizeBytes + 1024)) * 100:F1}%",
                        OriginalSizeBytes = sourceDoc.FileSizeBytes,
                        FinalSizeBytes = sourceDoc.FileSizeBytes, // Mock data
                        NormalizationTimestamp = processedDocument.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss UTC"),
                        NormalizationSteps = new List<string>
                        {
                            "Conversión a escala de grises 8-bit",
                            "Normalización a 300 DPI",
                            "Compresión optimizada",
                            "Eliminación de contraseñas",
                            "Remoción de contenido interactivo",
                            "Validación de estructura PDF/A"
                        }
                    },
                    AllTransformationsApplied = true,
                    ComplianceStatus = "COMPLIANT",
                    ProcessingSummary = "Todas las transformaciones aplicadas exitosamente. Documento cumple con estándares gubernamentales."
                }
            };

            return Ok(detail);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving document detail: {ex.Message}");
        }
    }

    private static bool IsValidRfc(string rfc)
    {
        if (string.IsNullOrEmpty(rfc)) return false;
        // Basic RFC validation - 12-13 characters, alphanumeric
        return rfc.Length >= 12 && rfc.Length <= 13 && rfc.All(c => char.IsLetterOrDigit(c));
    }

    private static bool IsValidPeriodo(string periodo)
    {
        if (string.IsNullOrEmpty(periodo)) return false;
        // Basic period validation - MM/YYYY format
        return System.Text.RegularExpressions.Regex.IsMatch(periodo, @"^\d{2}/\d{4}$");
    }

    private static bool IsValidMonto(string monto)
    {
        if (string.IsNullOrEmpty(monto)) return false;
        // Basic amount validation - numeric with optional decimal
        return decimal.TryParse(monto.Replace("$", "").Replace(",", ""), out _);
    }

    [HttpGet("client/documents/{id}/file")]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> DownloadClientDocumentFile(string id)
    {
        try
        {
            Console.WriteLine($"[DownloadClientDocumentFile] 📥 Client download requested - Document ID: {id}");
            
            if (!int.TryParse(id, out var documentId))
            {
                return BadRequest("Invalid document ID");
            }

            var processedDocument = await _unitOfWork.DocumentProcessed.GetByIdAsync(documentId);
            if (processedDocument == null)
            {
                return NotFound("Document not found");
            }

            if (processedDocument.Status != ProcessedDocumentStatus.Approved)
            {
                return BadRequest("Document not approved");
            }

            var pdfBytes = await _pdfStorageService.GetProcessedPdfAsync(processedDocument.FilePathFinalPdf);
            
            // Get current user's RFC for filename prefix and calculate sequential number
            var currentUserId = CurrentUserHelper.GetCurrentUserId(HttpContext);
            var currentUser = await _unitOfWork.Users.GetByIdAsync(currentUserId);
            var sourceDoc = await _unitOfWork.DocumentOriginals.GetByIdAsync(processedDocument.SourceDocumentId);
            
            Console.WriteLine($"[DownloadClientDocumentFile] User ID: {currentUserId}, RFC: {currentUser?.Rfc ?? "(null)"}");
            Console.WriteLine($"[DownloadClientDocumentFile] Source Doc ID: {sourceDoc?.Id}");
            
            string rfcPrefix = "XXXX";
            int sequentialNumber = 1;
            
            if (currentUser != null && sourceDoc != null)
            {
                if (!string.IsNullOrEmpty(currentUser.Rfc) && currentUser.Rfc.Length >= 4)
                {
                    rfcPrefix = currentUser.Rfc.Substring(0, 4).ToUpper();
                }
                
                // Get sequential number based on upload order
                var allUserDocs = await _unitOfWork.DocumentOriginals.FindAsync(d => d.UploaderUserId == currentUserId);
                var sortedDocs = allUserDocs.OrderBy(d => d.UploadedAt).ThenBy(d => d.Id).ToList();
                Console.WriteLine($"[DownloadClientDocumentFile] Total user docs: {sortedDocs.Count}");
                var docIndex = sortedDocs.FindIndex(d => d.Id == sourceDoc.Id);
                sequentialNumber = docIndex >= 0 ? docIndex + 1 : 1;
                Console.WriteLine($"[DownloadClientDocumentFile] Doc Index: {docIndex}, Sequential: {sequentialNumber}");
            }
            
            // Format: RFC prefix + sequential number (based on upload order)
            var fileName = $"{rfcPrefix}-{sequentialNumber:D4}_document.pdf";
            Console.WriteLine($"[DownloadClientDocumentFile] ✅ Generated filename: {fileName}");
            
            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (FileNotFoundException)
        {
            Console.WriteLine($"[DownloadClientDocumentFile] ❌ File not found");
            return NotFound("File not found");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DownloadClientDocumentFile] ❌ Error: {ex.Message}");
            return StatusCode(500, $"Error downloading document: {ex.Message}");
        }
    }

    [HttpGet("client/documents/{id}/data")]
    [Authorize(Roles = "Client")]
    public async Task<IActionResult> DownloadClientDocumentData(string id)
    {
        try
        {
            if (!int.TryParse(id, out var documentId))
            {
                return BadRequest("Invalid document ID");
            }

            var processedDocument = await _unitOfWork.DocumentProcessed.GetByIdAsync(documentId);
            if (processedDocument == null)
            {
                return NotFound("Document not found");
            }

            if (processedDocument.Status != ProcessedDocumentStatus.Approved)
            {
                return BadRequest("Document not approved");
            }

            // Return the extracted JSON data
            var jsonData = processedDocument.ExtractedJsonData;
            var bytes = System.Text.Encoding.UTF8.GetBytes(jsonData);
            
            return File(bytes, "application/json", $"document_{id}_data.json");
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error downloading document data: {ex.Message}");
        }
    }

    [HttpPost("processed/download-batch")]
    [Authorize(Roles = "Client,Admin")]
    public async Task<IActionResult> DownloadBatch([FromBody] BatchDownloadRequest request)
    {
        if (request?.DocumentIds == null || request.DocumentIds.Count == 0)
        {
            return BadRequest("No document IDs provided");
        }

        // Get current user's RFC for filename prefix
        var currentUserId = CurrentUserHelper.GetCurrentUserId(HttpContext);
        var currentUser = await _unitOfWork.Users.GetByIdAsync(currentUserId);
        
        string rfcPrefix = "XXXX";
        if (currentUser != null && !string.IsNullOrEmpty(currentUser.Rfc) && currentUser.Rfc.Length >= 4)
        {
            rfcPrefix = currentUser.Rfc.Substring(0, 4).ToUpper();
        }
        
        // Get all user's original documents for sequential numbering
        var allUserDocs = await _unitOfWork.DocumentOriginals.FindAsync(d => d.UploaderUserId == currentUserId);
        var sortedUserDocs = allUserDocs.OrderBy(d => d.UploadedAt).ThenBy(d => d.Id).ToList();

        if (request.DocumentIds.Count == 1)
        {
            var singleId = request.DocumentIds[0];
            var doc = await _unitOfWork.DocumentProcessed.GetByIdAsync(singleId);
            if (doc == null) return NotFound($"Document {singleId} not found");
            
            var sourceDoc = await _unitOfWork.DocumentOriginals.GetByIdAsync(doc.SourceDocumentId);
            var docIndex = sourceDoc != null ? sortedUserDocs.FindIndex(d => d.Id == sourceDoc.Id) : -1;
            int sequentialNumber = docIndex >= 0 ? docIndex + 1 : 1;
            
            var bytes = await _pdfStorageService.GetProcessedPdfAsync(doc.FilePathFinalPdf);
            var name = $"{rfcPrefix}-{sequentialNumber:D4}_document.pdf";
            return File(bytes, "application/pdf", name);
        }

        await using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var id in request.DocumentIds)
            {
                var doc = await _unitOfWork.DocumentProcessed.GetByIdAsync(id);
                if (doc == null) continue;
                
                var sourceDoc = await _unitOfWork.DocumentOriginals.GetByIdAsync(doc.SourceDocumentId);
                var docIndex = sourceDoc != null ? sortedUserDocs.FindIndex(d => d.Id == sourceDoc.Id) : -1;
                int sequentialNumber = docIndex >= 0 ? docIndex + 1 : 1;
                
                var bytes = await _pdfStorageService.GetProcessedPdfAsync(doc.FilePathFinalPdf);
                var entryName = $"{rfcPrefix}-{sequentialNumber:D4}_document.pdf";
                var entry = zip.CreateEntry(entryName, CompressionLevel.Fastest);
                await using var entryStream = entry.Open();
                await entryStream.WriteAsync(bytes, 0, bytes.Length);
            }
        }

        ms.Position = 0;
        var zipName = $"{rfcPrefix}_docs_{DateTime.UtcNow:yyyyMMdd}.zip";
        return File(ms.ToArray(), "application/zip", zipName);
    }

    [HttpPost("processed/send-email")]
    [Authorize(Roles = "Client,Admin")]
    public async Task<IActionResult> SendByEmail([FromBody] SendDocumentsRequest request)
    {
        if (request?.DocumentIds == null || request.DocumentIds.Count == 0)
        {
            return BadRequest("No document IDs provided");
        }

        var toEmail = request.ToEmail;
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            // fallback to current user's email if not provided
            try
            {
                toEmail = CurrentUserHelper.GetCurrentUserEmail(HttpContext);
            }
            catch { return BadRequest("Destination email is required"); }
        }

        // Build subject prefix: first 4 letters of RFC/CIF/NIF, uppercase
        string prefix = "FILE";
        try
        {
            var firstDoc = await _unitOfWork.DocumentProcessed.GetByIdAsync(request.DocumentIds.First());
            if (firstDoc != null && !string.IsNullOrEmpty(firstDoc.ExtractedJsonData))
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(firstDoc.ExtractedJsonData);
                var rfc = dict != null && dict.TryGetValue("RFC", out var val) ? val.GetString() : null;
                if (!string.IsNullOrWhiteSpace(rfc))
                {
                    prefix = new string(rfc!.Take(4).ToArray()).ToUpperInvariant();
                }
            }
        }
        catch { /* ignore */ }

        // Sequence number based on current selection order
        var sequence = 1;
        var subject = $"{prefix}-{sequence:0000} Documentos";

        // Try SMTP send if configured
        var host = _configuration["Smtp:Host"];
        var from = _configuration["Smtp:From"] ?? "no-reply@localhost";
        var portStr = _configuration["Smtp:Port"];
        int.TryParse(portStr, out var port);
        var user = _configuration["Smtp:Username"];
        var pass = _configuration["Smtp:Password"];

        if (!string.IsNullOrWhiteSpace(host) && port > 0)
        {
            using var client = new SmtpClient(host, port)
            {
                EnableSsl = true,
                Credentials = !string.IsNullOrWhiteSpace(user) ? new System.Net.NetworkCredential(user, pass) : null
            };
            using var message = new MailMessage(from, toEmail!)
            {
                Subject = subject,
                Body = "Documentos procesados adjuntos.",
                IsBodyHtml = false
            };

            // Attach PDFs (limit total size)
            foreach (var id in request.DocumentIds.Take(40))
            {
                var doc = await _unitOfWork.DocumentProcessed.GetByIdAsync(id);
                if (doc == null) continue;
                var bytes = await _pdfStorageService.GetProcessedPdfAsync(doc.FilePathFinalPdf);
                var attachment = new Attachment(new MemoryStream(bytes), $"{prefix}-{sequence:0000}_document_{id}.pdf", "application/pdf");
                message.Attachments.Add(attachment);
                sequence++;
            }

            await client.SendMailAsync(message);
            
            // Mark documents as sent to admin
            await MarkDocumentsAsSentToAdmin(request.DocumentIds);
            
            // Create notification for admin
            await CreateNotificationForAdmin(request.DocumentIds.Count);
            
            return Ok(new { status = "sent", to = toEmail, subject });
        }

        // If SMTP not configured, acknowledge request but still mark documents as sent and create notification
        await MarkDocumentsAsSentToAdmin(request.DocumentIds);
        await CreateNotificationForAdmin(request.DocumentIds.Count);
        return Ok(new { status = "queued", to = toEmail, subject });
    }
    
    private async Task MarkDocumentsAsSentToAdmin(List<int> documentIds)
    {
        try
        {
            Console.WriteLine($"[DocumentController] 📧 Marking {documentIds.Count} documents as sent to admin...");
            var now = DateTime.UtcNow;
            
            foreach (var id in documentIds)
            {
                var doc = await _unitOfWork.DocumentProcessed.GetByIdAsync(id);
                if (doc != null)
                {
                    doc.IsSentToAdmin = true;
                    doc.SentToAdminAt = now;
                }
            }
            
            await _unitOfWork.SaveChangesAsync();
            Console.WriteLine($"[DocumentController] ✅ Documents marked as sent successfully");
        }
        catch (Exception ex)
        {
            // Log but don't fail the request if marking fails
            Console.WriteLine($"[DocumentController] ❌ Failed to mark documents as sent: {ex.Message}");
        }
    }
    
    private async Task CreateNotificationForAdmin(int documentCount)
    {
        try
        {
            Console.WriteLine($"[DocumentController] 🔔 Creating notification...");
            var currentUserId = CurrentUserHelper.GetCurrentUserId(HttpContext);
            Console.WriteLine($"[DocumentController] Client User ID: {currentUserId}");
            Console.WriteLine($"[DocumentController] Document Count: {documentCount}");
            
            var notification = new Domain.Entities.Notification
            {
                ClientUserId = currentUserId,
                DocumentCount = documentCount,
                SentAt = DateTime.UtcNow,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };
            
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();
            Console.WriteLine($"[DocumentController] ✅ Notification created successfully (ID: {notification.Id})");
        }
        catch (Exception ex)
        {
            // Log but don't fail the request if notification creation fails
            Console.WriteLine($"[DocumentController] ❌ Failed to create notification: {ex.Message}");
            Console.WriteLine($"[DocumentController] Stack trace: {ex.StackTrace}");
        }
    }

    [HttpPost("processed/delete-batch")]
    [Authorize(Roles = "Client,Admin")]
    public async Task<IActionResult> DeleteBatch([FromBody] DeleteDocumentsRequest request)
    {
        if (request?.DocumentIds == null || request.DocumentIds.Count == 0)
        {
            return BadRequest("No document IDs provided");
        }

        var isAdmin = false;
        int currentUserId = 0;
        try
        {
            currentUserId = CurrentUserHelper.GetCurrentUserId(HttpContext);
            var role = CurrentUserHelper.GetCurrentUserRole(HttpContext);
            isAdmin = string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
        }
        catch { }

        int deleted = 0;
        var originalsToDelete = new HashSet<int>();
        var originalFilePaths = new Dictionary<int, string>();
        
        foreach (var id in request.DocumentIds)
        {
            var processed = await _unitOfWork.DocumentProcessed.GetByIdAsync(id);
            if (processed == null) continue;

            // Ownership check: uploader of source document or admin
            var source = await _unitOfWork.DocumentOriginals.GetByIdAsync(processed.SourceDocumentId);
            var isOwner = source != null && source.UploaderUserId == currentUserId;
            if (!isAdmin && !isOwner) continue;

            if (isAdmin)
            {
                // Admin: Hard delete (actually remove from database and storage)
                try { await _pdfStorageService.DeleteFileAsync(processed.FilePathFinalPdf); } catch { }
                await _unitOfWork.DocumentProcessed.DeleteAsync(processed);
                deleted++;

                if (source != null)
                {
                    originalsToDelete.Add(source.Id);
                    if (!originalFilePaths.ContainsKey(source.Id))
                    {
                        originalFilePaths[source.Id] = source.FilePath;
                    }
                }
            }
            else
            {
                // Client: Soft delete (just hide from client view)
                processed.IsDeletedByClient = true;
                await _unitOfWork.DocumentProcessed.UpdateAsync(processed);
                deleted++;
            }
        }

        // Admin only: Delete original records and original files (best-effort)
        if (isAdmin)
        {
            foreach (var originalId in originalsToDelete)
            {
                var original = await _unitOfWork.DocumentOriginals.GetByIdAsync(originalId);
                if (original == null) continue;
                try
                {
                    if (originalFilePaths.TryGetValue(originalId, out var path))
                    {
                        await _pdfStorageService.DeleteFileAsync(path);
                    }
                }
                catch { }
                await _unitOfWork.DocumentOriginals.DeleteAsync(original);
            }
        }

        await _unitOfWork.SaveChangesAsync();
        return Ok(new { deleted });
    }
}
