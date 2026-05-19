using FluentAssertions;
using inz.Models;
using inz.Repository.Interface;
using inz.Services.Implementation;
using inz.UserExceptions;
using Microsoft.Extensions.Logging;
using Moq;

namespace inz.Tests;

public class RoleServiceTests
{
    private readonly Mock<IRoleRepository> _roleRepositoryMock = new();
    private readonly Mock<IRolePermissionRepository> _rolePermissionRepositoryMock = new();
    private readonly Mock<IUserOrganizationRoleRepository> _userOrgRoleRepositoryMock = new();
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<ILogger<RoleService>> _loggerMock = new();

    private readonly RoleService _roleService;

    public RoleServiceTests()
    {
        _roleService = new RoleService(
            _roleRepositoryMock.Object,
            _rolePermissionRepositoryMock.Object,
            _userOrgRoleRepositoryMock.Object,
            _userRepositoryMock.Object,
            _loggerMock.Object);
    }

    #region AssignRoleToUserAsync

    [Fact]
    public async Task AssignRoleToUserAsync_ShouldAssignRole_WhenInputIsValid()
    {
        // Arrange
        var command = CreateAssignRoleCommand();
        var user = CreateUser();
        var role = CreateRole();

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(command.UserId))
            .ReturnsAsync(user);

        _roleRepositoryMock
            .Setup(x => x.GetByIdAsync(command.RoleId))
            .ReturnsAsync(role);

        _userOrgRoleRepositoryMock
            .Setup(x => x.ExistsAsync(command.UserId, command.OrganizationId, command.RoleId))
            .ReturnsAsync(false);

