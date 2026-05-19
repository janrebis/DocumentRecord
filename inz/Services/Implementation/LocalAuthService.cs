using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using inz.Models;
using inz.Repository.Interface;
using inz.Services.Interface;
using inz.UserExceptions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace inz.Services.Implementation;

public class LocalAuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IUserCredentialsRepository _credentialsRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IUserOrganizationRoleRepository _userOrgRoleRepository;
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<LocalAuthService> _logger;

    public LocalAuthService(
        IUserRepository userRepository,
        IUserCredentialsRepository credentialsRepository,
        IOrganizationRepository organizationRepository,
        IRoleRepository roleRepository,
        IUserOrganizationRoleRepository userOrgRoleRepository,
        IOptions<JwtSettings> jwtSettings,
        ILogger<LocalAuthService> logger)
    {
        _userRepository = userRepository;
        _credentialsRepository = credentialsRepository;
        _organizationRepository = organizationRepository;
        _roleRepository = roleRepository;
        _userOrgRoleRepository = userOrgRoleRepository;
        _jwtSettings = jwtSettings.Value;
        _logger = logger;
    }

    public async Task<int> RegisterAsync(RegisterCommand command)
    {
        ValidateRegisterCommand(command);

        var emailExists = await _credentialsRepository.ExistsByEmailAsync(command.Email);

        if (emailExists)
            throw new EmailAlreadyRegisteredException(
                $"Email {command.Email} jest już zarejestrowany.");

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(command.Password);

        // Pobierz domyślną organizację (id: 1 — utworzona przez seeder)
        var organization = await _organizationRepository.GetByIdAsync(1);

        if (organization is null)
            throw new OrganizationNotFoundException(
                "Nie znaleziono domyślnej organizacji. Upewnij się, że seeder został uruchomiony.");

        // Pobierz domyślną rolę Reader
        var readerRole = await _roleRepository.GetByNameAndOrganizationAsync("Reader", organization.Id);

        var user = new User
        {
            EntraObjectId = string.Empty,
            Email = command.Email,
            DisplayName = command.DisplayName,
            OrganizationId = organization.Id,
            LastLoginAt = DateTime.UtcNow
        };

        var userId = await _userRepository.AddUserAsync(user);

        var credentials = new UserCredentials
        {
            UserId = userId,
            Email = command.Email,
            PasswordHash = passwordHash
        };

        await _credentialsRepository.AddAsync(credentials);

        // Przypisz rolę Reader jeśli istnieje
        if (readerRole is not null)
        {
            await _userOrgRoleRepository.AddAsync(new UserOrganizationRole
            {
                UserId = userId,
                OrganizationId = organization.Id,
                RoleId = readerRole.Id
            });
        }

        _logger.LogInformation(
            "Zarejestrowano użytkownika {Email} (id: {UserId}).",
            command.Email,
            userId);

        return userId;
    }

    public async Task<string> LoginAsync(LoginCommand command)
    {
        ValidateLoginCommand(command);

        var credentials = await _credentialsRepository.GetByEmailAsync(command.Email);

        if (credentials is null)
            throw new InvalidCredentialsException("Nieprawidłowy email lub hasło.");

        var passwordValid = BCrypt.Net.BCrypt.Verify(command.Password, credentials.PasswordHash);

        if (!passwordValid)
            throw new InvalidCredentialsException("Nieprawidłowy email lub hasło.");

        var user = await _userRepository.GetByIdAsync(credentials.UserId);

        if (user is null)
            throw new UserNotFoundException("Nie znaleziono użytkownika.");

        if (!user.IsActive)
            throw new UserInactiveException(
                $"Konto użytkownika {user.Email} jest nieaktywne.");

        user.UpdateLastLogin();
        await _userRepository.UpdateUserAsync(user.Id, user);

        var token = GenerateJwtToken(user);

        _logger.LogInformation(
            "Użytkownik {Email} zalogował się pomyślnie.",
            user.Email);

        return token;
    }

    private string GenerateJwtToken(User user)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));

        var signingCredentials = new SigningCredentials(
            key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("oid", user.Id.ToString()),
            new("organizationId", user.OrganizationId.ToString()),
            new("displayName", user.DisplayName)
        };

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationInMinutes),
            signingCredentials: signingCredentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static void ValidateRegisterCommand(RegisterCommand command)
    {
        if (command is null)
            throw new ArgumentNullException(nameof(command));

        if (string.IsNullOrWhiteSpace(command.Email))
            throw new ArgumentException("Email jest wymagany.", nameof(command));

        if (string.IsNullOrWhiteSpace(command.Password))
            throw new ArgumentException("Hasło jest wymagane.", nameof(command));

        if (command.Password.Length < 8)
            throw new ArgumentException("Hasło musi mieć minimum 8 znaków.", nameof(command));

        if (string.IsNullOrWhiteSpace(command.DisplayName))
            throw new ArgumentException("Nazwa wyświetlana jest wymagana.", nameof(command));
    }

    private static void ValidateLoginCommand(LoginCommand command)
    {
        if (command is null)
            throw new ArgumentNullException(nameof(command));

        if (string.IsNullOrWhiteSpace(command.Email))
            throw new ArgumentException("Email jest wymagany.", nameof(command));

        if (string.IsNullOrWhiteSpace(command.Password))
            throw new ArgumentException("Hasło jest wymagane.", nameof(command));
    }
}
