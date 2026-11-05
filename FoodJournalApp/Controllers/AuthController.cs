
using FoodJournalApp.Domain;
using FoodJournalApp.Dtos;
using FoodJournalApp.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FoodJournalApp.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly JwtTokenService _tokens;

    public AuthController(UserManager<ApplicationUser> userManager, JwtTokenService tokens)
    {
        _userManager = userManager;
        _tokens = tokens;
    }

    // POST /api/auth/register
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest req)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = req.Email,
            Email = req.Email
        };

        var result = await _userManager.CreateAsync(user, req.Password);
        if (!result.Succeeded)
            return BadRequest(result.Errors.Select(e => e.Description));

        var (token, expiresAt) = _tokens.CreateToken(user);
        return Ok(new AuthResponse(user.Id, user.Email!, token, expiresAt));
    }

    // POST /api/auth/login
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest req)
    {
        var user = await _userManager.FindByEmailAsync(req.Email);
        if (user is null || !await _userManager.CheckPasswordAsync(user, req.Password))
            return Unauthorized("Invalid email or password.");

        var (token, expiresAt) = _tokens.CreateToken(user);
        return Ok(new AuthResponse(user.Id, user.Email!, token, expiresAt));
    }
}
