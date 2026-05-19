using System.Collections.Concurrent;
using inz.Models;
using inz.Repository.Interface;

namespace inz.Repository.Implementations;

public class InMemoryRoleRepository : IRoleRepository
{
    private readonly ConcurrentDictionary<int, Role> _roles = new();
    private int _idCounter;

    public Task<int> AddRoleAsync(Role role)
    {
        var id = Interlocked.Increment(ref _idCounter);

        role.Id = id;

        _roles.TryAdd(id, role);

        return Task.FromResult(id);
    }

    public Task<Role?> GetByIdAsync(int roleId)
    {
        _roles.TryGetValue(roleId, out var role);

        return Task.FromResult(role);
    }

    public Task<Role?> GetByNameAndOrganizationAsync(string name, int organizationId)
    {
        var role = _roles.Values
            .FirstOrDefault(r =>
                r.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                r.OrganizationId == organizationId);

        return Task.FromResult(role);
    }

    public Task<IReadOnlyList<Role>> GetByOrganizationIdAsync(int organizationId)
    {
        var roles = _roles.Values
            .Where(r => r.OrganizationId == organizationId)
            .ToList();

        return Task.FromResult<IReadOnlyList<Role>>(roles);
    }
}
