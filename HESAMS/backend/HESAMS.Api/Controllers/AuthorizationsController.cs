using HESAMS.Api.Data;
using HESAMS.Api.Models;
using HESAMS.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HESAMS.Api.Controllers;

[ApiController]
[Route("api/authorizations")]
public class AuthorizationsController : ControllerBase
{
    private readonly HesamsDbContext _db;
    private readonly AuditService _audit;
    public AuthorizationsController(HesamsDbContext db, AuditService audit) { _db = db; _audit = audit; }

    [HttpGet, Authorize(Policy = "authorizations.read")]
    public async Task<IActionResult> List(int? employeeId, int? equipmentId, bool activeOnly = true) =>
        Ok(await _db.Authorizations
            .Where(a => (!activeOnly || a.IsActive) &&
                        (employeeId == null || a.EmployeeId == employeeId) &&
                        (equipmentId == null || a.EquipmentId == equipmentId))
            .ToListAsync());

    /// <summary>
    /// Grant authorization. Scope by machine (EquipmentId) or class
    /// (EquipmentTypeId); optionally restrict by shift, location, department,
    /// and a Temporary validity window.
    /// </summary>
    [HttpPost, Authorize(Policy = "authorizations.write")]
    public async Task<IActionResult> Grant(EquipmentAuthorization auth)
    {
        if (auth.EquipmentId is null && auth.EquipmentTypeId is null)
            return BadRequest(new { error = "Provide EquipmentId or EquipmentTypeId" });
        if (auth.AuthType == "Temporary" && auth.ValidTo is null)
            return BadRequest(new { error = "Temporary authorization requires ValidTo" });
        _db.Authorizations.Add(auth);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("GrantAuthorization", "Authorization", auth.AuthorizationId.ToString(), newValue: auth);
        return Ok(auth);
    }

    [HttpDelete("{id:int}"), Authorize(Policy = "authorizations.write")]
    public async Task<IActionResult> Revoke(int id)
    {
        var a = await _db.Authorizations.FindAsync(id);
        if (a is null) return NotFound();
        a.IsActive = false;
        await _db.SaveChangesAsync();
        await _audit.LogAsync("RevokeAuthorization", "Authorization", id.ToString(), oldValue: a);
        return NoContent();
    }
}
