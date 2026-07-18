using HESAMS.Api.Data;
using HESAMS.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HESAMS.Api.Controllers;

public record LoginRequest(string Username, string Password);
public record RefreshRequest(string RefreshToken);

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly HesamsDbContext _db;
    private readonly TokenService _tokens;
    private readonly AuditService _audit;
    public AuthController(HesamsDbContext db, TokenService tokens, AuditService audit)
    { _db = db; _tokens = tokens; _audit = audit; }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        var user = await _db.Users.Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Username == req.Username);

        if (user is null || !user.IsActive)
            return Unauthorized(new { error = "Invalid credentials" });
        if (user.LockedUntil is not null && user.LockedUntil > DateTime.UtcNow)
            return Unauthorized(new { error = "Account locked. Try again later." });

        if (!BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
        {
            user.FailedAttempts++;
            if (user.FailedAttempts >= 5) user.LockedUntil = DateTime.UtcNow.AddMinutes(15);
            await _db.SaveChangesAsync();
            return Unauthorized(new { error = "Invalid credentials" });
        }

        user.FailedAttempts = 0;
        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        var (access, refresh) = await _tokens.IssueAsync(user);
        await _audit.LogAsync("Login", "User", user.UserId.ToString(), userId: user.UserId);
        return Ok(new { access, refresh, role = user.Role.RoleName });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshRequest req)
    {
        var rt = await _db.RefreshTokens.Include(r => r.User).ThenInclude(u => u.Role)
            .FirstOrDefaultAsync(r => r.Token == req.RefreshToken && r.RevokedAt == null);
        if (rt is null || rt.ExpiresAt < DateTime.UtcNow)
            return Unauthorized(new { error = "Refresh token invalid or expired" });

        rt.RevokedAt = DateTime.UtcNow; // rotation
        var (access, refresh) = await _tokens.IssueAsync(rt.User);
        return Ok(new { access, refresh });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest req)
    {
        await _db.RefreshTokens.Where(r => r.Token == req.RefreshToken)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.RevokedAt, DateTime.UtcNow));
        await _audit.LogAsync("Logout");
        return NoContent();
    }
}
