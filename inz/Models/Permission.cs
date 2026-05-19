using System.ComponentModel.DataAnnotations;

namespace inz.Models;

public class Permission
{
    [Key]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
}
