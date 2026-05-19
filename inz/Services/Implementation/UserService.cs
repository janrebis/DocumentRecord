using inz.Models;
using inz.Repository.Interface;
using inz.Services.Interface;
using inz.UserExceptions;

namespace inz.Services.Implementation;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IUserOrganizationRoleRepository _userOrgRoleRepository;
    private readonly ILogger<UserService> _logger;

    public UserService(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IRoleRepository roleRepository,
        IUserOrganizationRoleRepository userOrgRoleRepository,
        ILogger<UserService> logger)
    {
        _userRepository = userRepository;
        _organizationRepository = organizationRepository;
        _roleRepository = roleRepository;
        _userOrgRoleRepository = userOrgRoleRepository;
        _logger = logger;
    }

    public async Task<User> SyncUserFromEntraAsync(SyncUserCommand command)
    {
        ValidateSyncCommand(command);

        var existingUser = await _userRepository.GetByEntraObjectIdAsync(command.EntraObjectId);

        if (existingUser is not null)
        {
            existingUser.UpdateProfile(command.DisplayName, command.Email);
            existingUser.UpdateLastLogin();

            await _userRepository.UpdateUserAsync(existingUser.Id, existingUser);

            _logger.LogInformation(
                "Zsynchronizowano istniejącego użytkownika {Email}.",
                existingUser.Email);

            return existingUser;
        }

        var newUser = new User
        {
            EntraObjectId = command.EntraObjectId,
            Email = command.Email,
            DisplayName = command.DisplayName,
            LastLoginAt = DateTime.UtcNow
        };

        var userId = await _userRepository.AddUserAsync(newUser);

        _logger.LogInformation(
            "Utworzono nowego użytkownika {Email} (id: {UserId}) przez sync z Entra ID.",
            newUser.Email,
            userId);

        return newUser;
    }

    public async Task<int> CreateUserAsync(CreateUserCommand command)
    {
        ValidateCreateCommand(command);

        var existingUser = await _userRepository.GetByEntraObjectIdAsync(command.EntraObjectId);

        if (existingUser is not null)
            throw new UserAlreadyExistsException(
                $"Użytkownik z Entra Object ID {command.EntraObjectId} już istnieje.");

        var organization = await _organizationRepository.GetByIdAsync(command.OrganizationId);

        if (organization is null)
            throw new OrganizationNotFoundException(
                $"Nie znaleziono organizacji o id {command.OrganizationId}.");

        var role = await _roleRepository.GetByIdAsync(command.RoleId);

        if (role is null)
            throw new RoleNotFoundException(
                $"Nie znaleziono roli o id {command.RoleId}.");

        var user = new User
        {
            EntraObjectId = command.EntraObjectId,
            Email = command.Email,
            DisplayName = command.DisplayName,
            OrganizationId = command.OrganizationId
        };

        var userId = await _userRepository.AddUserAsync(user);

        var assignment = new UserOrganizationRole
        {
            UserId = userId,
            OrganizationId = command.OrganizationId,
            RoleId = command.RoleId
        };

        await _userOrgRoleRepository.AddAsync(assignment);

        _logger.LogInformation(
            "Admin utworzył użytkownika {Email} (id: {UserId}) z rolą {RoleName}.",
            user.Email,
            userId,
            role.Name);

        return userId;
    }

    public async Task<User> GetUserByIdAsync(int userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);

        if (user is null)
            throw new UserNotFoundException($"Nie znaleziono użytkownika o id {userId}.");

        return user;
    }

    public async Task<IReadOnlyList<User>> GetUsersByOrganizationAsync(int organizationId)
    {
        var organization = await _organizationRepository.GetByIdAsync(organizationId);

        if (organization is null)
            throw new OrganizationNotFoundException(
                $"Nie znaleziono organizacji o id {organizationId}.");

        return await _userRepository.GetByOrganizationIdAsync(organizationId);
    }

    public async Task DeactivateUserAsync(int userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);

        if (user is null)
            throw new UserNotFoundException($"Nie znaleziono użytkownika o id {userId}.");

        user.Deactivate();

        await _userRepository.UpdateUserAsync(userId, user);

        _logger.LogInformation(
            "Dezaktywowano użytkownika {Email} (id: {UserId}).",
            user.Email,
            userId);
    }

    public async Task ActivateUserAsync(int userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);

        if (user is null)
            throw new UserNotFoundException($"Nie znaleziono użytkownika o id {userId}.");

        user.Activate();

        await _userRepository.UpdateUserAsync(userId, user);

        _logger.LogInformation(
            "Aktywowano użytkownika {Email} (id: {UserId}).",
            user.Email,
            userId);
    }

    private static void ValidateSyncCommand(SyncUserCommand command)
    {
        if (command is null)
            throw new ArgumentNullException(nameof(command));

        if (string.IsNullOrWhiteSpace(command.EntraObjectId))
            throw new ArgumentException("EntraObjectId jest wymagany.", nameof(command));

        if (string.IsNullOrWhiteSpace(command.Email))
            throw new ArgumentException("Email jest wymagany.", nameof(command));

        if (string.IsNullOrWhiteSpace(command.DisplayName))
            throw new ArgumentException("Nazwa wyświetlana jest wymagana.", nameof(command));
    }

    private static void ValidateCreateCommand(CreateUserCommand command)
    {
        if (command is null)
            throw new ArgumentNullException(nameof(command));

        if (string.IsNullOrWhiteSpace(command.EntraObjectId))
            throw new ArgumentException("EntraObjectId jest wymagany.", nameof(command));

        if (string.IsNullOrWhiteSpace(command.Email))
            throw new ArgumentException("Email jest wymagany.", nameof(command));

        if (string.IsNullOrWhiteSpace(command.DisplayName))
            throw new ArgumentException("Nazwa wyświetlana jest wymagana.", nameof(command));

        if (command.OrganizationId <= 0)
            throw new ArgumentException("OrganizationId jest wymagany.", nameof(command));

        if (command.RoleId <= 0)
            throw new ArgumentException("RoleId jest wymagany.", nameof(command));
    }
}
