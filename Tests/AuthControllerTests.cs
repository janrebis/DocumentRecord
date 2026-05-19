using FluentAssertions;
using inz.Controllers;
using inz.Models;
using inz.Services.Interface;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace inz.Tests;

public class AuthControllerTests
{
    private readonly Mock<IAuthService> _authServiceMock = new();
    private readonly AuthController _controller;

    public AuthControllerTests()
    {
        _controller = new AuthController(_authServiceMock.Object);
    }

    #region Register

    [Fact]
    public async Task Register_ShouldReturnCreatedAtAction_WhenRegistrationSucceeds()
    {
        // Arrange
        var command = new RegisterCommand
        {
            Email = "user@test.pl",
            Password = "Password123!",
            DisplayName = "Test User"
        };
        var userId = 1;

        _authServiceMock
            .Setup(x => x.RegisterAsync(command))
            .ReturnsAsync(userId);

        // Act
        var result = await _controller.Register(command);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;

        createdResult.RouteValues!["id"].Should().Be(userId);

        _authServiceMock.Verify(x =>
            x.RegisterAsync(command),
            Times.Once);
    }

    #endregion

    #region Login

    [Fact]
    public async Task Login_ShouldReturnOkWithToken_WhenCredentialsAreValid()
    {
        // Arrange
        var command = new LoginCommand
        {
            Email = "user@test.pl",
            Password = "Password123!"
        };
        var token = "jwt-token-123";

        _authServiceMock
            .Setup(x => x.LoginAsync(command))
            .ReturnsAsync(token);

        // Act
        var result = await _controller.Login(command);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;

        okResult.Value.Should().NotBeNull();

        _authServiceMock.Verify(x =>
            x.LoginAsync(command),
            Times.Once);
    }

    #endregion
}
