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

        #region AddDocumentAsync

        [Fact]
        public async Task AddDocumentAsync_ShouldAddMetadataAndUploadFile_WhenInputIsValid()
        {
            var file = CreateFormFile("test.pdf", "dummy content");
            var metadata = CreateNewMetadata();
            var command = CreateCommand(file);
            var blobKey = "blob-key";

            _fileReaderMock
                .Setup(x => x.ReadFileAsync(file))
                .ReturnsAsync(metadata);

            _documentRepositoryMock
                .Setup(x => x.AddDocumentMetadataAsync(It.IsAny<DocumentMetadata>()))
                .Returns(Task.CompletedTask);

            _storageMock
                .Setup(x => x.AddDocumentToStorageAsync(file))
                .ReturnsAsync(blobKey);

            _documentRepositoryMock
                .Setup(x => x.UpdateDocumentMetadataAsync(metadata.Id, It.IsAny<DocumentMetadata>()))
                .Returns(Task.CompletedTask);

            var result = await _documentService.AddDocumentAsync(command);

            result.Should().Be(metadata.Id);

            _fileReaderMock.Verify(x => x.ReadFileAsync(file), Times.Once);
            _storageMock.Verify(x => x.AddDocumentToStorageAsync(file), Times.Once);
            _documentRepositoryMock.Verify(x => x.UpdateDocumentMetadataAsync(metadata.Id, It.Is<DocumentMetadata>(m => m.ProcessingStatus == ProcessStatus.AVAILABLE && m.BlobKey == blobKey)), Times.Once);
        }

        [Fact]
        public async Task AddDocumentAsync_ShouldSetStatusToFailed_WhenStorageUploadFailsAfterMetadataSave()
        {
            var file = CreateFormFile("document.pdf");
            var metadata = CreateNewMetadata();
            var command = CreateCommand(file);
            var storageException = new Exception("Storage failure");

            _fileReaderMock
                .Setup(x => x.ReadFileAsync(file))
                .ReturnsAsync(metadata);

            _documentRepositoryMock
                .Setup(x => x.AddDocumentMetadataAsync(It.IsAny<DocumentMetadata>()))
                .Returns(Task.CompletedTask);

            _storageMock
                .Setup(x => x.AddDocumentToStorageAsync(file))
                .ThrowsAsync(storageException);

            _documentRepositoryMock
                .Setup(x => x.UpdateMetadataProcessingStatusAsync(metadata.Id, ProcessStatus.FAILED_TO_ADD))
                .Returns(Task.CompletedTask);

            Func<Task> act = async () => await _documentService.AddDocumentAsync(command);

            var exception = await act.Should().ThrowAsync<DocumentProcessingFailureException>();

            exception.Which.InnerException.Should().Be(storageException);

            _documentRepositoryMock.Verify(x =>
                x.UpdateMetadataProcessingStatusAsync(metadata.Id, ProcessStatus.FAILED_TO_ADD),
                Times.Once);
        }

        [Fact]
        public async Task AddDocumentAsync_ShouldNotUploadFile_WhenMetadataSaveFails()
        {
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

            Func<Task> act = async () => await _documentService.AddDocumentAsync(command);

            await act.Should().ThrowAsync<DocumentProcessingFailureException>();

            _storageMock.Verify(x => x.AddDocumentToStorageAsync(It.IsAny<IFormFile>()), Times.Never);

            _documentRepositoryMock.Verify(x =>
                x.UpdateMetadataProcessingStatusAsync(It.IsAny<int>(), ProcessStatus.FAILED_TO_ADD),
                Times.Once);
        }

        [Fact]
        public async Task AddDocumentAsync_ShouldThrowArgumentNullException_WhenCommandIsNull()
        {
            Func<Task> act = async () => await _documentService.AddDocumentAsync(null!);

            await act.Should().ThrowAsync<ArgumentNullException>();
        }

        [Fact]
        public async Task AddDocumentAsync_ShouldThrowArgumentNullException_WhenFileIsNull()
        {
            var command = new CreateDocumentCommand
            {
                File = null!,
                OwnerId = "user-1",
                OrganizationId = 1
            };

            Func<Task> act = async () => await _documentService.AddDocumentAsync(command);

            await act.Should().ThrowAsync<ArgumentNullException>();

            _fileReaderMock.Verify(x => x.ReadFileAsync(It.IsAny<IFormFile>()), Times.Never);
            _documentRepositoryMock.Verify(x => x.AddDocumentMetadataAsync(It.IsAny<DocumentMetadata>()), Times.Never);
            _storageMock.Verify(x => x.AddDocumentToStorageAsync(It.IsAny<IFormFile>()), Times.Never);
        }

        [Fact]
        public async Task AddDocumentAsync_ShouldThrowUnauthorizedAccessException_WhenOwnerIdIsMissing()
        {
            var command = new CreateDocumentCommand
            {
                File = CreateFormFile("document.pdf"),
                OwnerId = "",
                OrganizationId = 1
            };

            Func<Task> act = async () => await _documentService.AddDocumentAsync(command);

            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }

        [Fact]
        public async Task AddDocumentAsync_ShouldThrowUnauthorizedAccessException_WhenOrganizationIdIsInvalid()
        {
            var command = new CreateDocumentCommand
            {
                File = CreateFormFile("document.pdf"),
                OwnerId = "user-1",
                OrganizationId = 0
            };

            Func<Task> act = async () => await _documentService.AddDocumentAsync(command);

            await act.Should().ThrowAsync<UnauthorizedAccessException>();
        }

        [Fact]
        public async Task AddDocumentAsync_ShouldThrowEmptyDocumentException_WhenFileIsEmpty()
        {
            var file = CreateFormFile("empty.pdf", string.Empty);
            var command = CreateCommand(file);

            Func<Task> act = async () => await _documentService.AddDocumentAsync(command);

            await act.Should().ThrowAsync<EmptyDocumentException>();

            _fileReaderMock.Verify(x => x.ReadFileAsync(It.IsAny<IFormFile>()), Times.Never);
            _documentRepositoryMock.Verify(x => x.AddDocumentMetadataAsync(It.IsAny<DocumentMetadata>()), Times.Never);
            _storageMock.Verify(x => x.AddDocumentToStorageAsync(It.IsAny<IFormFile>()), Times.Never);
        }

        [Fact]
        public async Task AddDocumentAsync_ShouldThrowUnsupportedDocumentTypeException_WhenExtensionIsNotSupported()
        {
            var file = CreateFormFile("malware.exe", "fake content");
            var command = CreateCommand(file);

            Func<Task> act = async () => await _documentService.AddDocumentAsync(command);

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
            var documentId = 123;

            _documentRepositoryMock
                .Setup(x => x.GetMetadataByIdAsync(documentId))
                .ReturnsAsync((DocumentMetadata?)null);

            Func<Task> act = async () => await _documentService.GetDocumentByIdAsync(documentId);

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
            var metadata = CreateMetadataWithStatus(status);
            var documentId = metadata.Id;

            _documentRepositoryMock
                .Setup(x => x.GetMetadataByIdAsync(documentId))
                .ReturnsAsync(metadata);

            Func<Task> act = async () => await _documentService.GetDocumentByIdAsync(documentId);

            await act.Should().ThrowAsync<DocumentUnavailableException>();
        }

        [Fact]
        public async Task GetDocumentByIdAsync_ShouldThrowDocumentRetrievalFailureException_WhenStorageFails()
        {
            var metadata = CreateAvailableMetadata(blobKey: "blob-key");
            var documentId = metadata.Id;

            _documentRepositoryMock
                .Setup(x => x.GetMetadataByIdAsync(documentId))
                .ReturnsAsync(metadata);

            _storageMock
                .Setup(x => x.GetDocumentStreamAsync(metadata.BlobKey))
                .ThrowsAsync(new Exception("Blob failure"));

            Func<Task> act = async () => await _documentService.GetDocumentByIdAsync(documentId);

            await act.Should().ThrowAsync<DocumentRetrievalFailureException>();
        }

        [Fact]
        public async Task GetDocumentByIdAsync_ShouldReturnStream_WhenFileIsAvailable()
        {
            var metadata = CreateAvailableMetadata(blobKey: "blob-key");
            var expectedStream = new MemoryStream();

            _documentRepositoryMock
                .Setup(x => x.GetMetadataByIdAsync(metadata.Id))
                .ReturnsAsync(metadata);

            _storageMock
                .Setup(x => x.GetDocumentStreamAsync(metadata.BlobKey))
                .ReturnsAsync(expectedStream);

            var result = await _documentService.GetDocumentByIdAsync(metadata.Id);

            result.Should().BeSameAs(expectedStream);

            _documentRepositoryMock.Verify(x => x.GetMetadataByIdAsync(metadata.Id), Times.Once);
            _storageMock.Verify(x => x.GetDocumentStreamAsync(metadata.BlobKey), Times.Once);
        }

        #endregion

        #region MarkDocumentToDeleteAsync

        [Fact]
        public async Task MarkDocumentToDeleteAsync_ShouldSetStatusToMarkedToDelete_WhenDocumentIsAvailable()
        {
            var metadata = CreateAvailableMetadata();

            _documentRepositoryMock
                .Setup(x => x.GetMetadataByIdAsync(metadata.Id))
                .ReturnsAsync(metadata);

            await _documentService.MarkDocumentToDeleteAsync(metadata.Id);

            _documentRepositoryMock.Verify(x =>
                x.UpdateMetadataProcessingStatusAsync(metadata.Id, ProcessStatus.MARKED_TO_DELETE),
                Times.Once);
        }

        [Fact]
        public async Task MarkDocumentToDeleteAsync_ShouldThrowDocumentMetadataNotFoundException_WhenMetadataNotFound()
        {
            var documentId = 123;

            _documentRepositoryMock
                .Setup(x => x.GetMetadataByIdAsync(documentId))
                .ReturnsAsync((DocumentMetadata?)null);

            Func<Task> act = async () => await _documentService.MarkDocumentToDeleteAsync(documentId);

            await act.Should().ThrowAsync<DocumentMetadataNotFoundException>();
        }

        [Theory]
        [InlineData(ProcessStatus.PROCESSING)]
        [InlineData(ProcessStatus.FAILED_TO_ADD)]
        [InlineData(ProcessStatus.MARKED_TO_DELETE)]
        [InlineData(ProcessStatus.DELETED)]
        public async Task MarkDocumentToDeleteAsync_ShouldThrowDocumentUnavailableException_WhenDocumentIsNotAvailable(ProcessStatus status)
        {
            var metadata = CreateMetadataWithStatus(status);

            _documentRepositoryMock
                .Setup(x => x.GetMetadataByIdAsync(metadata.Id))
                .ReturnsAsync(metadata);

            Func<Task> act = async () => await _documentService.MarkDocumentToDeleteAsync(metadata.Id);

            await act.Should().ThrowAsync<DocumentUnavailableException>();

            _documentRepositoryMock.Verify(x =>
                x.UpdateMetadataProcessingStatusAsync(It.IsAny<int>(), It.IsAny<ProcessStatus>()),
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
            var metadata = CreateMetadataWithStatus(status);

            _documentRepositoryMock
                .Setup(x => x.GetMetadataByIdAsync(metadata.Id))
                .ReturnsAsync(metadata);

            Func<Task> act = async () => await _documentService.DeleteDocumentAsync(metadata.Id);

            await act.Should().ThrowAsync<DocumentUnavailableException>();

            _storageMock.Verify(x => x.DeleteAsync(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task DeleteDocumentAsync_ShouldSetStatusToDeleted_WhenDocumentIsAlreadyDeletedInBlob()
        {
            var metadata = CreateMarkedToDeleteMetadata(blobKey: "blob-key");

            _documentRepositoryMock
                .Setup(x => x.GetMetadataByIdAsync(metadata.Id))
                .ReturnsAsync(metadata);

            _storageMock
                .Setup(x => x.ExistsAsync(metadata.BlobKey))
                .ReturnsAsync(false);

            await _documentService.DeleteDocumentAsync(metadata.Id);

            _storageMock.Verify(x => x.DeleteAsync(metadata.BlobKey), Times.Never);

            _documentRepositoryMock.Verify(x =>
                x.UpdateMetadataProcessingStatusAsync(metadata.Id, ProcessStatus.DELETED),
                Times.Once);
        }

        [Fact]
        public async Task DeleteDocumentAsync_ShouldThrowDocumentDeletionFailureException_WhenBlobDeletionFails()
        {
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

            Func<Task> act = async () => await _documentService.DeleteDocumentAsync(metadata.Id);

            await act.Should().ThrowAsync<DocumentDeletionFailureException>();

            _storageMock.Verify(x => x.ExistsAsync(metadata.BlobKey), Times.Once);
            _storageMock.Verify(x => x.DeleteAsync(metadata.BlobKey), Times.Once);

            _documentRepositoryMock.Verify(x =>
                x.UpdateMetadataProcessingStatusAsync(metadata.Id, ProcessStatus.FAILED_TO_DELETE),
                Times.Once);
        }

        [Fact]
        public async Task DeleteDocumentAsync_ShouldSetStatusToDeleted_WhenBlobDeletionSucceeds()
        {
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

            await _documentService.DeleteDocumentAsync(metadata.Id);

            _storageMock.Verify(x => x.ExistsAsync(metadata.BlobKey), Times.Once);
            _storageMock.Verify(x => x.DeleteAsync(metadata.BlobKey), Times.Once);

            _documentRepositoryMock.Verify(x =>
                x.UpdateMetadataProcessingStatusAsync(metadata.Id, ProcessStatus.DELETED),
                Times.Once);
        }

        #endregion

        #region UpdateDocumentAsync

        [Fact]
        public async Task UpdateDocumentAsync_ShouldThrowArgumentNullException_WhenSentFileIsNull()
        {
            var documentId = 123;

            Func<Task> act = async () => await _documentService.UpdateDocumentAsync(documentId, null!);

            await act.Should().ThrowAsync<ArgumentNullException>();

            _fileReaderMock.Verify(x => x.ReadFileAsync(It.IsAny<IFormFile>()), Times.Never);
            _documentRepositoryMock.Verify(x => x.GetMetadataByIdAsync(documentId), Times.Never);
            _storageMock.Verify(x => x.UpdateDocumentInStorageAsync(It.IsAny<string>(), It.IsAny<IFormFile>()), Times.Never);
        }

        [Fact]
        public async Task UpdateDocumentAsync_ShouldThrowEmptyDocumentException_WhenFileIsEmpty()
        {
            var documentId = 123;
            var file = CreateFormFile("empty.pdf", string.Empty);

            Func<Task> act = async () => await _documentService.UpdateDocumentAsync(documentId, file);

            await act.Should().ThrowAsync<EmptyDocumentException>();

            _fileReaderMock.Verify(x => x.ReadFileAsync(It.IsAny<IFormFile>()), Times.Never);
            _documentRepositoryMock.Verify(x => x.GetMetadataByIdAsync(documentId), Times.Never);
        }

        [Fact]
        public async Task UpdateDocumentAsync_ShouldThrowUnsupportedDocumentTypeException_WhenExtensionIsNotSupported()
        {
            var documentId = 123;
            var file = CreateFormFile("malware.exe", "fake content");

            Func<Task> act = async () => await _documentService.UpdateDocumentAsync(documentId, file);

            await act.Should().ThrowAsync<UnsupportedDocumentTypeException>();

            _fileReaderMock.Verify(x => x.ReadFileAsync(It.IsAny<IFormFile>()), Times.Never);
            _documentRepositoryMock.Verify(x => x.GetMetadataByIdAsync(documentId), Times.Never);
        }

        [Fact]
        public async Task UpdateDocumentAsync_ShouldThrowDocumentMetadataNotFoundException_WhenMetadataDoesNotExist()
        {
            var documentId = 123;
            var file = CreateFormFile("document.pdf", "dummy content");
            var newMetadata = CreateNewMetadata(999);

            _fileReaderMock
                .Setup(x => x.ReadFileAsync(file))
                .ReturnsAsync(newMetadata);

            _documentRepositoryMock
                .Setup(x => x.GetMetadataByIdAsync(documentId))
                .ReturnsAsync((DocumentMetadata?)null);

            Func<Task> act = async () => await _documentService.UpdateDocumentAsync(documentId, file);

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
            var file = CreateFormFile("document.pdf", "dummy content");
            var existingMetadata = CreateMetadataWithStatus(status, id: 123, blobKey: "existing-blob");
            var newMetadata = CreateNewMetadata(999);

            _fileReaderMock
                .Setup(x => x.ReadFileAsync(file))
                .ReturnsAsync(newMetadata);

            _documentRepositoryMock
                .Setup(x => x.GetMetadataByIdAsync(existingMetadata.Id))
                .ReturnsAsync(existingMetadata);

            Func<Task> act = async () => await _documentService.UpdateDocumentAsync(existingMetadata.Id, file);

            await act.Should().ThrowAsync<DocumentUnavailableException>();

            _documentRepositoryMock.Verify(x =>
                x.UpdateMetadataProcessingStatusAsync(It.IsAny<int>(), It.IsAny<ProcessStatus>()),
                Times.Never);

            _storageMock.Verify(x => x.UpdateDocumentInStorageAsync(It.IsAny<string>(), It.IsAny<IFormFile>()), Times.Never);
            _documentRepositoryMock.Verify(x => x.UpdateDocumentMetadataAsync(It.IsAny<int>(), It.IsAny<DocumentMetadata>()), Times.Never);
        }

        [Fact]
        public async Task UpdateDocumentAsync_ShouldThrowDocumentNotFoundException_WhenBlobIsNotFound()
        {
            var file = CreateFormFile("document.pdf", "dummy content");
            var existingMetadata = CreateAvailableMetadata(id: 123, blobKey: "existing-blob");
            var newMetadata = CreateNewMetadata(999);

            _fileReaderMock
                .Setup(x => x.ReadFileAsync(file))
                .ReturnsAsync(newMetadata);

            _documentRepositoryMock
                .Setup(x => x.GetMetadataByIdAsync(existingMetadata.Id))
                .ReturnsAsync(existingMetadata);

            _storageMock
                .Setup(x => x.ExistsAsync(existingMetadata.BlobKey))
                .ReturnsAsync(false);

            Func<Task> act = async () => await _documentService.UpdateDocumentAsync(existingMetadata.Id, file);

            await act.Should().ThrowAsync<DocumentNotFoundException>();

            _documentRepositoryMock.Verify(x =>
                x.UpdateMetadataProcessingStatusAsync(existingMetadata.Id, ProcessStatus.PROCESSING_UPDATE),
                Times.Once);

            _documentRepositoryMock.Verify(x =>
                x.UpdateMetadataProcessingStatusAsync(existingMetadata.Id, ProcessStatus.FAILED_TO_UPDATE),
                Times.Once);

            _storageMock.Verify(x => x.ExistsAsync(existingMetadata.BlobKey), Times.Once);
            _storageMock.Verify(x => x.UpdateDocumentInStorageAsync(It.IsAny<string>(), It.IsAny<IFormFile>()), Times.Never);
        }

        [Fact]
        public async Task UpdateDocumentAsync_ShouldThrowDocumentProcessingFailureException_WhenBlobUpdateFails()
        {
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

            _storageMock
                .Setup(x => x.ExistsAsync(existingMetadata.BlobKey))
                .ReturnsAsync(true);

            _storageMock
                .Setup(x => x.UpdateDocumentInStorageAsync(existingMetadata.BlobKey, file))
                .ThrowsAsync(storageException);

            Func<Task> act = async () => await _documentService.UpdateDocumentAsync(existingMetadata.Id, file);

            await act.Should().ThrowAsync<DocumentProcessingFailureException>();

            _documentRepositoryMock.Verify(x =>
                x.UpdateMetadataProcessingStatusAsync(existingMetadata.Id, ProcessStatus.PROCESSING_UPDATE),
                Times.Once);

            _documentRepositoryMock.Verify(x =>
                x.UpdateMetadataProcessingStatusAsync(existingMetadata.Id, ProcessStatus.FAILED_TO_UPDATE),
                Times.Once);

            _storageMock.Verify(x => x.UpdateDocumentInStorageAsync(existingMetadata.BlobKey, file), Times.Once);
            _documentRepositoryMock.Verify(x => x.UpdateDocumentMetadataAsync(It.IsAny<int>(), It.IsAny<DocumentMetadata>()), Times.Never);
        }

        [Fact]
        public async Task UpdateDocumentAsync_ShouldThrowDocumentProcessingFailureException_WhenMetadataUpdateFails()
        {
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

            _documentRepositoryMock
                .Setup(x => x.UpdateDocumentMetadataAsync(existingMetadata.Id, It.IsAny<DocumentMetadata>()))
                .ThrowsAsync(repositoryException);

            Func<Task> act = async () => await _documentService.UpdateDocumentAsync(existingMetadata.Id, file);

            await act.Should().ThrowAsync<DocumentProcessingFailureException>();

            _documentRepositoryMock.Verify(x =>
                x.UpdateMetadataProcessingStatusAsync(existingMetadata.Id, ProcessStatus.PROCESSING_UPDATE),
                Times.Once);

            _documentRepositoryMock.Verify(x =>
                x.UpdateDocumentMetadataAsync(existingMetadata.Id, It.IsAny<DocumentMetadata>()),
                Times.Once);

            _documentRepositoryMock.Verify(x =>
                x.UpdateMetadataProcessingStatusAsync(existingMetadata.Id, ProcessStatus.FAILED_TO_UPDATE),
                Times.Once);
        }

        [Fact]
        public async Task UpdateDocumentAsync_ShouldUpdateDocumentAndSetStatusAvailable_WhenUpdateSucceeds()
        {
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
                .Setup(x => x.UpdateDocumentMetadataAsync(
                    existingMetadata.Id,
                    It.IsAny<DocumentMetadata>()))
                .Returns(Task.CompletedTask);

            var result = await _documentService.UpdateDocumentAsync(existingMetadata.Id, file);

            result.Should().Be(existingMetadata.Id);

            _documentRepositoryMock.Verify(x =>
                x.UpdateMetadataProcessingStatusAsync(existingMetadata.Id, ProcessStatus.PROCESSING_UPDATE),
                Times.Once);

            _storageMock.Verify(x => x.ExistsAsync(existingMetadata.BlobKey), Times.Once);
            _storageMock.Verify(x => x.UpdateDocumentInStorageAsync(existingMetadata.BlobKey, file), Times.Once);

            _documentRepositoryMock.Verify(x =>
                x.UpdateDocumentMetadataAsync(existingMetadata.Id, It.Is<DocumentMetadata>(m =>
                    m.ProcessingStatus == ProcessStatus.AVAILABLE &&
                    m.DocumentName == "new-name.pdf")),
                Times.Once);

            _documentRepositoryMock.Verify(x =>
                x.UpdateMetadataProcessingStatusAsync(existingMetadata.Id, ProcessStatus.FAILED_TO_UPDATE),
                Times.Never);
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
                OwnerId = "user-1",
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