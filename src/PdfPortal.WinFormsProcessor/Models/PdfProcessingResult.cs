namespace PdfPortal.WinFormsProcessor.Models;

/// <summary>
/// Result of PDF processing operation
/// </summary>
public class PdfProcessingResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object> ExtractedData { get; set; } = new();
    public string? ExtractedText { get; set; }
    public byte[]? ProcessedPdfBytes { get; set; }
    
    /// <summary>
    /// Confidence score for extracted data (0-100)
    /// </summary>
    public int ConfidenceScore { get; set; }
    
    /// <summary>
    /// Specific fields extracted
    /// </summary>
    public FiscalData? FiscalData { get; set; }
}

/// <summary>
/// Fiscal data extracted from PDF
/// </summary>
public class FiscalData
{
    public string? RfcEmisor { get; set; }
    public string? Periodo { get; set; }
    public decimal? MontoTotal { get; set; }
    public string? NombreEmisor { get; set; }
    public string? FechaEmision { get; set; }
    public string? TipoComprobante { get; set; }
}

