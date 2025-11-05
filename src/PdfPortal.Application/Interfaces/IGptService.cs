using PdfPortal.Application.Models;

namespace PdfPortal.Application.Interfaces;

public interface IGptService
{
    Task<GptExtractionResult> ExtractDocumentInfoFromTextAsync(string extractedText, string prompt);
    Task<GptExtractionResult> ExtractDocumentInfoAsync(byte[] pdfBytes, string prompt);
}

