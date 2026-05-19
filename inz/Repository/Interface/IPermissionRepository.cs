using inz.Models;

namespace inz.Repository.Interface;

public interface IPermissionRepository
{
    Task<int> AddPermissionAsync(Permission permission);
    Task<Permission?> GetByNameAsync(string name);
    Task<IReadOnlyList<Permission>> GetAllAsync();
}
