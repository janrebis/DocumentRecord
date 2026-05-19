using inz.Models;
using inz.Models.Enums;
using inz.Repository.Interface;

namespace inz.Services.Implementation;

public class DataSeeder
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IPermissionRepository _permissionRepository;
    private readonly IRolePermissionRepository _rolePermissionRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUserOrganizationRoleRepository _userOrgRoleRepository;
    private readonly IUserCredentialsRepository _credentialsRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DataSeeder> _logger;

    public DataSeeder(
        IOrganizationRepository organizationRepository,
        IRoleRepository roleRepository,
        IPermissionRepository permissionRepository,
        IRolePermissionRepository rolePermissionRepository,
        IUserRepository userRepository,
        IUserOrganizationRoleRepository userOrgRoleRepository,
        IUserCredentialsRepository credentialsRepository,
        IConfiguration configuration,
        ILogger<DataSeeder> logger)
    {
        _organizationRepository = organizationRepository;
        _roleRepository = roleRepository;
        _permissionRepository = permissionRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _userRepository = userRepository;
        _userOrgRoleRepository = userOrgRoleRepository;
        _credentialsRepository = credentialsRepository;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        var orgId = await SeedDefaultOrganizationAsync();
        var permissionIds = await SeedPermissionsAsync();
        var adminRoleId = await SeedRolesAsync(orgId, permissionIds);
        await SeedAdminUserAsync(orgId, adminRoleId);

        _logger.LogInformation("Zakończono seedowanie danych początkowych.");
    }

    private async Task<int> SeedDefaultOrganizationAsync()
    {
        var organization = new Organization
        {
            Name = _configuration["Seed:OrganizationName"] ?? "Domyślna organizacja"
        };

        var id = await _organizationRepository.AddOrganizationAsync(organization);

        _logger.LogInformation("Utworzono domyślną organizację (id: {OrgId}).", id);

        return id;
    }

    private async Task<Dictionary<string, int>> SeedPermissionsAsync()
    {
        var permissionIds = new Dictionary<string, int>();

        foreach (var permissionName in Permissions.All)
        {
            var permission = new Permission
            {
                Name = permissionName,
                Description = permissionName
            };

            var id = await _permissionRepository.AddPermissionAsync(permission);
            permissionIds[permissionName] = id;
        }

        _logger.LogInformation(
            "Utworzono {Count} uprawnień.",
            permissionIds.Count);

        return permissionIds;
    }

    private async Task<int> SeedRolesAsync(
        int organizationId,
        Dictionary<string, int> permissionIds)
    {
        // Rola Admin — wszystkie uprawnienia
        var adminRole = new Role
        {
            Name = "Admin",
            Description = "Pełny dostęp do systemu.",
            IsSystemRole = true,
            OrganizationId = organizationId
        };

        var adminRoleId = await _roleRepository.AddRoleAsync(adminRole);

        foreach (var permissionId in permissionIds.Values)
        {
            await _rolePermissionRepository.AddRolePermissionAsync(new RolePermission
            {
                RoleId = adminRoleId,
                PermissionId = permissionId
            });
        }

        // Rola Reader — tylko odczyt
        var readerRole = new Role
        {
            Name = "Reader",
            Description = "Odczyt dokumentów i danych użytkowników.",
            IsSystemRole = true,
            OrganizationId = organizationId
        };

        var readerRoleId = await _roleRepository.AddRoleAsync(readerRole);

        var readerPermissions = new[]
        {
            Permissions.DocumentsRead,
            Permissions.UsersRead,
            Permissions.RolesRead
        };

        foreach (var permName in readerPermissions)
        {
            if (permissionIds.TryGetValue(permName, out var permId))
            {
                await _rolePermissionRepository.AddRolePermissionAsync(new RolePermission
                {
                    RoleId = readerRoleId,
                    PermissionId = permId
                });
            }
        }

        // Rola Editor — odczyt + zapis dokumentów
        var editorRole = new Role
        {
            Name = "Editor",
            Description = "Odczyt i edycja dokumentów.",
            IsSystemRole = true,
            OrganizationId = organizationId
        };

        var editorRoleId = await _roleRepository.AddRoleAsync(editorRole);

        var editorPermissions = new[]
        {
            Permissions.DocumentsRead,
            Permissions.DocumentsWrite,
            Permissions.UsersRead,
            Permissions.RolesRead
        };

        foreach (var permName in editorPermissions)
        {
            if (permissionIds.TryGetValue(permName, out var permId))
            {
                await _rolePermissionRepository.AddRolePermissionAsync(new RolePermission
                {
                    RoleId = editorRoleId,
                    PermissionId = permId
                });
            }
        }

        _logger.LogInformation(
            "Utworzono role: Admin (id: {AdminId}), Reader (id: {ReaderId}), Editor (id: {EditorId}).",
            adminRoleId,
            readerRoleId,
            editorRoleId);

        return adminRoleId;
    }

    private async Task SeedAdminUserAsync(int organizationId, int adminRoleId)
    {
        var adminEmail = _configuration["Seed:AdminEmail"];
        var adminPassword = _configuration["Seed:AdminPassword"];

        if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
        {
            _logger.LogWarning(
                "Brak Seed:AdminEmail lub Seed:AdminPassword w konfiguracji — nie utworzono admina.");
            return;
        }

        var adminUser = new User
        {
            EntraObjectId = string.Empty,
            Email = adminEmail,
            DisplayName = _configuration["Seed:AdminDisplayName"] ?? "Administrator",
            OrganizationId = organizationId
        };

        var userId = await _userRepository.AddUserAsync(adminUser);

        var credentials = new UserCredentials
        {
            UserId = userId,
            Email = adminEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminPassword)
        };

        await _credentialsRepository.AddAsync(credentials);

        await _userOrgRoleRepository.AddAsync(new UserOrganizationRole
        {
            UserId = userId,
            OrganizationId = organizationId,
            RoleId = adminRoleId
        });

        _logger.LogInformation(
            "Utworzono użytkownika admina {Email} (id: {UserId}).",
            adminUser.Email,
            userId);
    }
}
