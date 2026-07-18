using HESAMS.Api.Data;
using HESAMS.Api.Models;
using HESAMS.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HESAMS.Api.Controllers;

[ApiController]
[Route("api/trainings")]
public class TrainingsController : ControllerBase
{
    private readonly HesamsDbContext _db;
    private readonly AuditService _audit;
    public TrainingsController(HesamsDbContext db, AuditService audit) { _db = db; _audit = audit; }

    [HttpGet("courses"), Authorize(Policy = "training.read")]
    public async Task<IActionResult> Courses() => Ok(await _db.TrainingCourses.ToListAsync());

    [HttpPost("courses"), Authorize(Policy = "training.write")]
    public async Task<IActionResult> CreateCourse(TrainingCourse c)
    { _db.TrainingCourses.Add(c); await _db.SaveChangesAsync(); return Ok(c); }

    /// <summary>Record completed training; expiry auto-computed from course validity.</summary>
    [HttpPost("records"), Authorize(Policy = "training.write")]
    public async Task<IActionResult> AddRecord(EmployeeTraining t)
    {
        var course = await _db.TrainingCourses.FindAsync(t.CourseId);
        if (course is null) return NotFound(new { error = "Course not found" });
        if (t.ExpiryDate is null && course.ValidityMonths is not null)
            t.ExpiryDate = t.TrainingDate.AddMonths(course.ValidityMonths.Value);
        _db.EmployeeTrainings.Add(t);
        await _db.SaveChangesAsync();
        await _audit.LogAsync("AddTraining", "EmployeeTraining", t.EmployeeTrainingId.ToString(), newValue: t);
        return Ok(t);
    }

    /// <summary>Training matrix: rows = employees, columns = courses, cells = status.</summary>
    [HttpGet("matrix"), Authorize(Policy = "training.read")]
    public async Task<IActionResult> Matrix(int? departmentId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var rows = await _db.Employees
            .Where(e => e.Status == "Active" && (departmentId == null || e.DepartmentId == departmentId))
            .Select(e => new
            {
                e.EmployeeId, e.FullName,
                Trainings = e.Trainings.Select(t => new
                {
                    t.CourseId, t.Course.CourseName, t.ExpiryDate,
                    Status = t.ExpiryDate == null ? "Valid"
                           : t.ExpiryDate >= today ? (t.ExpiryDate <= today.AddDays(30) ? "ExpiringSoon" : "Valid")
                           : "Expired"
                })
            }).ToListAsync();
        return Ok(rows);
    }

    [HttpGet("expiring"), Authorize(Policy = "training.read")]
    public async Task<IActionResult> Expiring(int days = 30)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return Ok(await _db.EmployeeTrainings.Include(t => t.Course)
            .Where(t => t.ExpiryDate != null && t.ExpiryDate <= today.AddDays(days))
            .OrderBy(t => t.ExpiryDate).ToListAsync());
    }
}
