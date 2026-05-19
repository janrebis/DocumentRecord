using inz.Models;

namespace inz.Services.Interface;

public interface IRoleService
{
    Task AssignRoleToUserAsync(AssignRoleCommand command);

    Task RemoveRoleFromUserAsync(int userId, int organizationId, int roleId);

    Task<IReadOnlyList<string>> GetUserPermissionsAsync(int userId, int organizationId);

    Task<bool> UserHasPermissionAsync(int userId, int organizationId, string permission);

    Task<IReadOnlyList<Role>> GetRolesByOrganizationAsync(int organizationId);
}
