using System.Reflection.Metadata;
using System.Security.Cryptography;
using FluentAssertions;
using inz.Core;
using inz.Core.DocumentExceptions;
using inz.Models;
using inz.Service;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;


namespace inz.Tests
{

    public class DocumentServiceTests
    {

        
        

        private readonly Mock<IDocumentRepository> _documentRepositoryMock = new();
        private readonly Mock<IDocumentContentStorage> _storageMock = new();
        private readonly Mock<IFileReader> _fileReaderMock = new();
        private readonly Mock<ILogger<DocumentService>> _loggerMock = new();

        private readonly DocumentService _documentService;

        public DocumentServiceTests()
        {
            _documentService = new DocumentService(
                _documentRepositoryMock.Object,
                _storageMock.Object,
                _fileReaderMock.Object,
                _loggerMock.Object);
        }

        #region AddDocumentTests
        /// <summary>
        /// Założenia:
        /// 1. Dokument jest dodawany do bazy danych
        /// 2. Dokument jest poprawnie zwracany po wydaniu
        /// 3. Dokument jest dodany: metedane do SQL. plik do Azure Blop Storage 
        /// 4. Zapis dokumentu: 
        ///     - pobieram metadane
        ///     - próbuje zapisać do SQL ze statusem PROCESSING
        ///     - próbuje zapisać dokument do Blob
        ///     - jeśli się uda zapisać do blop, aktualizuje status SQL na Available
        /// 5. Jeśli nie powiedzie się dodanie dokumentu, to tworzymy log z błędem
        /// 9. Rzucany jest wyjątek o niedodaniu dokumentu 
        /// </summary>
        [Fact]
        public async Task AddDocumentAsync_ShouldAddMetadataAndUploadFile_WhenInputIsValid()
        {
            // Arrange
            var file = CreateFormFile("test.pdf", "dummy content");
            var metadata = CreateMetadata();

            _fileReaderMock
                .Setup(x => x.ReadFile(file))
                .ReturnsAsync(metadata);

            _documentRepositoryMock
                .Setup(x => x.AddDocumentMetadata(It.IsAny<DocumentMetadata>()))
                .Returns(Task.CompletedTask);

            _storageMock
                .Setup(x => x.AddDocumentToStorage(file))
                .Returns(Task.CompletedTask);

            _documentRepositoryMock
                .Setup(x => x.UpdateMetadataProcessingStatus(metadata.Id, ProcessStatus.AVAILABLE))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _documentService.AddDocumentAsync(file);

            // Assert
            result.Should().Be(metadata.Id);

            _fileReaderMock.Verify(x => x.ReadFile(file), Times.Once);

            _documentRepositoryMock.Verify(x =>
                x.AddDocumentMetadata(It.Is<DocumentMetadata>(m =>
                    m.Id == metadata.Id &&
                    m.ProcessingStatus == ProcessStatus.PROCESSING)),
                Times.Once);

            _storageMock.Verify(x => x.AddDocumentToStorage(file), Times.Once);

            _documentRepositoryMock.Verify(x =>
                x.UpdateMetadataProcessingStatus(metadata.Id, ProcessStatus.AVAILABLE),
                Times.Once);
        }

        [Fact]
        public async Task AddDocumentAsync_ShouldSetStatusToAvailable_WhenUploadSucceeds()
        {
            // Arrange
            var file = CreateFormFile("document.pdf");
            var metadata = CreateMetadata();

            _fileReaderMock
                .Setup(x => x.ReadFile(file))
                .ReturnsAsync(metadata);

            _documentRepositoryMock
                .Setup(x => x.AddDocumentMetadata(It.IsAny<DocumentMetadata>()))
                .Returns(Task.CompletedTask);

            _storageMock
                .Setup(x => x.AddDocumentToStorage(file))
                .Returns(Task.CompletedTask);

            _documentRepositoryMock
                .Setup(x => x.UpdateMetadataProcessingStatus(metadata.Id, ProcessStatus.AVAILABLE))
                .Returns(Task.CompletedTask);

            // Act
            await _documentService.AddDocumentAsync(file);

            // Assert
            _documentRepositoryMock.Verify(x =>
                x.UpdateMetadataProcessingStatus(metadata.Id, ProcessStatus.AVAILABLE),
                Times.Once);

            _documentRepositoryMock.Verify(x =>
                x.UpdateMetadataProcessingStatus(metadata.Id, ProcessStatus.FAILED),
                Times.Never);
        }

