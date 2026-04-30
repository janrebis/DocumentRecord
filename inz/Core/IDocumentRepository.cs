using System.Reflection.Metadata;
using inz.Models;

namespace inz.Service
{
    public interface IDocumentRepository
    {
        public Task AddDocumentMetadata(DocumentMetadata documentMetadata);
        public Task UpdateMetadataProcessingStatus(Guid documentMetadataId, ProcessStatus processingStatus);
        public Task<DocumentMetadata?> GetMetadaById(Guid documentMetadataId);
        public Task UpdateDocumentMetadataAsync(Guid documentMetadataId, DocumentMetadata documentMetadata);
    }
}