using HESAMS.Api.Data;
using HESAMS.Api.Models;
using HESAMS.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HESAMS.Api.Controllers;

[ApiController]
[Route("api/maintenance")]
public class MaintenanceController : ControllerBase
{
    private readonly HesamsDbContext _db;
    private readonly AuditService _audit;
    public MaintenanceController(HesamsDbContext db, AuditService audit) { _db = db; _audit = audit; }

    [HttpGet, Authorize(Policy = "maintenance.read")]
    public async Task<IActionResult> List(string? status, int? equipmentId, string? type) =>
        Ok(await _db.MaintenanceOrders.Include(m => m.Equipment).Include(m => m.Parts)
            .Where(m => (status == null || m.Status == status) &&
                        (equipmentId == null || m.EquipmentId == equipmentId) &&
                        (type == null || m.MaintenanceType == type))
            .OrderBy(m => m.ScheduledDate).ToListAsync());

    [HttpPost, Authorize(Policy = "maintenance.write")]
    public async Task<IActionResult> Create(MaintenanceOrder m)
    {
        _db.MaintenanceOrders.Add(m);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("CreateMaintenance", "MaintenanceOrder", m.MaintenanceId.ToString(), newValue: m);
        return Ok(m);
    }

    /// <summary>Complete an order: records cost/date, recomputes total cost from parts.</summary>
    [HttpPost("{id:int}/complete"), Authorize(Policy = "maintenance.write")]
    public async Task<IActionResult> Complete(int id, MaintenanceOrder input)
    {
        var m = await _db.MaintenanceOrders.Include(x => x.Parts)
            .FirstOrDefaultAsync(x => x.MaintenanceId == id);
        if (m is null) return NotFound();
        m.Status = "Completed";
        m.CompletionDate = input.CompletionDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        m.Cost = input.Cost ?? m.Parts.Sum(p => p.Quantity * (p.UnitCost ?? 0));
        m.Notes = input.Notes ?? m.Notes;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("CompleteMaintenance", "MaintenanceOrder", id.ToString(), newValue: m);
        return Ok(new { m, hint = "Call POST /api/equipment/{id}/unlock to clear the interlock." });
    }

    [HttpPost("{id:int}/parts"), Authorize(Policy = "maintenance.write")]
    public async Task<IActionResult> AddPart(int id, MaintenancePart part)
    {
        part.MaintenanceId = id;
        _db.MaintenanceParts.Add(part);
        await _db.SaveChangesAsync();
        return Ok(part);
    }
}
