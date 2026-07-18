using HESAMS.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HESAMS.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly HesamsDbContext _db;
    public NotificationsController(HesamsDbContext db) { _db = db; }

    private int Me => int.Parse(User.FindFirst("sub")!.Value);

    [HttpGet]
    public async Task<IActionResult> Mine(bool unreadOnly = false) =>
        Ok(await _db.Notifications
            .Where(n => (n.RecipientUserId == Me || n.RecipientUserId == null) &&
                        (!unreadOnly || n.ReadAt == null))
            .OrderByDescending(n => n.CreatedAt).Take(100).ToListAsync());

    [HttpPost("{id:long}/read")]
    public async Task<IActionResult> MarkRead(long id)
    {
        await _db.Notifications.Where(n => n.NotificationId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, DateTime.UtcNow)
                                      .SetProperty(n => n.Status, "Read"));
        return NoContent();
    }
}
