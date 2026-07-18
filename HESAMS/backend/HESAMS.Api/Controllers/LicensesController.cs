using HESAMS.Api.Data;
using HESAMS.Api.Models;
using HESAMS.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HESAMS.Api.Controllers;

[ApiController]
[Route("api/licenses")]
public class LicensesController : ControllerBase
{
    private readonly HesamsDbContext _db;
    private readonly AuditService _audit;
    public LicensesController(HesamsDbContext db, AuditService audit) { _db = db; _audit = audit; }

    [HttpGet, Authorize(Policy = "licenses.read")]
    public async Task<IActionResult> List(int? employeeId) =>
        Ok(await _db.EmployeeLicenses.Include(l => l.LicenseType)
            .Where(l => employeeId == null || l.EmployeeId == employeeId).ToListAsync());

    [HttpPost, Authorize(Policy = "licenses.write")]
    public async Task<IActionResult> Add(EmployeeLicense l)
    {
        _db.EmployeeLicenses.Add(l);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("AddLicense", "EmployeeLicense", l.LicenseId.ToString(), newValue: l);
        return Ok(l);
    }

    [HttpGet("expiring"), Authorize(Policy = "licenses.read")]
    public async Task<IActionResult> Expiring(int days = 30)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return Ok(await _db.EmployeeLicenses.Include(l => l.LicenseType)
            .Where(l => l.ExpiryDate <= today.AddDays(days))
            .OrderBy(l => l.ExpiryDate).ToListAsync());
    }
}
