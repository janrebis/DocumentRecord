namespace inz.Models;

public sealed class CreateUserCommand
{
    public string EntraObjectId { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public int OrganizationId { get; init; }

    public int RoleId { get; init; }
}
