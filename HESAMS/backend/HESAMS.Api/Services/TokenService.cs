using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using HESAMS.Api.Data;
using HESAMS.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace HESAMS.Api.Services;

public class TokenService
{
    private readonly IConfiguration _cfg;
    private readonly HesamsDbContext _db;
    public TokenService(IConfiguration cfg, HesamsDbContext db) { _cfg = cfg; _db = db; }

    public async Task<(string access, string refresh)> IssueAsync(User user)
    {
        var perms = await _db.RolePermissions
            .Where(rp => rp.RoleId == user.RoleId)
            .Select(rp => rp.Permission.PermissionCode)
            .ToListAsync();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(ClaimTypes.Role, user.Role.RoleName)
        };
        claims.AddRange(perms.Select(p => new Claim("perm", p)));

        var jwt = _cfg.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!));
        var token = new JwtSecurityToken(
            issuer: jwt["Issuer"], audience: jwt["Audience"], claims: claims,
            expires: DateTime.UtcNow.AddMinutes(int.Parse(jwt["AccessTokenMinutes"]!)),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        var refresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.UserId, Token = refresh,
            ExpiresAt = DateTime.UtcNow.AddDays(int.Parse(jwt["RefreshTokenDays"]!))
        });
        await _db.SaveChangesAsync();
        return (new JwtSecurityTokenHandler().WriteToken(token), refresh);
    }
}
