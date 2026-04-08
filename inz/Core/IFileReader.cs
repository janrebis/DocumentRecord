using inz.Service;

namespace inz.Core
{
    public interface IFileReader
    {
        public Task<DocumentMetadata> ReadFile(IFormFile file);
    }
}