using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HESAMS.Api.Data;
using HESAMS.Api.Models;
using HESAMS.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HESAMS.Tests;

/// <summary>
/// UT-01 .. UT-24 from the testing plan. The interlock chain is safety-critical:
/// every gate has a passing and a failing test, and no test may assert fail-open.
/// </summary>
public class AccessValidationTests
{
    private class FakeNotifier : INotificationService
    {
        public int Sent;
        public Task SendAsync(int s, string subj, string body, string? e = null, int? i = null, CancellationToken ct = default)
        { Sent++; return Task.CompletedTask; }
        public Task QueueAsync(int? u, string c, string subj, string body, string? e = null, int? i = null, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);
    private static readonly DateOnly Future = Today.AddYears(1);
    private static readonly DateOnly Past = Today.AddDays(-1);

    /// <summary>Builds a fully-valid world; each test breaks exactly one gate.</summary>
    private static (HesamsDbContext db, FakeNotifier notifier) Build(Action<HesamsDbContext>? mutate = null)
    {
        var opts = new DbContextOptionsBuilder<HesamsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new HesamsDbContext(opts);

        db.Companies.Add(new Company { CompanyId = 1, CompanyName = "ASMO" });
        db.Departments.Add(new Department { DepartmentId = 1, CompanyId = 1, DepartmentName = "Ops" });
        db.TrainingCourses.Add(new TrainingCourse { CourseId = 1, CourseName = "Forklift", ValidityMonths = 24 });
        db.LicenseTypes.Add(new LicenseType { LicenseTypeId = 1, LicenseTypeName = "Forklift License" });
        db.EquipmentTypes.Add(new EquipmentType { EquipmentTypeId = 1, TypeName = "Forklift" });
        db.EquipmentTypeTrainingRequirements.Add(new EquipmentTypeTrainingRequirement { EquipmentTypeId = 1, CourseId = 1 });
        db.EquipmentTypeLicenseRequirements.Add(new EquipmentTypeLicenseRequirement { EquipmentTypeId = 1, LicenseTypeId = 1 });

        db.Employees.Add(new Employee
        {
            EmployeeId = 1, EmployeeCode = "EMP-1", FullName = "Operator",
            CompanyId = 1, DepartmentId = 1, Status = "Active",
            MedicalClearanceExpiry = Future, SupervisorId = null
        });
        db.RfidCards.Add(new RfidCard { CardId = 1, CardUid = "04:AA", EmployeeId = 1, Status = "Active" });
        db.EmployeeTrainings.Add(new EmployeeTraining { EmployeeTrainingId = 1, EmployeeId = 1, CourseId = 1, TrainingDate = Today, ExpiryDate = Future });
        db.EmployeeLicenses.Add(new EmployeeLicense { LicenseId = 1, EmployeeId = 1, LicenseTypeId = 1, LicenseNumber = "L1", IssueDate = Today, ExpiryDate = Future });
        db.Equipment.Add(new Equipment
        {
            EquipmentId = 1, EquipmentCode = "EQ-1", EquipmentName = "FL-12",
            EquipmentTypeId = 1, Status = "Available", RfidReaderId = "RDR-A1", MaintenanceLocked = false
        });
        db.Authorizations.Add(new EquipmentAuthorization
        {
            AuthorizationId = 1, EmployeeId = 1, EquipmentId = 1,
            AuthType = "Permanent", ValidFrom = DateTime.UtcNow.AddDays(-10),
            IsActive = true, GrantedByUserId = 1
        });
        db.SaveChanges();
        mutate?.Invoke(db);
        db.SaveChanges();
        return (db, new FakeNotifier());
    }

    private static AccessValidationService Svc(HesamsDbContext db, FakeNotifier n) => new(db, n);

    [Fact] // UT-01
    public async Task AllGatesPass_Grants_OpensSession_SetsInUse()
    {
        var (db, n) = Build();
        var r = await Svc(db, n).ValidateAsync("RDR-A1", "04:AA");
        Assert.True(r.Granted);
        Assert.Null(r.DenialReason);
        Assert.Equal(8, r.Trace.Count);
        Assert.Equal("InUse", db.Equipment.Single().Status);
        Assert.Single(db.UsageSessions);
        Assert.Equal("Granted", db.AccessEvents.Single().Result);
    }

