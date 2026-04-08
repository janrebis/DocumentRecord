using System.Security.Cryptography.X509Certificates;
using inz.Core;
using inz.Core.DocumentExceptions;
using inz.Models;

namespace inz.Service
{
    public class DocumentService
    {
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

        public DocumentService(IDocumentRepository documentRepository, IDocumentContentStorage storage, IFileReader fileReader, ILogger<DocumentService> logger) 
        { 
            _documentRepository = documentRepository;
            _storage = storage; 
            _fileReader = fileReader;
            _logger = logger;
        }
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
        #region GetDocumentByIdAsync
        public async Task<Stream> GetDocumentByIdAsync(Guid documentId)
        {
            var metadata = await _documentRepository.GetMetadaById(documentId);
            ValidateMetadata(metadata);

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

        #region privateMethods
        private async Task<DocumentMetadata> ValidateInputDocumentMetadata(IFormFile file) {

            if (file == null) throw new ArgumentNullException(nameof(file), "Nie przekazano żadnego pliku");
            if (file.Length == 0) throw new EmptyDocumentException("Plik jest pusty");

            var extension = Path.GetExtension(file.FileName);

            if (!AllowedExtensions.Contains(extension)) throw new UnsupportedDocumentTypeException("Nieobsługiwany typ pliku.");

            //TODO: dodać implementacje ReadFile, aby zwracało poprawne metadane, ustawiam domyślnie status processing w Modelu obiektu
            return await _fileReader.ReadFile(file);
        }

        private void ValidateMetadata(DocumentMetadata? documentMetadata)
        {
            if (documentMetadata is null) throw new DocumentNotFoundException("Nie znaleziono szukanego dokumentu.");
            if (documentMetadata.ProcessingStatus != ProcessStatus.AVAILABLE) throw new DocumentUnavailableException($"{documentMetadata.DocumentName}: Dokument aktualnie nie jest dostępny", documentMetadata.DocumentName);
        }
        #endregion




    }
}
