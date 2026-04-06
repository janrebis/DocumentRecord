namespace inz.Service
{
    public interface IFileReader
    {
        public Task<FileMetadata> ReadFile(IFormFile file);
    }
}