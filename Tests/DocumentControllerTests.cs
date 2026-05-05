using FluentAssertions;
using inz.Controllers;
using inz.Models;
using inz.Services.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

public class DocumentsControllerTests
{
    private readonly Mock<IDocumentService> _documentServiceMock = new();
    private readonly DocumentController _controller;

    public DocumentsControllerTests()
    {
        _controller = new DocumentController(_documentServiceMock.Object);
    }

    [Fact]
    public async Task AddDocument_ShouldReturnCreatedAtAction_WhenDocumentIsAdded()
    {
        // Arrange
        var file = CreateFormFile("document.pdf");
        var documentId = 123;

        _documentServiceMock
            .Setup(x => x.AddDocumentAsync(It.IsAny<CreateDocumentCommand>()))
            .ReturnsAsync(documentId);

        // Act
        var result = await _controller.AddDocument(file);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;

        createdResult.ActionName.Should().Be(nameof(DocumentController.GetDocument));
        createdResult.RouteValues!["id"].Should().Be(documentId);

        _documentServiceMock.Verify(x =>
            x.AddDocumentAsync(It.Is<CreateDocumentCommand>(c =>
                c.File == file &&
                c.OwnerId == 1 &&
                c.OrganizationId == 1)),
            Times.Once);
    }

    [Fact]
    public async Task GetDocument_ShouldReturnFileResult_WhenDocumentExists()
    {
        // Arrange
        var documentId = 123;
        var stream = new MemoryStream();

        _documentServiceMock
            .Setup(x => x.GetDocumentByIdAsync(documentId))
            .ReturnsAsync(stream);

        // Act
        var result = await _controller.GetDocument(documentId);

        // Assert
        var fileResult = result.Should().BeOfType<FileStreamResult>().Subject;

        fileResult.FileStream.Should().BeSameAs(stream);
        fileResult.ContentType.Should().Be("application/octet-stream");

        _documentServiceMock.Verify(x =>
            x.GetDocumentByIdAsync(documentId),
            Times.Once);
    }
    
    [Fact]
    public async Task MarkDocumentToDelete_ShouldReturnNoContent_WhenDocumentIsMarked()
    {
        // Arrange
        var documentId = 123;

        _documentServiceMock
            .Setup(x => x.MarkDocumentToDeleteAsync(documentId))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.MarkDocumentToDelete(documentId);

        // Assert
        result.Should().BeOfType<NoContentResult>();

        _documentServiceMock.Verify(x =>
            x.MarkDocumentToDeleteAsync(documentId),
            Times.Once);
    }

    [Fact]
    public async Task UpdateDocument_ShouldReturnOk_WhenDocumentIsUpdated()
    {
        // Arrange
        var documentId = 123;
        var file = CreateFormFile("updated_document.pdf");
        var updatedDocumentId = 456;
        _documentServiceMock
            .Setup(x => x.UpdateDocumentAsync(documentId, file))
            .ReturnsAsync(updatedDocumentId);
        // Act
        var result = await _controller.UpdateDocument(documentId, file);
        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(updatedDocumentId);
        _documentServiceMock.Verify(x =>
            x.UpdateDocumentAsync(documentId, file),
            Times.Once);
    }

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
}