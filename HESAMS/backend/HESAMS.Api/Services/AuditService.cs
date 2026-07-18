using System.Text.Json;
using HESAMS.Api.Data;
using HESAMS.Api.Models;

namespace HESAMS.Api.Services;

public class AuditService
{
    private readonly HesamsDbContext _db;
    private readonly IHttpContextAccessor _http;
    public AuditService(HesamsDbContext db, IHttpContextAccessor http) { _db = db; _http = http; }

    public async Task LogAsync(string action, string? entity = null, string? entityId = null,
                               object? oldValue = null, object? newValue = null, int? userId = null)
    {
        var ctx = _http.HttpContext;
        _db.AuditLog.Add(new AuditLogEntry
        {
            Action = action,
            Entity = entity,
            EntityId = entityId,
            OldValue = oldValue is null ? null : JsonSerializer.Serialize(oldValue),
            NewValue = newValue is null ? null : JsonSerializer.Serialize(newValue),
            UserId = userId ?? TryGetUserId(ctx),
            IpAddress = ctx?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = ctx?.Request.Headers.UserAgent.ToString()
        });
        await _db.SaveChangesAsync();
    }

    private static int? TryGetUserId(HttpContext? ctx) =>
        int.TryParse(ctx?.User.FindFirst("sub")?.Value, out var id) ? id : null;
}
