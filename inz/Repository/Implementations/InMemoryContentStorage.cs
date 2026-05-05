using System.Reflection.Metadata;
using inz.Models;
using inz.Repository.Interface;

namespace inz.Repository.Implementations
{
    public class InMemoryContentStorage : IDocumentContentStorage

    {
        private readonly Dictionary<string, IFormFile> _inMemoryContentStorage = new();

        public Task<string> AddDocumentToStorageAsync(IFormFile file)
        {
            string id = Guid.NewGuid().ToString();
            _inMemoryContentStorage.Add(id, file);
            return Task.FromResult(id);
        }

        public Task DeleteAsync(string blobKey)
        {
            _inMemoryContentStorage.Remove(blobKey);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string blobKey)
        {
            var result = _inMemoryContentStorage.ContainsKey(blobKey);
            return Task.FromResult(result);
        }

        public Task<Stream> GetDocumentStreamAsync(string blobKey)
        {
            var file = _inMemoryContentStorage[blobKey].OpenReadStream();
            return Task.FromResult(file);
        }

        public Task UpdateDocumentInStorageAsync(string blobKey, IFormFile file)
        {
            _inMemoryContentStorage[blobKey] = file;
            return Task.CompletedTask;
        }
    }
}