        [Fact]
        public async Task AddDocumentAsync_ShouldSetStatusToFailed_WhenStorageUploadFailsAfterMetadataSave()
        {
            // Arrange
            var file = CreateFormFile("document.pdf");
            var metadata = CreateMetadata();
            var storageException = new Exception("Storage failure");

            _fileReaderMock
                .Setup(x => x.ReadFile(file))
                .ReturnsAsync(metadata);

            _documentRepositoryMock
                .Setup(x => x.AddDocumentMetadata(It.IsAny<DocumentMetadata>()))
                .Returns(Task.CompletedTask);

            _storageMock
                .Setup(x => x.AddDocumentToStorage(file))
                .ThrowsAsync(storageException);

            _documentRepositoryMock
                .Setup(x => x.UpdateMetadataProcessingStatus(metadata.Id, ProcessStatus.FAILED))
                .Returns(Task.CompletedTask);

            // Act
            Func<Task> act = async () => await _documentService.AddDocumentAsync(file);

            // Assert
            await act.Should().ThrowAsync<DocumentProcessingFailure>();

            _documentRepositoryMock.Verify(x =>
                x.UpdateMetadataProcessingStatus(metadata.Id, ProcessStatus.FAILED),
                Times.Once);
        }

        [Fact]
        public async Task AddDocumentAsync_ShouldNotSetStatusToFailed_WhenMetadataSaveFails()
        {
            // Arrange
            var file = CreateFormFile("document.pdf");
            var metadata = CreateMetadata();
            var repositoryException = new Exception("SQL failure");

            _fileReaderMock
                .Setup(x => x.ReadFile(file))
                .ReturnsAsync(metadata);

            _documentRepositoryMock
                .Setup(x => x.AddDocumentMetadata(It.IsAny<DocumentMetadata>()))
                .ThrowsAsync(repositoryException);

            // Act
            Func<Task> act = async () => await _documentService.AddDocumentAsync(file);

            // Assert
            await act.Should().ThrowAsync<DocumentProcessingFailure>();

            _storageMock.Verify(x => x.AddDocumentToStorage(It.IsAny<IFormFile>()), Times.Never);

            _documentRepositoryMock.Verify(x =>
                x.UpdateMetadataProcessingStatus(It.IsAny<Guid>(), ProcessStatus.FAILED),
                Times.Never);
        }

        [Fact]
        public async Task AddDocumentAsync_ShouldThrowDocumentProcessingFailure_WhenStorageUploadFails()
        {
            // Arrange
            var file = CreateFormFile("document.pdf");
            var metadata = CreateMetadata();
            var storageException = new Exception("Blob upload failed");

            _fileReaderMock
                .Setup(x => x.ReadFile(file))
                .ReturnsAsync(metadata);

            _documentRepositoryMock
                .Setup(x => x.AddDocumentMetadata(It.IsAny<DocumentMetadata>()))
                .Returns(Task.CompletedTask);

            _storageMock
                .Setup(x => x.AddDocumentToStorage(file))
                .ThrowsAsync(storageException);

            _documentRepositoryMock
                .Setup(x => x.UpdateMetadataProcessingStatus(metadata.Id, ProcessStatus.FAILED))
                .Returns(Task.CompletedTask);

            // Act
            Func<Task> act = async () => await _documentService.AddDocumentAsync(file);

            // Assert
            var exception = await act.Should().ThrowAsync<DocumentProcessingFailure>();
            exception.Which.InnerException.Should().Be(storageException);
            exception.Which.Message.Should().Contain("Wystąpił błąd podczas dodawania dokumentu");
        }

        [Fact]
        public async Task AddDocumentAsync_ShouldThrowArgumentNullException_WhenFileIsNull()
        {
            // Act
            Func<Task> act = async () => await _documentService.AddDocumentAsync(null!);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>();

            _fileReaderMock.Verify(x => x.ReadFile(It.IsAny<IFormFile>()), Times.Never);
            _documentRepositoryMock.Verify(x => x.AddDocumentMetadata(It.IsAny<DocumentMetadata>()), Times.Never);
            _storageMock.Verify(x => x.AddDocumentToStorage(It.IsAny<IFormFile>()), Times.Never);
        }

        [Fact]
        public async Task AddDocumentAsync_ShouldThrowEmptyDocumentException_WhenFileIsEmpty()
        {
            // Arrange
            var file = CreateFormFile("empty.pdf", string.Empty);

            // Act
            Func<Task> act = async () => await _documentService.AddDocumentAsync(file);

            // Assert
            await act.Should().ThrowAsync<EmptyDocumentException>();

            _fileReaderMock.Verify(x => x.ReadFile(It.IsAny<IFormFile>()), Times.Never);
            _documentRepositoryMock.Verify(x => x.AddDocumentMetadata(It.IsAny<DocumentMetadata>()), Times.Never);
            _storageMock.Verify(x => x.AddDocumentToStorage(It.IsAny<IFormFile>()), Times.Never);
        }

