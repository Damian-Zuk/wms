using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Wms.Application.Common.Auth;
using Wms.Application.Common.Auth.Dtos;
using Wms.Infrastructure.Identity;

namespace Wms.Api.Controllers;

[ApiController]
[Route("api/accounts")]
public class AccountsController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;
    private readonly ITokenService _tokenService;

    public AccountsController(UserManager<AppUser> userManager, ITokenService tokenService)
    {
        _userManager = userManager;
        _tokenService = tokenService;
    }

    // POST /api/accounts/login
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);

        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
            return Unauthorized(new { message = "Invalid email or password." });

        var roles = await _userManager.GetRolesAsync(user);
        var (token, expiresAt) = _tokenService.GenerateToken(
            user.Id, user.UserName!, user.Email!, user.FirstName, user.LastName, roles);

        var userDto = new UserDto(
            user.Id, user.Email!, user.UserName!,
            user.FirstName, user.LastName, roles);

        return Ok(new LoginResponse(token, expiresAt, userDto));
    }

    // POST /api/accounts/register
    [Authorize(Roles = "Admin")]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var allowedRoles = new[] { "Admin", "Manager", "Worker" };
        if (!allowedRoles.Contains(request.Role))
            return BadRequest(new { message = $"Role '{request.Role}' is not valid." });

        var user = new AppUser
        {
            Email = request.Email,
            UserName = request.UserName,
            FirstName = request.FirstName,
            LastName = request.LastName,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        await _userManager.AddToRoleAsync(user, request.Role);

        var userDto = new UserDto(
            user.Id, user.Email!, user.UserName!,
            user.FirstName, user.LastName, new List<string> { request.Role });

        return Created($"/api/accounts/{user.Id}", userDto);
    }

    // GET /api/accounts/me
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var user = await _userManager.FindByIdAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        if (user is null)
            return NotFound();

        var roles = await _userManager.GetRolesAsync(user);

        return Ok(new UserDto(
            user.Id,
            user.Email!,
            user.UserName!,
            user.FirstName,
            user.LastName,
            roles));
    }
}
