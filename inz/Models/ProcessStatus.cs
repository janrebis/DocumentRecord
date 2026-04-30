namespace inz.Models
{
    public enum ProcessStatus
    {
        PROCESSING,
        AVAILABLE,
        FAILED,
        MARKED_TO_DELETE,
        DELETED,
        FAILED_TO_DELETE,
        PROCESSING_UPDATE,
        FAILED_UPDATE
    }
}
