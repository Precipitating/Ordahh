using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ordahh.Api.Data;
using Ordahh.Api.Dtos;
using Ordahh.Api.Models;

namespace Ordahh.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDBContext _db;

    public AuthController(AppDBContext db)
    {
        _db = db;
    }

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
        return Ok("logged in");
    }
}