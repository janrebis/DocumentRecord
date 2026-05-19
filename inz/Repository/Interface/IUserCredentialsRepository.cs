using inz.Models;

namespace inz.Repository.Interface;

public interface IUserCredentialsRepository
{
    Task AddAsync(UserCredentials credentials);
    Task<UserCredentials?> GetByEmailAsync(string email);
    Task<bool> ExistsByEmailAsync(string email);
}
