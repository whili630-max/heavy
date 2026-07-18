using System.Text.Json;
using HESAMS.Api.Data;
using HESAMS.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HESAMS.Api.Services;

public record GateResult(string Gate, bool Passed, string? Reason);
public record ValidationResponse(bool Granted, string? DenialReason, long EventId, List<GateResult> Trace);

/// <summary>
/// The 9-gate safety interlock chain. Executed for every RFID scan received
/// from an ESP32 gate unit. First failing gate stops the chain, the attempt
/// is logged, and the supervisor is notified. On success the relay-unlock
/// command is returned, a usage session opens, and equipment goes InUse.
/// </summary>
public class AccessValidationService
{
    private readonly HesamsDbContext _db;
    private readonly INotificationService _notify;

    public AccessValidationService(HesamsDbContext db, INotificationService notify)
    {
        _db = db;
        _notify = notify;
    }

    public async Task<ValidationResponse> ValidateAsync(string readerId, string cardUid, CancellationToken ct = default)
    {
        var trace = new List<GateResult>();
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);

        var equipment = await _db.Equipment
            .Include(e => e.EquipmentType).ThenInclude(t => t.RequiredTrainings)
            .Include(e => e.EquipmentType).ThenInclude(t => t.RequiredLicenses)
            .FirstOrDefaultAsync(e => e.RfidReaderId == readerId, ct);

        var card = await _db.RfidCards
            .Include(c => c.Employee)!.ThenInclude(e => e!.Trainings)
            .Include(c => c.Employee)!.ThenInclude(e => e!.Licenses)
            .Include(c => c.Employee)!.ThenInclude(e => e!.Authorizations)
            .FirstOrDefaultAsync(c => c.CardUid == cardUid, ct);

        var emp = card?.Employee;

        bool Gate(string name, bool ok, string failReason)
        {
            trace.Add(new GateResult(name, ok, ok ? null : failReason));
            return ok;
        }

        string? denial = null;
        bool granted = false;

        // Gate 0 — reader must map to a machine (device configuration check)
        if (equipment is null)
            denial = "UnknownReader";
        // Gate 1 — card exists, active, not expired
        else if (!Gate("CardActive",
                 card is not null && card.Status == "Active" && (card.ExpiresAt is null || card.ExpiresAt > now),
                 card is null ? "CardNotFound" : "CardInactiveOrExpired"))
            denial = card is null ? "CardNotFound" : "CardInactiveOrExpired";
        // Gate 2 — employee active
        else if (!Gate("EmployeeActive", emp is not null && emp.Status == "Active", "EmployeeInactive"))
            denial = "EmployeeInactive";
        // Gate 3 — medical clearance (skipped when not required)
        else if (!Gate("MedicalValid",
                 emp!.MedicalClearanceExpiry is null || emp.MedicalClearanceExpiry >= today,
                 "MedicalExpired"))
            denial = "MedicalExpired";
        // Gate 4 — all trainings required by this equipment type are valid
        else if (!Gate("TrainingValid",
                 equipment.EquipmentType.RequiredTrainings.All(req =>
                     emp.Trainings.Any(t => t.CourseId == req.CourseId &&
                         (t.ExpiryDate is null || t.ExpiryDate >= today))),
                 "TrainingMissingOrExpired"))
            denial = "TrainingMissingOrExpired";
        // Gate 5 — required license valid
        else if (!Gate("LicenseValid",
                 equipment.EquipmentType.RequiredLicenses.All(req =>
                     emp.Licenses.Any(l => l.LicenseTypeId == req.LicenseTypeId && l.ExpiryDate >= today)),
                 "LicenseMissingOrExpired"))
            denial = "LicenseMissingOrExpired";
        // Gate 6 — authorization matrix (machine or type scope + validity + shift + location)
        else if (!Gate("Authorized", IsAuthorized(emp, equipment, now), "NotAuthorized"))
            denial = "NotAuthorized";
        // Gate 7 — equipment available
        else if (!Gate("EquipmentAvailable", equipment.Status == "Available",
                 equipment.Status == "InUse" ? "EquipmentInUse" : "EquipmentUnavailable"))
            denial = equipment.Status == "InUse" ? "EquipmentInUse" : "EquipmentUnavailable";
        // Gate 8 — maintenance interlock
        else if (!Gate("MaintenanceClear",
                 !equipment.MaintenanceLocked && equipment.Status != "UnderMaintenance",
                 "MaintenanceLock"))
            denial = "MaintenanceLock";
        else
            granted = true;

