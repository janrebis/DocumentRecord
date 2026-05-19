using inz.Models;

namespace inz.Repository.Interface;

public interface IDocumentRepository
{
    Task<int> AddDocumentMetadataAsync(DocumentMetadata documentMetadata);
    Task<DocumentMetadata?> GetMetadataByIdAsync(int documentMetadataId);
    Task UpdateDocumentMetadataAsync(int documentMetadataId, DocumentMetadata documentMetadata);
}