        [Fact]
        public async Task AddDocumentAsync_ShouldThrowUnsupportedDocumentTypeException_WhenExtensionIsNotSupported()
        {
            // Arrange
            var file = CreateFormFile("malware.exe", "fake content");

            // Act
            Func<Task> act = async () => await _documentService.AddDocumentAsync(file);

            // Assert
            await act.Should().ThrowAsync<UnsupportedDocumentTypeException>();

            _fileReaderMock.Verify(x => x.ReadFile(It.IsAny<IFormFile>()), Times.Never);
            _documentRepositoryMock.Verify(x => x.AddDocumentMetadata(It.IsAny<DocumentMetadata>()), Times.Never);
            _storageMock.Verify(x => x.AddDocumentToStorage(It.IsAny<IFormFile>()), Times.Never);
        }

        #endregion

        #region GetDocumentByIdTests
        /// Założenia:
        /// 1. Dokument jest wwyszukiwany po identyfikatorze przekazanym w metodzie
        /// 2. Pobierane są metadane z sql
        /// 3. Jeśli nie znaleziono, to zwracamy DocumentNotFoundException
        /// 4. Jeśli znaleziono, ale status jest inny niż Available, to zwracamy DocumentUnavailableException
        /// 5. Jeśli dokument znaleziony i jest available, to pobieramy zawartość z Azure Blob i zwraacamy strumnieć
        /// 6. Jeśli wystąpi błąd podczas pobierania, to rzucamy DoocumentRetrievalFailureException
        /// 7. Jeśli wszystko poprawnie, to zwracamy strumień dokumentu

        [Fact]
        public async Task GetDocumentByIdAsync_ShouldThrowDocumentNotFoundException_WhenMetadataNotFound()
        {
            // Arrange
            var documentId = Guid.NewGuid();

            _documentRepositoryMock
                .Setup(x => x.GetMetadaById(documentId))
                .ReturnsAsync((DocumentMetadata?)null);

            // Act
            Func<Task> act = async () =>
                await _documentService.GetDocumentMetadataByIdAsync(documentId);

            // Assert
            await act.Should().ThrowAsync<DocumentNotFoundException>();
        }

        [Theory]
        [InlineData(ProcessStatus.PROCESSING)]
        [InlineData(ProcessStatus.FAILED)]
        public async Task GetDocumentByIdAsync_ShouldThrowDocumentUnavailableException_WhenStatusIsNotAvailable(ProcessStatus status)
        {
            // Arrange
            var metadata = CreateMetadata();
            var documentId = metadata.Id;
            metadata.ProcessingStatus = status;

            _documentRepositoryMock
                .Setup(x => x.GetMetadaById(documentId))
                .ReturnsAsync(metadata);

            // Act
            Func<Task> act = async () =>
                await _documentService.GetDocumentMetadataByIdAsync(documentId);

            // Assert
            await act.Should().ThrowAsync<DocumentUnavailableException>();
        }

        [Fact]
        public async Task GetDocumentByIdAsync_ShouldThrowDocumentRetrievalFailureException_WhenStorageFails()
        {
            // Arrange
            var metadata = CreateMetadata();
            metadata.ProcessingStatus = ProcessStatus.AVAILABLE;
            var documentId = metadata.Id;
            metadata.BlobKey = "Blob";
            var blobKey = metadata.BlobKey;

            _documentRepositoryMock
                .Setup(x => x.GetMetadaById(documentId))
                .ReturnsAsync(metadata);

            _storageMock
                .Setup(x => x.GetDocumentStream(blobKey))
                .ThrowsAsync(new Exception("Blob failure"));

            // Act
            Func<Task> act = async () =>
                await _documentService.GetDocumentMetadataByIdAsync(documentId);

            // Assert
            await act.Should().ThrowAsync<DocumentRetrievalFailureException>();
        }

