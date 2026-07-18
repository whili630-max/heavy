using HESAMS.Api.Data;
using HESAMS.Api.Models;
using HESAMS.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HESAMS.Api.Controllers;

public record InvestigationInput(string RootCause, string CorrectiveAction, int InvestigatorId);

[ApiController]
[Route("api/incidents")]
public class IncidentsController : ControllerBase
{
    private readonly HesamsDbContext _db;
    private readonly AuditService _audit;
    public IncidentsController(HesamsDbContext db, AuditService audit) { _db = db; _audit = audit; }

    [HttpGet, Authorize(Policy = "incidents.read")]
    public async Task<IActionResult> List(string? type, string? status, DateTime? from, DateTime? to) =>
        Ok(await _db.Incidents
            .Where(i => (type == null || i.IncidentType == type) &&
                        (status == null || i.Status == status) &&
                        (from == null || i.IncidentDate >= from) &&
                        (to == null || i.IncidentDate <= to))
            .OrderByDescending(i => i.IncidentDate).ToListAsync());

    [HttpPost, Authorize(Policy = "incidents.write")]
    public async Task<IActionResult> Report(Incident i)
    {
        _db.Incidents.Add(i);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("ReportIncident", "Incident", i.IncidentId.ToString(), newValue: i);
        return Ok(i);
    }

    /// <summary>Workflow step: Open -> UnderInvestigation -> PendingApproval.</summary>
    [HttpPost("{id:int}/investigate"), Authorize(Policy = "incidents.write")]
    public async Task<IActionResult> Investigate(int id, InvestigationInput input)
    {
        var i = await _db.Incidents.FindAsync(id);
        if (i is null) return NotFound();
        i.RootCause = input.RootCause;
        i.CorrectiveAction = input.CorrectiveAction;
        i.InvestigatorId = input.InvestigatorId;
        i.Status = "PendingApproval";
        await _db.SaveChangesAsync();
        await _audit.LogAsync("InvestigateIncident", "Incident", id.ToString(), newValue: input);
        return Ok(i);
    }

    /// <summary>Final approval closes the incident. Safety Manager / Super Admin only.</summary>
    [HttpPost("{id:int}/approve"), Authorize(Policy = "incidents.approve")]
    public async Task<IActionResult> Approve(int id)
    {
        var i = await _db.Incidents.FindAsync(id);
        if (i is null) return NotFound();
        if (i.Status != "PendingApproval")
            return Conflict(new { error = "Incident must be investigated before approval" });
        i.Status = "Closed";
        i.ApprovedByUserId = int.Parse(User.FindFirst("sub")!.Value);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("ApproveIncident", "Incident", id.ToString());
        return Ok(i);
    }
}
