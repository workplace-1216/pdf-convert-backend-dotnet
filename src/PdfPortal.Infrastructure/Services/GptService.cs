using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http;
using PdfPortal.Application.Interfaces;
using PdfPortal.Application.Models;
using System.Text;
using System.Text.Json;

namespace PdfPortal.Infrastructure.Services;

public class GptService : IGptService
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;
    private readonly string? _model;

    public GptService(IConfiguration configuration, IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _httpClient = httpClientFactory.CreateClient();
        
        // Priority: Environment Variables > appsettings.json
        _apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") 
            ?? _configuration["OpenAI:ApiKey"];
        _model = Environment.GetEnvironmentVariable("OPENAI_MODEL") 
            ?? _configuration["OpenAI:Model"] 
            ?? "gpt-4-turbo-preview";
        
        if (string.IsNullOrEmpty(_apiKey))
        {
            Console.WriteLine("[GptService] WARNING: OpenAI API key not configured");
            Console.WriteLine("[GptService] Please set OPENAI_API_KEY in .env file or appsettings.json");
        }
        else
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "PdfPortal/1.0");
            Console.WriteLine($"[GptService] ✓ OpenAI API configured (Model: {_model})");
        }
    }

    public async Task<GptExtractionResult> ExtractDocumentInfoFromTextAsync(string extractedText, string prompt)
    {
        var result = new GptExtractionResult();
        
        try
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                result.Success = false;
                result.ErrorMessage = "OpenAI API key not configured";
                return result;
            }

            Console.WriteLine("[GptService] Starting GPT analysis of extracted text...");
            Console.WriteLine($"[GptService] Text length: {extractedText.Length} characters");
            Console.WriteLine($"[GptService] Text preview (first 500 chars): {extractedText.Substring(0, Math.Min(500, extractedText.Length))}...");
            
            // Prepare the GPT request with text-only
            Console.WriteLine("[GptService] 📤 PREPARING REQUEST TO GPT");
            Console.WriteLine($"[GptService] Model: {_model}");
            
            var requestBody = new
            {
                model = _model,
                messages = new object[]
                {
                    new
                    {
                        role = "system",
                        content = "You are an expert at analyzing documents. Extract structured information including title, summary, and contact information. Return your response in JSON format with the following structure: {\"title\": \"...\", \"summary\": \"...\", \"contactInformation\": \"...\", \"extractedText\": \"...\"}"
                    },
                    new
                    {
                        role = "user",
                        content = prompt
                    }
                },
                max_tokens = 4000,
                temperature = 0.3
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            Console.WriteLine($"[GptService] Request JSON size: {jsonContent.Length} characters");
            
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            Console.WriteLine("[GptService] 🌐 Sending request to OpenAI API...");
            var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[GptService] ❌ API ERROR: {response.StatusCode}");
                Console.WriteLine($"[GptService] Error details: {errorContent}");
                result.Success = false;
                result.ErrorMessage = $"OpenAI API error: {response.StatusCode}";
                return result;
            }

            Console.WriteLine($"[GptService] ✓ Response received with status: {response.StatusCode}");
            
            var responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[GptService] 📥 RESPONSE FROM GPT");
            Console.WriteLine($"[GptService] Response length: {responseContent.Length} characters");
            Console.WriteLine($"[GptService] Full Response JSON:");
            Console.WriteLine(responseContent);
            
            var gptResponse = JsonSerializer.Deserialize<GptApiResponse>(responseContent);

            if (gptResponse?.Choices == null || gptResponse.Choices.Length == 0)
            {
                Console.WriteLine($"[GptService] ❌ No choices in response");
                Console.WriteLine($"[GptService] gptResponse is null: {gptResponse == null}");
                if (gptResponse != null)
                {
                    Console.WriteLine($"[GptService] Choices is null: {gptResponse.Choices == null}");
                    Console.WriteLine($"[GptService] Choices length: {gptResponse.Choices?.Length ?? -1}");
                }
                result.Success = false;
                result.ErrorMessage = "No response from GPT";
                return result;
            }

            Console.WriteLine($"[GptService] Number of choices: {gptResponse.Choices.Length}");
            
            var gptMessage = gptResponse.Choices[0].Message?.Content;
            if (string.IsNullOrEmpty(gptMessage))
            {
                Console.WriteLine($"[GptService] ❌ Empty message content");
                result.Success = false;
                result.ErrorMessage = "Empty response from GPT";
                return result;
            }

            Console.WriteLine($"[GptService] 📄 GPT MESSAGE CONTENT (length: {gptMessage.Length}):");
            Console.WriteLine($"[GptService] ==================== START ====================");
            Console.WriteLine(gptMessage);
            Console.WriteLine($"[GptService] ==================== END ====================");

            // Try to parse JSON response
            try
            {
                // Remove markdown code blocks if present
                gptMessage = gptMessage.Trim();
                if (gptMessage.StartsWith("```json"))
                {
                    gptMessage = gptMessage.Substring(7);
                }
                if (gptMessage.StartsWith("```"))
                {
                    gptMessage = gptMessage.Substring(3);
                }
                if (gptMessage.EndsWith("```"))
                {
                    gptMessage = gptMessage.Substring(0, gptMessage.Length - 3);
                }
                gptMessage = gptMessage.Trim();

                var extractedData = JsonSerializer.Deserialize<GptExtractedData>(gptMessage);
                
                if (extractedData != null)
                {
                    result.Title = extractedData.Title ?? string.Empty;
                    result.Summary = extractedData.Summary ?? string.Empty;
                    result.ContactInformation = extractedData.ContactInformation ?? string.Empty;
                    result.ExtractedText = extractedData.ExtractedText ?? extractedText;
                    result.Success = true;
                    
                    Console.WriteLine($"[GptService] ✓ JSON PARSING SUCCESSFUL");
                    Console.WriteLine($"[GptService] 📊 EXTRACTED DATA:");
                    Console.WriteLine($"[GptService]   - Title: {result.Title}");
                    Console.WriteLine($"[GptService]   - Summary (length {result.Summary.Length}): {(result.Summary.Length > 100 ? result.Summary.Substring(0, 100) + "..." : result.Summary)}");
                    Console.WriteLine($"[GptService]   - Contact Info: {result.ContactInformation}");
                }
                else
                {
                    result.ExtractedText = extractedText;
                    result.Success = true;
                    Console.WriteLine("[GptService] ⚠ Could not parse structured data");
                }
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"[GptService] ❌ JSON parsing failed: {ex.Message}");
                result.ExtractedText = extractedText;
                result.Success = true;
            }

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GptService] ❌ Exception: {ex.Message}");
            Console.WriteLine($"[GptService] Stack trace: {ex.StackTrace}");
            result.Success = false;
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    public async Task<GptExtractionResult> ExtractDocumentInfoAsync(byte[] pdfBytes, string prompt)
    {
        var result = new GptExtractionResult();
        
        try
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                result.Success = false;
                result.ErrorMessage = "OpenAI API key not configured";
                return result;
            }

            Console.WriteLine("[GptService] Starting GPT extraction...");
            Console.WriteLine($"[GptService] PDF Size: {pdfBytes.Length} bytes");
            
            // Convert PDF bytes to base64
            string base64Pdf = Convert.ToBase64String(pdfBytes);
            Console.WriteLine($"[GptService] Base64 PDF length: {base64Pdf.Length} characters");
            Console.WriteLine($"[GptService] Base64 preview (first 100 chars): {base64Pdf.Substring(0, Math.Min(100, base64Pdf.Length))}...");
            
            // Prepare the GPT request
            Console.WriteLine("[GptService] 📤 PREPARING REQUEST TO GPT");
            Console.WriteLine($"[GptService] Model: {_model}");
            Console.WriteLine($"[GptService] Prompt: {prompt}");
            
            var requestBody = new
            {
                model = _model,
                messages = new object[]
                {
                    new
                    {
                        role = "system",
                        content = "You are an expert at extracting information from PDF documents. Extract all text content and provide structured information including title, summary, and contact information. Return your response in JSON format with the following structure: {\"title\": \"...\", \"summary\": \"...\", \"contactInformation\": \"...\", \"extractedText\": \"...\"}"
                    },
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text", text = prompt },
                            new
                            {
                                type = "image_url",
                                image_url = new
                                {
                                    url = $"data:application/pdf;base64,{base64Pdf}"
                                }
                            }
                        }
                    }
                },
                max_tokens = 4000,
                temperature = 0.3
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            Console.WriteLine($"[GptService] Request JSON size: {jsonContent.Length} characters");
            
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            Console.WriteLine("[GptService] 🌐 Sending request to OpenAI API...");
            var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[GptService] ❌ API ERROR: {response.StatusCode}");
                Console.WriteLine($"[GptService] Error details: {errorContent}");
                result.Success = false;
                result.ErrorMessage = $"OpenAI API error: {response.StatusCode}";
                return result;
            }

            Console.WriteLine($"[GptService] ✓ Response received with status: {response.StatusCode}");
            
            var responseContent = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"[GptService] 📥 RESPONSE FROM GPT");
            Console.WriteLine($"[GptService] Response length: {responseContent.Length} characters");
            Console.WriteLine($"[GptService] Full Response: {responseContent}");
            
            var gptResponse = JsonSerializer.Deserialize<GptApiResponse>(responseContent);

            if (gptResponse?.Choices == null || gptResponse.Choices.Length == 0)
            {
                Console.WriteLine($"[GptService] ❌ No choices in response");
                result.Success = false;
                result.ErrorMessage = "No response from GPT";
                return result;
            }

            Console.WriteLine($"[GptService] Number of choices: {gptResponse.Choices.Length}");
            
            var gptMessage = gptResponse.Choices[0].Message?.Content;
            if (string.IsNullOrEmpty(gptMessage))
            {
                Console.WriteLine($"[GptService] ❌ Empty message content");
                result.Success = false;
                result.ErrorMessage = "Empty response from GPT";
                return result;
            }

            Console.WriteLine($"[GptService] 📄 GPT MESSAGE CONTENT (length: {gptMessage.Length}):");
            Console.WriteLine($"[GptService] ==================== START ====================");
            Console.WriteLine(gptMessage);
            Console.WriteLine($"[GptService] ==================== END ====================");

            // Try to parse JSON response
            try
            {
                // Remove markdown code blocks if present
                gptMessage = gptMessage.Trim();
                if (gptMessage.StartsWith("```json"))
                {
                    gptMessage = gptMessage.Substring(7);
                }
                if (gptMessage.StartsWith("```"))
                {
                    gptMessage = gptMessage.Substring(3);
                }
                if (gptMessage.EndsWith("```"))
                {
                    gptMessage = gptMessage.Substring(0, gptMessage.Length - 3);
                }
                gptMessage = gptMessage.Trim();

                var extractedData = JsonSerializer.Deserialize<GptExtractedData>(gptMessage);
                
                if (extractedData != null)
                {
                    result.Title = extractedData.Title ?? string.Empty;
                    result.Summary = extractedData.Summary ?? string.Empty;
                    result.ContactInformation = extractedData.ContactInformation ?? string.Empty;
                    result.ExtractedText = extractedData.ExtractedText ?? string.Empty;
                    result.Success = true;
                    
                    Console.WriteLine($"[GptService] ✓ JSON PARSING SUCCESSFUL");
                    Console.WriteLine($"[GptService] 📊 EXTRACTED DATA:");
                    Console.WriteLine($"[GptService]   - Title: {result.Title}");
                    Console.WriteLine($"[GptService]   - Summary (length {result.Summary.Length}): {(result.Summary.Length > 100 ? result.Summary.Substring(0, 100) + "..." : result.Summary)}");
                    Console.WriteLine($"[GptService]   - Contact Info: {result.ContactInformation}");
                    Console.WriteLine($"[GptService]   - Extracted Text (length {result.ExtractedText.Length}): {(result.ExtractedText.Length > 200 ? result.ExtractedText.Substring(0, 200) + "..." : result.ExtractedText)}");
                }
                else
                {
                    // Fallback: Use full response as extracted text
                    result.ExtractedText = gptMessage;
                    result.Success = true;
                    Console.WriteLine("[GptService] ⚠ Could not parse structured data, using full response as text");
                }
            }
            catch (JsonException ex)
            {
                // If JSON parsing fails, use the full response as extracted text
                Console.WriteLine($"[GptService] ❌ JSON parsing failed: {ex.Message}");
                Console.WriteLine($"[GptService] Using full response as extracted text");
                result.ExtractedText = gptMessage;
                result.Success = true;
            }

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GptService] Error: {ex.Message}");
            result.Success = false;
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    private class GptApiResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("choices")]
        public GptChoice[]? Choices { get; set; }
    }

    private class GptChoice
    {
        [System.Text.Json.Serialization.JsonPropertyName("message")]
        public GptMessage? Message { get; set; }
    }

    private class GptMessage
    {
        [System.Text.Json.Serialization.JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    private class GptExtractedData
    {
        [System.Text.Json.Serialization.JsonPropertyName("title")]
        public string? Title { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("summary")]
        public string? Summary { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("contactInformation")]
        public string? ContactInformation { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("extractedText")]
        public string? ExtractedText { get; set; }
    }
}

