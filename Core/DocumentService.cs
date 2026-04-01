using inz.Core;
using inz.Core.DocumentExceptions;

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

        public async Task<Guid> AddDocumentAsync(IFormFile file)
        {
            var metadata = await ValidateInputDocument(file);
            var metadataSaved = false;
            var fileName = file.FileName;

            _logger.LogInformation("{FileName}: Rozpoczęto dodawanie dokumentu", file.FileName);

            try
            {
                metadata.ProcessingStatus = ProcessStatus.PROCESSING;
                await _documentRepository.AddDocumentMetadata(metadata);
                metadataSaved = true;
                
                await _storage.AddDocumentToStorage(file);
                await _documentRepository.UpdateMetadataProcessingStatus(metadata.Id, ProcessStatus.AVAILABLE);
                _logger.LogInformation("{FileName}: Zakończono dodawanie dokumentu. Jest teraz w pełni dostępny", fileName);
            }

            catch (Exception e){
                if(metadataSaved) await _documentRepository.UpdateMetadataProcessingStatus(metadata.Id, ProcessStatus.FAILED);

                _logger.LogError(e, "{FileName}: Dodawanie dokumentu zakończono błędem", fileName);
                throw new DocumentProcessingFailure(" Wystąpił błąd podczas dodawania dokumentu.", fileName, e);
            }

            return metadata.Id;

        }

        private async Task<FileMetadata> ValidateInputDocument(IFormFile file) {

            if (file == null) throw new ArgumentNullException(nameof(file), "Nie przekazano żadnego pliku");
            if (file.Length == 0) throw new EmptyDocumentException("Plik jest pusty");

            var extension = Path.GetExtension(file.FileName);

            if (!AllowedExtensions.Contains(extension)) throw new UnsupportedDocumentTypeException("Nieobsługiwany typ pliku.");

            //TODO: dodać implementacje ReadFile, aby zwracało poprawne metadane, ustawiam domyślnie status processing w Modelu obiektu
            return await _fileReader.ReadFile(file);
        }
        



    }
}
