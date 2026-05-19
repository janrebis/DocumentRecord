using inz.Models;

namespace inz.Services.Interface;

public interface IUserService
{
    /// <summary>
    /// Synchronizuje użytkownika z Entra ID przy logowaniu (just-in-time provisioning).
    /// Tworzy użytkownika jeśli nie istnieje, aktualizuje dane jeśli istnieje.
    /// </summary>
    Task<User> SyncUserFromEntraAsync(SyncUserCommand command);

    /// <summary>
    /// Tworzy nowego użytkownika (przez admina) i przypisuje mu rolę.
    /// </summary>
    Task<int> CreateUserAsync(CreateUserCommand command);

    Task<User> GetUserByIdAsync(int userId);

    Task<IReadOnlyList<User>> GetUsersByOrganizationAsync(int organizationId);

    Task DeactivateUserAsync(int userId);

    Task ActivateUserAsync(int userId);
}
