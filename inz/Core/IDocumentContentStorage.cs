using System.Reflection.Metadata;

namespace inz.Service
{
    public interface IDocumentContentStorage
    {
        public Task AddDocumentToStorage(IFormFile file);
        public Task<Stream> GetDocumentStream(string BlokKey);
        public Task DeleteAsync(string BlobKey);
        public Task<bool> ExistsAsync(string BlobKey);
        public Task UpdateDocumentInStorageAsync(string BlobKey, IFormFile file);
    }
}