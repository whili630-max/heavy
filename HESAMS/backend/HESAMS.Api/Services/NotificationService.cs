using System.Net.Mail;
using HESAMS.Api.Data;
using HESAMS.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HESAMS.Api.Services;

public interface INotificationService
{
    Task SendAsync(int supervisorEmployeeId, string subject, string body,
                   string? relatedEntity = null, int? relatedId = null, CancellationToken ct = default);
    Task QueueAsync(int? recipientUserId, string channel, string subject, string body,
                    string? relatedEntity = null, int? relatedId = null, CancellationToken ct = default);
}

/// <summary>
/// Multi-channel dispatcher. Every notification is persisted as InApp; Email is
/// sent via SMTP. SMS / Push / Teams / WhatsApp are pluggable: implement the
/// same QueueAsync contract against the provider gateway (Twilio, FCM, Graph,
/// WhatsApp Business API) and register in DI.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly HesamsDbContext _db;
    private readonly IConfiguration _cfg;
    public NotificationService(HesamsDbContext db, IConfiguration cfg) { _db = db; _cfg = cfg; }

    public async Task SendAsync(int supervisorEmployeeId, string subject, string body,
                                string? relatedEntity = null, int? relatedId = null, CancellationToken ct = default)
    {
        var supUser = await _db.Users.FirstOrDefaultAsync(u => u.EmployeeId == supervisorEmployeeId, ct);
        await QueueAsync(supUser?.UserId, "InApp", subject, body, relatedEntity, relatedId, ct);
        if (supUser?.Email is not null)
            await QueueAsync(supUser.UserId, "Email", subject, body, relatedEntity, relatedId, ct);
    }

    public async Task QueueAsync(int? recipientUserId, string channel, string subject, string body,
                                 string? relatedEntity = null, int? relatedId = null, CancellationToken ct = default)
    {
        var n = new Notification
        {
            RecipientUserId = recipientUserId, Channel = channel,
            Subject = subject, Body = body,
            RelatedEntity = relatedEntity, RelatedEntityId = relatedId
        };
        _db.Notifications.Add(n);
        await _db.SaveChangesAsync(ct);

        if (channel == "Email" && recipientUserId is not null)
        {
            try
            {
                var email = await _db.Users.Where(u => u.UserId == recipientUserId)
                                           .Select(u => u.Email).FirstAsync(ct);
                var smtp = _cfg.GetSection("Smtp");
                using var client = new SmtpClient(smtp["Host"]!, int.Parse(smtp["Port"]!));
                await client.SendMailAsync(new MailMessage(smtp["From"]!, email, subject, body), ct);
                n.Status = "Sent"; n.SentAt = DateTime.UtcNow;
            }
            catch { n.Status = "Failed"; }
            await _db.SaveChangesAsync(ct);
        }
    }
}
