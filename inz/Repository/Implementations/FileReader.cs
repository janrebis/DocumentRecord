using inz.Models;
using inz.Repository.Interface;

namespace inz.Repository.Implementations;

public class FileReader : IFileReader
{
    public Task<DocumentMetadata> ReadFileAsync(IFormFile file)
    {
        var metadata = new DocumentMetadata
        {
            DocumentName = file.FileName
        };

        return Task.FromResult(metadata);
    }
}