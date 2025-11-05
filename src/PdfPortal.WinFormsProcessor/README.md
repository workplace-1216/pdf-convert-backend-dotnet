# PdfPortal.WinFormsProcessor

## Overview
This is a WinForms-based PDF processing library that provides enhanced PDF text extraction and fiscal data parsing capabilities for the PdfPortal application.

## Features

### 1. **PDF Text Extraction**
- Uses iText7 library for robust text extraction
- Supports multi-page PDFs
- Location-based text extraction strategy for accurate parsing

### 2. **Fiscal Data Parsing**
- **RFC (Tax ID)**: Extracts Mexican RFC format (12-13 characters)
- **Periodo (Period)**: Extracts dates, months, quarters
- **Monto Total (Total Amount)**: Parses currency amounts in various formats
- **Additional Data**:
  - Nombre Emisor (Issuer Name)
  - Fecha Emisión (Emission Date)
  - Tipo Comprobante (Document Type)

### 3. **Confidence Scoring**
- Calculates confidence score (0-100%) based on:
  - RFC validity (30 points)
  - Periodo presence (25 points)
  - Monto Total presence (25 points)
  - Fecha Emisión presence (10 points)
  - Nombre Emisor presence (10 points)

## Architecture

```
PdfProcessor (Main Entry Point)
├── TextExtractor (iText7-based extraction)
│   ├── ExtractText()
│   ├── ExtractTextFromPage()
│   └── GetPageCount()
└── FiscalDataParser (Pattern matching & parsing)
    ├── ParseFiscalData()
    ├── CalculateConfidenceScore()
    └── Various regex patterns for data extraction
```

## Usage

### Basic Processing
```csharp
var processor = new PdfProcessor();
var pdfBytes = File.ReadAllBytes("document.pdf");
var result = await processor.ProcessPdfAsync(pdfBytes, "document.pdf");

if (result.Success)
{
    Console.WriteLine($"RFC: {result.FiscalData.RfcEmisor}");
    Console.WriteLine($"Periodo: {result.FiscalData.Periodo}");
    Console.WriteLine($"Monto: {result.FiscalData.MontoTotal}");
    Console.WriteLine($"Confidence: {result.ConfidenceScore}%");
}
```

### Extract Text Only
```csharp
var processor = new PdfProcessor();
var text = processor.ExtractTextOnly(pdfBytes);
```

### Get PDF Metadata
```csharp
var processor = new PdfProcessor();
var metadata = processor.GetMetadata(pdfBytes);
Console.WriteLine($"Pages: {metadata.PageCount}");
Console.WriteLine($"Size: {metadata.FileSize} bytes");
```

## Integration with Backend

The `PdfProcessingService` in the Infrastructure layer automatically uses this processor:

1. User uploads PDF → API receives file
2. API calls `PdfProcessingService.ExtractDataFromPdfAsync()`
3. Service uses `PdfProcessor` to extract fiscal data
4. Results are stored in database
5. Frontend displays extracted data

## Dependencies

- **iText7** (8.0.2): PDF manipulation and text extraction
- **iText7.pdfhtml** (5.0.2): HTML to PDF conversion support
- **System.Drawing.Common** (8.0.0): Image processing support
- **.NET 8.0-windows**: Target framework

## Pattern Matching

### RFC Pattern
```regex
\b([A-ZÑ&]{3,4}\d{6}[A-Z0-9]{3})\b
```
Matches Mexican RFC format: 3-4 letters + 6 digits + 3 alphanumeric characters

### Periodo Pattern
```regex
(?:periodo|period|mes|month|fecha)[\s:]*(\d{1,2}[-/]\d{1,2}[-/]\d{2,4}|...)
```
Matches various date formats and month names

### Monto Pattern
```regex
(?:total|monto|amount|suma|importe)[\s:]*\$?\s*([0-9]{1,3}(?:[,\s]?[0-9]{3})*(?:\.[0-9]{2})?)
```
Matches currency amounts with optional thousand separators

## Error Handling

- Returns `Success = false` if processing fails
- Provides detailed error messages in `ErrorMessage`
- Gracefully handles malformed PDFs
- Returns fallback values for missing data

## Performance

- Asynchronous processing support via `ProcessPdfAsync()`
- Efficient text extraction using iText7
- Minimal memory footprint
- Processes typical invoices in < 1 second

## Future Enhancements

- [ ] Add PDF watermarking capabilities
- [ ] Implement PDF stamps/annotations
- [ ] Support for encrypted PDFs
- [ ] OCR support for scanned documents
- [ ] Machine learning for better data extraction
- [ ] Template-based extraction for specific document types

## Testing

To test the processor:

```csharp
// Create sample PDF with fiscal data
var processor = new PdfProcessor();
var testPdfBytes = CreateTestPdf(); // Your test PDF
var result = processor.ProcessPdf(testPdfBytes, "test.pdf");

Assert.True(result.Success);
Assert.NotNull(result.FiscalData.RfcEmisor);
Assert.True(result.ConfidenceScore > 50);
```

## License

Part of the PdfPortal project.

