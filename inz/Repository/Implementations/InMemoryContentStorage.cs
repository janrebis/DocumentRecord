using inz.DocumentExceptions;
using inz.Repository.Interface;

namespace inz.Repository.Implementations;

public class InMemoryContentStorage : IDocumentContentStorage
{
    private readonly Dictionary<string, byte[]> _storage = new();

    public async Task<string> AddDocumentToStorageAsync(IFormFile file)
    {
        var blobKey = Guid.NewGuid().ToString();

        await using var inputStream = file.OpenReadStream();
        using var memoryStream = new MemoryStream();

        await inputStream.CopyToAsync(memoryStream);

        _storage[blobKey] = memoryStream.ToArray();

        return blobKey;
    }

    public Task<Stream> GetDocumentStreamAsync(string blobKey)
    {
        if (!_storage.TryGetValue(blobKey, out var content))
            throw new DocumentNotFoundException("Nie znaleziono pliku w magazynie.");

        Stream stream = new MemoryStream(content);

        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string blobKey)
    {
        _storage.Remove(blobKey);

        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string blobKey)
    {
        return Task.FromResult(_storage.ContainsKey(blobKey));
    }

    public async Task UpdateDocumentInStorageAsync(string blobKey, IFormFile file)
    {
        if (!_storage.ContainsKey(blobKey))
            throw new DocumentNotFoundException("Nie znaleziono pliku w magazynie.");

        await using var inputStream = file.OpenReadStream();
        using var memoryStream = new MemoryStream();

        await inputStream.CopyToAsync(memoryStream);

        _storage[blobKey] = memoryStream.ToArray();
    }
}