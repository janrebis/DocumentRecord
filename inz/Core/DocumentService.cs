using System.Security.Cryptography.X509Certificates;
using inz.Core;
using inz.Core.DocumentExceptions;
using inz.Models;

namespace inz.Service
{
    public class DocumentService
    {
        #region fields
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf",
            ".docx",
            ".txt"
        };
        private readonly IDocumentRepository _documentRepository;
        private readonly IDocumentContentStorage _storage;
        private readonly IFileReader _fileReader;
        private readonly ILogger<DocumentService> _logger;
        #endregion
        #region constructor
        public DocumentService(IDocumentRepository documentRepository, IDocumentContentStorage storage, IFileReader fileReader, ILogger<DocumentService> logger) 
        { 
            _documentRepository = documentRepository;
            _storage = storage; 
            _fileReader = fileReader;
            _logger = logger;
        }
        #endregion
        #region AddDocumentAsync
        public async Task<Guid> AddDocumentAsync(IFormFile file)
        {
            var metadata = await ValidateInputDocumentMetadata(file);
            var metadataSaved = false;
            var documentName = metadata.DocumentName;

            _logger.LogInformation($"{documentName}: Rozpoczęto dodawanie dokumentu", documentName);

            try
            {
                metadata.ProcessingStatus = ProcessStatus.PROCESSING;
                await _documentRepository.AddDocumentMetadata(metadata);
                metadataSaved = true;

                await _storage.AddDocumentToStorage(file);
                await _documentRepository.UpdateMetadataProcessingStatus(metadata.Id, ProcessStatus.AVAILABLE);
                _logger.LogInformation($"{documentName}: Zakończono dodawanie dokumentu. Jest teraz w pełni dostępny", documentName);
            }

            catch (Exception e)
            {
                if (metadataSaved) await _documentRepository.UpdateMetadataProcessingStatus(metadata.Id, ProcessStatus.FAILED);

                _logger.LogError(e, $"{documentName}: Dodawanie dokumentu zakończono błędem", documentName);
                throw new DocumentProcessingFailure($"{documentName}: Wystąpił błąd podczas dodawania dokumentu.", documentName, e);
            }

            return metadata.Id;
        }
        #endregion
        #region GetDocumentMetadataByIdAsync
        public async Task<Stream> GetDocumentMetadataByIdAsync(Guid documentId)
        {
            var metadata = await _documentRepository.GetMetadaById(documentId);
            ValidateMetadata(metadata, ProcessStatus.AVAILABLE);

            try
            {
                return await _storage.GetDocumentStream(metadata.BlobKey);
            }
            catch (Exception ex)
            {
                throw new DocumentRetrievalFailureException($"{metadata.DocumentName}: Nie udało się pobrać dokumentu.", metadata.DocumentName, ex);
            }
        }
        #endregion
        #region DeleteDocumentAsync
        public async Task DeleteDocumentAsync(Guid documentId)
        {
            var metadata = await _documentRepository.GetMetadaById(documentId);
            ValidateMetadata(metadata, ProcessStatus.MARKED_TO_DELETE);

            var metadataId = metadata.Id;
            var documentName = metadata.DocumentName;
            var blobKey = metadata.BlobKey;

            var blobExists = await _storage.ExistsAsync(blobKey);
            if (!blobExists)
            {
                await _documentRepository.UpdateMetadataProcessingStatus(metadataId, ProcessStatus.DELETED);
                return;
            }
            try
            {
              await _storage.DeleteAsync(blobKey);
              await _documentRepository.UpdateMetadataProcessingStatus(metadataId, ProcessStatus.DELETED);
            } catch(Exception e)
            {
                _logger.LogError(e, "{documentName}: Nie udało się usunąć dokumentu z magazynu. Oznaczono jako FAILED_TO_DELETE", documentName);
                await _documentRepository.UpdateMetadataProcessingStatus(metadataId, ProcessStatus.FAILED_TO_DELETE);
                throw new DocumentDeletionFailureException($"{documentName}: Wystąpił błąd podczas usuwania dokumentu.", documentName, e);
            }

        }

        #endregion
        #region privateMethods
        private async Task<DocumentMetadata> ValidateInputDocumentMetadata(IFormFile file) {

            if (file == null) throw new ArgumentNullException(nameof(file), "Nie przekazano żadnego pliku");
            if (file.Length == 0) throw new EmptyDocumentException("Plik jest pusty");

            var extension = Path.GetExtension(file.FileName);

            if (!AllowedExtensions.Contains(extension)) throw new UnsupportedDocumentTypeException("Nieobsługiwany typ pliku.");

            //TODO: dodać implementacje ReadFile, aby zwracało poprawne metadane, ustawiam domyślnie status processing w Modelu obiektu
            return await _fileReader.ReadFile(file);
        }

        private void ValidateMetadata(DocumentMetadata? documentMetadata, ProcessStatus expectedStatus)
        {
            if (documentMetadata is null) throw new DocumentNotFoundException("Nie znaleziono szukanego dokumentu.");
            if (documentMetadata.ProcessingStatus != expectedStatus) throw new DocumentUnavailableException($"{documentMetadata.DocumentName}: Dokument aktualnie nie jest dostępny do tej akcji", documentMetadata.DocumentName);
        }
        #endregion

    }
}
