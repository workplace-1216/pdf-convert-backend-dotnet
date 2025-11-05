using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using System.Text;

namespace PdfPortal.WinFormsProcessor.Core;

/// <summary>
/// Extracts text from PDF files using iText7
/// </summary>
public class TextExtractor
{
    /// <summary>
    /// Extract all text from a PDF file
    /// </summary>
    public string ExtractText(byte[] pdfBytes)
    {
        try
        {
            using var memoryStream = new MemoryStream(pdfBytes);
            using var pdfReader = new PdfReader(memoryStream);
            using var pdfDocument = new PdfDocument(pdfReader);

            var textBuilder = new StringBuilder();
            var numberOfPages = pdfDocument.GetNumberOfPages();

            for (int i = 1; i <= numberOfPages; i++)
            {
                var page = pdfDocument.GetPage(i);
                var strategy = new LocationTextExtractionStrategy();
                var pageText = PdfTextExtractor.GetTextFromPage(page, strategy);
                
                textBuilder.AppendLine(pageText);
                textBuilder.AppendLine("---PAGE-BREAK---");
            }

            return textBuilder.ToString();
        }
        catch (Exception ex)
        {
            throw new Exception($"Error extracting text from PDF: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Extract text from a specific page
    /// </summary>
    public string ExtractTextFromPage(byte[] pdfBytes, int pageNumber)
    {
        try
        {
            using var memoryStream = new MemoryStream(pdfBytes);
            using var pdfReader = new PdfReader(memoryStream);
            using var pdfDocument = new PdfDocument(pdfReader);

            if (pageNumber < 1 || pageNumber > pdfDocument.GetNumberOfPages())
            {
                throw new ArgumentException($"Invalid page number: {pageNumber}");
            }

            var page = pdfDocument.GetPage(pageNumber);
            var strategy = new LocationTextExtractionStrategy();
            return PdfTextExtractor.GetTextFromPage(page, strategy);
        }
        catch (Exception ex)
        {
            throw new Exception($"Error extracting text from page {pageNumber}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Get total number of pages in PDF
    /// </summary>
    public int GetPageCount(byte[] pdfBytes)
    {
        try
        {
            using var memoryStream = new MemoryStream(pdfBytes);
            using var pdfReader = new PdfReader(memoryStream);
            using var pdfDocument = new PdfDocument(pdfReader);
            
            return pdfDocument.GetNumberOfPages();
        }
        catch (Exception ex)
        {
            throw new Exception($"Error getting page count: {ex.Message}", ex);
        }
    }
}

