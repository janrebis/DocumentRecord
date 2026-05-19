namespace inz.Models;

public sealed class SyncUserCommand
{
    public string EntraObjectId { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;
}
