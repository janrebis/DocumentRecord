using inz.Models;
using inz.Services.Interface;
using Microsoft.AspNetCore.Mvc;

namespace inz.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterCommand command)
    {
        var userId = await _authService.RegisterAsync(command);

        return CreatedAtAction(
            nameof(Register),
            new { id = userId },
            new { id = userId });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginCommand command)
    {
        var token = await _authService.LoginAsync(command);

        return Ok(new { token });
    }
}
