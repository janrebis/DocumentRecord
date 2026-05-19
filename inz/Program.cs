using System.Text;
using inz.Authorization;
using inz.Middleware;
using inz.Models;
using inz.Repository.Implementations;
using inz.Repository.Interface;
using inz.Services.Implementation;
using inz.Services.Interface;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// === JWT Settings ===
var jwtSection = builder.Configuration.GetSection("Jwt");
builder.Services.Configure<JwtSettings>(jwtSection);

var jwtSettings = jwtSection.Get<JwtSettings>()!;

// === Autentykacja — lokalny JWT ===
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,

            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),

            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

// === Autoryzacja — RBAC z custom permission handler ===
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

// === Serwisy ===
builder.Services.AddScoped<IAuthService, LocalAuthService>();
builder.Services.AddScoped<IDocumentService, DocumentService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<IFileReader, FileReader>();

// === Repozytoria (InMemory — do zamiany na SQL) ===
builder.Services.AddSingleton<IDocumentRepository, InMemoryDocumentRepository>();
builder.Services.AddSingleton<IDocumentContentStorage, InMemoryContentStorage>();
builder.Services.AddSingleton<IUserRepository, InMemoryUserRepository>();
builder.Services.AddSingleton<IOrganizationRepository, InMemoryOrganizationRepository>();
builder.Services.AddSingleton<IRoleRepository, InMemoryRoleRepository>();
builder.Services.AddSingleton<IPermissionRepository, InMemoryPermissionRepository>();
builder.Services.AddSingleton<IRolePermissionRepository, InMemoryRolePermissionRepository>();
builder.Services.AddSingleton<IUserOrganizationRoleRepository, InMemoryUserOrganizationRoleRepository>();
builder.Services.AddSingleton<IUserCredentialsRepository, InMemoryUserCredentialsRepository>();

// === Seeder ===
builder.Services.AddTransient<DataSeeder>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var app = builder.Build();

// === Seedowanie danych początkowych ===
using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
    await seeder.SeedAsync();
}

app.UseGlobalExceptionMiddleware();

app.UseCors("frontend");

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
