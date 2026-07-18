using HESAMS.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HESAMS.Api.Data;

public class HesamsDbContext : DbContext
{
    public HesamsDbContext(DbContextOptions<HesamsDbContext> options) : base(options) { }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<EmployeeDocument> EmployeeDocuments => Set<EmployeeDocument>();
    public DbSet<RfidCard> RfidCards => Set<RfidCard>();
    public DbSet<CardAssignmentLog> CardAssignmentLogs => Set<CardAssignmentLog>();
    public DbSet<EquipmentType> EquipmentTypes => Set<EquipmentType>();
    public DbSet<Equipment> Equipment => Set<Equipment>();
    public DbSet<EquipmentFile> EquipmentFiles => Set<EquipmentFile>();
    public DbSet<TrainingCourse> TrainingCourses => Set<TrainingCourse>();
    public DbSet<EquipmentTypeTrainingRequirement> EquipmentTypeTrainingRequirements => Set<EquipmentTypeTrainingRequirement>();
    public DbSet<EmployeeTraining> EmployeeTrainings => Set<EmployeeTraining>();
    public DbSet<LicenseType> LicenseTypes => Set<LicenseType>();
    public DbSet<EquipmentTypeLicenseRequirement> EquipmentTypeLicenseRequirements => Set<EquipmentTypeLicenseRequirement>();
    public DbSet<EmployeeLicense> EmployeeLicenses => Set<EmployeeLicense>();
    public DbSet<EquipmentAuthorization> Authorizations => Set<EquipmentAuthorization>();
    public DbSet<AccessEvent> AccessEvents => Set<AccessEvent>();
    public DbSet<UsageSession> UsageSessions => Set<UsageSession>();
    public DbSet<MaintenanceOrder> MaintenanceOrders => Set<MaintenanceOrder>();
    public DbSet<MaintenancePart> MaintenanceParts => Set<MaintenancePart>();
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AuditLogEntry> AuditLog => Set<AuditLogEntry>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Role>().HasIndex(x => x.RoleName).IsUnique();
        b.Entity<Permission>().HasIndex(x => x.PermissionCode).IsUnique();
        b.Entity<RolePermission>().HasKey(x => new { x.RoleId, x.PermissionId });
        b.Entity<User>().HasIndex(x => x.Username).IsUnique();
        b.Entity<User>().HasIndex(x => x.Email).IsUnique();
        b.Entity<Employee>().HasIndex(x => x.EmployeeCode).IsUnique();
        b.Entity<Employee>()
            .HasOne(x => x.Supervisor).WithMany()
            .HasForeignKey(x => x.SupervisorId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<RfidCard>().HasKey(x => x.CardId);
        b.Entity<RfidCard>().HasIndex(x => x.CardUid).IsUnique();
        b.Entity<Equipment>().HasIndex(x => x.EquipmentCode).IsUnique();
        b.Entity<Equipment>().HasIndex(x => x.RfidReaderId).IsUnique();
        b.Entity<Equipment>().HasIndex(x => x.Status);
        b.Entity<Equipment>().HasIndex(x => x.NextMaintenanceDate);
        b.Entity<EquipmentTypeTrainingRequirement>().HasKey(x => new { x.EquipmentTypeId, x.CourseId });
        b.Entity<TrainingCourse>().HasKey(x => x.CourseId);
        b.Entity<EquipmentTypeLicenseRequirement>().HasKey(x => new { x.EquipmentTypeId, x.LicenseTypeId });
        b.Entity<EmployeeTraining>().HasIndex(x => new { x.EmployeeId, x.CourseId, x.ExpiryDate });
        b.Entity<EmployeeLicense>().HasKey(x => x.LicenseId);
        b.Entity<EmployeeLicense>().HasIndex(x => new { x.EmployeeId, x.LicenseTypeId, x.ExpiryDate });
        b.Entity<EquipmentFile>().HasKey(x => x.FileId);
        b.Entity<EquipmentAuthorization>().HasKey(x => x.AuthorizationId);
        b.Entity<EquipmentAuthorization>().HasIndex(x => x.EmployeeId).HasFilter("[IsActive] = 1");
        b.Entity<AccessEvent>().HasKey(x => x.EventId);
        b.Entity<AccessEvent>().HasIndex(x => x.EventAt).IsDescending();
        b.Entity<AccessEvent>().HasIndex(x => new { x.EquipmentId, x.EventAt });
        b.Entity<UsageSession>().HasKey(x => x.SessionId);
        b.Entity<UsageSession>().HasIndex(x => new { x.EquipmentId, x.StartTime });
        b.Entity<MaintenanceOrder>().HasKey(x => x.MaintenanceId);
        b.Entity<MaintenancePart>().HasKey(x => x.PartUsageId);
        b.Entity<MaintenanceOrder>().HasIndex(x => new { x.EquipmentId, x.Status });
        b.Entity<EmployeeDocument>().HasKey(x => x.DocumentId);
        b.Entity<EmployeeLicense>().HasKey(x => x.LicenseId);
        b.Entity<AuditLogEntry>().HasKey(x => x.AuditId);
        b.Entity<CardAssignmentLog>().HasKey(x => x.LogId);
        b.Entity<AuditLogEntry>().HasIndex(x => x.OccurredAt).IsDescending();

        foreach (var p in b.Model.GetEntityTypes()
                     .SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
            p.SetColumnType("decimal(12,2)");
    }
}
