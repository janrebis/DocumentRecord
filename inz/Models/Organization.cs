using System.ComponentModel.DataAnnotations;

namespace inz.Models;

public class Organization
{
    [Key]
    public int Id { get; set; }

    public Guid PublicId { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public void Deactivate()
    {
        if (!IsActive)
            throw new InvalidOperationException("Organizacja jest już nieaktywna.");

        IsActive = false;
    }
}
