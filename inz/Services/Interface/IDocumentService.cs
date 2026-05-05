using inz.Models;

namespace inz.Services.Interface
{
    public interface IDocumentService
    {
        Task<int> AddDocumentAsync(CreateDocumentCommand command);
        Task<Stream> GetDocumentByIdAsync(int documentId);
        Task MarkDocumentToDeleteAsync(int documentId);
        Task DeleteDocumentAsync(int documentId);
        Task<int> UpdateDocumentAsync(int documentId, IFormFile file);
    }
}
