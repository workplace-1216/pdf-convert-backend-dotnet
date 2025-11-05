namespace PdfPortal.Application.Interfaces;

public interface IPdfProcessingService
{
    Task<string> ProcessPdfAsync(string inputPdfPath, string templateJsonDefinition);
    Task<Dictionary<string, object>> ExtractDataFromPdfAsync(string pdfPath);
    Task<bool> ValidatePdfAsync(string pdfPath);
}
