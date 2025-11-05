using PdfPortal.Application.DTOs;
using PdfPortal.Application.Interfaces;
using PdfPortal.Domain.Entities;

namespace PdfPortal.Application.Services;

public class TemplateService
{
    private readonly IUnitOfWork _unitOfWork;

    public TemplateService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<TemplateRuleSetDto> CreateTemplateAsync(CreateTemplateRequest request, int userId)
    {
        var template = new TemplateRuleSet
        {
            Name = request.Name,
            JsonDefinition = request.JsonDefinition,
            CreatedByUserId = userId
        };

        await _unitOfWork.TemplateRuleSets.AddAsync(template);
        await _unitOfWork.SaveChangesAsync();

        return new TemplateRuleSetDto
        {
            Id = template.Id,
            Name = template.Name,
            JsonDefinition = template.JsonDefinition,
            CreatedByUserId = template.CreatedByUserId,
            CreatedAt = template.CreatedAt,
            IsActive = template.IsActive
        };
    }

    public async Task<TemplateRuleSetDto?> UpdateTemplateAsync(int templateId, UpdateTemplateRequest request)
    {
        var template = await _unitOfWork.TemplateRuleSets.GetByIdAsync(templateId);
        if (template == null)
            return null;

        template.Name = request.Name;
        template.JsonDefinition = request.JsonDefinition;
        template.IsActive = request.IsActive;
        template.UpdatedAt = DateTime.UtcNow;

        await _unitOfWork.TemplateRuleSets.UpdateAsync(template);
        await _unitOfWork.SaveChangesAsync();

        return new TemplateRuleSetDto
        {
            Id = template.Id,
            Name = template.Name,
            JsonDefinition = template.JsonDefinition,
            CreatedByUserId = template.CreatedByUserId,
            CreatedAt = template.CreatedAt,
            IsActive = template.IsActive
        };
    }

    public async Task<IEnumerable<TemplateRuleSetDto>> GetAllTemplatesAsync()
    {
        var templates = await _unitOfWork.TemplateRuleSets.GetAllAsync();
        
        return templates.Select(t => new TemplateRuleSetDto
        {
            Id = t.Id,
            Name = t.Name,
            JsonDefinition = t.JsonDefinition,
            CreatedByUserId = t.CreatedByUserId,
            CreatedAt = t.CreatedAt,
            IsActive = t.IsActive
        });
    }

    public async Task<TemplateRuleSetDto?> GetTemplateByIdAsync(int templateId)
    {
        var template = await _unitOfWork.TemplateRuleSets.GetByIdAsync(templateId);
        if (template == null)
            return null;

        return new TemplateRuleSetDto
        {
            Id = template.Id,
            Name = template.Name,
            JsonDefinition = template.JsonDefinition,
            CreatedByUserId = template.CreatedByUserId,
            CreatedAt = template.CreatedAt,
            IsActive = template.IsActive
        };
    }

    public async Task<bool> DeleteTemplateAsync(int templateId)
    {
        var template = await _unitOfWork.TemplateRuleSets.GetByIdAsync(templateId);
        if (template == null)
            return false;

        await _unitOfWork.TemplateRuleSets.DeleteAsync(template);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }
}
