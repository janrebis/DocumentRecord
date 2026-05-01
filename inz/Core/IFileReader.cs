using inz.Service;

namespace inz.Core
{
    public interface IFileReader
    {
        public Task<DocumentMetadata> ReadFileAsync(IFormFile file);
    }
}