using FluentAssertions;
using inz.Core;
using inz.Core.DocumentExceptions;
using inz.Service;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;


namespace inz.Tests
{

    public class DocumentServiceTests
    {

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

        private readonly Mock<IDocumentRepository> _documentRepositoryMock = new();
        private readonly Mock<IDocumentContentStorage> _storageMock = new();
        private readonly Mock<IFileReader> _fileReaderMock = new();
        private readonly Mock<ILogger<DocumentService>> _loggerMock = new();

        private readonly DocumentService _sut;

        public DocumentServiceTests()
        {
            _sut = new DocumentService(
                _documentRepositoryMock.Object,
                _storageMock.Object,
                _fileReaderMock.Object,
                _loggerMock.Object);
        }

        #region AddDocumentTests

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
                .Setup(x => x.AddDocumentMetadata(It.IsAny<FileMetadata>()))
                .Returns(Task.CompletedTask);

            _storageMock
                .Setup(x => x.AddDocumentToStorage(file))
                .Returns(Task.CompletedTask);

            _documentRepositoryMock
                .Setup(x => x.UpdateMetadataProcessingStatus(metadata.Id, ProcessStatus.AVAILABLE))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _sut.AddDocumentAsync(file);

            // Assert
            result.Should().Be(metadata.Id);

            _fileReaderMock.Verify(x => x.ReadFile(file), Times.Once);

            _documentRepositoryMock.Verify(x =>
                x.AddDocumentMetadata(It.Is<FileMetadata>(m =>
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
                .Setup(x => x.AddDocumentMetadata(It.IsAny<FileMetadata>()))
                .Returns(Task.CompletedTask);

            _storageMock
                .Setup(x => x.AddDocumentToStorage(file))
                .Returns(Task.CompletedTask);

            _documentRepositoryMock
                .Setup(x => x.UpdateMetadataProcessingStatus(metadata.Id, ProcessStatus.AVAILABLE))
                .Returns(Task.CompletedTask);

            // Act
            await _sut.AddDocumentAsync(file);

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
                .Setup(x => x.AddDocumentMetadata(It.IsAny<FileMetadata>()))
                .Returns(Task.CompletedTask);

            _storageMock
                .Setup(x => x.AddDocumentToStorage(file))
                .ThrowsAsync(storageException);

            _documentRepositoryMock
                .Setup(x => x.UpdateMetadataProcessingStatus(metadata.Id, ProcessStatus.FAILED))
                .Returns(Task.CompletedTask);

            // Act
            Func<Task> act = async () => await _sut.AddDocumentAsync(file);

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
                .Setup(x => x.AddDocumentMetadata(It.IsAny<FileMetadata>()))
                .ThrowsAsync(repositoryException);

            // Act
            Func<Task> act = async () => await _sut.AddDocumentAsync(file);

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
                .Setup(x => x.AddDocumentMetadata(It.IsAny<FileMetadata>()))
                .Returns(Task.CompletedTask);

            _storageMock
                .Setup(x => x.AddDocumentToStorage(file))
                .ThrowsAsync(storageException);

            _documentRepositoryMock
                .Setup(x => x.UpdateMetadataProcessingStatus(metadata.Id, ProcessStatus.FAILED))
                .Returns(Task.CompletedTask);

            // Act
            Func<Task> act = async () => await _sut.AddDocumentAsync(file);

            // Assert
            var exception = await act.Should().ThrowAsync<DocumentProcessingFailure>();
            exception.Which.InnerException.Should().Be(storageException);
            exception.Which.Message.Should().Contain("Wystąpił błąd podczas dodawania dokumentu");
        }

        [Fact]
        public async Task AddDocumentAsync_ShouldThrowArgumentNullException_WhenFileIsNull()
        {
            // Act
            Func<Task> act = async () => await _sut.AddDocumentAsync(null!);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>();

            _fileReaderMock.Verify(x => x.ReadFile(It.IsAny<IFormFile>()), Times.Never);
            _documentRepositoryMock.Verify(x => x.AddDocumentMetadata(It.IsAny<FileMetadata>()), Times.Never);
            _storageMock.Verify(x => x.AddDocumentToStorage(It.IsAny<IFormFile>()), Times.Never);
        }

        [Fact]
        public async Task AddDocumentAsync_ShouldThrowEmptyDocumentException_WhenFileIsEmpty()
        {
            // Arrange
            var file = CreateFormFile("empty.pdf", string.Empty);

            // Act
            Func<Task> act = async () => await _sut.AddDocumentAsync(file);

            // Assert
            await act.Should().ThrowAsync<EmptyDocumentException>();

            _fileReaderMock.Verify(x => x.ReadFile(It.IsAny<IFormFile>()), Times.Never);
            _documentRepositoryMock.Verify(x => x.AddDocumentMetadata(It.IsAny<FileMetadata>()), Times.Never);
            _storageMock.Verify(x => x.AddDocumentToStorage(It.IsAny<IFormFile>()), Times.Never);
        }

        [Fact]
        public async Task AddDocumentAsync_ShouldThrowUnsupportedDocumentTypeException_WhenExtensionIsNotSupported()
        {
            // Arrange
            var file = CreateFormFile("malware.exe", "fake content");

            // Act
            Func<Task> act = async () => await _sut.AddDocumentAsync(file);

            // Assert
            await act.Should().ThrowAsync<UnsupportedDocumentTypeException>();

            _fileReaderMock.Verify(x => x.ReadFile(It.IsAny<IFormFile>()), Times.Never);
            _documentRepositoryMock.Verify(x => x.AddDocumentMetadata(It.IsAny<FileMetadata>()), Times.Never);
            _storageMock.Verify(x => x.AddDocumentToStorage(It.IsAny<IFormFile>()), Times.Never);
        }

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

        private static FileMetadata CreateMetadata()
        {
            return new FileMetadata
            {
                ProcessingStatus = ProcessStatus.PROCESSING
            };
        }
        #endregion

    }

}
