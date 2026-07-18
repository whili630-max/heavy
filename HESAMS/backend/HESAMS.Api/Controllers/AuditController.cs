using HESAMS.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HESAMS.Api.Controllers;

[ApiController]
[Route("api/audit")]
[Authorize(Policy = "audit.read")]
public class AuditController : ControllerBase
{
    private readonly HesamsDbContext _db;
    public AuditController(HesamsDbContext db) { _db = db; }

    [HttpGet]
    public async Task<IActionResult> List(string? action, string? entity, int? userId,
                                          DateTime? from, DateTime? to, int page = 1, int pageSize = 50)
    {
        var q = _db.AuditLog.AsQueryable();
        if (!string.IsNullOrEmpty(action)) q = q.Where(a => a.Action == action);
        if (!string.IsNullOrEmpty(entity)) q = q.Where(a => a.Entity == entity);
        if (userId is not null) q = q.Where(a => a.UserId == userId);
        if (from is not null) q = q.Where(a => a.OccurredAt >= from);
        if (to is not null) q = q.Where(a => a.OccurredAt <= to);
        var total = await q.CountAsync();
        var items = await q.OrderByDescending(a => a.OccurredAt)
                           .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(new { total, page, pageSize, items });
    }
}
