using inz.Models;

namespace inz.Service
{
    public class DocumentMetadata
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string DocumentName { get; set; } = string.Empty;
        public string BlobKey { get; set; } = default!;
        public ProcessStatus ProcessingStatus { get; set; } = ProcessStatus.PROCESSING;
        
    }
}