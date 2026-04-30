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
        /// <summary>
        /// Dodaje dokument do bazy danych. Pobiera z IFormFile metadane, waliduje prywatną metodą, następnie próbuje zapisać metadane do sql i plik do magazynu
        /// </summary>
        /// <param name="file">Dokument do zapisu</param>
        /// <returns>Identyfikator dokumentu</returns>
        /// <exception cref="DocumentProcessingFailureException">Wyrzucany w przypadku błędu podczas dodawania dokumentu</exception>
        public async Task<int> AddDocumentAsync(IFormFile file)
        {
            var metadata = await ValidateInputDocumentMetadata(file); 
            var metadataSaved = false; // ustawiam na true, żeby łatwiej obsłużyć sytuację, gdy błąd wystąpi po zapisaniu metadanych, a przed zapisaniem pliku do magazynu
            var documentName = metadata.DocumentName; //pobieram nazwe dokumentu do logów

            _logger.LogInformation($"{documentName}: Rozpoczęto dodawanie dokumentu", documentName);

            try
            {
                metadata.ProcessingStatus = ProcessStatus.PROCESSING; //ustawiam status na processing, żeby w bazie był od razu widoczny, że dokument jest w trakcie dodawania
                await _documentRepository.AddDocumentMetadata(metadata); 
                metadataSaved = true; //ustawiam true jeśli uda sie zapis do bazy

                await _storage.AddDocumentToStorage(file); 
                await _documentRepository.UpdateMetadataProcessingStatus(metadata.Id, ProcessStatus.AVAILABLE); 
                _logger.LogInformation($"{documentName}: Zakończono dodawanie dokumentu. Jest teraz w pełni dostępny", documentName);
            }

            catch (Exception e)
            {
                if (metadataSaved) await _documentRepository.UpdateMetadataProcessingStatus(metadata.Id, ProcessStatus.FAILED);

                _logger.LogError(e, $"{documentName}: Dodawanie dokumentu zakończono błędem", documentName);
                throw new DocumentProcessingFailureException($"{documentName}: Wystąpił błąd podczas dodawania dokumentu.", documentName, e);
            }

            return metadata.Id;
        }
        #endregion
        #region GetDocumentByIdAsync
        /// <summary>
        /// Metoda pobiera dane dokumentu na podstawie jego identyfikatora
        /// </summary>
        /// <param name="documentId">Identyfikator dokumentu</param>
        /// <returns>Strumień z danymi dokumentu</returns>
        /// <exception cref="DocumentRetrievalFailureException">Rzucany w przypadku błędu podczas pobierania dokumentu</exception>
        public async Task<Stream> GetDocumentByIdAsync(int documentId)
        {
            var metadata = await _documentRepository.GetMetadaById(documentId); 
            ValidateMetadata(metadata, ProcessStatus.AVAILABLE); //Sprawdzam czy dokument jest dostępny do pobrania i odczytu

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
        /// <summary>
        /// Metoda usuwa dokument z bazy danych
        /// </summary>
        /// <param name="documentId">Identyfikator dokumentu</param>
        /// <returns></returns>
        /// <exception cref="DocumentDeletionFailureException">Rzucany w przypadku błędu podczas usuwania dokumentu</exception>
        public async Task DeleteDocumentAsync(int documentId)
        {
            var metadata = await _documentRepository.GetMetadaById(documentId);
            ValidateMetadata(metadata, ProcessStatus.MARKED_TO_DELETE); //Sprawdzam czy dokument oznaczony do usunięcia, zabezpieczenie przed przypadkowym usunięciem dokumentu, który nie jest oznaczony do usunięcia

            //pobieram potrzebne dane do logów i usuwania z magazynu
            var metadataId = metadata.Id;
            var documentName = metadata.DocumentName;
            var blobKey = metadata.BlobKey;

            var blobExists = await _storage.ExistsAsync(blobKey);
            if (!blobExists) 
            {
                //Jeśli dokumentu nie ma już w magazynie, to ustawiam metadane jako usunięte, aby nie było niespójności między magazynem a bazą danych, i kończę metodę, bo nie ma już dokumentu do usunięciaS
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
        #region UpdateDocumentAsync

        public async Task<int> UpdateDocumentAsync(int documentId, IFormFile file)
        {
            _logger.LogInformation("Rozpoczęcie aktualizacji dokumentu o id {DocumentId}.", documentId);

            var newMetadata = await ValidateInputDocumentMetadata(file);
            var existingMetadata = await _documentRepository.GetMetadaById(documentId);
            ValidateMetadata(existingMetadata, ProcessStatus.AVAILABLE);

            var metadataId = existingMetadata.Id;
            var documentName = existingMetadata.DocumentName;
            var blobKey = existingMetadata.BlobKey;

            await _documentRepository.UpdateMetadataProcessingStatus( metadataId, ProcessStatus.PROCESSING_UPDATE);

            var blobExists = await _storage.ExistsAsync(blobKey);

            if (!blobExists)
            {
                await _documentRepository.UpdateMetadataProcessingStatus( metadataId, ProcessStatus.FAILED_UPDATE);

                _logger.LogWarning("Dokument o id {DocumentId} nie został znaleziony w magazynie. Ustawiono status FAILED_UPDATE.", metadataId);
                throw new DocumentNotFoundException("Dokument nie został znaleziony w magazynie.");
            }

            try
            {
                await _storage.UpdateDocumentInStorageAsync(blobKey, file);

                newMetadata.Id = metadataId;
                newMetadata.BlobKey = blobKey;
                newMetadata.ProcessingStatus = ProcessStatus.AVAILABLE;

                await _documentRepository.UpdateDocumentMetadataAsync(metadataId, newMetadata);
            }
            catch (Exception e)
            {
                await _documentRepository.UpdateMetadataProcessingStatus( metadataId, ProcessStatus.FAILED_UPDATE);
                _logger.LogError( e, "{DocumentName}: Wystąpił błąd podczas aktualizacji dokumentu.", documentName);
                throw new DocumentProcessingFailureException($"{documentName}: Wystąpił błąd podczas aktualizacji dokumentu.", documentName, e);
            }
            _logger.LogInformation( "Zakończono aktualizację dokumentu o id {DocumentId}.", metadataId);

            return metadataId;
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
            if (documentMetadata is null) throw new DocumentMetadataNotFoundException("Nie znaleziono danych dokumentu.");
            if (documentMetadata.ProcessingStatus != expectedStatus) throw new DocumentUnavailableException($"{documentMetadata.DocumentName}: Dokument aktualnie nie jest dostępny do tej akcji", documentMetadata.DocumentName);
        }
        #endregion

    }
}
