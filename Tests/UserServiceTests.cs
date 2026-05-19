using FluentAssertions;
using inz.Models;
using inz.Repository.Interface;
using inz.Services.Implementation;
using inz.UserExceptions;
using Microsoft.Extensions.Logging;
using Moq;

namespace inz.Tests;

public class UserServiceTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IOrganizationRepository> _organizationRepositoryMock = new();
    private readonly Mock<IRoleRepository> _roleRepositoryMock = new();
    private readonly Mock<IUserOrganizationRoleRepository> _userOrgRoleRepositoryMock = new();
    private readonly Mock<ILogger<UserService>> _loggerMock = new();

    private readonly UserService _userService;

    public UserServiceTests()
    {
        _userService = new UserService(
            _userRepositoryMock.Object,
            _organizationRepositoryMock.Object,
            _roleRepositoryMock.Object,
            _userOrgRoleRepositoryMock.Object,
            _loggerMock.Object);
    }

    #region SyncUserFromEntraAsync

    [Fact]
    public async Task SyncUserFromEntraAsync_ShouldUpdateExistingUser_WhenUserExists()
    {
        // Arrange
        var command = CreateSyncCommand();
        var existingUser = CreateUser(entraObjectId: command.EntraObjectId);

        _userRepositoryMock
            .Setup(x => x.GetByEntraObjectIdAsync(command.EntraObjectId))
            .ReturnsAsync(existingUser);

        _userRepositoryMock
            .Setup(x => x.UpdateUserAsync(existingUser.Id, existingUser))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _userService.SyncUserFromEntraAsync(command);

        // Assert
        result.Should().BeSameAs(existingUser);
        result.Email.Should().Be(command.Email);
        result.DisplayName.Should().Be(command.DisplayName);
        result.LastLoginAt.Should().NotBeNull();

        _userRepositoryMock.Verify(x =>
            x.UpdateUserAsync(existingUser.Id, existingUser),
            Times.Once);

        _userRepositoryMock.Verify(x =>
            x.AddUserAsync(It.IsAny<User>()),
            Times.Never);
    }

    [Fact]
    public async Task SyncUserFromEntraAsync_ShouldCreateNewUser_WhenUserDoesNotExist()
    {
        // Arrange
        var command = CreateSyncCommand();
        var newUserId = 5;

        _userRepositoryMock
            .Setup(x => x.GetByEntraObjectIdAsync(command.EntraObjectId))
            .ReturnsAsync((User?)null);

        _userRepositoryMock
            .Setup(x => x.AddUserAsync(It.IsAny<User>()))
            .ReturnsAsync(newUserId);

        // Act
        var result = await _userService.SyncUserFromEntraAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.EntraObjectId.Should().Be(command.EntraObjectId);
        result.Email.Should().Be(command.Email);
        result.DisplayName.Should().Be(command.DisplayName);

        _userRepositoryMock.Verify(x =>
            x.AddUserAsync(It.Is<User>(u =>
                u.EntraObjectId == command.EntraObjectId &&
                u.Email == command.Email)),
            Times.Once);

        _userRepositoryMock.Verify(x =>
            x.UpdateUserAsync(It.IsAny<int>(), It.IsAny<User>()),
            Times.Never);
    }

    [Fact]
    public async Task SyncUserFromEntraAsync_ShouldThrowArgumentNullException_WhenCommandIsNull()
    {
        // Act
        Func<Task> act = async () => await _userService.SyncUserFromEntraAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SyncUserFromEntraAsync_ShouldThrowArgumentException_WhenEntraObjectIdIsEmpty()
    {
        // Arrange
        var command = new SyncUserCommand
        {
            EntraObjectId = "",
            Email = "user@test.pl",
            DisplayName = "Test User"
        };

        // Act
        Func<Task> act = async () => await _userService.SyncUserFromEntraAsync(command);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SyncUserFromEntraAsync_ShouldThrowArgumentException_WhenEmailIsEmpty()
    {
        // Arrange
        var command = new SyncUserCommand
        {
            EntraObjectId = "entra-oid-123",
            Email = "",
            DisplayName = "Test User"
        };

        // Act
        Func<Task> act = async () => await _userService.SyncUserFromEntraAsync(command);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region CreateUserAsync

    [Fact]
    public async Task CreateUserAsync_ShouldCreateUserAndAssignRole_WhenInputIsValid()
    {
        // Arrange
        var command = CreateCreateUserCommand();
        var organization = CreateOrganization();
        var role = CreateRole();
        var userId = 10;

        _userRepositoryMock
            .Setup(x => x.GetByEntraObjectIdAsync(command.EntraObjectId))
            .ReturnsAsync((User?)null);

        _organizationRepositoryMock
            .Setup(x => x.GetByIdAsync(command.OrganizationId))
            .ReturnsAsync(organization);

        _roleRepositoryMock
            .Setup(x => x.GetByIdAsync(command.RoleId))
            .ReturnsAsync(role);

        _userRepositoryMock
            .Setup(x => x.AddUserAsync(It.IsAny<User>()))
            .ReturnsAsync(userId);

        _userOrgRoleRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<UserOrganizationRole>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _userService.CreateUserAsync(command);

        // Assert
        result.Should().Be(userId);

        _userRepositoryMock.Verify(x =>
            x.AddUserAsync(It.Is<User>(u =>
                u.Email == command.Email &&
                u.OrganizationId == command.OrganizationId)),
            Times.Once);

        _userOrgRoleRepositoryMock.Verify(x =>
            x.AddAsync(It.Is<UserOrganizationRole>(a =>
                a.UserId == userId &&
                a.OrganizationId == command.OrganizationId &&
                a.RoleId == command.RoleId)),
            Times.Once);
    }

    [Fact]
    public async Task CreateUserAsync_ShouldThrowUserAlreadyExistsException_WhenEntraObjectIdExists()
    {
        // Arrange
        var command = CreateCreateUserCommand();
        var existingUser = CreateUser(entraObjectId: command.EntraObjectId);

        _userRepositoryMock
            .Setup(x => x.GetByEntraObjectIdAsync(command.EntraObjectId))
            .ReturnsAsync(existingUser);

        // Act
        Func<Task> act = async () => await _userService.CreateUserAsync(command);

        // Assert
        await act.Should().ThrowAsync<UserAlreadyExistsException>();

        _userRepositoryMock.Verify(x =>
            x.AddUserAsync(It.IsAny<User>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateUserAsync_ShouldThrowOrganizationNotFoundException_WhenOrganizationDoesNotExist()
    {
        // Arrange
        var command = CreateCreateUserCommand();

        _userRepositoryMock
            .Setup(x => x.GetByEntraObjectIdAsync(command.EntraObjectId))
            .ReturnsAsync((User?)null);

        _organizationRepositoryMock
            .Setup(x => x.GetByIdAsync(command.OrganizationId))
            .ReturnsAsync((Organization?)null);

        // Act
        Func<Task> act = async () => await _userService.CreateUserAsync(command);

        // Assert
        await act.Should().ThrowAsync<OrganizationNotFoundException>();

        _userRepositoryMock.Verify(x =>
            x.AddUserAsync(It.IsAny<User>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateUserAsync_ShouldThrowRoleNotFoundException_WhenRoleDoesNotExist()
    {
        // Arrange
        var command = CreateCreateUserCommand();
        var organization = CreateOrganization();

        _userRepositoryMock
            .Setup(x => x.GetByEntraObjectIdAsync(command.EntraObjectId))
            .ReturnsAsync((User?)null);

        _organizationRepositoryMock
            .Setup(x => x.GetByIdAsync(command.OrganizationId))
            .ReturnsAsync(organization);

        _roleRepositoryMock
            .Setup(x => x.GetByIdAsync(command.RoleId))
            .ReturnsAsync((Role?)null);

        // Act
        Func<Task> act = async () => await _userService.CreateUserAsync(command);

        // Assert
        await act.Should().ThrowAsync<RoleNotFoundException>();

        _userRepositoryMock.Verify(x =>
            x.AddUserAsync(It.IsAny<User>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateUserAsync_ShouldThrowArgumentNullException_WhenCommandIsNull()
    {
        // Act
        Func<Task> act = async () => await _userService.CreateUserAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task CreateUserAsync_ShouldThrowArgumentException_WhenOrganizationIdIsInvalid()
    {
        // Arrange
        var command = new CreateUserCommand
        {
            EntraObjectId = "entra-oid-123",
            Email = "user@test.pl",
            DisplayName = "Test User",
            OrganizationId = 0,
            RoleId = 1
        };

        // Act
        Func<Task> act = async () => await _userService.CreateUserAsync(command);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region GetUserByIdAsync

    [Fact]
    public async Task GetUserByIdAsync_ShouldReturnUser_WhenUserExists()
    {
        // Arrange
        var user = CreateUser(id: 5);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(5))
            .ReturnsAsync(user);

        // Act
        var result = await _userService.GetUserByIdAsync(5);

        // Assert
        result.Should().BeSameAs(user);
    }

    [Fact]
    public async Task GetUserByIdAsync_ShouldThrowUserNotFoundException_WhenUserDoesNotExist()
    {
        // Arrange
        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(99))
            .ReturnsAsync((User?)null);

        // Act
        Func<Task> act = async () => await _userService.GetUserByIdAsync(99);

        // Assert
        await act.Should().ThrowAsync<UserNotFoundException>();
    }

    #endregion

    #region GetUsersByOrganizationAsync

    [Fact]
    public async Task GetUsersByOrganizationAsync_ShouldReturnUsers_WhenOrganizationExists()
    {
        // Arrange
        var organization = CreateOrganization();
        var users = new List<User> { CreateUser(id: 1), CreateUser(id: 2) };

        _organizationRepositoryMock
            .Setup(x => x.GetByIdAsync(organization.Id))
            .ReturnsAsync(organization);

        _userRepositoryMock
            .Setup(x => x.GetByOrganizationIdAsync(organization.Id))
            .ReturnsAsync(users);

        // Act
        var result = await _userService.GetUsersByOrganizationAsync(organization.Id);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetUsersByOrganizationAsync_ShouldThrowOrganizationNotFoundException_WhenOrganizationDoesNotExist()
    {
        // Arrange
        _organizationRepositoryMock
            .Setup(x => x.GetByIdAsync(99))
            .ReturnsAsync((Organization?)null);

        // Act
        Func<Task> act = async () => await _userService.GetUsersByOrganizationAsync(99);

        // Assert
        await act.Should().ThrowAsync<OrganizationNotFoundException>();
    }

    #endregion

    #region DeactivateUserAsync

    [Fact]
    public async Task DeactivateUserAsync_ShouldDeactivateUser_WhenUserIsActive()
    {
        // Arrange
        var user = CreateUser(id: 1);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(1))
            .ReturnsAsync(user);

        _userRepositoryMock
            .Setup(x => x.UpdateUserAsync(1, user))
            .Returns(Task.CompletedTask);

        // Act
        await _userService.DeactivateUserAsync(1);

        // Assert
        user.IsActive.Should().BeFalse();

        _userRepositoryMock.Verify(x =>
            x.UpdateUserAsync(1, It.Is<User>(u => !u.IsActive)),
            Times.Once);
    }

    [Fact]
    public async Task DeactivateUserAsync_ShouldThrowUserNotFoundException_WhenUserDoesNotExist()
    {
        // Arrange
        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(99))
            .ReturnsAsync((User?)null);

        // Act
        Func<Task> act = async () => await _userService.DeactivateUserAsync(99);

        // Assert
        await act.Should().ThrowAsync<UserNotFoundException>();
    }

    [Fact]
    public async Task DeactivateUserAsync_ShouldThrowInvalidOperationException_WhenUserIsAlreadyInactive()
    {
        // Arrange
        var user = CreateUser(id: 1, isActive: false);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(1))
            .ReturnsAsync(user);

        // Act
        Func<Task> act = async () => await _userService.DeactivateUserAsync(1);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();

        _userRepositoryMock.Verify(x =>
            x.UpdateUserAsync(It.IsAny<int>(), It.IsAny<User>()),
            Times.Never);
    }

    #endregion

    #region ActivateUserAsync

    [Fact]
    public async Task ActivateUserAsync_ShouldActivateUser_WhenUserIsInactive()
    {
        // Arrange
        var user = CreateUser(id: 1, isActive: false);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(1))
            .ReturnsAsync(user);

        _userRepositoryMock
            .Setup(x => x.UpdateUserAsync(1, user))
            .Returns(Task.CompletedTask);

        // Act
        await _userService.ActivateUserAsync(1);

        // Assert
        user.IsActive.Should().BeTrue();

        _userRepositoryMock.Verify(x =>
            x.UpdateUserAsync(1, It.Is<User>(u => u.IsActive)),
            Times.Once);
    }

    [Fact]
    public async Task ActivateUserAsync_ShouldThrowInvalidOperationException_WhenUserIsAlreadyActive()
    {
        // Arrange
        var user = CreateUser(id: 1, isActive: true);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(1))
            .ReturnsAsync(user);

        // Act
        Func<Task> act = async () => await _userService.ActivateUserAsync(1);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    #endregion

    #region Helpers

    private static SyncUserCommand CreateSyncCommand(
        string entraObjectId = "entra-oid-123",
        string email = "user@test.pl",
        string displayName = "Test User")
    {
        return new SyncUserCommand
        {
            EntraObjectId = entraObjectId,
            Email = email,
            DisplayName = displayName
        };
    }

    private static CreateUserCommand CreateCreateUserCommand(
        string entraObjectId = "entra-oid-123",
        string email = "user@test.pl",
        string displayName = "Test User",
        int organizationId = 1,
        int roleId = 1)
    {
        return new CreateUserCommand
        {
            EntraObjectId = entraObjectId,
            Email = email,
            DisplayName = displayName,
            OrganizationId = organizationId,
            RoleId = roleId
        };
    }

    private static Organization CreateOrganization(int id = 1, string name = "Test Org")
    {
        return new Organization { Id = id, Name = name };
    }

    private static Role CreateRole(int id = 1, string name = "Admin", int organizationId = 1)
    {
        return new Role { Id = id, Name = name, OrganizationId = organizationId };
    }

    private static User CreateUser(
        int id = 1,
        string email = "user@test.pl",
        string displayName = "Test User",
        string entraObjectId = "entra-oid-123",
        int organizationId = 1,
        bool isActive = true)
    {
        var user = new User
        {
            Id = id,
            Email = email,
            DisplayName = displayName,
            EntraObjectId = entraObjectId,
            OrganizationId = organizationId
        };

        if (!isActive)
            user.Deactivate();

        return user;
    }

    #endregion
}
