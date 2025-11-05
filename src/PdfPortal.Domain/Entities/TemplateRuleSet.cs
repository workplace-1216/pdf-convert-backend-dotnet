using System.ComponentModel.DataAnnotations;

namespace PdfPortal.Domain.Entities;

public class TemplateRuleSet
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    public string JsonDefinition { get; set; } = string.Empty;
    
    [Required]
    public int CreatedByUserId { get; set; }
    
    public User CreatedByUser { get; set; } = null!;
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsActive { get; set; } = true;
}
