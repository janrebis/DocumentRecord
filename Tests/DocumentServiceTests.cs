using FluentAssertions;
using inz.DocumentExceptions;
using inz.Models;
using inz.Models.Enums;
using inz.Repository.Interface;
using inz.Services.Implementation;
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

        #region AddDocumentAsync

        [Fact]
        public async Task AddDocumentAsync_ShouldAddMetadataAndUploadFile_WhenInputIsValid()
        {
            // Arrange
            var file = CreateFormFile("test.pdf", "dummy content");
            var metadata = CreateNewMetadata();
            var command = CreateCommand(file);
            var blobKey = "blob-key";

            _fileReaderMock
                .Setup(x => x.ReadFileAsync(file))
                .ReturnsAsync(metadata);

            _documentRepositoryMock
                .Setup(x => x.AddDocumentMetadataAsync(It.IsAny<DocumentMetadata>()))
                .ReturnsAsync(metadata.Id);

            _storageMock
                .Setup(x => x.AddDocumentToStorageAsync(file))
                .ReturnsAsync(blobKey);

            _documentRepositoryMock
                .Setup(x => x.UpdateDocumentMetadataAsync(metadata.Id, It.IsAny<DocumentMetadata>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _documentService.AddDocumentAsync(command);

            // Assert
            result.Should().Be(metadata.Id);

            _fileReaderMock.Verify(x => x.ReadFileAsync(file), Times.Once);
            _storageMock.Verify(x => x.AddDocumentToStorageAsync(file), Times.Once);

            _documentRepositoryMock.Verify(x =>
                x.UpdateDocumentMetadataAsync(metadata.Id,
                    It.Is<DocumentMetadata>(m =>
                        m.ProcessingStatus == ProcessStatus.AVAILABLE &&
                        m.BlobKey == blobKey)),
                Times.Once);
        }

        [Fact]
        public async Task AddDocumentAsync_ShouldSetStatusToFailed_WhenStorageUploadFailsAfterMetadataSave()
        {
            // Arrange
            var file = CreateFormFile("document.pdf");
            var metadata = CreateNewMetadata();
            var command = CreateCommand(file);
            var storageException = new Exception("Storage failure");

            _fileReaderMock
                .Setup(x => x.ReadFileAsync(file))
                .ReturnsAsync(metadata);

            _documentRepositoryMock
                .Setup(x => x.AddDocumentMetadataAsync(It.IsAny<DocumentMetadata>()))
                .ReturnsAsync(metadata.Id);

            _storageMock
                .Setup(x => x.AddDocumentToStorageAsync(file))
                .ThrowsAsync(storageException);

            _documentRepositoryMock
                .Setup(x => x.UpdateDocumentMetadataAsync(metadata.Id, It.IsAny<DocumentMetadata>()))
                .Returns(Task.CompletedTask);

            // Act
            Func<Task> act = async () => await _documentService.AddDocumentAsync(command);

            // Assert
            var exception = await act.Should().ThrowAsync<DocumentProcessingFailureException>();

            exception.Which.InnerException.Should().Be(storageException);

            _documentRepositoryMock.Verify(x =>
                x.UpdateDocumentMetadataAsync(metadata.Id,
                    It.Is<DocumentMetadata>(m =>
                        m.ProcessingStatus == ProcessStatus.FAILED_TO_ADD)),
                Times.Once);
        }

        [Fact]
        public async Task AddDocumentAsync_ShouldNotUploadFile_WhenMetadataSaveFails()
        {
            // Arrange
            var file = CreateFormFile("document.pdf");
            var metadata = CreateNewMetadata();
            var command = CreateCommand(file);
            var repositoryException = new Exception("SQL failure");

            _fileReaderMock
                .Setup(x => x.ReadFileAsync(file))
                .ReturnsAsync(metadata);

            _documentRepositoryMock
                .Setup(x => x.AddDocumentMetadataAsync(It.IsAny<DocumentMetadata>()))
                .ThrowsAsync(repositoryException);

            // Act
            Func<Task> act = async () => await _documentService.AddDocumentAsync(command);

            // Assert
            await act.Should().ThrowAsync<DocumentProcessingFailureException>();

            _storageMock.Verify(x =>
                x.AddDocumentToStorageAsync(It.IsAny<IFormFile>()),
                Times.Never);
        }

        [Fact]
        public async Task AddDocumentAsync_ShouldThrowArgumentNullException_WhenCommandIsNull()
        {
            // Act
            Func<Task> act = async () => await _documentService.AddDocumentAsync(null!);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>();
        }

        [Fact]
        public async Task AddDocumentAsync_ShouldThrowArgumentNullException_WhenFileIsNull()
        {
            // Arrange
            var command = new CreateDocumentCommand
            {
                File = null!,
                OwnerId = 1,
                OrganizationId = 1
            };

            // Act
            Func<Task> act = async () => await _documentService.AddDocumentAsync(command);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>();

            _fileReaderMock.Verify(x => x.ReadFileAsync(It.IsAny<IFormFile>()), Times.Never);
            _documentRepositoryMock.Verify(x => x.AddDocumentMetadataAsync(It.IsAny<DocumentMetadata>()), Times.Never);
            _storageMock.Verify(x => x.AddDocumentToStorageAsync(It.IsAny<IFormFile>()), Times.Never);
        }

        [Fact]
        public async Task AddDocumentAsync_ShouldThrowUnauthorizedAccessException_WhenOrganizationIdIsInvalid()
        {
            // Arrange
            var command = new CreateDocumentCommand
            {
                File = CreateFormFile("document.pdf"),
                OwnerId = 1,
                OrganizationId = 0
            };

            // Act
            Func<Task> act = async () => await _documentService.AddDocumentAsync(command);

            // Assert
            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }

        [Fact]
        public async Task AddDocumentAsync_ShouldThrowEmptyDocumentException_WhenFileIsEmpty()
        {
            // Arrange
            var file = CreateFormFile("empty.pdf", string.Empty);
            var command = CreateCommand(file);

            // Act
            Func<Task> act = async () => await _documentService.AddDocumentAsync(command);

            // Assert
            await act.Should().ThrowAsync<EmptyDocumentException>();

            _fileReaderMock.Verify(x => x.ReadFileAsync(It.IsAny<IFormFile>()), Times.Never);
            _documentRepositoryMock.Verify(x => x.AddDocumentMetadataAsync(It.IsAny<DocumentMetadata>()), Times.Never);
            _storageMock.Verify(x => x.AddDocumentToStorageAsync(It.IsAny<IFormFile>()), Times.Never);
        }

        [Fact]
        public async Task AddDocumentAsync_ShouldThrowUnsupportedDocumentTypeException_WhenExtensionIsNotSupported()
        {
            // Arrange
            var file = CreateFormFile("malware.exe", "fake content");
            var command = CreateCommand(file);

            // Act
            Func<Task> act = async () => await _documentService.AddDocumentAsync(command);

            // Assert
            await act.Should().ThrowAsync<UnsupportedDocumentTypeException>();

            _fileReaderMock.Verify(x => x.ReadFileAsync(It.IsAny<IFormFile>()), Times.Never);
            _documentRepositoryMock.Verify(x => x.AddDocumentMetadataAsync(It.IsAny<DocumentMetadata>()), Times.Never);
            _storageMock.Verify(x => x.AddDocumentToStorageAsync(It.IsAny<IFormFile>()), Times.Never);
        }

        #endregion

        #region GetDocumentByIdAsync

        [Fact]
        public async Task GetDocumentByIdAsync_ShouldThrowDocumentMetadataNotFoundException_WhenMetadataNotFound()
        {
            // Arrange
            var documentId = 123;

            _documentRepositoryMock
                .Setup(x => x.GetMetadataByIdAsync(documentId))
                .ReturnsAsync((DocumentMetadata?)null);

            // Act
            Func<Task> act = async () => await _documentService.GetDocumentByIdAsync(documentId);

            // Assert
            await act.Should().ThrowAsync<DocumentMetadataNotFoundException>();
        }

        [Theory]
        [InlineData(ProcessStatus.NEW_FILE)]
        [InlineData(ProcessStatus.PROCESSING)]
        [InlineData(ProcessStatus.FAILED_TO_ADD)]
        [InlineData(ProcessStatus.MARKED_TO_DELETE)]
        [InlineData(ProcessStatus.DELETED)]
        [InlineData(ProcessStatus.FAILED_TO_DELETE)]
        [InlineData(ProcessStatus.PROCESSING_UPDATE)]
        [InlineData(ProcessStatus.FAILED_TO_UPDATE)]
        public async Task GetDocumentByIdAsync_ShouldThrowDocumentUnavailableException_WhenStatusIsNotAvailable(ProcessStatus status)
        {
            // Arrange
            var metadata = CreateMetadataWithStatus(status);
            var documentId = metadata.Id;

            _documentRepositoryMock
                .Setup(x => x.GetMetadataByIdAsync(documentId))
                .ReturnsAsync(metadata);

            // Act
            Func<Task> act = async () => await _documentService.GetDocumentByIdAsync(documentId);

            // Assert
            await act.Should().ThrowAsync<DocumentUnavailableException>();
        }

        [Fact]
        public async Task GetDocumentByIdAsync_ShouldThrowDocumentRetrievalFailureException_WhenStorageFails()
        {
            // Arrange
            var metadata = CreateAvailableMetadata(blobKey: "blob-key");
            var documentId = metadata.Id;

            _documentRepositoryMock
                .Setup(x => x.GetMetadataByIdAsync(documentId))
                .ReturnsAsync(metadata);

            _storageMock
                .Setup(x => x.GetDocumentStreamAsync(metadata.BlobKey))
                .ThrowsAsync(new Exception("Blob failure"));

            // Act
            Func<Task> act = async () => await _documentService.GetDocumentByIdAsync(documentId);

            // Assert
            await act.Should().ThrowAsync<DocumentRetrievalFailureException>();
        }

        [Fact]
        public async Task GetDocumentByIdAsync_ShouldReturnStream_WhenFileIsAvailable()
        {
            // Arrange
            var metadata = CreateAvailableMetadata(blobKey: "blob-key");
            var expectedStream = new MemoryStream();

            _documentRepositoryMock
                .Setup(x => x.GetMetadataByIdAsync(metadata.Id))
                .ReturnsAsync(metadata);

            _storageMock
                .Setup(x => x.GetDocumentStreamAsync(metadata.BlobKey))
                .ReturnsAsync(expectedStream);

            // Act
            var result = await _documentService.GetDocumentByIdAsync(metadata.Id);

            // Assert
            result.Should().BeSameAs(expectedStream);

            _documentRepositoryMock.Verify(x => x.GetMetadataByIdAsync(metadata.Id), Times.Once);
            _storageMock.Verify(x => x.GetDocumentStreamAsync(metadata.BlobKey), Times.Once);
        }

        #endregion

        #region MarkDocumentToDeleteAsync

        [Fact]
        public async Task MarkDocumentToDeleteAsync_ShouldSetStatusToMarkedToDelete_WhenDocumentIsAvailable()
        {
            // Arrange
            var metadata = CreateAvailableMetadata();

            _documentRepositoryMock
                .Setup(x => x.GetMetadataByIdAsync(metadata.Id))
                .ReturnsAsync(metadata);

            _documentRepositoryMock
                .Setup(x => x.UpdateDocumentMetadataAsync(metadata.Id, It.IsAny<DocumentMetadata>()))
                .Returns(Task.CompletedTask);

            // Act
            await _documentService.MarkDocumentToDeleteAsync(metadata.Id);

            // Assert
            _documentRepositoryMock.Verify(x =>
                x.UpdateDocumentMetadataAsync(metadata.Id,
                    It.Is<DocumentMetadata>(m =>
                        m.ProcessingStatus == ProcessStatus.MARKED_TO_DELETE)),
                Times.Once);
        }

        [Fact]
        public async Task MarkDocumentToDeleteAsync_ShouldThrowDocumentMetadataNotFoundException_WhenMetadataNotFound()
        {
            // Arrange
            var documentId = 123;

            _documentRepositoryMock
                .Setup(x => x.GetMetadataByIdAsync(documentId))
                .ReturnsAsync((DocumentMetadata?)null);

            // Act
            Func<Task> act = async () => await _documentService.MarkDocumentToDeleteAsync(documentId);

            // Assert
            await act.Should().ThrowAsync<DocumentMetadataNotFoundException>();
        }

        [Theory]
        [InlineData(ProcessStatus.PROCESSING)]
        [InlineData(ProcessStatus.FAILED_TO_ADD)]
        [InlineData(ProcessStatus.MARKED_TO_DELETE)]
        [InlineData(ProcessStatus.DELETED)]
        public async Task MarkDocumentToDeleteAsync_ShouldThrowDocumentUnavailableException_WhenDocumentIsNotAvailable(ProcessStatus status)
        {
            // Arrange
            var metadata = CreateMetadataWithStatus(status);

            _documentRepositoryMock
                .Setup(x => x.GetMetadataByIdAsync(metadata.Id))
                .ReturnsAsync(metadata);

            // Act
            Func<Task> act = async () => await _documentService.MarkDocumentToDeleteAsync(metadata.Id);

            // Assert
            await act.Should().ThrowAsync<DocumentUnavailableException>();

            _documentRepositoryMock.Verify(x =>
                x.UpdateDocumentMetadataAsync(It.IsAny<int>(), It.IsAny<DocumentMetadata>()),
                Times.Never);
        }

        #endregion

        #region DeleteDocumentAsync

        [Theory]
        [InlineData(ProcessStatus.PROCESSING)]
        [InlineData(ProcessStatus.FAILED_TO_ADD)]
        [InlineData(ProcessStatus.AVAILABLE)]
        public async Task DeleteDocumentAsync_ShouldThrowDocumentUnavailableException_WhenStatusOtherThanMarkedToDelete(ProcessStatus status)
        {
            // Arrange
            var metadata = CreateMetadataWithStatus(status);

            _documentRepositoryMock
                .Setup(x => x.GetMetadataByIdAsync(metadata.Id))
                .ReturnsAsync(metadata);

            // Act
            Func<Task> act = async () => await _documentService.DeleteDocumentAsync(metadata.Id);

            // Assert
            await act.Should().ThrowAsync<DocumentUnavailableException>();

            _storageMock.Verify(x => x.DeleteAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task DeleteDocumentAsync_ShouldSetStatusToDeleted_WhenDocumentIsAlreadyDeletedInBlob()
        {
            // Arrange
            var metadata = CreateMarkedToDeleteMetadata(blobKey: "blob-key");

            _documentRepositoryMock
                .Setup(x => x.GetMetadataByIdAsync(metadata.Id))
                .ReturnsAsync(metadata);

            _storageMock
                .Setup(x => x.ExistsAsync(metadata.BlobKey))
                .ReturnsAsync(false);

            _documentRepositoryMock
                .Setup(x => x.UpdateDocumentMetadataAsync(metadata.Id, It.IsAny<DocumentMetadata>()))
                .Returns(Task.CompletedTask);

            // Act
            await _documentService.DeleteDocumentAsync(metadata.Id);

            // Assert
            _storageMock.Verify(x => x.DeleteAsync(metadata.BlobKey), Times.Never);

            _documentRepositoryMock.Verify(x =>
                x.UpdateDocumentMetadataAsync(metadata.Id,
                    It.Is<DocumentMetadata>(m =>
                        m.ProcessingStatus == ProcessStatus.DELETED)),
                Times.Once);
        }

        [Fact]
        public async Task DeleteDocumentAsync_ShouldThrowDocumentDeletionFailureException_WhenBlobDeletionFails()
        {
            // Arrange
            var metadata = CreateMarkedToDeleteMetadata(blobKey: "blob-key");
            var storageException = new Exception("Blob deletion failed");

            _documentRepositoryMock
                .Setup(x => x.GetMetadataByIdAsync(metadata.Id))
                .ReturnsAsync(metadata);

            _storageMock
                .Setup(x => x.ExistsAsync(metadata.BlobKey))
                .ReturnsAsync(true);

            _storageMock
                .Setup(x => x.DeleteAsync(metadata.BlobKey))
                .ThrowsAsync(storageException);

            _documentRepositoryMock
                .Setup(x => x.UpdateDocumentMetadataAsync(metadata.Id, It.IsAny<DocumentMetadata>()))
                .Returns(Task.CompletedTask);

            // Act
            Func<Task> act = async () => await _documentService.DeleteDocumentAsync(metadata.Id);

            // Assert
            await act.Should().ThrowAsync<DocumentDeletionFailureException>();

            _storageMock.Verify(x => x.ExistsAsync(metadata.BlobKey), Times.Once);
            _storageMock.Verify(x => x.DeleteAsync(metadata.BlobKey), Times.Once);

            _documentRepositoryMock.Verify(x =>
                x.UpdateDocumentMetadataAsync(metadata.Id,
                    It.Is<DocumentMetadata>(m =>
                        m.ProcessingStatus == ProcessStatus.FAILED_TO_DELETE)),
                Times.Once);
        }

        [Fact]
        public async Task DeleteDocumentAsync_ShouldSetStatusToDeleted_WhenBlobDeletionSucceeds()
        {
            // Arrange
            var metadata = CreateMarkedToDeleteMetadata(blobKey: "blob-key");

            _documentRepositoryMock
                .Setup(x => x.GetMetadataByIdAsync(metadata.Id))
                .ReturnsAsync(metadata);

            _storageMock
                .Setup(x => x.ExistsAsync(metadata.BlobKey))
                .ReturnsAsync(true);

            _storageMock
                .Setup(x => x.DeleteAsync(metadata.BlobKey))
                .Returns(Task.CompletedTask);

            _documentRepositoryMock
                .Setup(x => x.UpdateDocumentMetadataAsync(metadata.Id, It.IsAny<DocumentMetadata>()))
                .Returns(Task.CompletedTask);

            // Act
            await _documentService.DeleteDocumentAsync(metadata.Id);

            // Assert
            _storageMock.Verify(x => x.ExistsAsync(metadata.BlobKey), Times.Once);
            _storageMock.Verify(x => x.DeleteAsync(metadata.BlobKey), Times.Once);

            _documentRepositoryMock.Verify(x =>
                x.UpdateDocumentMetadataAsync(metadata.Id,
                    It.Is<DocumentMetadata>(m =>
                        m.ProcessingStatus == ProcessStatus.DELETED)),
                Times.Once);
        }

        #endregion

        #region UpdateDocumentAsync

        [Fact]
        public async Task UpdateDocumentAsync_ShouldThrowArgumentNullException_WhenSentFileIsNull()
        {
            // Arrange
            var documentId = 123;

            // Act
            Func<Task> act = async () => await _documentService.UpdateDocumentAsync(documentId, null!);

            // Assert
            await act.Should().ThrowAsync<ArgumentNullException>();

            _fileReaderMock.Verify(x => x.ReadFileAsync(It.IsAny<IFormFile>()), Times.Never);
            _documentRepositoryMock.Verify(x => x.GetMetadataByIdAsync(documentId), Times.Never);
            _storageMock.Verify(x => x.UpdateDocumentInStorageAsync(It.IsAny<string>(), It.IsAny<IFormFile>()), Times.Never);
        }

        [Fact]
        public async Task UpdateDocumentAsync_ShouldThrowEmptyDocumentException_WhenFileIsEmpty()
        {
            // Arrange
            var documentId = 123;
            var file = CreateFormFile("empty.pdf", string.Empty);

            // Act
            Func<Task> act = async () => await _documentService.UpdateDocumentAsync(documentId, file);

            // Assert
            await act.Should().ThrowAsync<EmptyDocumentException>();

            _fileReaderMock.Verify(x => x.ReadFileAsync(It.IsAny<IFormFile>()), Times.Never);
            _documentRepositoryMock.Verify(x => x.GetMetadataByIdAsync(documentId), Times.Never);
        }

        [Fact]
        public async Task UpdateDocumentAsync_ShouldThrowUnsupportedDocumentTypeException_WhenExtensionIsNotSupported()
        {
            // Arrange
            var documentId = 123;
            var file = CreateFormFile("malware.exe", "fake content");

            // Act
            Func<Task> act = async () => await _documentService.UpdateDocumentAsync(documentId, file);

            // Assert
            await act.Should().ThrowAsync<UnsupportedDocumentTypeException>();

            _fileReaderMock.Verify(x => x.ReadFileAsync(It.IsAny<IFormFile>()), Times.Never);
            _documentRepositoryMock.Verify(x => x.GetMetadataByIdAsync(documentId), Times.Never);
        }

        [Fact]
        public async Task UpdateDocumentAsync_ShouldThrowDocumentMetadataNotFoundException_WhenMetadataDoesNotExist()
        {
            // Arrange
            var documentId = 123;
            var file = CreateFormFile("document.pdf", "dummy content");
            var newMetadata = CreateNewMetadata(999);

            _fileReaderMock
                .Setup(x => x.ReadFileAsync(file))
                .ReturnsAsync(newMetadata);

            _documentRepositoryMock
                .Setup(x => x.GetMetadataByIdAsync(documentId))
                .ReturnsAsync((DocumentMetadata?)null);

            // Act
            Func<Task> act = async () => await _documentService.UpdateDocumentAsync(documentId, file);

            // Assert
            await act.Should().ThrowAsync<DocumentMetadataNotFoundException>();

            _fileReaderMock.Verify(x => x.ReadFileAsync(file), Times.Once);
            _documentRepositoryMock.Verify(x => x.GetMetadataByIdAsync(documentId), Times.Once);
            _storageMock.Verify(x => x.UpdateDocumentInStorageAsync(It.IsAny<string>(), It.IsAny<IFormFile>()), Times.Never);
            _documentRepositoryMock.Verify(x => x.UpdateDocumentMetadataAsync(It.IsAny<int>(), It.IsAny<DocumentMetadata>()), Times.Never);
        }

        [Theory]
        [InlineData(ProcessStatus.PROCESSING)]
        [InlineData(ProcessStatus.FAILED_TO_ADD)]
        [InlineData(ProcessStatus.FAILED_TO_DELETE)]
        [InlineData(ProcessStatus.PROCESSING_UPDATE)]
        [InlineData(ProcessStatus.FAILED_TO_UPDATE)]
        [InlineData(ProcessStatus.MARKED_TO_DELETE)]
        public async Task UpdateDocumentAsync_ShouldThrowDocumentUnavailableException_WhenStatusIsNotAvailable(ProcessStatus status)
        {
            // Arrange
            var file = CreateFormFile("document.pdf", "dummy content");
            var existingMetadata = CreateMetadataWithStatus(status, id: 123, blobKey: "existing-blob");
            var newMetadata = CreateNewMetadata(999);

            _fileReaderMock
                .Setup(x => x.ReadFileAsync(file))
                .ReturnsAsync(newMetadata);

            _documentRepositoryMock
                .Setup(x => x.GetMetadataByIdAsync(existingMetadata.Id))
                .ReturnsAsync(existingMetadata);

            // Act
            Func<Task> act = async () => await _documentService.UpdateDocumentAsync(existingMetadata.Id, file);

            // Assert
            await act.Should().ThrowAsync<DocumentUnavailableException>();

            _storageMock.Verify(x => x.UpdateDocumentInStorageAsync(It.IsAny<string>(), It.IsAny<IFormFile>()), Times.Never);
            _documentRepositoryMock.Verify(x => x.UpdateDocumentMetadataAsync(It.IsAny<int>(), It.IsAny<DocumentMetadata>()), Times.Never);
        }

        [Fact]
        public async Task UpdateDocumentAsync_ShouldThrowDocumentNotFoundException_WhenBlobIsNotFound()
        {
            // Arrange
            var file = CreateFormFile("document.pdf", "dummy content");
            var existingMetadata = CreateAvailableMetadata(id: 123, blobKey: "existing-blob");
            var newMetadata = CreateNewMetadata(999);

            _fileReaderMock
                .Setup(x => x.ReadFileAsync(file))
                .ReturnsAsync(newMetadata);

            _documentRepositoryMock
                .Setup(x => x.GetMetadataByIdAsync(existingMetadata.Id))
                .ReturnsAsync(existingMetadata);

            _documentRepositoryMock
                .Setup(x => x.UpdateDocumentMetadataAsync(existingMetadata.Id, It.IsAny<DocumentMetadata>()))
                .Returns(Task.CompletedTask);

            _storageMock
                .Setup(x => x.ExistsAsync(existingMetadata.BlobKey))
                .ReturnsAsync(false);

            // Act
            Func<Task> act = async () => await _documentService.UpdateDocumentAsync(existingMetadata.Id, file);

            // Assert
            await act.Should().ThrowAsync<DocumentNotFoundException>();

            _storageMock.Verify(x => x.ExistsAsync(existingMetadata.BlobKey), Times.Once);
            _storageMock.Verify(x => x.UpdateDocumentInStorageAsync(It.IsAny<string>(), It.IsAny<IFormFile>()), Times.Never);

            // Weryfikujemy że serwis wywołał update metadata dwukrotnie:
            // 1) StartUpdate → PROCESSING_UPDATE
            // 2) FailUpdate → FAILED_TO_UPDATE
            _documentRepositoryMock.Verify(x =>
                x.UpdateDocumentMetadataAsync(existingMetadata.Id, It.IsAny<DocumentMetadata>()),
                Times.Exactly(2));
        }

        [Fact]
        public async Task UpdateDocumentAsync_ShouldThrowDocumentProcessingFailureException_WhenBlobUpdateFails()
        {
            // Arrange
            var file = CreateFormFile("document.pdf", "dummy content");
            var existingMetadata = CreateAvailableMetadata(id: 123, blobKey: "existing-blob");
            var newMetadata = CreateNewMetadata(999);
            var storageException = new Exception("Blob update failed");

            _fileReaderMock
                .Setup(x => x.ReadFileAsync(file))
                .ReturnsAsync(newMetadata);

            _documentRepositoryMock
                .Setup(x => x.GetMetadataByIdAsync(existingMetadata.Id))
                .ReturnsAsync(existingMetadata);

            _documentRepositoryMock
                .Setup(x => x.UpdateDocumentMetadataAsync(existingMetadata.Id, It.IsAny<DocumentMetadata>()))
                .Returns(Task.CompletedTask);

            _storageMock
                .Setup(x => x.ExistsAsync(existingMetadata.BlobKey))
                .ReturnsAsync(true);

            _storageMock
                .Setup(x => x.UpdateDocumentInStorageAsync(existingMetadata.BlobKey, file))
                .ThrowsAsync(storageException);

            // Act
            Func<Task> act = async () => await _documentService.UpdateDocumentAsync(existingMetadata.Id, file);

            // Assert
            await act.Should().ThrowAsync<DocumentProcessingFailureException>();

            _storageMock.Verify(x => x.UpdateDocumentInStorageAsync(existingMetadata.BlobKey, file), Times.Once);

            // 1) StartUpdate → PROCESSING_UPDATE
            // 2) FailUpdateFromAnyUpdateState → FAILED_TO_UPDATE
            _documentRepositoryMock.Verify(x =>
                x.UpdateDocumentMetadataAsync(existingMetadata.Id, It.IsAny<DocumentMetadata>()),
                Times.Exactly(2));
        }

        [Fact]
        public async Task UpdateDocumentAsync_ShouldThrowDocumentProcessingFailureException_WhenFinalMetadataUpdateFails()
        {
            // Arrange
            var file = CreateFormFile("document.pdf", "dummy content");
            var existingMetadata = CreateAvailableMetadata(id: 123, blobKey: "existing-blob");
            var newMetadata = CreateNewMetadata(999);
            var repositoryException = new Exception("Metadata update failed");

            _fileReaderMock
                .Setup(x => x.ReadFileAsync(file))
                .ReturnsAsync(newMetadata);

            _documentRepositoryMock
                .Setup(x => x.GetMetadataByIdAsync(existingMetadata.Id))
                .ReturnsAsync(existingMetadata);

            _storageMock
                .Setup(x => x.ExistsAsync(existingMetadata.BlobKey))
                .ReturnsAsync(true);

            _storageMock
                .Setup(x => x.UpdateDocumentInStorageAsync(existingMetadata.BlobKey, file))
                .Returns(Task.CompletedTask);

            // Pierwszy wywołanie (StartUpdate) — OK
            // Drugie wywołanie (FinishUpdate + save) — rzuca wyjątek
            // Trzecie wywołanie (FailUpdateFromAnyUpdateState) — OK
            var callCount = 0;
            _documentRepositoryMock
                .Setup(x => x.UpdateDocumentMetadataAsync(existingMetadata.Id, It.IsAny<DocumentMetadata>()))
                .Returns(() =>
                {
                    callCount++;
                    if (callCount == 2)
                        throw repositoryException;
                    return Task.CompletedTask;
                });

            // Act
            Func<Task> act = async () => await _documentService.UpdateDocumentAsync(existingMetadata.Id, file);

            // Assert
            await act.Should().ThrowAsync<DocumentProcessingFailureException>();

            _storageMock.Verify(x =>
                x.UpdateDocumentInStorageAsync(existingMetadata.BlobKey, file),
                Times.Once);

            // 1) StartUpdate, 2) FinishUpdate (fails), 3) FailUpdateFromAnyUpdateState
            _documentRepositoryMock.Verify(x =>
                x.UpdateDocumentMetadataAsync(existingMetadata.Id, It.IsAny<DocumentMetadata>()),
                Times.Exactly(3));
        }

        [Fact]
        public async Task UpdateDocumentAsync_ShouldUpdateDocumentAndSetStatusAvailable_WhenUpdateSucceeds()
        {
            // Arrange
            var file = CreateFormFile("document.pdf", "dummy content");
            var existingMetadata = CreateAvailableMetadata(id: 123, blobKey: "existing-blob");
            var newMetadata = CreateNewMetadata(999, documentName: "new-name.pdf");

            _fileReaderMock
                .Setup(x => x.ReadFileAsync(file))
                .ReturnsAsync(newMetadata);

            _documentRepositoryMock
                .Setup(x => x.GetMetadataByIdAsync(existingMetadata.Id))
                .ReturnsAsync(existingMetadata);

            _storageMock
                .Setup(x => x.ExistsAsync(existingMetadata.BlobKey))
                .ReturnsAsync(true);

            _storageMock
                .Setup(x => x.UpdateDocumentInStorageAsync(existingMetadata.BlobKey, file))
                .Returns(Task.CompletedTask);

            _documentRepositoryMock
                .Setup(x => x.UpdateDocumentMetadataAsync(existingMetadata.Id, It.IsAny<DocumentMetadata>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _documentService.UpdateDocumentAsync(existingMetadata.Id, file);

            // Assert
            result.Should().Be(existingMetadata.Id);

            _storageMock.Verify(x => x.ExistsAsync(existingMetadata.BlobKey), Times.Once);
            _storageMock.Verify(x => x.UpdateDocumentInStorageAsync(existingMetadata.BlobKey, file), Times.Once);

            // Ostatni zapis powinien mieć AVAILABLE i nową nazwę
            _documentRepositoryMock.Verify(x =>
                x.UpdateDocumentMetadataAsync(existingMetadata.Id,
                    It.Is<DocumentMetadata>(m =>
                        m.ProcessingStatus == ProcessStatus.AVAILABLE &&
                        m.DocumentName == "new-name.pdf")),
                Times.Once);
        }

        #endregion

        #region Helpers

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

        private static CreateDocumentCommand CreateCommand(IFormFile file)
        {
            return new CreateDocumentCommand
            {
                File = file,
                OwnerId = 1,
                OrganizationId = 1
            };
        }

        private static DocumentMetadata CreateNewMetadata(int id = 2, string documentName = "document.pdf")
        {
            return new DocumentMetadata
            {
                Id = id,
                DocumentName = documentName
            };
        }

        private static DocumentMetadata CreateAvailableMetadata(
            int id = 2,
            string documentName = "document.pdf",
            string blobKey = "blob-key")
        {
            var metadata = CreateNewMetadata(id, documentName);

            metadata.StartProcessing();
            metadata.FinishProcessing(blobKey);

            return metadata;
        }

        private static DocumentMetadata CreateMarkedToDeleteMetadata(
            int id = 2,
            string documentName = "document.pdf",
            string blobKey = "blob-key")
        {
            var metadata = CreateAvailableMetadata(id, documentName, blobKey);

            metadata.MarkToDelete();

            return metadata;
        }

        private static DocumentMetadata CreateMetadataWithStatus(
            ProcessStatus status,
            int id = 2,
            string documentName = "document.pdf",
            string blobKey = "blob-key")
        {
            return status switch
            {
                ProcessStatus.NEW_FILE => CreateNewMetadata(id, documentName),
                ProcessStatus.PROCESSING => CreateProcessingMetadata(id, documentName),
                ProcessStatus.AVAILABLE => CreateAvailableMetadata(id, documentName, blobKey),
                ProcessStatus.FAILED_TO_ADD => CreateFailedToAddMetadata(id, documentName),
                ProcessStatus.MARKED_TO_DELETE => CreateMarkedToDeleteMetadata(id, documentName, blobKey),
                ProcessStatus.DELETED => CreateDeletedMetadata(id, documentName, blobKey),
                ProcessStatus.FAILED_TO_DELETE => CreateFailedToDeleteMetadata(id, documentName, blobKey),
                ProcessStatus.PROCESSING_UPDATE => CreateProcessingUpdateMetadata(id, documentName, blobKey),
                ProcessStatus.FAILED_TO_UPDATE => CreateFailedToUpdateMetadata(id, documentName, blobKey),
                _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
            };
        }

        private static DocumentMetadata CreateProcessingMetadata(int id, string documentName)
        {
            var metadata = CreateNewMetadata(id, documentName);
            metadata.StartProcessing();
            return metadata;
        }

        private static DocumentMetadata CreateFailedToAddMetadata(int id, string documentName)
        {
            var metadata = CreateProcessingMetadata(id, documentName);
            metadata.FailProcessing();
            return metadata;
        }

        private static DocumentMetadata CreateDeletedMetadata(int id, string documentName, string blobKey)
        {
            var metadata = CreateMarkedToDeleteMetadata(id, documentName, blobKey);
            metadata.MarkDeleted();
            return metadata;
        }

        private static DocumentMetadata CreateFailedToDeleteMetadata(int id, string documentName, string blobKey)
        {
            var metadata = CreateMarkedToDeleteMetadata(id, documentName, blobKey);
            metadata.FailDelete();
            return metadata;
        }

        private static DocumentMetadata CreateProcessingUpdateMetadata(int id, string documentName, string blobKey)
        {
            var metadata = CreateAvailableMetadata(id, documentName, blobKey);
            metadata.StartUpdate();
            return metadata;
        }

        private static DocumentMetadata CreateFailedToUpdateMetadata(int id, string documentName, string blobKey)
        {
            var metadata = CreateProcessingUpdateMetadata(id, documentName, blobKey);
            metadata.FailUpdate();
            return metadata;
        }

        #endregion
    }
}
