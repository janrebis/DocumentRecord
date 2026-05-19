using FluentAssertions;
using inz.Models;
using inz.Repository.Interface;
using inz.Services.Implementation;
using inz.UserExceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace inz.Tests;

public class LocalAuthServiceTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly Mock<IUserCredentialsRepository> _credentialsRepositoryMock = new();
    private readonly Mock<IOrganizationRepository> _organizationRepositoryMock = new();
    private readonly Mock<IRoleRepository> _roleRepositoryMock = new();
    private readonly Mock<IUserOrganizationRoleRepository> _userOrgRoleRepositoryMock = new();
    private readonly Mock<ILogger<LocalAuthService>> _loggerMock = new();

    private readonly LocalAuthService _authService;

    private static readonly JwtSettings DefaultJwtSettings = new()
    {
        SecretKey = "TestSecretKeyThatIsAtLeast32Characters!",
        Issuer = "test-issuer",
        Audience = "test-audience",
        ExpirationInMinutes = 60
    };

    public LocalAuthServiceTests()
    {
        var jwtOptions = Options.Create(DefaultJwtSettings);

        _authService = new LocalAuthService(
            _userRepositoryMock.Object,
            _credentialsRepositoryMock.Object,
            _organizationRepositoryMock.Object,
            _roleRepositoryMock.Object,
            _userOrgRoleRepositoryMock.Object,
            jwtOptions,
            _loggerMock.Object);
    }

    #region RegisterAsync

    [Fact]
    public async Task RegisterAsync_ShouldCreateUserAndCredentials_WhenInputIsValid()
    {
        // Arrange
        var command = CreateRegisterCommand();
        var organization = CreateOrganization();
        var readerRole = CreateRole(id: 2, name: "Reader");
        var userId = 1;

        _credentialsRepositoryMock
            .Setup(x => x.ExistsByEmailAsync(command.Email))
            .ReturnsAsync(false);

        _organizationRepositoryMock
            .Setup(x => x.GetByIdAsync(1))
            .ReturnsAsync(organization);

        _roleRepositoryMock
            .Setup(x => x.GetByNameAndOrganizationAsync("Reader", organization.Id))
            .ReturnsAsync(readerRole);

        _userRepositoryMock
            .Setup(x => x.AddUserAsync(It.IsAny<User>()))
            .ReturnsAsync(userId);

        _credentialsRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<UserCredentials>()))
            .Returns(Task.CompletedTask);

        _userOrgRoleRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<UserOrganizationRole>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _authService.RegisterAsync(command);

        // Assert
        result.Should().Be(userId);

        _userRepositoryMock.Verify(x =>
            x.AddUserAsync(It.Is<User>(u =>
                u.Email == command.Email &&
                u.DisplayName == command.DisplayName &&
                u.OrganizationId == organization.Id)),
            Times.Once);

        _credentialsRepositoryMock.Verify(x =>
            x.AddAsync(It.Is<UserCredentials>(c =>
                c.UserId == userId &&
                c.Email == command.Email &&
                !string.IsNullOrEmpty(c.PasswordHash))),
            Times.Once);

        _userOrgRoleRepositoryMock.Verify(x =>
            x.AddAsync(It.Is<UserOrganizationRole>(a =>
                a.UserId == userId &&
                a.OrganizationId == organization.Id &&
                a.RoleId == readerRole.Id)),
            Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_ShouldCreateUserWithoutRole_WhenReaderRoleDoesNotExist()
    {
        // Arrange
        var command = CreateRegisterCommand();
        var organization = CreateOrganization();

        _credentialsRepositoryMock
            .Setup(x => x.ExistsByEmailAsync(command.Email))
            .ReturnsAsync(false);

        _organizationRepositoryMock
            .Setup(x => x.GetByIdAsync(1))
            .ReturnsAsync(organization);

        _roleRepositoryMock
            .Setup(x => x.GetByNameAndOrganizationAsync("Reader", organization.Id))
            .ReturnsAsync((Role?)null);

        _userRepositoryMock
            .Setup(x => x.AddUserAsync(It.IsAny<User>()))
            .ReturnsAsync(1);

        _credentialsRepositoryMock
            .Setup(x => x.AddAsync(It.IsAny<UserCredentials>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _authService.RegisterAsync(command);

        // Assert
        result.Should().Be(1);

        _userOrgRoleRepositoryMock.Verify(x =>
            x.AddAsync(It.IsAny<UserOrganizationRole>()),
            Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_ShouldThrowEmailAlreadyRegisteredException_WhenEmailExists()
    {
        // Arrange
        var command = CreateRegisterCommand();

        _credentialsRepositoryMock
            .Setup(x => x.ExistsByEmailAsync(command.Email))
            .ReturnsAsync(true);

        // Act
        Func<Task> act = async () => await _authService.RegisterAsync(command);

        // Assert
        await act.Should().ThrowAsync<EmailAlreadyRegisteredException>();

        _userRepositoryMock.Verify(x =>
            x.AddUserAsync(It.IsAny<User>()),
            Times.Never);

        _credentialsRepositoryMock.Verify(x =>
            x.AddAsync(It.IsAny<UserCredentials>()),
            Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_ShouldThrowOrganizationNotFoundException_WhenDefaultOrganizationDoesNotExist()
    {
        // Arrange
        var command = CreateRegisterCommand();

        _credentialsRepositoryMock
            .Setup(x => x.ExistsByEmailAsync(command.Email))
            .ReturnsAsync(false);

        _organizationRepositoryMock
            .Setup(x => x.GetByIdAsync(1))
            .ReturnsAsync((Organization?)null);

        // Act
        Func<Task> act = async () => await _authService.RegisterAsync(command);

        // Assert
        await act.Should().ThrowAsync<OrganizationNotFoundException>();

        _userRepositoryMock.Verify(x =>
            x.AddUserAsync(It.IsAny<User>()),
            Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_ShouldThrowArgumentNullException_WhenCommandIsNull()
    {
        // Act
        Func<Task> act = async () => await _authService.RegisterAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task RegisterAsync_ShouldThrowArgumentException_WhenEmailIsEmpty()
    {
        // Arrange
        var command = new RegisterCommand
        {
            Email = "",
            Password = "Password123!",
            DisplayName = "Test User"
        };

        // Act
        Func<Task> act = async () => await _authService.RegisterAsync(command);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();

        _credentialsRepositoryMock.Verify(x =>
            x.ExistsByEmailAsync(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_ShouldThrowArgumentException_WhenPasswordIsEmpty()
    {
        // Arrange
        var command = new RegisterCommand
        {
            Email = "user@test.pl",
            Password = "",
            DisplayName = "Test User"
        };

        // Act
        Func<Task> act = async () => await _authService.RegisterAsync(command);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RegisterAsync_ShouldThrowArgumentException_WhenPasswordIsTooShort()
    {
        // Arrange
        var command = new RegisterCommand
        {
            Email = "user@test.pl",
            Password = "short",
            DisplayName = "Test User"
        };

        // Act
        Func<Task> act = async () => await _authService.RegisterAsync(command);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RegisterAsync_ShouldThrowArgumentException_WhenDisplayNameIsEmpty()
    {
        // Arrange
        var command = new RegisterCommand
        {
            Email = "user@test.pl",
            Password = "Password123!",
            DisplayName = ""
        };

        // Act
        Func<Task> act = async () => await _authService.RegisterAsync(command);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region LoginAsync

    [Fact]
    public async Task LoginAsync_ShouldReturnToken_WhenCredentialsAreValid()
    {
        // Arrange
        var command = CreateLoginCommand();
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(command.Password);
        var credentials = CreateCredentials(userId: 1, email: command.Email, passwordHash: passwordHash);
        var user = CreateUser(id: 1, email: command.Email);

        _credentialsRepositoryMock
            .Setup(x => x.GetByEmailAsync(command.Email))
            .ReturnsAsync(credentials);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(credentials.UserId))
            .ReturnsAsync(user);

        _userRepositoryMock
            .Setup(x => x.UpdateUserAsync(user.Id, user))
            .Returns(Task.CompletedTask);

        // Act
        var token = await _authService.LoginAsync(command);

        // Assert
        token.Should().NotBeNullOrEmpty();

        _userRepositoryMock.Verify(x =>
            x.UpdateUserAsync(user.Id, It.Is<User>(u =>
                u.LastLoginAt != null)),
            Times.Once);
    }

    [Fact]
    public async Task LoginAsync_ShouldThrowInvalidCredentialsException_WhenEmailDoesNotExist()
    {
        // Arrange
        var command = CreateLoginCommand();

        _credentialsRepositoryMock
            .Setup(x => x.GetByEmailAsync(command.Email))
            .ReturnsAsync((UserCredentials?)null);

        // Act
        Func<Task> act = async () => await _authService.LoginAsync(command);

        // Assert
        await act.Should().ThrowAsync<InvalidCredentialsException>();

        _userRepositoryMock.Verify(x =>
            x.GetByIdAsync(It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public async Task LoginAsync_ShouldThrowInvalidCredentialsException_WhenPasswordIsWrong()
    {
        // Arrange
        var command = CreateLoginCommand();
        var credentials = CreateCredentials(
            userId: 1,
            email: command.Email,
            passwordHash: BCrypt.Net.BCrypt.HashPassword("WrongPassword123!"));

        _credentialsRepositoryMock
            .Setup(x => x.GetByEmailAsync(command.Email))
            .ReturnsAsync(credentials);

        // Act
        Func<Task> act = async () => await _authService.LoginAsync(command);

        // Assert
        await act.Should().ThrowAsync<InvalidCredentialsException>();

        _userRepositoryMock.Verify(x =>
            x.GetByIdAsync(It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public async Task LoginAsync_ShouldThrowUserNotFoundException_WhenUserDoesNotExist()
    {
        // Arrange
        var command = CreateLoginCommand();
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(command.Password);
        var credentials = CreateCredentials(userId: 99, email: command.Email, passwordHash: passwordHash);

        _credentialsRepositoryMock
            .Setup(x => x.GetByEmailAsync(command.Email))
            .ReturnsAsync(credentials);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(credentials.UserId))
            .ReturnsAsync((User?)null);

        // Act
        Func<Task> act = async () => await _authService.LoginAsync(command);

        // Assert
        await act.Should().ThrowAsync<UserNotFoundException>();
    }

    [Fact]
    public async Task LoginAsync_ShouldThrowUserInactiveException_WhenUserIsDeactivated()
    {
        // Arrange
        var command = CreateLoginCommand();
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(command.Password);
        var credentials = CreateCredentials(userId: 1, email: command.Email, passwordHash: passwordHash);
        var user = CreateUser(id: 1, email: command.Email, isActive: false);

        _credentialsRepositoryMock
            .Setup(x => x.GetByEmailAsync(command.Email))
            .ReturnsAsync(credentials);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(credentials.UserId))
            .ReturnsAsync(user);

        // Act
        Func<Task> act = async () => await _authService.LoginAsync(command);

        // Assert
        await act.Should().ThrowAsync<UserInactiveException>();

        _userRepositoryMock.Verify(x =>
            x.UpdateUserAsync(It.IsAny<int>(), It.IsAny<User>()),
            Times.Never);
    }

    [Fact]
    public async Task LoginAsync_ShouldThrowArgumentNullException_WhenCommandIsNull()
    {
        // Act
        Func<Task> act = async () => await _authService.LoginAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task LoginAsync_ShouldThrowArgumentException_WhenEmailIsEmpty()
    {
        // Arrange
        var command = new LoginCommand { Email = "", Password = "Password123!" };

        // Act
        Func<Task> act = async () => await _authService.LoginAsync(command);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task LoginAsync_ShouldThrowArgumentException_WhenPasswordIsEmpty()
    {
        // Arrange
        var command = new LoginCommand { Email = "user@test.pl", Password = "" };

        // Act
        Func<Task> act = async () => await _authService.LoginAsync(command);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    #endregion

    #region Helpers

    private static RegisterCommand CreateRegisterCommand(
        string email = "user@test.pl",
        string password = "Password123!",
        string displayName = "Test User")
    {
        return new RegisterCommand
        {
            Email = email,
            Password = password,
            DisplayName = displayName
        };
    }

    private static LoginCommand CreateLoginCommand(
        string email = "user@test.pl",
        string password = "Password123!")
    {
        return new LoginCommand
        {
            Email = email,
            Password = password
        };
    }

    private static Organization CreateOrganization(int id = 1, string name = "Test Org")
    {
        return new Organization
        {
            Id = id,
            Name = name
        };
    }

    private static Role CreateRole(int id = 1, string name = "Reader", int organizationId = 1)
    {
        return new Role
        {
            Id = id,
            Name = name,
            OrganizationId = organizationId
        };
    }

    private static User CreateUser(
        int id = 1,
        string email = "user@test.pl",
        string displayName = "Test User",
        int organizationId = 1,
        bool isActive = true)
    {
        var user = new User
        {
            Id = id,
            Email = email,
            DisplayName = displayName,
            OrganizationId = organizationId
        };

        if (!isActive)
            user.Deactivate();

        return user;
    }

    private static UserCredentials CreateCredentials(
        int userId,
        string email,
        string passwordHash)
    {
        return new UserCredentials
        {
            UserId = userId,
            Email = email,
            PasswordHash = passwordHash
        };
    }

    #endregion
}
