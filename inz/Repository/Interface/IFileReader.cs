using inz.Models;

namespace inz.Repository.Interface
{
    public interface IFileReader
    {
        public Task<DocumentMetadata> ReadFileAsync(IFormFile file);
    }
}