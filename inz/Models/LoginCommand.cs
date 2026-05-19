namespace inz.Models;

public sealed class LoginCommand
{
    public string Email { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}
