using System.Collections.Concurrent;
using inz.Models;
using inz.Repository.Interface;

namespace inz.Repository.Implementations;

public class InMemoryRolePermissionRepository : IRolePermissionRepository
{
    private readonly ConcurrentBag<RolePermission> _rolePermissions = new();

    private readonly IPermissionRepository _permissionRepository;

    public InMemoryRolePermissionRepository(IPermissionRepository permissionRepository)
    {
        _permissionRepository = permissionRepository;
    }

    public Task AddRolePermissionAsync(RolePermission rolePermission)
    {
        _rolePermissions.Add(rolePermission);

        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<string>> GetPermissionNamesByRoleIdAsync(int roleId)
    {
        var permissionIds = _rolePermissions
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.PermissionId)
            .ToHashSet();

        var allPermissions = await _permissionRepository.GetAllAsync();

        var names = allPermissions
            .Where(p => permissionIds.Contains(p.Id))
            .Select(p => p.Name)
            .ToList();

        return names;
    }

    public Task RemoveAllByRoleIdAsync(int roleId)
    {
        // ConcurrentBag nie wspiera usuwania — w produkcji użyj prawdziwej bazy
        // Dla InMemory tworzymy nową kolekcję bez elementów danej roli
        var remaining = _rolePermissions
            .Where(rp => rp.RoleId != roleId)
            .ToList();

        while (_rolePermissions.TryTake(out _)) { }

        foreach (var item in remaining)
            _rolePermissions.Add(item);

        return Task.CompletedTask;
    }
}

public class InMemoryUserOrganizationRoleRepository : IUserOrganizationRoleRepository
{
    private readonly ConcurrentBag<UserOrganizationRole> _assignments = new();

    public Task AddAsync(UserOrganizationRole userOrganizationRole)
    {
        _assignments.Add(userOrganizationRole);

        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(int userId, int organizationId, int roleId)
    {
        var exists = _assignments.Any(a =>
            a.UserId == userId &&
            a.OrganizationId == organizationId &&
            a.RoleId == roleId);

        return Task.FromResult(exists);
    }

    public Task<IReadOnlyList<int>> GetRoleIdsByUserAndOrganizationAsync(
        int userId, int organizationId)
    {
        var roleIds = _assignments
            .Where(a => a.UserId == userId && a.OrganizationId == organizationId)
            .Select(a => a.RoleId)
            .ToList();

        return Task.FromResult<IReadOnlyList<int>>(roleIds);
    }

    public Task RemoveAsync(int userId, int organizationId, int roleId)
    {
        var remaining = _assignments
            .Where(a => !(a.UserId == userId &&
                          a.OrganizationId == organizationId &&
                          a.RoleId == roleId))
            .ToList();

        while (_assignments.TryTake(out _)) { }

        foreach (var item in remaining)
            _assignments.Add(item);

        return Task.CompletedTask;
    }
}
