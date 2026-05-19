using inz.DocumentExceptions;
using inz.Models;
using inz.Repository.Interface;

namespace inz.Repository.Implementations;

public class InMemoryDocumentRepository : IDocumentRepository
{
    private readonly Dictionary<int, DocumentMetadata> _database = new();
    private int _idIncrementator = 1;

    public Task<int> AddDocumentMetadataAsync(DocumentMetadata documentMetadata)
    {
        var id = _idIncrementator;

        documentMetadata.Id = id;

        _database.Add(id, documentMetadata);

        _idIncrementator++;

        return Task.FromResult(id);
    }

    public Task<DocumentMetadata?> GetMetadataByIdAsync(int documentMetadataId)
    {
        _database.TryGetValue(documentMetadataId, out var document);

        return Task.FromResult(document);
    }

    public Task UpdateDocumentMetadataAsync(int documentMetadataId, DocumentMetadata documentMetadata)
    {
        if (!_database.ContainsKey(documentMetadataId))
            throw new DocumentMetadataNotFoundException("Nie znaleziono danych dokumentu.");

        documentMetadata.Id = documentMetadataId;

        _database[documentMetadataId] = documentMetadata;

        return Task.CompletedTask;
    }
}