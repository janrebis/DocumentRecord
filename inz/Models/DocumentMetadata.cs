using System.ComponentModel.DataAnnotations;
using inz.DocumentExceptions;
using inz.Models.Enums;

namespace inz.Models
{
    public class DocumentMetadata
    {
        [Key]
        public int Id { get; set; }

        public Guid PublicId { get; set; } = Guid.NewGuid();

        public string DocumentName { get; set; } = string.Empty;

        public string BlobKey { get; set; } = string.Empty;

        public ProcessStatus ProcessingStatus { get; private set; } = ProcessStatus.NEW_FILE;
        public int OwnerId { get; private set; }
        public int OrganizationId { get; private set; }

        public void StartProcessing()
        {
            if (ProcessingStatus != ProcessStatus.NEW_FILE)
                throw new DocumentWrongStatusException($"{DocumentName}: Dokument nie jest nowym plikiem.", DocumentName);

            ProcessingStatus = ProcessStatus.PROCESSING;
        }

        public void FinishProcessing(string blobKey)
        {
            if (ProcessingStatus != ProcessStatus.PROCESSING)
                throw new DocumentWrongStatusException($"{DocumentName}: Dokument nie jest w trakcie dodawania.", DocumentName);

            BlobKey = blobKey;
            ProcessingStatus = ProcessStatus.AVAILABLE;
        }

        public void FailProcessing()
        {
            if (ProcessingStatus != ProcessStatus.PROCESSING)
                throw new DocumentWrongStatusException($"{DocumentName}: Dokument nie jest w trakcie dodawania.", DocumentName);

            ProcessingStatus = ProcessStatus.FAILED_TO_ADD;
        }

        public void MarkToDelete()
        {
            if (ProcessingStatus != ProcessStatus.AVAILABLE)
                throw new DocumentUnavailableException($"{DocumentName}: Dokument nie może zostać oznaczony do usunięcia.", DocumentName);

            ProcessingStatus = ProcessStatus.MARKED_TO_DELETE;
        }

        public void EnsureMarkedToDelete()
        {
            if (ProcessingStatus != ProcessStatus.MARKED_TO_DELETE)
                throw new DocumentUnavailableException($"{DocumentName}: Dokument nie jest oznaczony do usunięcia.", DocumentName);
        }

        public void MarkDeleted()
        {
            if (ProcessingStatus != ProcessStatus.MARKED_TO_DELETE)
                throw new DocumentWrongStatusException($"{DocumentName}: Nie można oznaczyć dokumentu jako usunięty.", DocumentName);

            ProcessingStatus = ProcessStatus.DELETED;
        }

        public void FailDelete()
        {
            if (ProcessingStatus != ProcessStatus.MARKED_TO_DELETE)
                throw new DocumentWrongStatusException($"{DocumentName}: Nie można oznaczyć usuwania jako nieudanego.", DocumentName);

            ProcessingStatus = ProcessStatus.FAILED_TO_DELETE;
        }

        public void StartUpdate()
        {
            if (ProcessingStatus != ProcessStatus.AVAILABLE)
                throw new DocumentUnavailableException($"{DocumentName}: Dokument nie jest dostępny do aktualizacji.", DocumentName);

            ProcessingStatus = ProcessStatus.PROCESSING_UPDATE;
        }

        public void FinishUpdate(DocumentMetadata newMetadata)
        {
            if (ProcessingStatus != ProcessStatus.PROCESSING_UPDATE)
                throw new DocumentWrongStatusException($"{DocumentName}: Dokument nie jest w trakcie aktualizacji.", DocumentName);

            DocumentName = newMetadata.DocumentName;
            ProcessingStatus = ProcessStatus.AVAILABLE;
        }

        public void FailUpdate()
        {
            if (ProcessingStatus != ProcessStatus.PROCESSING_UPDATE)
                throw new DocumentWrongStatusException($"{DocumentName}: Dokument nie jest w trakcie aktualizacji.", DocumentName);

            ProcessingStatus = ProcessStatus.FAILED_TO_UPDATE;
        }

        public void AssignOwnership(int ownerId, int organizationId)
        {
            if (organizationId <= 0)
                throw new ArgumentException("OwnerId jest wymagany.", nameof(ownerId));

            if (organizationId <= 0)
                throw new ArgumentException("OrganizationId jest wymagany.", nameof(organizationId));

            OwnerId = ownerId;
            OrganizationId = organizationId;
        }

        public void FailUpdateFromAnyUpdateState()
        {
            if (ProcessingStatus != ProcessStatus.PROCESSING_UPDATE &&
                ProcessingStatus != ProcessStatus.AVAILABLE)
            {
                throw new DocumentWrongStatusException(
                    $"{DocumentName}: Nie można oznaczyć aktualizacji jako nieudanej.",
                    DocumentName);
            }

            ProcessingStatus = ProcessStatus.FAILED_TO_UPDATE;
        }
    }
}