using HESAMS.Api.Data;
using HESAMS.Api.Models;
using HESAMS.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HESAMS.Api.Controllers;

public record IssueCardRequest(string CardUid, int EmployeeId, string CardType, DateTime? ExpiresAt);
public record CardActionRequest(string Reason);

[ApiController]
[Route("api/rfid-cards")]
[Authorize(Policy = "rfid.write")]
public class RfidCardsController : ControllerBase
{
    private readonly HesamsDbContext _db;
    private readonly AuditService _audit;
    public RfidCardsController(HesamsDbContext db, AuditService audit) { _db = db; _audit = audit; }

    [HttpGet, Authorize(Policy = "rfid.read")]
    public async Task<IActionResult> List(string? status, int? employeeId) =>
        Ok(await _db.RfidCards.Include(c => c.Employee)
            .Where(c => (status == null || c.Status == status) &&
                        (employeeId == null || c.EmployeeId == employeeId))
            .OrderByDescending(c => c.IssuedAt).ToListAsync());

    [HttpGet("{id:int}/history"), Authorize(Policy = "rfid.read")]
    public async Task<IActionResult> History(int id) =>
        Ok(await _db.CardAssignmentLogs.Where(l => l.CardId == id)
            .OrderByDescending(l => l.ActionAt).ToListAsync());

    [HttpPost("issue")]
    public async Task<IActionResult> Issue(IssueCardRequest req)
    {
        if (await _db.RfidCards.AnyAsync(c => c.CardUid == req.CardUid && c.Status == "Active"))
            return Conflict(new { error = "Card UID already active" });

        var card = new RfidCard
        {
            CardUid = req.CardUid, EmployeeId = req.EmployeeId,
            CardType = req.CardType, ExpiresAt = req.ExpiresAt
        };
        _db.RfidCards.Add(card);
        await _db.SaveChangesAsync();
        _db.CardAssignmentLogs.Add(new CardAssignmentLog
            { CardId = card.CardId, EmployeeId = req.EmployeeId, Action = "Issued" });
        await _db.SaveChangesAsync();
        await _audit.LogAsync("IssueCard", "RfidCard", card.CardId.ToString(), newValue: req);
        return Ok(card);
    }

    [HttpPost("{id:int}/deactivate")]
    public async Task<IActionResult> Deactivate(int id, CardActionRequest req)
    {
        var card = await _db.RfidCards.FindAsync(id);
        if (card is null) return NotFound();
        card.Status = "Inactive";
        _db.CardAssignmentLogs.Add(new CardAssignmentLog
            { CardId = id, EmployeeId = card.EmployeeId, Action = "Deactivated", Reason = req.Reason });
        await _db.SaveChangesAsync();
        await _audit.LogAsync("DeactivateCard", "RfidCard", id.ToString(), newValue: req);
        return Ok(card);
    }

    /// <summary>Report lost + issue replacement in one transaction.</summary>
    [HttpPost("{id:int}/replace")]
    public async Task<IActionResult> Replace(int id, IssueCardRequest req)
    {
        var old = await _db.RfidCards.FindAsync(id);
        if (old is null) return NotFound();
        old.Status = "Lost";
        var repl = new RfidCard
        {
            CardUid = req.CardUid, EmployeeId = old.EmployeeId,
            CardType = "Replacement", ExpiresAt = req.ExpiresAt
        };
        _db.RfidCards.Add(repl);
        await _db.SaveChangesAsync();
        _db.CardAssignmentLogs.AddRange(
            new CardAssignmentLog { CardId = id, EmployeeId = old.EmployeeId, Action = "ReportedLost" },
            new CardAssignmentLog { CardId = repl.CardId, EmployeeId = old.EmployeeId, Action = "Replaced" });
        await _db.SaveChangesAsync();
        await _audit.LogAsync("ReplaceCard", "RfidCard", id.ToString(), old, repl);
        return Ok(repl);
    }
}
