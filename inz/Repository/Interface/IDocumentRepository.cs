using inz.Models;
using inz.Models.Enums;

namespace inz.Repository.Interface
{
    public interface IDocumentRepository
    {
        Task AddDocumentMetadataAsync(DocumentMetadata documentMetadata);
        Task UpdateMetadataProcessingStatusAsync(int documentMetadataId, ProcessStatus processingStatus);
        Task<DocumentMetadata?> GetMetadataByIdAsync(int documentMetadataId);
        Task UpdateDocumentMetadataAsync(int documentMetadataId, DocumentMetadata documentMetadata);
    }
}