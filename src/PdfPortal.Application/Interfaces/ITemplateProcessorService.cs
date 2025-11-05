using PdfPortal.Application.Models;

namespace PdfPortal.Application.Interfaces;

public interface ITemplateProcessorService
{
    Task<ProcessResult> ProcessAsync(byte[] originalPdfBytes, TemplateRuleDefinition rules, VendorContext vendor, string? documentTitle = null, GptExtractionResult? gptData = null);
    Task<Dictionary<string, object>> ExtractDataFromPdfAsync(Stream pdfStream, string templateJsonDefinition);
    Task<byte[]> GenerateStandardizedPdfAsync(Stream originalPdfStream, string templateJsonDefinition, Dictionary<string, object> extractedData);
    Task<bool> ValidateTemplateAsync(string templateJsonDefinition);
}
