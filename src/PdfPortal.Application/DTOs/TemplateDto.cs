namespace PdfPortal.Application.DTOs;

public class TemplateRuleSetDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string JsonDefinition { get; set; } = string.Empty;
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
}

public class CreateTemplateRequest
{
    public string Name { get; set; } = string.Empty;
    public string JsonDefinition { get; set; } = string.Empty;
}

public class CreateTemplateResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string JsonDefinition { get; set; } = string.Empty;
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UpdateTemplateRequest
{
    public string Name { get; set; } = string.Empty;
    public string JsonDefinition { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}