    [Fact] // UT-02
    public async Task InactiveCard_Denied_StopsAtFirstGate()
    {
        var (db, n) = Build(d => d.RfidCards.Single().Status = "Inactive");
        var r = await Svc(db, n).ValidateAsync("RDR-A1", "04:AA");
        Assert.False(r.Granted);
        Assert.Equal("CardInactiveOrExpired", r.DenialReason);
        Assert.Single(r.Trace); // UT-24: chain stops immediately
        Assert.Equal("Available", db.Equipment.Single().Status);
    }

    [Fact] // UT-03
    public async Task ExpiredCard_Denied()
    {
        var (db, n) = Build(d => d.RfidCards.Single().ExpiresAt = DateTime.UtcNow.AddDays(-1));
        var r = await Svc(db, n).ValidateAsync("RDR-A1", "04:AA");
        Assert.Equal("CardInactiveOrExpired", r.DenialReason);
    }

    [Fact] // UT-04
    public async Task UnknownCard_Denied()
    {
        var (db, n) = Build();
        var r = await Svc(db, n).ValidateAsync("RDR-A1", "99:ZZ");
        Assert.False(r.Granted);
        Assert.Equal("CardNotFound", r.DenialReason);
    }

    [Fact] // UT-05
    public async Task TerminatedEmployee_Denied()
    {
        var (db, n) = Build(d => d.Employees.Single().Status = "Terminated");
        var r = await Svc(db, n).ValidateAsync("RDR-A1", "04:AA");
        Assert.Equal("EmployeeInactive", r.DenialReason);
    }

    [Fact] // UT-06
    public async Task ExpiredMedical_Denied()
    {
        var (db, n) = Build(d => d.Employees.Single().MedicalClearanceExpiry = Past);
        var r = await Svc(db, n).ValidateAsync("RDR-A1", "04:AA");
        Assert.Equal("MedicalExpired", r.DenialReason);
    }

    [Fact] // UT-07
    public async Task NullMedical_MeansNotRequired_Passes()
    {
        var (db, n) = Build(d => d.Employees.Single().MedicalClearanceExpiry = null);
        var r = await Svc(db, n).ValidateAsync("RDR-A1", "04:AA");
        Assert.True(r.Granted);
    }

    [Fact] // UT-08
    public async Task ExpiredTraining_Denied()
    {
        var (db, n) = Build(d => d.EmployeeTrainings.Single().ExpiryDate = Past);
        var r = await Svc(db, n).ValidateAsync("RDR-A1", "04:AA");
        Assert.Equal("TrainingMissingOrExpired", r.DenialReason);
    }

    [Fact] // UT-09
    public async Task MissingTraining_Denied()
    {
        var (db, n) = Build(d => d.EmployeeTrainings.RemoveRange(d.EmployeeTrainings));
        var r = await Svc(db, n).ValidateAsync("RDR-A1", "04:AA");
        Assert.Equal("TrainingMissingOrExpired", r.DenialReason);
    }

    [Fact] // UT-10
    public async Task TrainingWithNoExpiry_Passes()
    {
        var (db, n) = Build(d => d.EmployeeTrainings.Single().ExpiryDate = null);
        var r = await Svc(db, n).ValidateAsync("RDR-A1", "04:AA");
        Assert.True(r.Granted);
    }

    [Fact] // UT-11
    public async Task ExpiredLicense_Denied()
    {
        var (db, n) = Build(d => d.EmployeeLicenses.Single().ExpiryDate = Past);
        var r = await Svc(db, n).ValidateAsync("RDR-A1", "04:AA");
        Assert.Equal("LicenseMissingOrExpired", r.DenialReason);
    }

