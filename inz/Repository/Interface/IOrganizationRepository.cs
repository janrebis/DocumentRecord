using inz.Models;

namespace inz.Repository.Interface;

public interface IOrganizationRepository
{
    Task<int> AddOrganizationAsync(Organization organization);
    Task<Organization?> GetByIdAsync(int organizationId);
}
