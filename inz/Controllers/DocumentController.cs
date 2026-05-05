using inz.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace inz.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentController : Controller
    {
        private readonly IDocumentService _documentService;

        public DocumentController(IDocumentService documentService)
        {
            _documentService = documentService;
        }
        [HttpPost]
        public async Task<IActionResult> AddDocument(IFormFile file)
        {
            var command = new Models.CreateDocumentCommand
            {
                File = file,
                OwnerId = 1,
                OrganizationId = 1//tu domyślnie z requesta
            };
            var documentId = await _documentService.AddDocumentAsync(command);

            return CreatedAtAction(nameof(GetDocument), new { id = documentId }, new { id = documentId });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetDocument(int id)
        {
            var document = await _documentService.GetDocumentByIdAsync(id);

            return File(document, "application/octet-stream");
        }

        [HttpPatch("{id}/mark-to-delete")]
        public async Task<IActionResult> MarkDocumentToDelete(int id)
        {
            await _documentService.MarkDocumentToDeleteAsync(id);
            return NoContent();
        }
        [HttpPost("{id}/update")]
        public async Task<IActionResult> UpdateDocument(int id, IFormFile file)
        { 
            var document = await _documentService.UpdateDocumentAsync(id, file);
            return Ok(document);
        }
    }
}
