using System.Security.Claims;
using inz.Repository.Interface;
using inz.Services.Interface;
using Microsoft.AspNetCore.Authorization;

namespace inz.Authorization;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RequirePermissionAttribute : AuthorizeAttribute, IAuthorizationRequirement
{
    public string Permission { get; }

    public RequirePermissionAttribute(string permission)
        : base(policy: $"Permission:{permission}")
    {
        Permission = permission;
    }
}

public class PermissionAuthorizationHandler
    : AuthorizationHandler<RequirePermissionAttribute>
{
    private readonly IRoleService _roleService;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<PermissionAuthorizationHandler> _logger;

    public PermissionAuthorizationHandler(
        IRoleService roleService,
        IUserRepository userRepository,
        ILogger<PermissionAuthorizationHandler> logger)
    {
        _roleService = roleService;
        _userRepository = userRepository;
        _logger = logger;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RequirePermissionAttribute requirement)
    {
        var user = await ResolveUserAsync(context.User);

        if (user is null || !user.IsActive)
        {
            _logger.LogWarning("Użytkownik nie znaleziony lub nieaktywny.");
            context.Fail();
            return;
        }

        var hasPermission = await _roleService.UserHasPermissionAsync(
            user.Id, user.OrganizationId, requirement.Permission);

        if (hasPermission)
        {
            context.Succeed(requirement);
        }
        else
        {
            _logger.LogWarning(
                "Użytkownik {Email} nie posiada uprawnienia {Permission}.",
                user.Email,
                requirement.Permission);
            context.Fail();
        }
    }

    private async Task<Models.User?> ResolveUserAsync(ClaimsPrincipal principal)
    {
        // Próba 1: lokalny JWT — claim "sub" zawiera userId jako int
        var subClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                       ?? principal.FindFirstValue("sub");

        if (int.TryParse(subClaim, out var userId))
        {
            return await _userRepository.GetByIdAsync(userId);
        }

        // Próba 2: Azure Entra ID — claim "oid" zawiera Entra Object ID
        var entraOid = principal.FindFirstValue(
            "http://schemas.microsoft.com/identity/claims/objectidentifier")
            ?? principal.FindFirstValue("oid");

        if (!string.IsNullOrEmpty(entraOid))
        {
            return await _userRepository.GetByEntraObjectIdAsync(entraOid);
        }

        _logger.LogWarning("Nie udało się zidentyfikować użytkownika z tokenu.");
        return null;
    }
}
