using System.Collections.Concurrent;
using inz.Models;
using inz.Repository.Interface;

namespace inz.Repository.Implementations;

public class InMemoryPermissionRepository : IPermissionRepository
{
    private readonly ConcurrentDictionary<int, Permission> _permissions = new();
    private int _idCounter;

    public Task<int> AddPermissionAsync(Permission permission)
    {
        var id = Interlocked.Increment(ref _idCounter);

        permission.Id = id;

        _permissions.TryAdd(id, permission);

        return Task.FromResult(id);
    }

    public Task<Permission?> GetByNameAsync(string name)
    {
        var permission = _permissions.Values
            .FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(permission);
    }

    public Task<IReadOnlyList<Permission>> GetAllAsync()
    {
        var permissions = _permissions.Values.ToList();

        return Task.FromResult<IReadOnlyList<Permission>>(permissions);
    }
}
