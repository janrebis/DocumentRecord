using inz.Models;

namespace inz.Repository.Interface;

public interface IRolePermissionRepository
{
    Task AddRolePermissionAsync(RolePermission rolePermission);
    Task<IReadOnlyList<string>> GetPermissionNamesByRoleIdAsync(int roleId);
    Task RemoveAllByRoleIdAsync(int roleId);
}

public interface IUserOrganizationRoleRepository
{
    Task AddAsync(UserOrganizationRole userOrganizationRole);
    Task<bool> ExistsAsync(int userId, int organizationId, int roleId);
    Task<IReadOnlyList<int>> GetRoleIdsByUserAndOrganizationAsync(int userId, int organizationId);
    Task RemoveAsync(int userId, int organizationId, int roleId);
}
