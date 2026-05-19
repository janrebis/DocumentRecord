using FluentAssertions;
using inz.Controllers;
using inz.Models;
using inz.Services.Interface;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace inz.Tests;

public class UserControllerTests
{
    private readonly Mock<IUserService> _userServiceMock = new();
    private readonly Mock<IRoleService> _roleServiceMock = new();
    private readonly UserController _controller;

    public UserControllerTests()
    {
        _controller = new UserController(_userServiceMock.Object, _roleServiceMock.Object);
    }

    #region GetUser

    [Fact]
    public async Task GetUser_ShouldReturnOk_WhenUserExists()
    {
        // Arrange
        var user = CreateUser(id: 1);

        _userServiceMock
            .Setup(x => x.GetUserByIdAsync(1))
            .ReturnsAsync(user);

        // Act
        var result = await _controller.GetUser(1);

        // Assert
        result.Should().BeOfType<OkObjectResult>();

        _userServiceMock.Verify(x =>
            x.GetUserByIdAsync(1),
            Times.Once);
    }

    #endregion

    #region GetUsersByOrganization

    [Fact]
    public async Task GetUsersByOrganization_ShouldReturnOk_WhenOrganizationExists()
    {
        // Arrange
        var users = new List<User> { CreateUser(id: 1), CreateUser(id: 2) };

        _userServiceMock
            .Setup(x => x.GetUsersByOrganizationAsync(1))
            .ReturnsAsync(users);

        // Act
        var result = await _controller.GetUsersByOrganization(1);

        // Assert
        result.Should().BeOfType<OkObjectResult>();

        _userServiceMock.Verify(x =>
            x.GetUsersByOrganizationAsync(1),
            Times.Once);
    }

    #endregion

    #region CreateUser

    [Fact]
    public async Task CreateUser_ShouldReturnCreatedAtAction_WhenUserIsCreated()
    {
        // Arrange
        var command = new CreateUserCommand
        {
            EntraObjectId = "entra-oid-123",
            Email = "user@test.pl",
            DisplayName = "Test User",
            OrganizationId = 1,
            RoleId = 1
        };
        var userId = 10;

        _userServiceMock
            .Setup(x => x.CreateUserAsync(command))
            .ReturnsAsync(userId);

        // Act
        var result = await _controller.CreateUser(command);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;

        createdResult.ActionName.Should().Be(nameof(UserController.GetUser));
        createdResult.RouteValues!["id"].Should().Be(userId);

        _userServiceMock.Verify(x =>
            x.CreateUserAsync(command),
            Times.Once);
    }

    #endregion

    #region DeactivateUser

    [Fact]
    public async Task DeactivateUser_ShouldReturnNoContent_WhenUserIsDeactivated()
    {
        // Arrange
        _userServiceMock
            .Setup(x => x.DeactivateUserAsync(1))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.DeactivateUser(1);

        // Assert
        result.Should().BeOfType<NoContentResult>();

        _userServiceMock.Verify(x =>
            x.DeactivateUserAsync(1),
            Times.Once);
    }

    #endregion

    #region ActivateUser

    [Fact]
    public async Task ActivateUser_ShouldReturnNoContent_WhenUserIsActivated()
    {
        // Arrange
        _userServiceMock
            .Setup(x => x.ActivateUserAsync(1))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.ActivateUser(1);

        // Assert
        result.Should().BeOfType<NoContentResult>();

        _userServiceMock.Verify(x =>
            x.ActivateUserAsync(1),
            Times.Once);
    }

    #endregion

    #region AssignRole

    [Fact]
    public async Task AssignRole_ShouldReturnNoContent_WhenRoleIsAssigned()
    {
        // Arrange
        var request = new AssignRoleRequest
        {
            OrganizationId = 1,
            RoleId = 2
        };

        _roleServiceMock
            .Setup(x => x.AssignRoleToUserAsync(It.IsAny<AssignRoleCommand>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.AssignRole(1, request);

        // Assert
        result.Should().BeOfType<NoContentResult>();

        _roleServiceMock.Verify(x =>
            x.AssignRoleToUserAsync(It.Is<AssignRoleCommand>(c =>
                c.UserId == 1 &&
                c.OrganizationId == 1 &&
                c.RoleId == 2)),
            Times.Once);
    }

    #endregion

    #region RemoveRole

    [Fact]
    public async Task RemoveRole_ShouldReturnNoContent_WhenRoleIsRemoved()
    {
        // Arrange
        _roleServiceMock
            .Setup(x => x.RemoveRoleFromUserAsync(1, 1, 2))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.RemoveRole(1, 2, 1);

        // Assert
        result.Should().BeOfType<NoContentResult>();

        _roleServiceMock.Verify(x =>
            x.RemoveRoleFromUserAsync(1, 1, 2),
            Times.Once);
    }

    #endregion

    #region GetUserPermissions

    [Fact]
    public async Task GetUserPermissions_ShouldReturnOk_WithPermissionsList()
    {
        // Arrange
        var permissions = new List<string> { "documents:read", "documents:write" };

        _roleServiceMock
            .Setup(x => x.GetUserPermissionsAsync(1, 1))
            .ReturnsAsync(permissions);

        // Act
        var result = await _controller.GetUserPermissions(1, 1);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;

        okResult.Value.Should().Be(permissions);

        _roleServiceMock.Verify(x =>
            x.GetUserPermissionsAsync(1, 1),
            Times.Once);
    }

    #endregion

    #region GetRoles

    [Fact]
    public async Task GetRoles_ShouldReturnOk_WithRolesList()
    {
        // Arrange
        var roles = new List<Role>
        {
            new() { Id = 1, Name = "Admin", IsSystemRole = true },
            new() { Id = 2, Name = "Reader", IsSystemRole = true }
        };

        _roleServiceMock
            .Setup(x => x.GetRolesByOrganizationAsync(1))
            .ReturnsAsync(roles);

        // Act
        var result = await _controller.GetRoles(1);

        // Assert
        result.Should().BeOfType<OkObjectResult>();

        _roleServiceMock.Verify(x =>
            x.GetRolesByOrganizationAsync(1),
            Times.Once);
    }

    #endregion

    #region Helpers

    private static User CreateUser(
        int id = 1,
        string email = "user@test.pl",
        string displayName = "Test User",
        int organizationId = 1)
    {
        return new User
        {
            Id = id,
            Email = email,
            DisplayName = displayName,
            OrganizationId = organizationId
        };
    }

    #endregion
}
