using inz.Models;
using inz.Repository.Interface;
using inz.Services.Interface;
using inz.UserExceptions;

namespace inz.Services.Implementation;

public class RoleService : IRoleService
{
    private readonly IRoleRepository _roleRepository;
    private readonly IRolePermissionRepository _rolePermissionRepository;
    private readonly IUserOrganizationRoleRepository _userOrgRoleRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<RoleService> _logger;

    public RoleService(
        IRoleRepository roleRepository,
        IRolePermissionRepository rolePermissionRepository,
        IUserOrganizationRoleRepository userOrgRoleRepository,
        IUserRepository userRepository,
        ILogger<RoleService> logger)
    {
        _roleRepository = roleRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _userOrgRoleRepository = userOrgRoleRepository;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task AssignRoleToUserAsync(AssignRoleCommand command)
    {
        if (command is null)
            throw new ArgumentNullException(nameof(command));

        var user = await _userRepository.GetByIdAsync(command.UserId);

        if (user is null)
            throw new UserNotFoundException(
                $"Nie znaleziono użytkownika o id {command.UserId}.");

        if (!user.IsActive)
            throw new UserInactiveException(
                $"Użytkownik {user.Email} jest nieaktywny.");

        var role = await _roleRepository.GetByIdAsync(command.RoleId);

        if (role is null)
            throw new RoleNotFoundException(
                $"Nie znaleziono roli o id {command.RoleId}.");

        var alreadyAssigned = await _userOrgRoleRepository.ExistsAsync(
            command.UserId, command.OrganizationId, command.RoleId);

        if (alreadyAssigned)
            throw new RoleAlreadyAssignedException(
                $"Rola {role.Name} jest już przypisana do użytkownika {user.Email}.");

        var assignment = new UserOrganizationRole
        {
            UserId = command.UserId,
            OrganizationId = command.OrganizationId,
            RoleId = command.RoleId
        };

        await _userOrgRoleRepository.AddAsync(assignment);

        _logger.LogInformation(
            "Przypisano rolę {RoleName} użytkownikowi {Email} w organizacji {OrgId}.",
            role.Name,
            user.Email,
            command.OrganizationId);
    }

    public async Task RemoveRoleFromUserAsync(int userId, int organizationId, int roleId)
    {
        var exists = await _userOrgRoleRepository.ExistsAsync(userId, organizationId, roleId);

        if (!exists)
            throw new RoleNotFoundException(
                "Nie znaleziono takiego przypisania roli.");

        await _userOrgRoleRepository.RemoveAsync(userId, organizationId, roleId);

        _logger.LogInformation(
            "Usunięto rolę {RoleId} użytkownikowi {UserId} w organizacji {OrgId}.",
            roleId,
            userId,
            organizationId);
    }

    public async Task<IReadOnlyList<string>> GetUserPermissionsAsync(
        int userId, int organizationId)
    {
        var roleIds = await _userOrgRoleRepository
            .GetRoleIdsByUserAndOrganizationAsync(userId, organizationId);

        var allPermissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var roleId in roleIds)
        {
            var permissions = await _rolePermissionRepository
                .GetPermissionNamesByRoleIdAsync(roleId);

            foreach (var permission in permissions)
                allPermissions.Add(permission);
        }

        return allPermissions.ToList();
    }

    public async Task<bool> UserHasPermissionAsync(
        int userId, int organizationId, string permission)
    {
        var permissions = await GetUserPermissionsAsync(userId, organizationId);

        return permissions.Contains(permission);
    }

    public async Task<IReadOnlyList<Role>> GetRolesByOrganizationAsync(int organizationId)
    {
        return await _roleRepository.GetByOrganizationIdAsync(organizationId);
    }
}
