using Microsoft.AspNetCore.Hosting;
using PdfPortal.Application.Interfaces;
using PdfPortal.Application.Models;
using PdfPortal.WinFormsProcessor;
using System.Text.Json;

namespace PdfPortal.Infrastructure.Services;

public class PdfProcessingService : IPdfProcessingService
{
    private readonly IWebHostEnvironment _environment;
    private readonly PdfProcessor _winFormsProcessor;

    public PdfProcessingService(IWebHostEnvironment environment)
    {
        _environment = environment;
        _winFormsProcessor = new PdfProcessor();
        
        Console.WriteLine("[PdfProcessingService] Initialized with WinForms PDF Processor");
    }

    public async Task<string> ProcessPdfAsync(string inputPdfPath, string templateJsonDefinition)
    {
        try
        {
            Console.WriteLine($"[PdfProcessingService] Processing PDF: {inputPdfPath}");
            
            // Read PDF bytes
            var pdfBytes = await File.ReadAllBytesAsync(inputPdfPath);
            var fileName = Path.GetFileName(inputPdfPath);
            
            // Process using WinForms processor
            var result = await _winFormsProcessor.ProcessPdfAsync(pdfBytes, fileName);
            
            if (!result.Success)
            {
                Console.WriteLine($"[PdfProcessingService] Processing failed: {result.ErrorMessage}");
                throw new Exception($"PDF processing failed: {result.ErrorMessage}");
            }

            Console.WriteLine($"[PdfProcessingService] Processing successful. Confidence: {result.ConfidenceScore}%");
            
            // Save processed PDF
            var outputPath = Path.Combine(
                _environment.ContentRootPath, 
                "storage", 
                "processed", 
                $"{Guid.NewGuid()}.pdf"
            );
            
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            
            if (result.ProcessedPdfBytes != null)
            {
                await File.WriteAllBytesAsync(outputPath, result.ProcessedPdfBytes);
            }

            return outputPath;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PdfProcessingService] Error: {ex.Message}");
            throw;
        }
    }

    public async Task<Dictionary<string, object>> ExtractDataFromPdfAsync(string pdfPath)
    {
        try
        {
            Console.WriteLine($"[PdfProcessingService] Extracting data from: {pdfPath}");
            
            // Read PDF bytes
            var pdfBytes = await File.ReadAllBytesAsync(pdfPath);
            var fileName = Path.GetFileName(pdfPath);
            
            // Process using WinForms processor
            var result = await _winFormsProcessor.ProcessPdfAsync(pdfBytes, fileName);
            
            if (!result.Success)
            {
                Console.WriteLine($"[PdfProcessingService] Extraction failed: {result.ErrorMessage}");
                // Return minimal data if processing fails
                return new Dictionary<string, object>
                {
                    ["RFC"] = "Error en procesamiento",
                    ["periodo"] = "Error en procesamiento",
                    ["monto_total"] = "0.00",
                    ["error"] = result.ErrorMessage ?? "Unknown error",
                    ["success"] = false
                };
            }

            Console.WriteLine($"[PdfProcessingService] Extraction successful");
            Console.WriteLine($"  RFC: {result.ExtractedData.GetValueOrDefault("RFC", "N/A")}");
            Console.WriteLine($"  Periodo: {result.ExtractedData.GetValueOrDefault("periodo", "N/A")}");
            Console.WriteLine($"  Monto: {result.ExtractedData.GetValueOrDefault("monto_total", "N/A")}");
            
            // Return extracted data
            var extractedData = new Dictionary<string, object>(result.ExtractedData)
            {
                ["success"] = true,
                ["extractedAt"] = DateTime.UtcNow,
                ["fileName"] = fileName
            };

            return extractedData;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PdfProcessingService] Error extracting data: {ex.Message}");
            return new Dictionary<string, object>
            {
                ["RFC"] = "Error en procesamiento",
                ["periodo"] = "Error en procesamiento",
                ["monto_total"] = "0.00",
                ["error"] = ex.Message,
                ["success"] = false
            };
        }
    }

    public async Task<bool> ValidatePdfAsync(string pdfPath)
    {
        try
        {
            Console.WriteLine($"[PdfProcessingService] Validating PDF: {pdfPath}");
            
            if (!File.Exists(pdfPath))
            {
                Console.WriteLine($"[PdfProcessingService] File not found");
                return false;
            }

            var pdfBytes = await File.ReadAllBytesAsync(pdfPath);
            
            if (pdfBytes.Length == 0)
            {
                Console.WriteLine($"[PdfProcessingService] File is empty");
                return false;
            }

            // Use WinForms processor to validate
            var metadata = _winFormsProcessor.GetMetadata(pdfBytes);
            var isValid = metadata.PageCount > 0;
            
            Console.WriteLine($"[PdfProcessingService] Validation result: {isValid} (Pages: {metadata.PageCount})");
            
            return isValid;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PdfProcessingService] Validation error: {ex.Message}");
            return false;
        }
    }
}
