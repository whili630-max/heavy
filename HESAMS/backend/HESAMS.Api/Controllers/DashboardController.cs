using HESAMS.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HESAMS.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize(Policy = "dashboard.read")]
public class DashboardController : ControllerBase
{
    private readonly HesamsDbContext _db;
    public DashboardController(HesamsDbContext db) { _db = db; }

    /// <summary>All executive KPI tiles in one round-trip.</summary>
    [HttpGet("kpis")]
    public async Task<IActionResult> Kpis()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var weekAgo = DateTime.UtcNow.AddDays(-7);

        var totalEquipment = await _db.Equipment.CountAsync();
        var inUse = await _db.Equipment.CountAsync(e => e.Status == "InUse");
        var available = await _db.Equipment.CountAsync(e => e.Status == "Available");

        // Utilization = machine-hours logged / (machines x 8h x 7d)
        var hours = await _db.UsageSessions.Where(s => s.StartTime >= weekAgo && s.EndTime != null)
            .SumAsync(s => (decimal?)s.HoursAdded) ?? 0;
        var capacity = totalEquipment * 8 * 7;
        var utilization = capacity == 0 ? 0 : Math.Round(hours / capacity * 100, 1);

        return Ok(new
        {
            totalEmployees = await _db.Employees.CountAsync(e => e.Status == "Active"),
            totalEquipment,
            activeEquipment = available + inUse,
            underMaintenance = await _db.Equipment.CountAsync(e => e.Status == "UnderMaintenance"),
            activeOperators = await _db.UsageSessions.Where(s => s.EndTime == null)
                                                     .Select(s => s.EmployeeId).Distinct().CountAsync(),
            unauthorizedAttempts7d = await _db.AccessEvents
                .CountAsync(e => e.Result == "Denied" && e.EventAt >= weekAgo),
            expiredLicenses = await _db.EmployeeLicenses.CountAsync(l => l.ExpiryDate < today),
            expiredTrainings = await _db.EmployeeTrainings
                .CountAsync(t => t.ExpiryDate != null && t.ExpiryDate < today),
            maintenanceDueToday = await _db.MaintenanceOrders
                .CountAsync(m => m.ScheduledDate == today && m.Status != "Completed"),
            maintenanceOverdue = await _db.MaintenanceOrders.CountAsync(m => m.Status == "Overdue"),
            upcomingMaintenance30d = await _db.MaintenanceOrders
                .CountAsync(m => m.Status == "Scheduled" && m.ScheduledDate <= today.AddDays(30)),
            lockedEquipment = await _db.Equipment.CountAsync(e => e.MaintenanceLocked),
            utilizationRate = utilization
        });
    }

    [HttpGet("live-events")]
    public async Task<IActionResult> LiveEvents(int take = 20) =>
        Ok(await _db.AccessEvents.OrderByDescending(e => e.EventAt).Take(take)
            .Select(e => new
            {
                e.EventId, e.EventAt, e.Result, e.DenialReason, e.CardUid, e.RfidReaderId,
                Employee = _db.Employees.Where(x => x.EmployeeId == e.EmployeeId)
                                        .Select(x => x.FullName).FirstOrDefault(),
                Equipment = _db.Equipment.Where(x => x.EquipmentId == e.EquipmentId)
                                         .Select(x => x.EquipmentName).FirstOrDefault()
            }).ToListAsync());

    [HttpGet("daily-usage")]
    public async Task<IActionResult> DailyUsage(int days = 7)
    {
        var from = DateTime.UtcNow.AddDays(-days);
        return Ok(await _db.UsageSessions.Where(s => s.StartTime >= from)
            .GroupBy(s => s.StartTime.Date)
            .Select(g => new { date = g.Key, hours = g.Sum(x => x.HoursAdded ?? 0), sessions = g.Count() })
            .OrderBy(x => x.date).ToListAsync());
    }

    /// <summary>Safety KPI: share of scans granted, plus denial breakdown by reason.</summary>
    [HttpGet("safety-kpi")]
    public async Task<IActionResult> SafetyKpi(int days = 30)
    {
        var from = DateTime.UtcNow.AddDays(-days);
        var total = await _db.AccessEvents.CountAsync(e => e.EventAt >= from);
        var granted = await _db.AccessEvents.CountAsync(e => e.EventAt >= from && e.Result == "Granted");
        var byReason = await _db.AccessEvents
            .Where(e => e.EventAt >= from && e.Result == "Denied")
            .GroupBy(e => e.DenialReason)
            .Select(g => new { reason = g.Key, count = g.Count() }).ToListAsync();
        return Ok(new
        {
            totalScans = total,
            granted,
            denied = total - granted,
            complianceRate = total == 0 ? 100 : Math.Round((double)granted / total * 100, 1),
            denialBreakdown = byReason,
            incidents = await _db.Incidents.CountAsync(i => i.IncidentDate >= from)
        });
    }
}
