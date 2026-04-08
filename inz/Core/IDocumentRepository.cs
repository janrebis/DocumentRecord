using inz.Models;

namespace inz.Service
{
    public interface IDocumentRepository
    {
        public Task AddDocumentMetadata(FileMetadata fileMetadata);
        public Task UpdateMetadataProcessingStatus(Guid documentMetadataId, ProcessStatus processingStatus);
    }
}