        [Fact]
        public async Task GetDocumentByIdAsync_ShouldReturnStream_WhenFileIsAvailable()
        {
            // Arrange
            var metadata = CreateMetadata();
            metadata.ProcessingStatus = ProcessStatus.AVAILABLE;
            var documentId = metadata.Id;
            var blobKey = "Blob";
            metadata.BlobKey = blobKey;
            var expectedStream = new MemoryStream();

            _documentRepositoryMock
                .Setup(x => x.GetMetadaById(documentId))
                .ReturnsAsync(metadata);

            _storageMock
                .Setup(x => x.GetDocumentStream(blobKey))
                .ReturnsAsync(expectedStream);

            // Act
            var result = await _documentService.GetDocumentMetadataByIdAsync(documentId);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeSameAs(expectedStream);

            _documentRepositoryMock.Verify(x => x.GetMetadaById(documentId), Times.Once);
            _storageMock.Verify(x => x.GetDocumentStream(blobKey), Times.Once);
        }

        #endregion

        #region DeleteDocumentAsyncTests
        /// Założenia:
        /// Rozdzielam odpowiedzialności na oznaczenie dokumentu do usunięcia, wykonany osobnym metodą MarkDocumentToDeletAsync, który ustawia status documentu na MARKED_TO_DELETE, oraz metoda DeleteDocument, która usuwa fizycznie dokument
        /// Metoda DeleteDocument:
        /// 1. Dostaje jako parametr identyfikator metadanych
        /// 2. Sprawdza czy metadane są w SQL
        /// 3. Sprawdza czy metoda jest oznaczona jako MARKED_TO_DELETE, jeśli nie to zwracam DocumentUnavailableException
        /// 4. Wyszukuje dokument w Blob
        /// 5. Jeśli nie znajde dokumentu, to zakładam że jest już usuniętyy, zmieniam status na DELETED i kończę
        /// 6. Jeśli znajdzie, to usuwam dokument z Blob, aktualizuję status na DELETED
        /// 7. Jeśli nie uda się usunąć z Blob, oznaczam jako DELETE_FAILED, robie log (na ten moment daje new DocumentDeletionFailureException, ale docelowo obsługa Retry)


        [Theory]
        [InlineData(ProcessStatus.PROCESSING)]
        [InlineData(ProcessStatus.FAILED)]
        [InlineData(ProcessStatus.AVAILABLE)]
        public async Task DeleteDocumentAsync_ShouldThrowDocumentWrongStatusException_WhenStatusOtherThanMarkedToDelete(ProcessStatus status)
        {
            // Arrange
            var metadata = CreateMetadata();
            metadata.ProcessingStatus = status;

            _documentRepositoryMock
                .Setup(x => x.GetMetadaById(metadata.Id))
                .ReturnsAsync(metadata);
                
            //Act
            Func<Task> act = async () => await _documentService.DeleteDocumentAsync(metadata.Id);

            //Assert
            await act.Should().ThrowAsync<DocumentUnavailableException>();
            _storageMock.Verify(x => x.DeleteAsync(It.IsAny<string>()), Times.Never); //bo sprawdzam czy nie poszło dalej 
        }

        [Fact]
        public async Task DeleteDocumentAsync_ShouldSetStatusToDeleted_WhenDocumentIsAlreadyDeletedInBlob()
        {
            // Arrange
            var metadata = CreateMetadata();
            metadata.ProcessingStatus = ProcessStatus.MARKED_TO_DELETE;
            var documentId = metadata.Id;
            var blobKey = "Blob";
            metadata.BlobKey = blobKey;

            _documentRepositoryMock
                .Setup(x => x.GetMetadaById(documentId))
                .ReturnsAsync(metadata);

            _storageMock
                .Setup(x => x.ExistsAsync(blobKey))
                .ReturnsAsync(false); // zakładam, że dokument już jest usunięty w Blob

            _documentRepositoryMock
                .Setup(x => x.UpdateMetadataProcessingStatus(documentId, ProcessStatus.DELETED))
                .Returns(Task.CompletedTask);

            // Act
            await _documentService.DeleteDocumentAsync(documentId);

            // Assert
            _storageMock.Verify(x => x.DeleteAsync(blobKey), Times.Never);
            _documentRepositoryMock.Verify(x =>
                x.UpdateMetadataProcessingStatus(documentId, ProcessStatus.DELETED),
                Times.Once);
        }

