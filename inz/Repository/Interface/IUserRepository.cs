using inz.Models;

namespace inz.Repository.Interface;

public interface IUserRepository
{
    Task<int> AddUserAsync(User user);
    Task<User?> GetByIdAsync(int userId);
    Task<User?> GetByEntraObjectIdAsync(string entraObjectId);
    Task<IReadOnlyList<User>> GetByOrganizationIdAsync(int organizationId);
    Task UpdateUserAsync(int userId, User user);
}
