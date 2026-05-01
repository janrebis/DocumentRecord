namespace inz.Core
{
    public sealed class CreateDocumentCommand
    {
        public IFormFile File { get; init; } = default!;
        public string OwnerId { get; init; } = string.Empty;
        public int OrganizationId { get; init; }
    }
}
