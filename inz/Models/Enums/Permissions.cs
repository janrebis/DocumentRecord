namespace inz.Models.Enums;

public static class Permissions
{
    public const string DocumentsRead = "documents:read";
    public const string DocumentsWrite = "documents:write";
    public const string DocumentsDelete = "documents:delete";

    public const string UsersRead = "users:read";
    public const string UsersManage = "users:manage";

    public const string RolesRead = "roles:read";
    public const string RolesManage = "roles:manage";

    public const string OrganizationManage = "organization:manage";

    public static readonly string[] All =
    {
        DocumentsRead,
        DocumentsWrite,
        DocumentsDelete,
        UsersRead,
        UsersManage,
        RolesRead,
        RolesManage,
        OrganizationManage
    };
}
