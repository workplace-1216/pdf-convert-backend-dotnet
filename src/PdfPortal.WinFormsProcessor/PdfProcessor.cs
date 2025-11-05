using PdfPortal.WinFormsProcessor.Core;
using PdfPortal.WinFormsProcessor.Models;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;

namespace PdfPortal.WinFormsProcessor;

/// <summary>
/// Main PDF processor using WinForms libraries for enhanced PDF processing
/// </summary>
public class PdfProcessor
{
    private readonly TextExtractor _textExtractor;
    private readonly FiscalDataParser _fiscalDataParser;

    public PdfProcessor()
    {
        _textExtractor = new TextExtractor();
        _fiscalDataParser = new FiscalDataParser();
    }

    /// <summary>
    /// Process a PDF file and extract fiscal data
    /// </summary>
    /// <param name="pdfBytes">PDF file bytes</param>
    /// <param name="fileName">Original file name</param>
    /// <param name="userEmail">Logged-in user's email (optional)</param>
    /// <param name="gptTitle">Title extracted by GPT (optional)</param>
    /// <param name="gptSummary">Summary extracted by GPT (optional)</param>
    /// <param name="gptContactInfo">Contact information extracted by GPT (optional)</param>
    /// <returns>Processing result with extracted data</returns>
    public PdfProcessingResult ProcessPdf(byte[] pdfBytes, string fileName, string userEmail = null, string gptTitle = null, string gptSummary = null, string gptContactInfo = null)
    {
        var result = new PdfProcessingResult
        {
            Success = false
        };

        try
        {
            // Validate input
            if (pdfBytes == null || pdfBytes.Length == 0)
            {
                result.ErrorMessage = "PDF file is empty";
                return result;
            }

            // Extract text from PDF
            Console.WriteLine($"[PdfProcessor] Extracting text from {fileName}...");
            result.ExtractedText = _textExtractor.ExtractText(pdfBytes);
            
            if (string.IsNullOrWhiteSpace(result.ExtractedText))
            {
                result.ErrorMessage = "No text could be extracted from PDF";
                return result;
            }

            Console.WriteLine($"[PdfProcessor] Extracted {result.ExtractedText.Length} characters of text");

            // Parse fiscal data
            Console.WriteLine($"[PdfProcessor] Parsing fiscal data...");
            result.FiscalData = _fiscalDataParser.ParseFiscalData(result.ExtractedText);

            // Calculate confidence score
            result.ConfidenceScore = _fiscalDataParser.CalculateConfidenceScore(result.FiscalData);
            Console.WriteLine($"[PdfProcessor] Confidence score: {result.ConfidenceScore}%");

            // Build extracted data dictionary
            result.ExtractedData = new Dictionary<string, object>
            {
                ["RFC"] = result.FiscalData.RfcEmisor ?? "N/A",
                ["periodo"] = result.FiscalData.Periodo ?? "N/A",
                ["monto_total"] = result.FiscalData.MontoTotal?.ToString("F2") ?? "0.00",
                ["nombre_emisor"] = result.FiscalData.NombreEmisor ?? "N/A",
                ["fecha_emision"] = result.FiscalData.FechaEmision ?? "N/A",
                ["tipo_comprobante"] = result.FiscalData.TipoComprobante ?? "Comprobante Fiscal",
                ["confidence_score"] = result.ConfidenceScore,
                ["page_count"] = _textExtractor.GetPageCount(pdfBytes)
            };

            // Process PDF: Add stamps, watermarks, and extracted data
            result.ProcessedPdfBytes = AddProcessingStamp(pdfBytes, result.FiscalData, result.ConfidenceScore, userEmail, gptTitle ?? "", gptSummary ?? "", gptContactInfo ?? "");

            result.Success = true;
            Console.WriteLine($"[PdfProcessor] Processing completed successfully");

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PdfProcessor] Error processing PDF: {ex.Message}");
            result.Success = false;
            result.ErrorMessage = $"Error processing PDF: {ex.Message}";
            return result;
        }
    }

    /// <summary>
    /// Process a PDF file asynchronously
    /// </summary>
    public Task<PdfProcessingResult> ProcessPdfAsync(byte[] pdfBytes, string fileName, string userEmail = null, string gptTitle = null, string gptSummary = null, string gptContactInfo = null)
    {
        return Task.Run(() => ProcessPdf(pdfBytes, fileName, userEmail, gptTitle, gptSummary, gptContactInfo));
    }

    /// <summary>
    /// Extract only text from PDF (lightweight operation)
    /// </summary>
    public string ExtractTextOnly(byte[] pdfBytes)
    {
        return _textExtractor.ExtractText(pdfBytes);
    }

    /// <summary>
    /// Get metadata about PDF without full processing
    /// </summary>
    public PdfMetadata GetMetadata(byte[] pdfBytes)
    {
        return new PdfMetadata
        {
            PageCount = _textExtractor.GetPageCount(pdfBytes),
            FileSize = pdfBytes.Length,
            HasText = !string.IsNullOrWhiteSpace(_textExtractor.ExtractText(pdfBytes))
        };
    }

    /// <summary>
    /// Create a professional form PDF with raw PDF syntax (no iText7 to avoid BouncyCastle)
    /// </summary>
    private byte[] AddProcessingStamp(byte[] originalPdfBytes, FiscalData fiscalData, int confidenceScore, string userEmail = null, string gptTitle = null, string gptSummary = null, string gptContactInfo = null)
    {
        Console.WriteLine("[PdfProcessor] Creating professional form PDF using raw PDF syntax");
        
        try
        {
            // Create a complete PDF with form content using raw PDF structure
            // Green color for "de valor a Vucem": RGB(165,204,85) = 0.647 0.8 0.333
            // Cyan color for box: RGB(100,199,205) = 0.392 0.78 0.804
            
            // Use provided email and GPT data
            var displayEmail = userEmail ?? "";
            var displayTitle = gptTitle ?? "";
            var displaySummary = gptSummary ?? "";
            var displayContactInfo = gptContactInfo ?? "";
            
            // Escape special characters for PDF content (will be done in EscapePdfString method)
            
            // Split title into multiple lines if too long (max 50 chars per line for font 26)
            var titleLines = SplitTextIntoLines(displayTitle, 30);
            
            // Split summary and contact info into lines if too long
            var summaryLines = SplitTextIntoLines(displaySummary, 70);
            var contactLines = SplitTextIntoLines(displayContactInfo, 60);
            
            // Extract email from contact info for separate positioning
            var emailMatch = System.Text.RegularExpressions.Regex.Match(displayContactInfo, @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}");
            var extractedEmail = emailMatch.Success ? emailMatch.Value : displayEmail;
            
            var pdfContent = @"%PDF-1.4
1 0 obj
<<
/Type /Catalog
/Pages 2 0 R
>>
endobj
2 0 obj
<<
/Type /Pages
/Kids [3 0 R]
/Count 1
>>
endobj
3 0 obj
<<
/Type /Page
/Parent 2 0 R
/MediaBox [0 0 595 842]
/Contents 4 0 R
/Resources <<
/Font <<
/F1 5 0 R
/F2 6 0 R
>>
>>
>>
endobj
4 0 obj
<<
/Length 3500
>>
 stream
 q
 0.766 0.643 -0.643 0.766 -50 700 cm
 0.392 0.78 0.804 rg
 -5 -30 m
 120 -30 l
 125.52 -30 130 -25.52 130 -20 c
 130 50 l
 130 55.52 125.52 60 120 60 c
 -5 60 l
 -10.52 60 -15 55.52 -15 50 c
 -15 -20 l
 -15 -25.52 -10.52 -30 -5 -30 c
 h
 f
 Q
 q
 0.766 0.643 -0.643 0.766 430 750 cm
 0.392 0.78 0.804 rg
 -5 -30 m
 225 -30 l
 230.52 -30 235 -25.52 235 -20 c
 235 35 l
 235 40.52 230.52 45 225 45 c
 -5 45 l
 -10.52 45 -15 40.52 -15 35 c
 -15 -20 l
 -15 -25.52 -10.52 -30 -5 -30 c
 h
 f
 Q
 q
 0.766 0.643 -0.643 0.766 445 685 cm
 0.929 0.188 0.537 rg
 -5 -30 m
 265 -30 l
 270.52 -30 275 -25.52 275 -20 c
 275 35 l
 275 40.52 270.52 45 265 45 c
 -5 45 l
 -10.52 45 -15 40.52 -15 35 c
 -15 -20 l
 -15 -25.52 -10.52 -30 -5 -30 c
 h
 f
 Q
 q
 0.766 0.643 -0.643 0.766 550 635 cm
 0.647 0.8 0.333 rg
 -5 -30 m
 105 -30 l
 110.52 -30 115 -25.52 115 -20 c
 115 35 l
 115 40.52 110.52 45 105 45 c
 -5 45 l
 -10.52 45 -15 40.52 -15 35 c
 -15 -20 l
 -15 -25.52 -10.52 -30 -5 -30 c
 h
 f
 Q
BT
/F1 24 Tf
70 730 Td";

            // Add title lines (multi-line support for long titles)
            if (titleLines.Count > 0)
            {
                pdfContent += "\n(" + EscapePdfString(titleLines[0]) + ") Tj";
                for (int i = 1; i < titleLines.Count; i++)
                {
                    pdfContent += "\n0 -28 Td\n(" + EscapePdfString(titleLines[i]) + ") Tj";
                }
            }
            else
            {
                pdfContent += "\n(Untitled) Tj";
            }

            // Add summary section with improved spacing
            pdfContent += "\n0 0 0 rg\n/F2 14 Tf\n0 -50 Td";
            
            var currentY = -50;
            if (summaryLines.Count > 0)
            {
                foreach (var line in summaryLines)
                {
                    pdfContent += "\n(" + EscapePdfString(line) + ") Tj\n0 -18 Td";
                    currentY -= 18;
                }
            }
            else
            {
                pdfContent += "\n(No hay resumen disponible.) Tj\n0 -18 Td";
            }

            pdfContent += "\nET\n";
            pdfContent += @"q
0.7 0.7 0.7 RG
1 w
30 75 m
565 75 l
S
Q
BT
0 0 0 rg
/F2 12 Tf
405 90 Td";

            // Add contact information in footer
            if (contactLines.Count > 0)
            {
                // Filter out email from contact lines since it will be positioned separately
                var contactLinesWithoutEmail = contactLines
                    .Where(line => !line.Contains("@"))
                    .ToList();
                
                foreach (var line in contactLinesWithoutEmail)
                {
                    pdfContent += "\n(" + EscapePdfString(line) + ") Tj\n0 -16 Td";
                }
            }
            
            pdfContent += @"
ET
BT
0 0 0 rg
/F2 11 Tf
405 50 Td
( ) Tj
/F2 10 Tf
405 35 Td
(" + EscapePdfString(extractedEmail) + @") Tj
ET
q
0.145 0.827 0.4 rg
50 28 m
52.761 28 55 30.239 55 33 c
55 35.761 52.761 38 50 38 c
47.239 38 45 35.761 45 33 c
45 30.239 47.239 28 50 28 c
h
f
1 1 1 rg
1 1 1 RG
0.5 w
51.5 34.8 m
51.4 34.7 50.8 34.4 50.7 34.3 c
50.6 34.2 50.5 34.2 50.4 34.3 c
50.3 34.4 50.1 34.6 50 34.7 c
49.9 34.8 49.8 34.9 49.7 34.8 c
49.6 34.7 49.2 34.6 48.8 34.2 c
48.4 33.8 48.2 33.4 48.1 33.3 c
48 33.2 48.1 33.1 48.2 33 c
48.3 32.9 48.4 32.8 48.5 32.7 c
48.6 32.6 48.6 32.5 48.7 32.4 c
48.8 32.3 48.7 32.2 48.7 32.1 c
48.6 32 48.3 31.4 48.2 31.1 c
48.1 30.8 48 30.8 47.9 30.8 c
47.8 30.8 47.7 30.8 47.6 30.8 c
47.5 30.8 47.4 30.9 47.3 31 c
47.2 31.1 46.9 31.4 46.9 32 c
46.9 32.6 47.3 33.2 47.4 33.3 c
47.5 33.4 48.3 34.7 49.5 35.2 c
49.7 35.3 49.9 35.4 50.1 35.5 c
50.3 35.6 50.5 35.5 50.7 35.5 c
50.9 35.4 51.3 35.1 51.4 34.8 c
51.5 34.5 51.5 34.3 51.5 34.2 c
51.4 34.1 51.3 34.1 51.2 34 c
h
S
46 33 m
46 34.5 46.5 35.9 47.4 37 c
47.4 37.1 47.2 37.7 47 38.2 c
47.6 38 48.2 37.8 48.7 37.4 c
49.1 37.5 49.5 37.5 50 37.5 c
52.2 37.5 54 35.7 54 33.5 c
54 31.3 52.2 29.5 50 29.5 c
47.8 29.5 46 31.3 46 33.5 c
h
S
Q
BT
0 0 0 rg
/F1 13 Tf
62 30 Td
0.165 0.639 0.376 rg
(6643864700) Tj
ET
endstream
endobj
5 0 obj
<<
/Type /Font
/Subtype /Type1
/BaseFont /Helvetica-Bold
>>
endobj
6 0 obj
<<
/Type /Font
/Subtype /Type1
/BaseFont /Helvetica
>>
endobj
xref
0 7
0000000000 65535 f 
0000000009 00000 n 
0000000058 00000 n 
0000000115 00000 n 
0000000261 00000 n 
0000003816 00000 n 
0000003907 00000 n 
trailer
<<
/Size 7
/Root 1 0 R
>>
startxref
3993
%%EOF";
            
            var bytes = System.Text.Encoding.UTF8.GetBytes(pdfContent);
            Console.WriteLine($"[PdfProcessor] ✓ Professional form PDF created successfully ({bytes.Length} bytes)");
            
            return bytes;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[PdfProcessor] ✗ Error creating form PDF: {ex.Message}");
            
            // Fallback to original
            return originalPdfBytes;
        }
    }

    /// <summary>
    /// Split text into lines with maximum character length
    /// </summary>
    private List<string> SplitTextIntoLines(string text, int maxCharsPerLine)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new List<string>();
        }

        var lines = new List<string>();
        var words = text.Split(' ');
        var currentLine = "";

        foreach (var word in words)
        {
            if ((currentLine + " " + word).Length <= maxCharsPerLine)
            {
                currentLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
            }
            else
            {
                if (!string.IsNullOrEmpty(currentLine))
                {
                    lines.Add(currentLine);
                }
                currentLine = word;
            }
        }

        if (!string.IsNullOrEmpty(currentLine))
        {
            lines.Add(currentLine);
        }

        return lines.Count > 0 ? lines : new List<string> { text };
    }

    /// <summary>
    /// Escape special characters for PDF string content
    /// </summary>
    private string EscapePdfString(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "";
        }
        
        return text
            .Replace("\\", "\\\\")
            .Replace("(", "\\(")
            .Replace(")", "\\)")
            .Replace("\r", "")
            .Replace("\n", " ");
    }
}

/// <summary>
/// PDF metadata information
/// </summary>
public class PdfMetadata
{
    public int PageCount { get; set; }
    public long FileSize { get; set; }
    public bool HasText { get; set; }
}

