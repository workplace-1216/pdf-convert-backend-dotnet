using PdfPortal.Application.Models;
using System.Text.Json;

namespace PdfPortal.Application.Services;

public class TemplateRuleParser
{
    private readonly JsonSerializerOptions _jsonOptions;

    public TemplateRuleParser()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public TemplateRuleDefinition Parse(string jsonDefinition)
    {
        if (string.IsNullOrWhiteSpace(jsonDefinition))
        {
            throw new ArgumentException("JSON definition cannot be null or empty", nameof(jsonDefinition));
        }

        try
        {
            var ruleDefinition = JsonSerializer.Deserialize<TemplateRuleDefinition>(jsonDefinition, _jsonOptions);
            return ruleDefinition ?? new TemplateRuleDefinition();
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"Invalid JSON definition: {ex.Message}", nameof(jsonDefinition), ex);
        }
    }

    public bool IsValid(string jsonDefinition)
    {
        try
        {
            Parse(jsonDefinition);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
