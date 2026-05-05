using inz.Models;
using inz.Models.Enums;
using inz.Repository.Interface;

namespace inz.Repository.Implementations
{
    public class InMemoryDocumentRepository : IDocumentRepository

    {
        private readonly Dictionary<int, DocumentMetadata> _inMemoryDocumentDatabase = new();
        int idIncrementator = 1;
        public Task AddDocumentMetadataAsync(DocumentMetadata documentMetadata)
        {
            _inMemoryDocumentDatabase.Add(idIncrementator, documentMetadata);
            idIncrementator++;
            return Task.CompletedTask;
        }

        public Task<DocumentMetadata?> GetMetadataByIdAsync(int documentMetadataId)
        {
            _inMemoryDocumentDatabase.TryGetValue(documentMetadataId, out var document);
            return Task.FromResult(document);
        }

        public Task UpdateDocumentMetadataAsync(int documentMetadataId, DocumentMetadata documentMetadata)
        {
            _inMemoryDocumentDatabase[documentMetadataId] = documentMetadata;
            return Task.CompletedTask;
        }

        public Task UpdateMetadataProcessingStatusAsync(int documentMetadataId, ProcessStatus processingStatus)
        {
            _inMemoryDocumentDatabase.Remove(documentMetadataId);
            return Task.CompletedTask;
        }
    }
}