        // ---- Persist the event (always) ----
        var evt = new AccessEvent
        {
            EventAt = now,
            RfidReaderId = readerId,
            CardUid = cardUid,
            CardId = card?.CardId,
            EmployeeId = emp?.EmployeeId,
            EquipmentId = equipment?.EquipmentId,
            Result = granted ? "Granted" : "Denied",
            DenialReason = denial,
            ValidationTrace = JsonSerializer.Serialize(trace)
        };
        _db.AccessEvents.Add(evt);

        if (granted)
        {
            equipment!.Status = "InUse";
            equipment.CurrentOperatorId = emp!.EmployeeId;
            await _db.SaveChangesAsync(ct); // materialize EventId
            _db.UsageSessions.Add(new UsageSession
            {
                EquipmentId = equipment.EquipmentId,
                EmployeeId = emp.EmployeeId,
                StartEventId = evt.EventId,
                StartTime = now,
                SupervisorId = equipment.SupervisorId
            });
        }
        await _db.SaveChangesAsync(ct);

        // ---- Notify supervisor on any denied attempt ----
        if (!granted && emp?.SupervisorId is not null)
            await _notify.SendAsync(
                supervisorEmployeeId: emp.SupervisorId.Value,
                subject: "HESAMS: denied access attempt",
                body: $"{emp.FullName} was denied on {equipment?.EquipmentName ?? readerId}: {denial}",
                relatedEntity: "AccessEvent", relatedId: (int)evt.EventId, ct);

        return new ValidationResponse(granted, denial, evt.EventId, trace);
    }

    /// <summary>Close the usage session when the machine is switched off / card tapped out.</summary>
    public async Task EndSessionAsync(string readerId, decimal? hoursAdded, decimal? fuel, CancellationToken ct = default)
    {
        var equipment = await _db.Equipment.FirstOrDefaultAsync(e => e.RfidReaderId == readerId, ct);
        if (equipment is null) return;

        var session = await _db.UsageSessions
            .Where(s => s.EquipmentId == equipment.EquipmentId && s.EndTime == null)
            .OrderByDescending(s => s.StartTime).FirstOrDefaultAsync(ct);
        if (session is null) return;

        session.EndTime = DateTime.UtcNow;
        session.HoursAdded = hoursAdded;
        session.FuelConsumed = fuel;
        equipment.Status = "Available";
        equipment.CurrentOperatorId = null;
        if (hoursAdded is not null) equipment.OperatingHours += hoursAdded.Value;
        await _db.SaveChangesAsync(ct);
    }

    private static bool IsAuthorized(Employee emp, Equipment eq, DateTime now)
    {
        var t = TimeOnly.FromDateTime(now);
        return emp.Authorizations.Any(a =>
            a.IsActive
            && (a.EquipmentId == eq.EquipmentId ||
                (a.EquipmentId == null && a.EquipmentTypeId == eq.EquipmentTypeId))
            && a.ValidFrom <= now
            && (a.ValidTo == null || a.ValidTo >= now)
            && (a.LocationId == null || a.LocationId == eq.LocationId)
            && (a.DepartmentId == null || a.DepartmentId == eq.DepartmentId)
            && (a.Shift == null || InShift(a.Shift, t)));
    }

    private static bool InShift(Shift s, TimeOnly t) =>
        s.StartTime <= s.EndTime
            ? t >= s.StartTime && t <= s.EndTime
            : t >= s.StartTime || t <= s.EndTime; // overnight shift
}
