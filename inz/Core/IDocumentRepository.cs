using System.Reflection.Metadata;
using inz.Models;

namespace inz.Service
{
    public interface IDocumentRepository
    {
        public Task AddDocumentMetadata(DocumentMetadata documentMetadata);
        public Task UpdateMetadataProcessingStatus(int documentMetadataId, ProcessStatus processingStatus);
        public Task<DocumentMetadata?> GetMetadaById(int documentMetadataId);
        public Task UpdateDocumentMetadataAsync(int documentMetadataId, DocumentMetadata documentMetadata);
    }
}