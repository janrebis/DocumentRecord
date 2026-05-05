namespace inz.Models
{
    public sealed class CreateDocumentCommand
    {
        public IFormFile File { get; init; } = default!;
        public int OwnerId { get; init; } = default;
        public int OrganizationId { get; init; }
    }
}