        _userOrgRoleRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<UserOrganizationRole>()))
            .Returns(Task.CompletedTask);

        // Act
        await _roleService.AssignRoleToUserAsync(command);

        // Assert
        _userOrgRoleRepositoryMock.Verify(x =>
            x.AddAsync(It.Is<UserOrganizationRole>(a =>
                a.UserId == command.UserId &&
                a.OrganizationId == command.OrganizationId &&
                a.RoleId == command.RoleId)),
            Times.Once);
    }

    [Fact]
    public async Task AssignRoleToUserAsync_ShouldThrowUserNotFoundException_WhenUserDoesNotExist()
    {
        // Arrange
        var command = CreateAssignRoleCommand();

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(command.UserId))
            .ReturnsAsync((User?)null);

        // Act
        Func<Task> act = async () => await _roleService.AssignRoleToUserAsync(command);

        // Assert
        await act.Should().ThrowAsync<UserNotFoundException>();

        _userOrgRoleRepositoryMock.Verify(x =>
            x.AddAsync(It.IsAny<UserOrganizationRole>()),
            Times.Never);
    }

    [Fact]
    public async Task AssignRoleToUserAsync_ShouldThrowUserInactiveException_WhenUserIsInactive()
    {
        // Arrange
        var command = CreateAssignRoleCommand();
        var user = CreateUser(isActive: false);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(command.UserId))
            .ReturnsAsync(user);

        // Act
        Func<Task> act = async () => await _roleService.AssignRoleToUserAsync(command);

        // Assert
        await act.Should().ThrowAsync<UserInactiveException>();

        _roleRepositoryMock.Verify(x =>
            x.GetByIdAsync(It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public async Task AssignRoleToUserAsync_ShouldThrowRoleNotFoundException_WhenRoleDoesNotExist()
    {
        // Arrange
        var command = CreateAssignRoleCommand();
        var user = CreateUser();

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(command.UserId))
            .ReturnsAsync(user);

        _roleRepositoryMock
            .Setup(x => x.GetByIdAsync(command.RoleId))
            .ReturnsAsync((Role?)null);

        // Act
        Func<Task> act = async () => await _roleService.AssignRoleToUserAsync(command);

        // Assert
        await act.Should().ThrowAsync<RoleNotFoundException>();

        _userOrgRoleRepositoryMock.Verify(x =>
            x.AddAsync(It.IsAny<UserOrganizationRole>()),
            Times.Never);
    }

    [Fact]
    public async Task AssignRoleToUserAsync_ShouldThrowRoleAlreadyAssignedException_WhenRoleAlreadyAssigned()
    {
        // Arrange
        var command = CreateAssignRoleCommand();
        var user = CreateUser();
        var role = CreateRole();

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(command.UserId))
            .ReturnsAsync(user);

        _roleRepositoryMock
            .Setup(x => x.GetByIdAsync(command.RoleId))
            .ReturnsAsync(role);

        _userOrgRoleRepositoryMock
            .Setup(x => x.ExistsAsync(command.UserId, command.OrganizationId, command.RoleId))
            .ReturnsAsync(true);

        // Act
        Func<Task> act = async () => await _roleService.AssignRoleToUserAsync(command);

        // Assert
        await act.Should().ThrowAsync<RoleAlreadyAssignedException>();

        _userOrgRoleRepositoryMock.Verify(x =>
            x.AddAsync(It.IsAny<UserOrganizationRole>()),
            Times.Never);
    }

    [Fact]
    public async Task AssignRoleToUserAsync_ShouldThrowArgumentNullException_WhenCommandIsNull()
    {
        // Act
        Func<Task> act = async () => await _roleService.AssignRoleToUserAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region RemoveRoleFromUserAsync

    [Fact]
    public async Task RemoveRoleFromUserAsync_ShouldRemoveRole_WhenAssignmentExists()
    {
        // Arrange
        _userOrgRoleRepositoryMock
            .Setup(x => x.ExistsAsync(1, 1, 1))
            .ReturnsAsync(true);

        _userOrgRoleRepositoryMock
            .Setup(x => x.RemoveAsync(1, 1, 1))
            .Returns(Task.CompletedTask);

        // Act
        await _roleService.RemoveRoleFromUserAsync(1, 1, 1);

        // Assert
        _userOrgRoleRepositoryMock.Verify(x =>
            x.RemoveAsync(1, 1, 1),
            Times.Once);
    }

    [Fact]
    public async Task RemoveRoleFromUserAsync_ShouldThrowRoleNotFoundException_WhenAssignmentDoesNotExist()
    {
        // Arrange
        _userOrgRoleRepositoryMock
            .Setup(x => x.ExistsAsync(1, 1, 99))
            .ReturnsAsync(false);

        // Act
        Func<Task> act = async () => await _roleService.RemoveRoleFromUserAsync(1, 1, 99);

        // Assert
        await act.Should().ThrowAsync<RoleNotFoundException>();

        _userOrgRoleRepositoryMock.Verify(x =>
            x.RemoveAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()),
            Times.Never);
    }

    #endregion

    #region GetUserPermissionsAsync

    [Fact]
    public async Task GetUserPermissionsAsync_ShouldReturnMergedPermissions_WhenUserHasMultipleRoles()
    {
        // Arrange
        var roleIds = new List<int> { 1, 2 };

        _userOrgRoleRepositoryMock
            .Setup(x => x.GetRoleIdsByUserAndOrganizationAsync(1, 1))
            .ReturnsAsync(roleIds);

        _rolePermissionRepositoryMock
            .Setup(x => x.GetPermissionNamesByRoleIdAsync(1))
            .ReturnsAsync(new List<string> { "documents:read", "documents:write" });

        _rolePermissionRepositoryMock
            .Setup(x => x.GetPermissionNamesByRoleIdAsync(2))
            .ReturnsAsync(new List<string> { "documents:read", "users:read" });

        // Act
        var result = await _roleService.GetUserPermissionsAsync(1, 1);

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain("documents:read");
        result.Should().Contain("documents:write");
        result.Should().Contain("users:read");
    }

    [Fact]
    public async Task GetUserPermissionsAsync_ShouldReturnEmptyList_WhenUserHasNoRoles()
    {
        // Arrange
        _userOrgRoleRepositoryMock
            .Setup(x => x.GetRoleIdsByUserAndOrganizationAsync(1, 1))
            .ReturnsAsync(new List<int>());

        // Act
        var result = await _roleService.GetUserPermissionsAsync(1, 1);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region UserHasPermissionAsync

    [Fact]
    public async Task UserHasPermissionAsync_ShouldReturnTrue_WhenUserHasPermission()
    {
        // Arrange
        _userOrgRoleRepositoryMock
            .Setup(x => x.GetRoleIdsByUserAndOrganizationAsync(1, 1))
            .ReturnsAsync(new List<int> { 1 });

        _rolePermissionRepositoryMock
            .Setup(x => x.GetPermissionNamesByRoleIdAsync(1))
            .ReturnsAsync(new List<string> { "documents:read" });

        // Act
        var result = await _roleService.UserHasPermissionAsync(1, 1, "documents:read");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task UserHasPermissionAsync_ShouldReturnFalse_WhenUserDoesNotHavePermission()
    {
        // Arrange
        _userOrgRoleRepositoryMock
            .Setup(x => x.GetRoleIdsByUserAndOrganizationAsync(1, 1))
            .ReturnsAsync(new List<int> { 1 });

        _rolePermissionRepositoryMock
            .Setup(x => x.GetPermissionNamesByRoleIdAsync(1))
            .ReturnsAsync(new List<string> { "documents:read" });

        // Act
        var result = await _roleService.UserHasPermissionAsync(1, 1, "users:manage");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region GetRolesByOrganizationAsync

    [Fact]
    public async Task GetRolesByOrganizationAsync_ShouldReturnRoles()
    {
        // Arrange
        var roles = new List<Role>
        {
            CreateRole(id: 1, name: "Admin"),
            CreateRole(id: 2, name: "Reader")
        };

        _roleRepositoryMock
            .Setup(x => x.GetByOrganizationIdAsync(1))
            .ReturnsAsync(roles);

        // Act
        var result = await _roleService.GetRolesByOrganizationAsync(1);

        // Assert
        result.Should().HaveCount(2);
    }

    #endregion

    #region Helpers

    private static AssignRoleCommand CreateAssignRoleCommand(
        int userId = 1,
        int organizationId = 1,
        int roleId = 1)
    {
        return new AssignRoleCommand
        {
            UserId = userId,
            OrganizationId = organizationId,
            RoleId = roleId
        };
    }

    private static User CreateUser(
        int id = 1,
        string email = "user@test.pl",
        bool isActive = true)
    {
        var user = new User
        {
            Id = id,
            Email = email,
            DisplayName = "Test User",
            OrganizationId = 1
        };

        if (!isActive)
            user.Deactivate();

        return user;
    }

    private static Role CreateRole(
        int id = 1,
        string name = "Admin",
        int organizationId = 1)
    {
        return new Role
        {
            Id = id,
            Name = name,
            OrganizationId = organizationId
        };
    }

    #endregion
}
