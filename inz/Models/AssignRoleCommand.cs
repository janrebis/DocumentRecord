namespace inz.Models;

public sealed class AssignRoleCommand
{
    public int UserId { get; init; }

    public int OrganizationId { get; init; }

    public int RoleId { get; init; }
}
