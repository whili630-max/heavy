using HESAMS.Api.Data;
using HESAMS.Api.Models;
using HESAMS.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HESAMS.Api.Controllers;

[ApiController]
[Route("api/employees")]
public class EmployeesController : ControllerBase
{
    private readonly HesamsDbContext _db;
    private readonly AuditService _audit;
    public EmployeesController(HesamsDbContext db, AuditService audit) { _db = db; _audit = audit; }

    [HttpGet, Authorize(Policy = "employees.read")]
    public async Task<IActionResult> List(string? search, string? status, int? departmentId,
                                          int page = 1, int pageSize = 25)
    {
        var q = _db.Employees.Include(e => e.Department).AsQueryable();
        if (!string.IsNullOrEmpty(search))
            q = q.Where(e => e.FullName.Contains(search) || e.EmployeeCode.Contains(search));
        if (!string.IsNullOrEmpty(status)) q = q.Where(e => e.Status == status);
        if (departmentId is not null) q = q.Where(e => e.DepartmentId == departmentId);

        var total = await q.CountAsync();
        var items = await q.OrderBy(e => e.FullName)
                           .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(new { total, page, pageSize, items });
    }

    [HttpGet("{id:int}"), Authorize(Policy = "employees.read")]
    public async Task<IActionResult> Get(int id)
    {
        var e = await _db.Employees
            .Include(x => x.Department).Include(x => x.Cards)
            .Include(x => x.Trainings).ThenInclude(t => t.Course)
            .Include(x => x.Licenses).ThenInclude(l => l.LicenseType)
            .Include(x => x.Authorizations).Include(x => x.Documents)
            .FirstOrDefaultAsync(x => x.EmployeeId == id);
        return e is null ? NotFound() : Ok(e);
    }

    [HttpPost, Authorize(Policy = "employees.write")]
    public async Task<IActionResult> Create(Employee emp)
    {
        _db.Employees.Add(emp);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Create", "Employee", emp.EmployeeId.ToString(), newValue: emp);
        return CreatedAtAction(nameof(Get), new { id = emp.EmployeeId }, emp);
    }

    [HttpPut("{id:int}"), Authorize(Policy = "employees.write")]
    public async Task<IActionResult> Update(int id, Employee input)
    {
        var e = await _db.Employees.FindAsync(id);
        if (e is null) return NotFound();
        var old = new { e.FullName, e.Status, e.DepartmentId, e.JobTitle, e.MedicalClearanceExpiry };
        e.FullName = input.FullName; e.Status = input.Status;
        e.DepartmentId = input.DepartmentId; e.JobTitle = input.JobTitle;
        e.Email = input.Email; e.Phone = input.Phone;
        e.MedicalClearanceExpiry = input.MedicalClearanceExpiry;
        e.SupervisorId = input.SupervisorId; e.Notes = input.Notes;
        e.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Update", "Employee", id.ToString(), old, input);
        return Ok(e);
    }

    [HttpDelete("{id:int}"), Authorize(Policy = "users.admin")]
    public async Task<IActionResult> Deactivate(int id)
    {
        var e = await _db.Employees.FindAsync(id);
        if (e is null) return NotFound();
        e.Status = "Terminated"; // soft delete
        foreach (var c in _db.RfidCards.Where(c => c.EmployeeId == id)) c.Status = "Inactive";
        await _db.SaveChangesAsync();
        await _audit.LogAsync("Deactivate", "Employee", id.ToString());
        return NoContent();
    }
}
