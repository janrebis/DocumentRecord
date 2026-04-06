namespace inz.Service
{
    public interface IDocumentContentStorage
    {
        public Task AddDocumentToStorage(IFormFile file);
    }
}