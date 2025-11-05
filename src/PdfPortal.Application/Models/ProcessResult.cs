namespace PdfPortal.Application.Models;

public class ProcessResult
{
    public byte[] FinalPdfBytes { get; set; } = Array.Empty<byte>();
    public Dictionary<string, string> ExtractedFields { get; set; } = new();
}
