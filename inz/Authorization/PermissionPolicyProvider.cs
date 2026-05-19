using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace inz.Authorization;

public class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private const string PolicyPrefix = "Permission:";
    private readonly DefaultAuthorizationPolicyProvider _fallbackProvider;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallbackProvider = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
    {
        return _fallbackProvider.GetDefaultPolicyAsync();
    }

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
    {
        return _fallbackProvider.GetFallbackPolicyAsync();
    }

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(PolicyPrefix, StringComparison.OrdinalIgnoreCase))
            return _fallbackProvider.GetPolicyAsync(policyName);

        var permission = policyName[PolicyPrefix.Length..];

        var policy = new AuthorizationPolicyBuilder()
            .AddRequirements(new RequirePermissionAttribute(permission))
            .Build();

        return Task.FromResult<AuthorizationPolicy?>(policy);
    }
}
