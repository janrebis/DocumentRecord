using System.ComponentModel.DataAnnotations;

namespace inz.Models;

public class User
{
    [Key]
    public int Id { get; set; }

    public Guid PublicId { get; set; } = Guid.NewGuid();

    public string EntraObjectId { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public int OrganizationId { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAt { get; set; }

    public void UpdateLastLogin()
    {
        LastLoginAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        if (!IsActive)
            throw new InvalidOperationException("Użytkownik jest już nieaktywny.");

        IsActive = false;
    }

    public void Activate()
    {
        if (IsActive)
            throw new InvalidOperationException("Użytkownik jest już aktywny.");

        IsActive = true;
    }

    public void UpdateProfile(string displayName, string email)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Nazwa wyświetlana jest wymagana.", nameof(displayName));

        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email jest wymagany.", nameof(email));

        DisplayName = displayName;
        Email = email;
    }
}
