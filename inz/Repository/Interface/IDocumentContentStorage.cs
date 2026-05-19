namespace inz.Repository.Interface;

public interface IDocumentContentStorage
{
    Task<string> AddDocumentToStorageAsync(IFormFile file);
    Task<Stream> GetDocumentStreamAsync(string blobKey);
    Task DeleteAsync(string blobKey);
    Task<bool> ExistsAsync(string blobKey);
    Task UpdateDocumentInStorageAsync(string blobKey, IFormFile file);
}