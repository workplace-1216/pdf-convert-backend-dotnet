namespace PdfPortal.Application.Models;

public class GptExtractionResult
{
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string ContactInformation { get; set; } = string.Empty;
    public string ExtractedText { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

