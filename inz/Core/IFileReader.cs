using inz.Service;

namespace inz.Core
{
    public interface IFileReader
    {
        public Task<FileMetadata> ReadFile(IFormFile file);
    }
}