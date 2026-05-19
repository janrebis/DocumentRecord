using inz.Models;

namespace inz.Repository.Interface;

public interface IRoleRepository
{
    Task<int> AddRoleAsync(Role role);
    Task<Role?> GetByIdAsync(int roleId);
    Task<Role?> GetByNameAndOrganizationAsync(string name, int organizationId);
    Task<IReadOnlyList<Role>> GetByOrganizationIdAsync(int organizationId);
}
