using System.Collections.Concurrent;
using inz.Models;
using inz.Repository.Interface;
using inz.UserExceptions;

namespace inz.Repository.Implementations;

public class InMemoryUserRepository : IUserRepository
{
    private readonly ConcurrentDictionary<int, User> _users = new();
    private int _idCounter;

    public Task<int> AddUserAsync(User user)
    {
        var id = Interlocked.Increment(ref _idCounter);

        user.Id = id;

        if (!_users.TryAdd(id, user))
            throw new UserAlreadyExistsException("Nie udało się dodać użytkownika.");

        return Task.FromResult(id);
    }

    public Task<User?> GetByIdAsync(int userId)
    {
        _users.TryGetValue(userId, out var user);

        return Task.FromResult(user);
    }

    public Task<User?> GetByEntraObjectIdAsync(string entraObjectId)
    {
        var user = _users.Values
            .FirstOrDefault(u => u.EntraObjectId == entraObjectId);

        return Task.FromResult(user);
    }

    public Task<IReadOnlyList<User>> GetByOrganizationIdAsync(int organizationId)
    {
        var users = _users.Values
            .Where(u => u.OrganizationId == organizationId)
            .ToList();

        return Task.FromResult<IReadOnlyList<User>>(users);
    }

    public Task UpdateUserAsync(int userId, User user)
    {
        if (!_users.ContainsKey(userId))
            throw new UserNotFoundException("Nie znaleziono użytkownika.");

        user.Id = userId;

        _users[userId] = user;

        return Task.CompletedTask;
    }
}
