using System.ComponentModel.DataAnnotations;

namespace inz.Models;

public class Role
{
    [Key]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsSystemRole { get; set; }

    public int OrganizationId { get; set; }
}
