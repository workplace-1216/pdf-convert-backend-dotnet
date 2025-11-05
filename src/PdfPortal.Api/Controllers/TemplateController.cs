using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PdfPortal.Application.DTOs;
using PdfPortal.Application.Helpers;
using PdfPortal.Application.Interfaces;
using PdfPortal.Application.Services;

namespace PdfPortal.Api.Controllers;

[ApiController]
[Route("api/templates")]
[Authorize]
public class TemplateController : ControllerBase
{
    private readonly TemplateService _templateService;
    private readonly ITemplateProcessorService _templateProcessorService;

    public TemplateController(TemplateService templateService, ITemplateProcessorService templateProcessorService)
    {
        _templateService = templateService;
        _templateProcessorService = templateProcessorService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TemplateRuleSetDto>>> GetTemplates()
    {
        try
        {
            var templates = await _templateService.GetAllTemplatesAsync();
            return Ok(templates);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving templates: {ex.Message}");
        }
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<CreateTemplateResponse>> CreateTemplate([FromBody] CreateTemplateRequest request)
    {
        try
        {
            // Validate template JSON
            if (!await _templateProcessorService.ValidateTemplateAsync(request.JsonDefinition))
            {
                return BadRequest("Invalid template JSON definition");
            }

            var userId = CurrentUserHelper.GetCurrentUserId(HttpContext);

            var result = await _templateService.CreateTemplateAsync(request, userId);
            return CreatedAtAction(nameof(GetTemplate), new { id = result.Id }, result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error creating template: {ex.Message}");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TemplateRuleSetDto>> GetTemplate(int id)
    {
        try
        {
            var template = await _templateService.GetTemplateByIdAsync(id);
            if (template == null)
            {
                return NotFound();
            }
            return Ok(template);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving template: {ex.Message}");
        }
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<TemplateRuleSetDto>> UpdateTemplate(int id, [FromBody] UpdateTemplateRequest request)
    {
        try
        {
            // Validate template JSON
            if (!await _templateProcessorService.ValidateTemplateAsync(request.JsonDefinition))
            {
                return BadRequest("Invalid template JSON definition");
            }

            var result = await _templateService.UpdateTemplateAsync(id, request);
            if (result == null)
            {
                return NotFound();
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error updating template: {ex.Message}");
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> DeleteTemplate(int id)
    {
        try
        {
            var success = await _templateService.DeleteTemplateAsync(id);
            if (!success)
            {
                return NotFound();
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error deleting template: {ex.Message}");
        }
    }
}
