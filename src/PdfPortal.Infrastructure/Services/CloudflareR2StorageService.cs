using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using PdfPortal.Application.Interfaces;

namespace PdfPortal.Infrastructure.Services;

public class CloudflareR2StorageService : IPdfStorageService
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucket;

    public CloudflareR2StorageService()
    {
        // Read configuration from environment variables
        var accountId = Environment.GetEnvironmentVariable("R2_ACCOUNT_ID") ?? string.Empty;
        var accessKey = Environment.GetEnvironmentVariable("R2_ACCESS_KEY_ID") ?? string.Empty;
        var secretKey = Environment.GetEnvironmentVariable("R2_SECRET_ACCESS_KEY") ?? string.Empty;
        var endpoint = Environment.GetEnvironmentVariable("R2_ENDPOINT")
            ?? (string.IsNullOrEmpty(accountId) ? string.Empty : $"https://{accountId}.r2.cloudflarestorage.com");

        _bucket = Environment.GetEnvironmentVariable("R2_BUCKET") ?? "pdf";

        Console.WriteLine($"[CloudflareR2] 🔧 Initializing R2 Storage Service");
        Console.WriteLine($"[CloudflareR2] Endpoint: {endpoint}");
        Console.WriteLine($"[CloudflareR2] Bucket: {_bucket}");
        Console.WriteLine($"[CloudflareR2] Access key: {(string.IsNullOrEmpty(accessKey) ? "❌ MISSING" : "✓ " + accessKey.Substring(0, Math.Min(8, accessKey.Length)) + "...")}");
        Console.WriteLine($"[CloudflareR2] Secret key: {(string.IsNullOrEmpty(secretKey) ? "❌ MISSING" : "✓ " + secretKey.Substring(0, Math.Min(8, secretKey.Length)) + "...")}");

        if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(accessKey) || string.IsNullOrEmpty(secretKey))
        {
            throw new InvalidOperationException("Cloudflare R2 credentials are not configured. Set R2_ENDPOINT, R2_ACCESS_KEY_ID and R2_SECRET_ACCESS_KEY");
        }

        // R2-specific configuration for AWS S3 SDK
        var config = new AmazonS3Config
        {
            ServiceURL = endpoint,
            ForcePathStyle = true,
            SignatureVersion = "4",
            UseHttp = false,
            Timeout = TimeSpan.FromSeconds(300),
            MaxErrorRetry = 3
        };
        
        var credentials = new BasicAWSCredentials(accessKey, secretKey);
        _s3 = new AmazonS3Client(credentials, config);
        
        Console.WriteLine("[CloudflareR2] ✓ S3 client initialized successfully");
    }

    public async Task<string> SaveOriginalPdfAsync(Stream pdfStream, string fileName)
    {
        try
        {
            var key = $"original/{Guid.NewGuid():N}.pdf";
            Console.WriteLine($"[CloudflareR2] 📤 Uploading original PDF: {key} to bucket: {_bucket}");
            
            var putRequest = new PutObjectRequest
            {
                BucketName = _bucket,
                Key = key,
                InputStream = pdfStream,
                ContentType = "application/pdf",
                DisablePayloadSigning = false,
                UseChunkEncoding = false // Disable chunked encoding for R2 compatibility
            };
            
            var response = await _s3.PutObjectAsync(putRequest);
            Console.WriteLine($"[CloudflareR2] ✅ Original PDF uploaded successfully. Status: {response.HttpStatusCode}, ETag: {response.ETag}");
            
            return key;
        }
        catch (Amazon.S3.AmazonS3Exception ex)
        {
            Console.WriteLine($"[CloudflareR2] ❌ R2 Upload Error:");
            Console.WriteLine($"    Status Code: {ex.StatusCode}");
            Console.WriteLine($"    Error Code: {ex.ErrorCode}");
            Console.WriteLine($"    Message: {ex.Message}");
            Console.WriteLine($"    Request ID: {ex.RequestId}");
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CloudflareR2] ❌ Upload failed: {ex.GetType().Name} - {ex.Message}");
            Console.WriteLine($"[CloudflareR2] Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    public async Task<string> SaveProcessedPdfAsync(byte[] pdfBytes, string fileName)
    {
        try
        {
            var key = $"processed/{Guid.NewGuid():N}.pdf";
            Console.WriteLine($"[CloudflareR2] 📤 Uploading processed PDF: {key} to bucket: {_bucket}, Size: {pdfBytes.Length} bytes");
            
            using var ms = new MemoryStream(pdfBytes);
            var putRequest = new PutObjectRequest
            {
                BucketName = _bucket,
                Key = key,
                InputStream = ms,
                ContentType = "application/pdf",
                DisablePayloadSigning = false,
                UseChunkEncoding = false // Disable chunked encoding for R2 compatibility
            };
            
            var response = await _s3.PutObjectAsync(putRequest);
            Console.WriteLine($"[CloudflareR2] ✅ Processed PDF uploaded successfully. Status: {response.HttpStatusCode}");
            
            return key;
        }
        catch (Amazon.S3.AmazonS3Exception ex)
        {
            Console.WriteLine($"[CloudflareR2] ❌ R2 Upload Error:");
            Console.WriteLine($"    Status Code: {ex.StatusCode}");
            Console.WriteLine($"    Error Code: {ex.ErrorCode}");
            Console.WriteLine($"    Message: {ex.Message}");
            Console.WriteLine($"    Request ID: {ex.RequestId}");
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CloudflareR2] ❌ Processed PDF upload failed: {ex.GetType().Name} - {ex.Message}");
            throw;
        }
    }

    public async Task<byte[]> GetProcessedPdfAsync(string filePath)
    {
        // filePath is the key returned from SaveProcessedPdfAsync
        var getRequest = new GetObjectRequest
        {
            BucketName = _bucket,
            Key = filePath
        };
        using var response = await _s3.GetObjectAsync(getRequest);
        using var ms = new MemoryStream();
        await response.ResponseStream.CopyToAsync(ms);
        return ms.ToArray();
    }

    public async Task<bool> DeleteFileAsync(string filePath)
    {
        try
        {
            var del = new DeleteObjectRequest { BucketName = _bucket, Key = filePath };
            var resp = await _s3.DeleteObjectAsync(del);
            return resp.HttpStatusCode == System.Net.HttpStatusCode.NoContent || resp.HttpStatusCode == System.Net.HttpStatusCode.OK;
        }
        catch
        {
            return false;
        }
    }

    public Task<string> GetTempFilePathAsync(string tempId)
    {
        // Not used with R2; return a virtual key under temp
        return Task.FromResult($"temp/{tempId}.pdf");
    }
}


