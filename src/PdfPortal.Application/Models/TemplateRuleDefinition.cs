using System.Text.Json.Serialization;

namespace PdfPortal.Application.Models;

public class TemplateRuleDefinition
{
    [JsonPropertyName("metadataRules")]
    public Dictionary<string, string> MetadataRules { get; set; } = new();

    [JsonPropertyName("pageRules")]
    public PageRules? PageRules { get; set; }

    [JsonPropertyName("coverPage")]
    public CoverPage? CoverPage { get; set; }
}

public class PageRules
{
    [JsonPropertyName("keepPages")]
    public int[]? KeepPages { get; set; }

    [JsonPropertyName("footerText")]
    public string? FooterText { get; set; }
}

public class CoverPage
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("fields")]
    public Dictionary<string, string> Fields { get; set; } = new();
}
