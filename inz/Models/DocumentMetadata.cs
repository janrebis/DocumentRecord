using System.ComponentModel.DataAnnotations;
using inz.Core.DocumentExceptions;
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
        public ProcessStatus ProcessingStatus { get; private set; } = ProcessStatus.NEW_FILE;

        public void StartProcessing() 
        {
            if (ProcessingStatus != ProcessStatus.NEW_FILE) throw new DocumentWrongStatusException($"{DocumentName}: Nie można zakończyć dodawania dokumentu, bo nie jest nowym dokumentem.", DocumentName);
            ProcessingStatus = ProcessStatus.PROCESSING; 
        }
        public void FinishProcessing() 
        {
            if (ProcessingStatus != ProcessStatus.PROCESSING)  throw new DocumentWrongStatusException($"{DocumentName}: Nie można zakończyć dodawania dokumentu, bo dokument nie jest przetwarzany.", DocumentName);
            ProcessingStatus = ProcessStatus.AVAILABLE; 
        }
        public void FailProcessing() 
        {
            if (ProcessingStatus != ProcessStatus.PROCESSING) throw new DocumentWrongStatusException($"{DocumentName}: Nie można oznaczyć dodawania jako nieudanego, bo dokument nie jest przetwarzany.", DocumentName);
            ProcessingStatus = ProcessStatus.FAILED_TO_ADD; 
        }
        public void EnsureMarkedToDelete()
        {
            if (ProcessingStatus != ProcessStatus.MARKED_TO_DELETE) throw new DocumentUnavailableException( $"{DocumentName}: Dokument nie jest oznaczony do usunięcia.", DocumentName);
        }

        public void MarkDeleted()
        {
            if (ProcessingStatus != ProcessStatus.MARKED_TO_DELETE) throw new DocumentWrongStatusException( $"{DocumentName}: Nie można oznaczyć jako usunięty.", DocumentName);
            ProcessingStatus = ProcessStatus.DELETED;
        }

        public void FailDelete()
        {
            if (ProcessingStatus != ProcessStatus.MARKED_TO_DELETE) throw new DocumentWrongStatusException($"{DocumentName}: Nie można oznaczyć usuwania jako nieudanego.", DocumentName);
            ProcessingStatus = ProcessStatus.FAILED_TO_DELETE;
        }

        public void StartUpdate()
        {
            if (ProcessingStatus != ProcessStatus.AVAILABLE) throw new DocumentUnavailableException($"{DocumentName}: Dokument nie jest dostępny do aktualizacji.", DocumentName);
            ProcessingStatus = ProcessStatus.PROCESSING_UPDATE;
        }

        public void FinishUpdate()
        {
            if (ProcessingStatus != ProcessStatus.PROCESSING_UPDATE) throw new DocumentWrongStatusException($"{DocumentName}: Nie można zakończyć aktualizacji, bo dokument nie jest w trakcie aktualizacji.", DocumentName);
            ProcessingStatus = ProcessStatus.AVAILABLE;
        }

        public void FailUpdate()
        {
            if (ProcessingStatus != ProcessStatus.PROCESSING_UPDATE) throw new DocumentWrongStatusException($"{DocumentName}: Nie można oznaczyć aktualizacji jako nieudanej, bo dokument nie jest w trakcie aktualizacji.", DocumentName);
            ProcessingStatus = ProcessStatus.FAILED_TO_UPDATE;
        }
    }
}