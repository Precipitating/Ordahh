using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Ordahh.Api.Data;
using Ordahh.Api.Dtos;
using Ordahh.Api.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Ordahh.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDBContext _db;
    private readonly IConfiguration _config;
    public AuthController(AppDBContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }


    private CreateLoginResponse BuildToken(User user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddHours(8);

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        var jwt = new JwtSecurityTokenHandler().WriteToken(token);

        return new CreateLoginResponse
        {
            Token = jwt,
            ExpiryTime = expires
        }; 

    }
    [EnableRateLimiting("login")]
    [HttpPost("login")]
    public async Task<IActionResult> Login(CreateLoginRequest request)
    {
        // 1. Find the user by username (or null)
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == request.Username);

        // 2. No such user: generic 401
        if (user == null)
            return Unauthorized("Invalid username or password.");

        // 3. Verify the password against the stored hash
        var hasher = new PasswordHasher<User>();
        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

        // 4. Wrong password: the SAME generic 401
        if (result == PasswordVerificationResult.Failed)
            return Unauthorized("Invalid username or password.");

        // 5. For now, just confirm it worked (a token comes next)
        return Ok(BuildToken(user));


    }
}