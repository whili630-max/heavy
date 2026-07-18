using System.Text;
using ClosedXML.Excel;
using HESAMS.Api.Data;
using HESAMS.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HESAMS.Api.Controllers;

/// <summary>
/// 12 report types x 3 export formats (JSON for the UI grid, XLSX, CSV, PDF).
/// GET /api/reports/{key}?format=xlsx&amp;from=&amp;to=
/// Keys: employee-qualification, rfid-activity, equipment-utilization,
///       operator-history, maintenance-schedule, maintenance-cost,
///       license-expiry, training-expiry, unauthorized-attempts,
///       downtime, incidents, safety-kpi
/// </summary>
[ApiController]
[Route("api/reports")]
[Authorize(Policy = "reports.read")]
public class ReportsController : ControllerBase
{
    private readonly HesamsDbContext _db;
    private readonly AuditService _audit;
    public ReportsController(HesamsDbContext db, AuditService audit) { _db = db; _audit = audit; }

    [HttpGet("{key}")]
    public async Task<IActionResult> Get(string key, string format = "json",
                                         DateTime? from = null, DateTime? to = null)
    {
        from ??= DateTime.UtcNow.AddDays(-30);
        to ??= DateTime.UtcNow;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        (string title, List<string> cols, List<object[]> rows) = key switch
        {
            "employee-qualification" => ("Employee Qualification Report",
                new List<string> { "Code", "Name", "Department", "Trainings valid", "Trainings expired", "Licenses valid", "Licenses expired" },
                (await _db.Employees.Include(e => e.Department).Include(e => e.Trainings).Include(e => e.Licenses)
                    .Where(e => e.Status == "Active").ToListAsync())
                    .Select(e => new object[] { e.EmployeeCode, e.FullName, e.Department.DepartmentName,
                        e.Trainings.Count(t => t.ExpiryDate == null || t.ExpiryDate >= today),
                        e.Trainings.Count(t => t.ExpiryDate != null && t.ExpiryDate < today),
                        e.Licenses.Count(l => l.ExpiryDate >= today),
                        e.Licenses.Count(l => l.ExpiryDate < today) }).ToList()),

            "rfid-activity" => ("RFID Activity Report",
                new List<string> { "Time", "Card UID", "Employee", "Equipment", "Result", "Reason" },
                (await _db.AccessEvents.Where(e => e.EventAt >= from && e.EventAt <= to)
                    .OrderByDescending(e => e.EventAt).Take(5000)
                    .Select(e => new { e.EventAt, e.CardUid, e.Result, e.DenialReason,
                        Emp = _db.Employees.Where(x => x.EmployeeId == e.EmployeeId).Select(x => x.FullName).FirstOrDefault(),
                        Eq = _db.Equipment.Where(x => x.EquipmentId == e.EquipmentId).Select(x => x.EquipmentName).FirstOrDefault() })
                    .ToListAsync())
                    .Select(e => new object[] { e.EventAt, e.CardUid, e.Emp ?? "-", e.Eq ?? "-", e.Result, e.DenialReason ?? "-" }).ToList()),

            "equipment-utilization" => ("Equipment Utilization Report",
                new List<string> { "Code", "Equipment", "Type", "Sessions", "Hours logged", "Operating hours", "Status" },
                (await _db.Equipment.Include(e => e.EquipmentType).ToListAsync())
                    .Select(e => new object[] { e.EquipmentCode, e.EquipmentName, e.EquipmentType.TypeName,
                        _db.UsageSessions.Count(s => s.EquipmentId == e.EquipmentId && s.StartTime >= from),
                        _db.UsageSessions.Where(s => s.EquipmentId == e.EquipmentId && s.StartTime >= from)
                                         .Sum(s => s.HoursAdded ?? 0),
                        e.OperatingHours, e.Status }).ToList()),

            "operator-history" => ("Operator History Report",
                new List<string> { "Operator", "Equipment", "Start", "End", "Hours", "Fuel" },
                (await _db.UsageSessions.Where(s => s.StartTime >= from && s.StartTime <= to)
                    .OrderByDescending(s => s.StartTime).Take(5000)
                    .Select(s => new {
                        Emp = _db.Employees.Where(x => x.EmployeeId == s.EmployeeId).Select(x => x.FullName).FirstOrDefault(),
                        Eq = _db.Equipment.Where(x => x.EquipmentId == s.EquipmentId).Select(x => x.EquipmentName).FirstOrDefault(),
                        s.StartTime, s.EndTime, s.HoursAdded, s.FuelConsumed }).ToListAsync())
                    .Select(s => new object[] { s.Emp ?? "-", s.Eq ?? "-", s.StartTime,
                        (object?)s.EndTime ?? "in progress", s.HoursAdded ?? 0, s.FuelConsumed ?? 0 }).ToList()),

            "maintenance-schedule" => ("Maintenance Schedule Report",
                new List<string> { "Order", "Equipment", "Type", "Scheduled", "Priority", "Status" },
                (await _db.MaintenanceOrders.Include(m => m.Equipment).OrderBy(m => m.ScheduledDate).ToListAsync())
                    .Select(m => new object[] { m.MaintenanceId, m.Equipment.EquipmentName, m.MaintenanceType,
                        (object?)m.ScheduledDate ?? "-", m.Priority, m.Status }).ToList()),

            "maintenance-cost" => ("Maintenance Cost Report",
                new List<string> { "Equipment", "Orders", "Parts cost", "Total cost" },
                (await _db.MaintenanceOrders.Include(m => m.Equipment).Include(m => m.Parts)
                    .Where(m => m.CompletionDate != null).ToListAsync())
                    .GroupBy(m => m.Equipment.EquipmentName)
                    .Select(g => new object[] { g.Key, g.Count(),
                        g.SelectMany(m => m.Parts).Sum(p => p.Quantity * (p.UnitCost ?? 0)),
                        g.Sum(m => m.Cost ?? 0) }).ToList()),

            "license-expiry" => ("License Expiry Report",
                new List<string> { "Employee", "License type", "Number", "Expiry", "Days left", "Status" },
                (await _db.EmployeeLicenses.Include(l => l.LicenseType).ToListAsync())
                    .Select(l => new object[] {
                        _db.Employees.Where(e => e.EmployeeId == l.EmployeeId).Select(e => e.FullName).FirstOrDefault() ?? "-",
                        l.LicenseType.LicenseTypeName, l.LicenseNumber, l.ExpiryDate,
                        l.ExpiryDate.DayNumber - today.DayNumber,
                        l.ExpiryDate < today ? "Expired" : l.ExpiryDate <= today.AddDays(30) ? "Expiring soon" : "Valid" }).ToList()),

            "training-expiry" => ("Training Expiry Report",
                new List<string> { "Employee", "Course", "Completed", "Expiry", "Status" },
                (await _db.EmployeeTrainings.Include(t => t.Course).ToListAsync())
                    .Select(t => new object[] {
                        _db.Employees.Where(e => e.EmployeeId == t.EmployeeId).Select(e => e.FullName).FirstOrDefault() ?? "-",
                        t.Course.CourseName, t.TrainingDate, (object?)t.ExpiryDate ?? "no expiry",
                        t.ExpiryDate == null || t.ExpiryDate >= today ? "Valid" : "Expired" }).ToList()),

            "unauthorized-attempts" => ("Unauthorized Attempts Report",
                new List<string> { "Time", "Employee", "Equipment", "Reason", "Reader" },
                (await _db.AccessEvents.Where(e => e.Result == "Denied" && e.EventAt >= from)
                    .OrderByDescending(e => e.EventAt)
                    .Select(e => new { e.EventAt, e.DenialReason, e.RfidReaderId,
                        Emp = _db.Employees.Where(x => x.EmployeeId == e.EmployeeId).Select(x => x.FullName).FirstOrDefault(),
                        Eq = _db.Equipment.Where(x => x.EquipmentId == e.EquipmentId).Select(x => x.EquipmentName).FirstOrDefault() })
                    .ToListAsync())
                    .Select(e => new object[] { e.EventAt, e.Emp ?? "unknown card", e.Eq ?? "-", e.DenialReason ?? "-", e.RfidReaderId }).ToList()),

            "downtime" => ("Downtime Report",
                new List<string> { "Equipment", "Maintenance days", "Locked", "Status" },
                (await _db.Equipment.ToListAsync())
                    .Select(e => new object[] { e.EquipmentName,
                        _db.MaintenanceOrders.Count(m => m.EquipmentId == e.EquipmentId && m.Status != "Completed"),
                        e.MaintenanceLocked ? "Yes" : "No", e.Status }).ToList()),

            "incidents" => ("Incident Report",
                new List<string> { "Date", "Type", "Equipment", "Status", "Root cause" },
                (await _db.Incidents.Where(i => i.IncidentDate >= from).OrderByDescending(i => i.IncidentDate)
                    .Select(i => new { i.IncidentDate, i.IncidentType, i.Status, i.RootCause,
                        Eq = _db.Equipment.Where(x => x.EquipmentId == i.EquipmentId).Select(x => x.EquipmentName).FirstOrDefault() })
                    .ToListAsync())
                    .Select(i => new object[] { i.IncidentDate, i.IncidentType, i.Eq ?? "-", i.Status, i.RootCause ?? "-" }).ToList()),

            "safety-kpi" => ("Safety KPI Report",
                new List<string> { "Metric", "Value" },
                await BuildSafetyKpiRows(from.Value)),

            _ => ("", new List<string>(), new List<object[]>())
        };

        if (cols.Count == 0) return NotFound(new { error = $"Unknown report key '{key}'" });
        await _audit.LogAsync("ExportReport", "Report", key, newValue: new { format, from, to });

        return format.ToLower() switch
        {
            "json" => Ok(new { title, columns = cols, rows }),
            "csv" => File(Csv(cols, rows), "text/csv", $"{key}.csv"),
            "xlsx" => File(Xlsx(title, cols, rows),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{key}.xlsx"),
            "pdf" => File(Pdf(title, cols, rows), "application/pdf", $"{key}.pdf"),
            _ => BadRequest(new { error = "format must be json, csv, xlsx, or pdf" })
        };
    }

    private async Task<List<object[]>> BuildSafetyKpiRows(DateTime from)
    {
        var total = await _db.AccessEvents.CountAsync(e => e.EventAt >= from);
        var granted = await _db.AccessEvents.CountAsync(e => e.EventAt >= from && e.Result == "Granted");
        return new List<object[]>
        {
            new object[] { "Total scans", total },
            new object[] { "Granted", granted },
            new object[] { "Denied", total - granted },
            new object[] { "Compliance rate %", total == 0 ? 100 : Math.Round((double)granted / total * 100, 1) },
            new object[] { "Incidents", await _db.Incidents.CountAsync(i => i.IncidentDate >= from) },
            new object[] { "Expired trainings", await _db.EmployeeTrainings.CountAsync(t => t.ExpiryDate != null && t.ExpiryDate < DateOnly.FromDateTime(DateTime.UtcNow)) },
            new object[] { "Locked equipment", await _db.Equipment.CountAsync(e => e.MaintenanceLocked) }
        };
    }

    private static byte[] Csv(List<string> cols, List<object[]> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", cols.Select(Q)));
        foreach (var r in rows) sb.AppendLine(string.Join(",", r.Select(v => Q(v?.ToString() ?? ""))));
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        static string Q(string s) => s.Contains(',') || s.Contains('"') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
    }

    private static byte[] Xlsx(string title, List<string> cols, List<object[]> rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet(title.Length > 31 ? title[..31] : title);
        for (int c = 0; c < cols.Count; c++)
        {
            ws.Cell(1, c + 1).Value = cols[c];
            ws.Cell(1, c + 1).Style.Font.Bold = true;
            ws.Cell(1, c + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1F2530");
            ws.Cell(1, c + 1).Style.Font.FontColor = XLColor.White;
        }
        for (int r = 0; r < rows.Count; r++)
            for (int c = 0; c < rows[r].Length; c++)
                ws.Cell(r + 2, c + 1).Value = XLCellValue.FromObject(rows[r][c]);
        ws.SheetView.FreezeRows(1);
        ws.RangeUsed()?.SetAutoFilter();
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static byte[] Pdf(string title, List<string> cols, List<object[]> rows)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(t => t.FontSize(8));
                page.Header().Column(col =>
                {
                    col.Item().Text("HESAMS").FontSize(16).Bold().FontColor("#FFB020");
                    col.Item().Text(title).FontSize(12).Bold();
                    col.Item().Text($"Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC · {rows.Count} rows")
                              .FontSize(7).FontColor(Colors.Grey.Darken1);
                });
                page.Content().PaddingVertical(8).Table(t =>
                {
                    t.ColumnsDefinition(d => { foreach (var _ in cols) d.RelativeColumn(); });
                    t.Header(h => { foreach (var c in cols)
                        h.Cell().Background("#1F2530").Padding(4).Text(c).FontColor("#FFFFFF").Bold(); });
                    foreach (var r in rows)
                        foreach (var v in r)
                            t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
                                    .Padding(3).Text(v?.ToString() ?? "-");
                });
                page.Footer().AlignCenter().Text(x => { x.CurrentPageNumber(); x.Span(" / "); x.TotalPages(); });
            });
        }).GeneratePdf();
    }
}
