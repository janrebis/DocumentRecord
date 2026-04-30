using System.ComponentModel.DataAnnotations;
using inz.Models;

namespace inz.Service
{
    public class DocumentMetadata
    {
        [Key]
        public int Id { get; set; }
        public Guid PublicId { get; set; } = Guid.NewGuid();
        public string DocumentName { get; set; } = string.Empty;
        public string BlobKey { get; set; } = default!;
        public ProcessStatus ProcessingStatus { get; set; } = ProcessStatus.PROCESSING;
        
    }
}