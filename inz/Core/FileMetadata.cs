using inz.Core;

namespace inz.Service
{
    public class FileMetadata
    {
        public Guid Id { get; } = Guid.NewGuid();
        public ProcessStatus ProcessingStatus { get; set; } = ProcessStatus.PROCESSING;
    }
}