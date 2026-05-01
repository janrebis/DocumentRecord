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

        public DocumentService(
            IDocumentRepository documentRepository,
            IDocumentContentStorage storage,
            IFileReader fileReader,
            ILogger<DocumentService> logger)
        {
            _documentRepository = documentRepository;
            _storage = storage;
            _fileReader = fileReader;
            _logger = logger;
        }

        public async Task<int> AddDocumentAsync(CreateDocumentCommand command)
        {
            ValidateCommand(command);

            var metadata = await ValidateInputDocumentMetadataAsync(command.File);

            metadata.AssignOwnership(command.OwnerId, command.OrganizationId);

            return await AddDocumentCoreAsync(metadata, command.File);
        }
        public async Task<Stream> GetDocumentByIdAsync(int documentId)
        {
            var metadata = await _documentRepository.GetMetadataByIdAsync(documentId);

            ValidateMetadata(metadata, ProcessStatus.AVAILABLE);

            try
            {
                return await _storage.GetDocumentStreamAsync(metadata.BlobKey);
            }
            catch (Exception ex)
            {
                throw new DocumentRetrievalFailureException(
                    $"{metadata.DocumentName}: Nie udało się pobrać dokumentu.",
                    metadata.DocumentName,
                    ex);
            }
        }

        public async Task MarkDocumentToDeleteAsync(int documentId)
        {
            var metadata = await _documentRepository.GetMetadataByIdAsync(documentId);

            ValidateMetadata(metadata, ProcessStatus.AVAILABLE);

            metadata.MarkToDelete();

            await _documentRepository.UpdateMetadataProcessingStatusAsync(
                metadata.Id,
                metadata.ProcessingStatus);

            _logger.LogInformation(
                "{DocumentName}: Dokument oznaczono do usunięcia.",
                metadata.DocumentName);
        }

        public async Task DeleteDocumentAsync(int documentId)
        {
            var metadata = await _documentRepository.GetMetadataByIdAsync(documentId);

            if (metadata is null)
                throw new DocumentMetadataNotFoundException("Nie znaleziono danych dokumentu.");

            metadata.EnsureMarkedToDelete();

            var metadataId = metadata.Id;
            var documentName = metadata.DocumentName;
            var blobKey = metadata.BlobKey;

            try
            {
                var blobExists = await _storage.ExistsAsync(blobKey);

                if (blobExists)
                {
                    await _storage.DeleteAsync(blobKey);
                }

                metadata.MarkDeleted();

                await _documentRepository.UpdateMetadataProcessingStatusAsync(
                    metadataId,
                    metadata.ProcessingStatus);

                _logger.LogInformation("{DocumentName}: Dokument usunięty.", documentName);
            }
            catch (Exception ex)
            {
                metadata.FailDelete();

                await _documentRepository.UpdateMetadataProcessingStatusAsync(
                    metadataId,
                    metadata.ProcessingStatus);

                _logger.LogError(
                    ex,
                    "{DocumentName}: Wystąpił błąd podczas usuwania dokumentu.",
                    documentName);

                throw new DocumentDeletionFailureException(
                    $"{documentName}: Wystąpił błąd podczas usuwania dokumentu.",
                    documentName,
                    ex);
            }
        }

        public async Task<int> UpdateDocumentAsync(int documentId, IFormFile file)
        {
            _logger.LogInformation(
                "Rozpoczęto aktualizację dokumentu o id {DocumentId}.",
                documentId);

            var newMetadata = await ValidateInputDocumentMetadataAsync(file);

            var existingMetadata = await _documentRepository.GetMetadataByIdAsync(documentId);

            ValidateMetadata(existingMetadata, ProcessStatus.AVAILABLE);

            var metadataId = existingMetadata.Id;
            var documentName = existingMetadata.DocumentName;
            var blobKey = existingMetadata.BlobKey;

            try
            {
                existingMetadata.StartUpdate();

                await _documentRepository.UpdateMetadataProcessingStatusAsync(
                    metadataId,
                    existingMetadata.ProcessingStatus);

                var blobExists = await _storage.ExistsAsync(blobKey);

                if (!blobExists)
                {
                    existingMetadata.FailUpdate();

                    await _documentRepository.UpdateMetadataProcessingStatusAsync(
                        metadataId,
                        existingMetadata.ProcessingStatus);

                    throw new DocumentNotFoundException(
                        $"{documentName}: Dokument nie został znaleziony w magazynie.");
                }

                await _storage.UpdateDocumentInStorageAsync(blobKey, file);

                existingMetadata.FinishUpdate(newMetadata);

                await _documentRepository.UpdateDocumentMetadataAsync(
                    metadataId,
                    existingMetadata);

                _logger.LogInformation(
                    "Zakończono aktualizację dokumentu o id {DocumentId}.",
                    metadataId);

                return metadataId;
            }
            catch (DocumentNotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                try
                {
                    existingMetadata.FailUpdateFromAnyUpdateState();

                    await _documentRepository.UpdateMetadataProcessingStatusAsync(
                        metadataId,
                        existingMetadata.ProcessingStatus);
                }
                catch (Exception statusUpdateException)
                {
                    _logger.LogError(
                        statusUpdateException,
                        "{DocumentName}: Nie udało się oznaczyć aktualizacji jako nieudanej.",
                        documentName);
                }

                _logger.LogError(
                    ex,
                    "{DocumentName}: Wystąpił błąd podczas aktualizacji dokumentu.",
                    documentName);

                throw new DocumentProcessingFailureException(
                    $"{documentName}: Wystąpił błąd podczas aktualizacji dokumentu.",
                    documentName,
                    ex);
            }
        }

        private async Task<DocumentMetadata> ValidateInputDocumentMetadataAsync(IFormFile file)
        {
            if (file is null)
                throw new ArgumentNullException(nameof(file), "Nie przekazano żadnego pliku.");

            if (file.Length == 0)
                throw new EmptyDocumentException("Plik jest pusty.");

            var extension = Path.GetExtension(file.FileName);

            if (!AllowedExtensions.Contains(extension))
                throw new UnsupportedDocumentTypeException("Nieobsługiwany typ pliku.");

            return await _fileReader.ReadFileAsync(file);
        }

        private async Task<int> AddDocumentCoreAsync(DocumentMetadata metadata, IFormFile file)
        {
            var documentName = metadata.DocumentName;

            _logger.LogInformation("{DocumentName}: Rozpoczęto dodawanie dokumentu.", documentName);

            try
            {
                metadata.StartProcessing();

                await _documentRepository.AddDocumentMetadataAsync(metadata);

                var blobKey = await _storage.AddDocumentToStorageAsync(file);

                metadata.FinishProcessing(blobKey);

                await _documentRepository.UpdateDocumentMetadataAsync(metadata.Id, metadata);

                _logger.LogInformation("{DocumentName}: Zakończono dodawanie dokumentu.", documentName);

                return metadata.Id;
            }
            catch (Exception ex)
            {
                await TryMarkAddingAsFailedAsync(metadata, documentName, ex);

                throw new DocumentProcessingFailureException(
                    $"{documentName}: Wystąpił błąd podczas dodawania dokumentu.",
                    documentName,
                    ex);
            }
        }
        private static void ValidateMetadata(DocumentMetadata? documentMetadata, ProcessStatus expectedStatus)
        {
            if (documentMetadata is null)
                throw new DocumentMetadataNotFoundException("Nie znaleziono danych dokumentu.");

            if (documentMetadata.ProcessingStatus != expectedStatus)
                throw new DocumentUnavailableException(
                    $"{documentMetadata.DocumentName}: Dokument aktualnie nie jest dostępny do tej akcji.",
                    documentMetadata.DocumentName);
        }

        private async Task TryMarkAddingAsFailedAsync(
            DocumentMetadata metadata,
            string documentName,
            Exception originalException)
        {
            try
            {
                if (metadata.ProcessingStatus == ProcessStatus.PROCESSING)
                {
                    metadata.FailProcessing();

                    await _documentRepository.UpdateMetadataProcessingStatusAsync(
                        metadata.Id,
                        metadata.ProcessingStatus);
                }
            }
            catch (Exception statusUpdateException)
            {
                _logger.LogError(
                    statusUpdateException,
                    "{DocumentName}: Nie udało się oznaczyć dodawania dokumentu jako nieudanego.",
                    documentName);
            }

            _logger.LogError(
                originalException,
                "{DocumentName}: Dodawanie dokumentu zakończono błędem.",
                documentName);
        }

        private static void ValidateCommand(CreateDocumentCommand command)
        {
            if (command is null)
                throw new ArgumentNullException(nameof(command));

            if (command.File is null)
                throw new ArgumentNullException(nameof(command.File), "Nie przekazano pliku.");

            if (string.IsNullOrWhiteSpace(command.OwnerId))
                throw new UnauthorizedAccessException("Brak identyfikatora użytkownika.");

            if (command.OrganizationId <= 0)
                throw new UnauthorizedAccessException("Brak identyfikatora organizacji.");
        }
    }
}