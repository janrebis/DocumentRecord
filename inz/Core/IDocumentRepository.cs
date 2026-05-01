using inz.Models;

namespace inz.Service
{
    public interface IDocumentRepository
    {
        Task AddDocumentMetadataAsync(DocumentMetadata documentMetadata);
        Task UpdateMetadataProcessingStatusAsync(int documentMetadataId, ProcessStatus processingStatus);
        Task<DocumentMetadata?> GetMetadataByIdAsync(int documentMetadataId);
        Task UpdateDocumentMetadataAsync(int documentMetadataId, DocumentMetadata documentMetadata);
    }
}