    [Fact] // UT-12
    public async Task AuthorizationForDifferentMachine_Denied()
    {
        var (db, n) = Build(d => d.Authorizations.Single().EquipmentId = 999);
        var r = await Svc(db, n).ValidateAsync("RDR-A1", "04:AA");
        Assert.Equal("NotAuthorized", r.DenialReason);
    }

    [Fact] // UT-13
    public async Task TypeScopedAuthorization_MatchingClass_Grants()
    {
        var (db, n) = Build(d =>
        {
            var a = d.Authorizations.Single();
            a.EquipmentId = null;
            a.EquipmentTypeId = 1;
        });
        var r = await Svc(db, n).ValidateAsync("RDR-A1", "04:AA");
        Assert.True(r.Granted);
    }

    [Fact] // UT-14
    public async Task ExpiredTemporaryAuthorization_Denied()
    {
        var (db, n) = Build(d =>
        {
            var a = d.Authorizations.Single();
            a.AuthType = "Temporary";
            a.ValidTo = DateTime.UtcNow.AddDays(-1);
        });
        var r = await Svc(db, n).ValidateAsync("RDR-A1", "04:AA");
        Assert.Equal("NotAuthorized", r.DenialReason);
    }

    [Fact] // UT-19
    public async Task MachineAlreadyInUse_Denied()
    {
        var (db, n) = Build(d => d.Equipment.Single().Status = "InUse");
        var r = await Svc(db, n).ValidateAsync("RDR-A1", "04:AA");
        Assert.Equal("EquipmentInUse", r.DenialReason);
    }

    [Fact] // UT-20
    public async Task MaintenanceLock_Denied_EvenWhenFullyQualified()
    {
        var (db, n) = Build(d => d.Equipment.Single().MaintenanceLocked = true);
        var r = await Svc(db, n).ValidateAsync("RDR-A1", "04:AA");
        Assert.False(r.Granted);
        Assert.Equal("MaintenanceLock", r.DenialReason);
    }

    [Fact] // UT-21
    public async Task UnderMaintenance_Denied()
    {
        var (db, n) = Build(d => d.Equipment.Single().Status = "UnderMaintenance");
        var r = await Svc(db, n).ValidateAsync("RDR-A1", "04:AA");
        Assert.False(r.Granted);
    }

    [Fact] // UT-22
    public async Task UnknownReader_Denied()
    {
        var (db, n) = Build();
        var r = await Svc(db, n).ValidateAsync("RDR-NOPE", "04:AA");
        Assert.False(r.Granted);
        Assert.Equal("UnknownReader", r.DenialReason);
    }

    [Fact] // UT-23
    public async Task Denial_LogsEvent_AndNotifiesSupervisor()
    {
        var (db, n) = Build(d =>
        {
            d.Employees.Add(new Employee
            {
                EmployeeId = 2, EmployeeCode = "SUP-1", FullName = "Supervisor",
                CompanyId = 1, DepartmentId = 1, Status = "Active"
            });
            d.SaveChanges();
            d.Employees.First(e => e.EmployeeId == 1).SupervisorId = 2;
            d.EmployeeLicenses.Single().ExpiryDate = Past;
        });
        var r = await Svc(db, n).ValidateAsync("RDR-A1", "04:AA");
        Assert.False(r.Granted);
        Assert.Equal("Denied", db.AccessEvents.Single().Result);
        Assert.NotNull(db.AccessEvents.Single().ValidationTrace);
        Assert.Equal(1, n.Sent);
        Assert.Empty(db.UsageSessions); // no session on denial
    }

    [Fact]
    public async Task EndSession_AddsHours_ReturnsMachineToAvailable()
    {
        var (db, n) = Build();
        var svc = Svc(db, n);
        await svc.ValidateAsync("RDR-A1", "04:AA");
        await svc.EndSessionAsync("RDR-A1", hoursAdded: 3.5m, fuel: 22m);

        var eq = db.Equipment.Single();
        Assert.Equal("Available", eq.Status);
        Assert.Null(eq.CurrentOperatorId);
        Assert.Equal(3.5m, eq.OperatingHours);
        Assert.NotNull(db.UsageSessions.Single().EndTime);
    }
}
