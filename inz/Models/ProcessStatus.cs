namespace inz.Models
{
    public enum ProcessStatus
    {
        NEW_FILE,
        PROCESSING,
        AVAILABLE,
        FAILED_TO_ADD,
        MARKED_TO_DELETE,
        DELETED,
        FAILED_TO_DELETE,
        PROCESSING_UPDATE,
        FAILED_TO_UPDATE
    }
}