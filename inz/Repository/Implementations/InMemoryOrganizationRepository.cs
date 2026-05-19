using System.Collections.Concurrent;
using inz.Models;
using inz.Repository.Interface;

namespace inz.Repository.Implementations;

public class InMemoryOrganizationRepository : IOrganizationRepository
{
    private readonly ConcurrentDictionary<int, Organization> _organizations = new();
    private int _idCounter;

    public Task<int> AddOrganizationAsync(Organization organization)
    {
        var id = Interlocked.Increment(ref _idCounter);

        organization.Id = id;

        _organizations.TryAdd(id, organization);

        return Task.FromResult(id);
    }

    public Task<Organization?> GetByIdAsync(int organizationId)
    {
        _organizations.TryGetValue(organizationId, out var organization);

        return Task.FromResult(organization);
    }
}
