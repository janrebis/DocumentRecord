using inz.Authorization;
using inz.Models;
using inz.Models.Enums;
using inz.Services.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace inz.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IRoleService _roleService;

    public UserController(IUserService userService, IRoleService roleService)
    {
        _userService = userService;
        _roleService = roleService;
    }

    [HttpGet("{id:int}")]
    [RequirePermission(Permissions.UsersRead)]
    public async Task<IActionResult> GetUser(int id)
    {
        var user = await _userService.GetUserByIdAsync(id);

        return Ok(new
        {
            user.Id,
            user.PublicId,
            user.Email,
            user.DisplayName,
            user.OrganizationId,
            user.IsActive,
            user.CreatedAt,
            user.LastLoginAt
        });
    }

    [HttpGet("organization/{organizationId:int}")]
    [RequirePermission(Permissions.UsersRead)]
    public async Task<IActionResult> GetUsersByOrganization(int organizationId)
    {
        var users = await _userService.GetUsersByOrganizationAsync(organizationId);

        var result = users.Select(u => new
        {
            u.Id,
            u.PublicId,
            u.Email,
            u.DisplayName,
            u.IsActive,
            u.LastLoginAt
        });

        return Ok(result);
    }

    [HttpPost]
    [RequirePermission(Permissions.UsersManage)]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserCommand command)
    {
        var userId = await _userService.CreateUserAsync(command);

        return CreatedAtAction(
            nameof(GetUser),
            new { id = userId },
            new { id = userId });
    }

    [HttpPatch("{id:int}/deactivate")]
    [RequirePermission(Permissions.UsersManage)]
    public async Task<IActionResult> DeactivateUser(int id)
    {
        await _userService.DeactivateUserAsync(id);

        return NoContent();
    }

    [HttpPatch("{id:int}/activate")]
    [RequirePermission(Permissions.UsersManage)]
    public async Task<IActionResult> ActivateUser(int id)
    {
        await _userService.ActivateUserAsync(id);

        return NoContent();
    }

    [HttpPost("{userId:int}/roles")]
    [RequirePermission(Permissions.RolesManage)]
    public async Task<IActionResult> AssignRole(int userId, [FromBody] AssignRoleRequest request)
    {
        var command = new AssignRoleCommand
        {
            UserId = userId,
            OrganizationId = request.OrganizationId,
            RoleId = request.RoleId
        };

        await _roleService.AssignRoleToUserAsync(command);

        return NoContent();
    }

    [HttpDelete("{userId:int}/roles/{roleId:int}")]
    [RequirePermission(Permissions.RolesManage)]
    public async Task<IActionResult> RemoveRole(
        int userId, int roleId, [FromQuery] int organizationId)
    {
        await _roleService.RemoveRoleFromUserAsync(userId, organizationId, roleId);

        return NoContent();
    }

    [HttpGet("{userId:int}/permissions")]
    [RequirePermission(Permissions.UsersRead)]
    public async Task<IActionResult> GetUserPermissions(
        int userId, [FromQuery] int organizationId)
    {
        var permissions = await _roleService.GetUserPermissionsAsync(userId, organizationId);

        return Ok(permissions);
    }

    [HttpGet("roles/organization/{organizationId:int}")]
    [RequirePermission(Permissions.RolesRead)]
    public async Task<IActionResult> GetRoles(int organizationId)
    {
        var roles = await _roleService.GetRolesByOrganizationAsync(organizationId);

        return Ok(roles.Select(r => new
        {
            r.Id,
            r.Name,
            r.Description,
            r.IsSystemRole
        }));
    }
}

public class AssignRoleRequest
{
    public int OrganizationId { get; set; }

    public int RoleId { get; set; }
}
