using HESAMS.Api.Data;
using HESAMS.Api.Models;
using HESAMS.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HESAMS.Api.Controllers;

[ApiController]
[Route("api/equipment")]
public class EquipmentController : ControllerBase
{
    private readonly HesamsDbContext _db;
    private readonly AuditService _audit;
    public EquipmentController(HesamsDbContext db, AuditService audit) { _db = db; _audit = audit; }

    [HttpGet, Authorize(Policy = "equipment.read")]
    public async Task<IActionResult> List(string? search, string? status, int? typeId,
                                          int page = 1, int pageSize = 25)
    {
        var q = _db.Equipment.Include(e => e.EquipmentType).AsQueryable();
        if (!string.IsNullOrEmpty(search))
            q = q.Where(e => e.EquipmentName.Contains(search) || e.EquipmentCode.Contains(search));
        if (!string.IsNullOrEmpty(status)) q = q.Where(e => e.Status == status);
        if (typeId is not null) q = q.Where(e => e.EquipmentTypeId == typeId);
        var total = await q.CountAsync();
        var items = await q.OrderBy(e => e.EquipmentCode)
                           .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(new { total, page, pageSize, items });
    }

    [HttpGet("{id:int}"), Authorize(Policy = "equipment.read")]
    public async Task<IActionResult> Get(int id)
    {
        var e = await _db.Equipment.Include(x => x.EquipmentType)
            .FirstOrDefaultAsync(x => x.EquipmentId == id);
        return e is null ? NotFound() : Ok(e);
    }

    [HttpGet("{id:int}/usage"), Authorize(Policy = "equipment.read")]
    public async Task<IActionResult> Usage(int id, DateTime? from, DateTime? to) =>
        Ok(await _db.UsageSessions
            .Where(s => s.EquipmentId == id &&
                        (from == null || s.StartTime >= from) &&
                        (to == null || s.StartTime <= to))
            .OrderByDescending(s => s.StartTime).Take(500).ToListAsync());

    [HttpPost, Authorize(Policy = "equipment.write")]
    public async Task<IActionResult> Create(Equipment eq)
    {
        _db.Equipment.Add(eq);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Create", "Equipment", eq.EquipmentId.ToString(), newValue: eq);
        return CreatedAtAction(nameof(Get), new { id = eq.EquipmentId }, eq);
    }

    [HttpPut("{id:int}"), Authorize(Policy = "equipment.write")]
    public async Task<IActionResult> Update(int id, Equipment input)
    {
        var e = await _db.Equipment.FindAsync(id);
        if (e is null) return NotFound();
        var old = new { e.Status, e.LocationId, e.NextMaintenanceDate, e.NextMaintenanceHours, e.OperatingHours };
        e.EquipmentName = input.EquipmentName; e.Status = input.Status;
        e.LocationId = input.LocationId; e.DepartmentId = input.DepartmentId;
        e.SupervisorId = input.SupervisorId; e.OperatingHours = input.OperatingHours;
        e.MaintenanceIntervalHours = input.MaintenanceIntervalHours;
        e.MaintenanceIntervalDays = input.MaintenanceIntervalDays;
        e.NextMaintenanceDate = input.NextMaintenanceDate;
        e.NextMaintenanceHours = input.NextMaintenanceHours;
        e.RfidReaderId = input.RfidReaderId;
        e.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Update", "Equipment", id.ToString(), old, input);
        return Ok(e);
    }

    /// <summary>Clear the maintenance interlock — requires a completed order.</summary>
    [HttpPost("{id:int}/unlock"), Authorize(Policy = "maintenance.unlock")]
    public async Task<IActionResult> Unlock(int id)
    {
        var e = await _db.Equipment.FindAsync(id);
        if (e is null) return NotFound();
        var hasCompleted = await _db.MaintenanceOrders.AnyAsync(m =>
            m.EquipmentId == id && m.Status == "Completed" &&
            m.CompletionDate >= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7)));
        if (!hasCompleted)
            return Conflict(new { error = "No completed maintenance order in the last 7 days. Complete maintenance first." });

        e.MaintenanceLocked = false;
        if (e.Status == "UnderMaintenance") e.Status = "Available";
        // roll next schedule forward
        if (e.MaintenanceIntervalDays is not null)
            e.NextMaintenanceDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(e.MaintenanceIntervalDays.Value);
        if (e.MaintenanceIntervalHours is not null)
            e.NextMaintenanceHours = e.OperatingHours + e.MaintenanceIntervalHours.Value;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("MaintenanceUnlock", "Equipment", id.ToString());
        return Ok(e);
    }
}
