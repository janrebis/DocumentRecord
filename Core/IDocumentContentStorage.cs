namespace inz.Service
{
    public interface IDocumentContentStorage
    {
        public Task<Object> AddDocumentToStorage(IFormFile file);
    }
}