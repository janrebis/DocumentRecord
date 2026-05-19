using inz.Models;

namespace inz.Services.Interface;

public interface IAuthService
{
    Task<int> RegisterAsync(RegisterCommand command);
    Task<string> LoginAsync(LoginCommand command);
}
