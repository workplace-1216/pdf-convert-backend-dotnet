namespace PdfPortal.Application.Interfaces;

public interface IPdfStorageService
{
    Task<string> SaveOriginalPdfAsync(Stream pdfStream, string fileName);
    Task<string> SaveProcessedPdfAsync(byte[] pdfBytes, string fileName);
    Task<byte[]> GetProcessedPdfAsync(string filePath);
    Task<bool> DeleteFileAsync(string filePath);
    Task<string> GetTempFilePathAsync(string tempId);
}
