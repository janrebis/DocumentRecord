using inz.Models;
using inz.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace inz.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentController : ControllerBase
{
    private readonly IDocumentService _documentService;

    public DocumentController(IDocumentService documentService)
    {
        _documentService = documentService;
    }

    [HttpGet("test")]
    public IActionResult Test()
    {
        return Ok("Documents controller działa");
    }

    [HttpPost]
    public async Task<IActionResult> AddDocument([FromForm] IFormFile file)
    {
        var command = new CreateDocumentCommand
        {
            File = file,
            OwnerId = 1,
            OrganizationId = 1
        };

        var documentId = await _documentService.AddDocumentAsync(command);

        return CreatedAtAction(
            nameof(GetDocument),
            new { id = documentId },
            new { id = documentId });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDocument(int id)
    {
        var document = await _documentService.GetDocumentByIdAsync(id);

        return File(document, "application/octet-stream");
    }

    [HttpPatch("{id:int}/mark-to-delete")]
    public async Task<IActionResult> MarkDocumentToDelete(int id)
    {
        await _documentService.MarkDocumentToDeleteAsync(id);

        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteDocument(int id)
    {
        await _documentService.DeleteDocumentAsync(id);

        return NoContent();
    }

    [HttpPost("{id:int}/update")]
    public async Task<IActionResult> UpdateDocument(int id, [FromForm] IFormFile file)
    {
        var documentId = await _documentService.UpdateDocumentAsync(id, file);

        return Ok(new { id = documentId });
    }
}