        [Fact]
        public async Task DeleteDocumentAsync_ShouldThrowDocumentDeletionFailureException_WhenBlobDeletionFails()
        {
            // Arrange
            var metadata = CreateMetadata();
            metadata.ProcessingStatus = ProcessStatus.MARKED_TO_DELETE;
            var documentId = metadata.Id;
            var blobKey = "Blob";
            metadata.BlobKey = blobKey;

            _documentRepositoryMock
                .Setup(x => x.GetMetadaById(documentId))
                .ReturnsAsync(metadata);

            _storageMock
                .Setup(x => x.ExistsAsync(blobKey))
                .ReturnsAsync(true);

            _storageMock
                .Setup(x => x.DeleteAsync(blobKey))
                .ThrowsAsync(new Exception("Blob deletion failed"));

            _documentRepositoryMock
                .Setup(x => x.UpdateMetadataProcessingStatus(documentId, ProcessStatus.FAILED_TO_DELETE))
                .Returns(Task.CompletedTask);

            // Act
            Func<Task> act = async () => await _documentService.DeleteDocumentAsync(documentId);

            // Assert
            await act.Should().ThrowAsync<DocumentDeletionFailureException>();

            _storageMock.Verify(x => x.ExistsAsync(blobKey), Times.Once);
            _storageMock.Verify(x => x.DeleteAsync(blobKey), Times.Once);
            _documentRepositoryMock.Verify(
                x => x.UpdateMetadataProcessingStatus(documentId, ProcessStatus.FAILED_TO_DELETE),
                Times.Once);
        }

        [Fact]
        public async Task DeleteDocumentAsync_ShouldSetStatusToDeleted_WhenBlobDeletionSucceeds()
        {
            // Arrange
            var metadata = CreateMetadata();
            metadata.ProcessingStatus = ProcessStatus.MARKED_TO_DELETE;
            var documentId = metadata.Id;
            var blobKey = "Blob";
            metadata.BlobKey = blobKey;

            _documentRepositoryMock
                .Setup(x => x.GetMetadaById(documentId))
                .ReturnsAsync(metadata);

            _storageMock
                .Setup(x => x.ExistsAsync(blobKey))
                .ReturnsAsync(true);

            _storageMock
                .Setup(x => x.DeleteAsync(blobKey))
                .Returns(Task.CompletedTask);

            _documentRepositoryMock
                .Setup(x => x.UpdateMetadataProcessingStatus(documentId, ProcessStatus.DELETED))
                .Returns(Task.CompletedTask);

            // Act
            await _documentService.DeleteDocumentAsync(documentId);

            // Assert
            _storageMock.Verify(x => x.ExistsAsync(blobKey), Times.Once);
            _storageMock.Verify(x => x.DeleteAsync(blobKey), Times.Once);
            _documentRepositoryMock.Verify(
                x => x.UpdateMetadataProcessingStatus(documentId, ProcessStatus.DELETED),
                Times.Once);
        }
        #endregion
        #region UpdateDocumentAsyncTests
        /// Założenia:
        /// Dziele to na pobranie nowych metadanych, ustawienie statusu PROCESSING_UPDATE, dodanie dokumentu do bloba, aktualizacje meta w SQL.
        /// 0. Walidacja:
        /// - jeśli plik null rzucam ArgumentNullException
        /// - jeśli pusty rzucam EmptyDocumentException
        /// - jeśli zły typ rzucam UnsupportedDocumentTypeException
        /// 1. Pobieram metadane z SQL, jeśli nie ma, to rzucam DocumentMetadataNotFoundException (tu stwierdziłem że wprowadze nowy wyjątek, żeby wiedzieć czy sie wyjebał metadane czy dokument)
        /// 2. Sprawdzam czy status dokumentu jest AVAILABLE, jeśli nie, to rzucam DocumentUnavailableException
        /// 3. Jeśli wszystko jest poprawne, to zmieniam staus na PROCESSING_UPDATE
        /// 4. Wyszykuje dokument w Blob, jeśli nie ma, to rzucam DocumentNotFoundException
        /// 5. Próbuje zaktualizować dokument w Blob. Jeśli się nie uda, to rzucam DocumentProcessingFailureException i ustawiam status FAILED_UPDATE
        /// 6. Po aktualizacji Bloba, aktualizuję metadane w SQL, jeśli się nie uda, to ustawiam status FAILED_UPDATE i rzucam DocumentProcessingFailureException (albo robie retry)
        /// 7. Jeśli wszystko okej, to aktualizuje status na AVAILABLE
        /// 



        #endregion
        #region AddDocumentRegionsPrivateMethods
        private static IFormFile CreateFormFile(string fileName, string content = "sample content")
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(content);
            var stream = new MemoryStream(bytes);

            return new FormFile(stream, 0, bytes.Length, "file", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = "application/octet-stream"
            };
        }

        private static DocumentMetadata CreateMetadata()
        {
            return new DocumentMetadata
            {
                ProcessingStatus = ProcessStatus.PROCESSING
            };

        }
    }
    #endregion

}