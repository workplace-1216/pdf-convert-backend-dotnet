using PdfPortal.WinFormsProcessor.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace PdfPortal.WinFormsProcessor.Core;

/// <summary>
/// Parses fiscal data from extracted PDF text
/// </summary>
public class FiscalDataParser
{
    // RFC patterns - Mexican Tax ID (12-13 characters)
    private static readonly Regex RfcPattern = new Regex(
        @"\b([A-ZÑ&]{3,4}\d{6}[A-Z0-9]{3})\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Period patterns (various formats)
    private static readonly Regex PeriodoPatterns = new Regex(
        @"(?:periodo|period|mes|month|fecha)[\s:]*(\d{1,2}[-/]\d{1,2}[-/]\d{2,4}|\d{4}[-/]\d{1,2}[-/]\d{1,2}|(?:enero|febrero|marzo|abril|mayo|junio|julio|agosto|septiembre|octubre|noviembre|diciembre)\s+\d{4}|Q[1-4]\s+\d{4})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Amount patterns (various currency formats)
    private static readonly Regex MontoPatterns = new Regex(
        @"(?:total|monto|amount|suma|importe|subtotal)[\s:]*\$?\s*([0-9]{1,3}(?:[,\s]?[0-9]{3})*(?:\.[0-9]{2})?)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Fecha emision patterns
    private static readonly Regex FechaEmisionPatterns = new Regex(
        @"(?:fecha\s+de\s+emisi[oó]n|fecha\s+emisi[oó]n|emission\s+date)[\s:]*(\d{1,2}[-/]\d{1,2}[-/]\d{2,4})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Parse fiscal data from extracted text
    /// </summary>
    public FiscalData ParseFiscalData(string extractedText)
    {
        var fiscalData = new FiscalData();

        // Extract RFC
        var rfcMatch = RfcPattern.Match(extractedText);
        if (rfcMatch.Success)
        {
            fiscalData.RfcEmisor = rfcMatch.Groups[1].Value.ToUpper();
        }

        // Extract Periodo
        var periodoMatch = PeriodoPatterns.Match(extractedText);
        if (periodoMatch.Success)
        {
            fiscalData.Periodo = NormalizePeriod(periodoMatch.Groups[1].Value);
        }

        // Extract Monto Total
        var montoMatch = MontoPatterns.Match(extractedText);
        if (montoMatch.Success)
        {
            var montoStr = montoMatch.Groups[1].Value.Replace(",", "").Replace(" ", "");
            if (decimal.TryParse(montoStr, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var monto))
            {
                fiscalData.MontoTotal = monto;
            }
        }

        // Extract Fecha Emision
        var fechaMatch = FechaEmisionPatterns.Match(extractedText);
        if (fechaMatch.Success)
        {
            fiscalData.FechaEmision = NormalizeDate(fechaMatch.Groups[1].Value);
        }

        // Extract Nombre Emisor (usually near RFC)
        if (!string.IsNullOrEmpty(fiscalData.RfcEmisor))
        {
            fiscalData.NombreEmisor = ExtractNombreEmisor(extractedText, fiscalData.RfcEmisor);
        }

        // Detect Tipo Comprobante
        fiscalData.TipoComprobante = DetectTipoComprobante(extractedText);

        return fiscalData;
    }

    /// <summary>
    /// Calculate confidence score based on extracted data
    /// </summary>
    public int CalculateConfidenceScore(FiscalData fiscalData)
    {
        int score = 0;
        int maxScore = 0;

        // RFC (30 points)
        maxScore += 30;
        if (!string.IsNullOrEmpty(fiscalData.RfcEmisor) && IsValidRfc(fiscalData.RfcEmisor))
        {
            score += 30;
        }

        // Periodo (25 points)
        maxScore += 25;
        if (!string.IsNullOrEmpty(fiscalData.Periodo))
        {
            score += 25;
        }

        // Monto Total (25 points)
        maxScore += 25;
        if (fiscalData.MontoTotal.HasValue && fiscalData.MontoTotal > 0)
        {
            score += 25;
        }

        // Fecha Emision (10 points)
        maxScore += 10;
        if (!string.IsNullOrEmpty(fiscalData.FechaEmision))
        {
            score += 10;
        }

        // Nombre Emisor (10 points)
        maxScore += 10;
        if (!string.IsNullOrEmpty(fiscalData.NombreEmisor))
        {
            score += 10;
        }

        return maxScore > 0 ? (int)((double)score / maxScore * 100) : 0;
    }

    private string NormalizePeriod(string period)
    {
        // Try to parse and normalize to YYYY-MM format
        period = period.Trim();
        
        // If it's already in a date format, extract year and month
        var dateMatch = Regex.Match(period, @"(\d{1,2})[-/](\d{1,2})[-/](\d{2,4})");
        if (dateMatch.Success)
        {
            var day = dateMatch.Groups[1].Value;
            var month = dateMatch.Groups[2].Value;
            var year = dateMatch.Groups[3].Value;
            
            if (year.Length == 2)
            {
                year = "20" + year;
            }
            
            return $"{year}-{month.PadLeft(2, '0')}";
        }

        // If it's in month name format
        var monthNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {"enero", "01"}, {"febrero", "02"}, {"marzo", "03"}, {"abril", "04"},
            {"mayo", "05"}, {"junio", "06"}, {"julio", "07"}, {"agosto", "08"},
            {"septiembre", "09"}, {"octubre", "10"}, {"noviembre", "11"}, {"diciembre", "12"}
        };

        foreach (var kvp in monthNames)
        {
            if (period.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
            {
                var yearMatch = Regex.Match(period, @"\d{4}");
                if (yearMatch.Success)
                {
                    return $"{yearMatch.Value}-{kvp.Value}";
                }
            }
        }

        return period;
    }

    private string NormalizeDate(string date)
    {
        // Normalize to YYYY-MM-DD format
        var dateMatch = Regex.Match(date, @"(\d{1,2})[-/](\d{1,2})[-/](\d{2,4})");
        if (dateMatch.Success)
        {
            var day = dateMatch.Groups[1].Value.PadLeft(2, '0');
            var month = dateMatch.Groups[2].Value.PadLeft(2, '0');
            var year = dateMatch.Groups[3].Value;
            
            if (year.Length == 2)
            {
                year = "20" + year;
            }
            
            return $"{year}-{month}-{day}";
        }

        return date;
    }

    private string? ExtractNombreEmisor(string text, string rfc)
    {
        // Look for text near the RFC
        var rfcIndex = text.IndexOf(rfc, StringComparison.OrdinalIgnoreCase);
        if (rfcIndex < 0) return null;

        // Get surrounding text (500 characters before and after)
        var start = Math.Max(0, rfcIndex - 500);
        var length = Math.Min(1000, text.Length - start);
        var surroundingText = text.Substring(start, length);

        // Look for name patterns (capitalized words before RFC)
        var namePattern = new Regex(@"([A-ZÁÉÍÓÚÑ][a-záéíóúñ]+(?:\s+[A-ZÁÉÍÓÚÑ][a-záéíóúñ]+){1,5})\s+" + Regex.Escape(rfc));
        var match = namePattern.Match(surroundingText);
        
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }

        return null;
    }

    private string DetectTipoComprobante(string text)
    {
        var lowerText = text.ToLower();
        
        if (lowerText.Contains("factura"))
            return "Factura";
        if (lowerText.Contains("nota de crédito") || lowerText.Contains("credit note"))
            return "Nota de Crédito";
        if (lowerText.Contains("nota de débito") || lowerText.Contains("debit note"))
            return "Nota de Débito";
        if (lowerText.Contains("recibo"))
            return "Recibo";
        if (lowerText.Contains("comprobante de pago"))
            return "Comprobante de Pago";
        
        return "Comprobante Fiscal";
    }

    private bool IsValidRfc(string rfc)
    {
        // Basic RFC validation
        if (string.IsNullOrEmpty(rfc)) return false;
        if (rfc.Length < 12 || rfc.Length > 13) return false;
        
        // Should start with 3-4 letters
        if (!Regex.IsMatch(rfc.Substring(0, Math.Min(4, rfc.Length)), @"^[A-ZÑ&]{3,4}$"))
            return false;
        
        return true;
    }
}

