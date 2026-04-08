using System.Reflection.Metadata;

namespace inz.Service
{
    public interface IDocumentContentStorage
    {
        public Task AddDocumentToStorage(IFormFile file);

        public Task<Stream> GetDocumentStream(Guid id);
    }
}