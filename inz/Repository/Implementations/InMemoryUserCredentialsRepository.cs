using System.Collections.Concurrent;
using inz.Models;
using inz.Repository.Interface;

namespace inz.Repository.Implementations;

public class InMemoryUserCredentialsRepository : IUserCredentialsRepository
{
    private readonly ConcurrentDictionary<string, UserCredentials> _credentials = new(StringComparer.OrdinalIgnoreCase);

    public Task AddAsync(UserCredentials credentials)
    {
        if (!_credentials.TryAdd(credentials.Email, credentials))
            throw new InvalidOperationException("Credentials dla tego emaila już istnieją.");

        return Task.CompletedTask;
    }

    public Task<UserCredentials?> GetByEmailAsync(string email)
    {
        _credentials.TryGetValue(email, out var credentials);

        return Task.FromResult(credentials);
    }

    public Task<bool> ExistsByEmailAsync(string email)
    {
        return Task.FromResult(_credentials.ContainsKey(email));
    }
}
