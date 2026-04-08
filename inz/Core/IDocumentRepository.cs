using System.Reflection.Metadata;
using inz.Models;

namespace inz.Service
{
    public interface IDocumentRepository
    {
        public Task AddDocumentMetadata(FileMetadata fileMetadata);
        public Task UpdateMetadataProcessingStatus(Guid documentMetadataId, ProcessStatus processingStatus);

        public Task<FileMetadata?> GetMetadaById(Guid id);
    }
}