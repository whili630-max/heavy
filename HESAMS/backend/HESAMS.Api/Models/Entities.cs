using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HESAMS.Api.Models;

// ---------- RBAC ----------
public class Role
{
    public int RoleId { get; set; }
    [MaxLength(50)] public string RoleName { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsSystemRole { get; set; }
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

public class Permission
{
    public int PermissionId { get; set; }
    [MaxLength(100)] public string PermissionCode { get; set; } = null!; // e.g. equipment.update
    [MaxLength(50)] public string Module { get; set; } = null!;
    public string? Description { get; set; }
}

public class RolePermission
{
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public int PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;
}

public class User
{
    public int UserId { get; set; }
    [MaxLength(100)] public string Username { get; set; } = null!;
    [MaxLength(150)] public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public int? EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public bool IsActive { get; set; } = true;
    public bool MfaEnabled { get; set; }
    public string? MfaSecret { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public int FailedAttempts { get; set; }
    public DateTime? LockedUntil { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class RefreshToken
{
    public long RefreshTokenId { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public string Token { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}

// ---------- ORGANIZATION ----------
public class Company { public int CompanyId { get; set; } [MaxLength(150)] public string CompanyName { get; set; } = null!; }
public class Department
{
    public int DepartmentId { get; set; }
    public int CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    [MaxLength(150)] public string DepartmentName { get; set; } = null!;
}
public class Location
{
    public int LocationId { get; set; }
    [MaxLength(150)] public string LocationName { get; set; } = null!;
    public string? Site { get; set; }
    public decimal? GpsLat { get; set; }
    public decimal? GpsLng { get; set; }
}
public class Shift
{
    public int ShiftId { get; set; }
    [MaxLength(50)] public string ShiftName { get; set; } = null!;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
}

// ---------- EMPLOYEES ----------
public class Employee
{
    public int EmployeeId { get; set; }
    [MaxLength(30)] public string EmployeeCode { get; set; } = null!;
    [MaxLength(150)] public string FullName { get; set; } = null!;
    public string? PhotoUrl { get; set; }
    public int CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public string? JobTitle { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Nationality { get; set; }
    [MaxLength(20)] public string Status { get; set; } = "Active"; // Active/Suspended/OnLeave/Terminated
    public DateOnly? MedicalClearanceExpiry { get; set; }          // null = not required
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public int? SupervisorId { get; set; }
    public Employee? Supervisor { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public ICollection<EmployeeDocument> Documents { get; set; } = new List<EmployeeDocument>();
    public ICollection<RfidCard> Cards { get; set; } = new List<RfidCard>();
    public ICollection<EmployeeTraining> Trainings { get; set; } = new List<EmployeeTraining>();
    public ICollection<EmployeeLicense> Licenses { get; set; } = new List<EmployeeLicense>();
    public ICollection<EquipmentAuthorization> Authorizations { get; set; } = new List<EquipmentAuthorization>();
}

public class EmployeeDocument
{
    [Key]
    public int DocumentId { get; set; }
    public int EmployeeId { get; set; }
    public string DocumentType { get; set; } = null!;
    public string FileUrl { get; set; } = null!;
    public DateOnly? ExpiryDate { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}

// ---------- RFID ----------
public class RfidCard
{
    public int CardId { get; set; }
    [MaxLength(50)] public string CardUid { get; set; } = null!;
    public int? EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    [MaxLength(20)] public string CardType { get; set; } = "Permanent"; // Permanent/Temporary/Replacement
    [MaxLength(20)] public string Status { get; set; } = "Active";      // Active/Inactive/Lost/Expired/Blocked
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public int? IssuedByUserId { get; set; }
}

public class CardAssignmentLog
{
    public long LogId { get; set; }
    public int CardId { get; set; }
    public int? EmployeeId { get; set; }
    [MaxLength(30)] public string Action { get; set; } = null!; // Issued/Deactivated/Replaced/Assigned
    public string? Reason { get; set; }
    public int? ActionByUserId { get; set; }
    public DateTime ActionAt { get; set; } = DateTime.UtcNow;
}

// ---------- EQUIPMENT ----------
public class EquipmentType
{
    public int EquipmentTypeId { get; set; }
    [MaxLength(100)] public string TypeName { get; set; } = null!;
    public ICollection<EquipmentTypeTrainingRequirement> RequiredTrainings { get; set; } = new List<EquipmentTypeTrainingRequirement>();
    public ICollection<EquipmentTypeLicenseRequirement> RequiredLicenses { get; set; } = new List<EquipmentTypeLicenseRequirement>();
}

public class Equipment
{
    public int EquipmentId { get; set; }
    [MaxLength(30)] public string EquipmentCode { get; set; } = null!;
    [MaxLength(150)] public string EquipmentName { get; set; } = null!;
    public int EquipmentTypeId { get; set; }
    public EquipmentType EquipmentType { get; set; } = null!;
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public short? ModelYear { get; set; }
    public string? VinSerial { get; set; }
    public string? EngineNumber { get; set; }
    public int? LocationId { get; set; }
    public int? DepartmentId { get; set; }
    [MaxLength(25)] public string Status { get; set; } = "Available"; // Available/InUse/UnderMaintenance/Locked/OutOfService
    public decimal OperatingHours { get; set; }
    public string? FuelType { get; set; }
    public int? CurrentOperatorId { get; set; }
    public int? SupervisorId { get; set; }
    public DateOnly? PurchaseDate { get; set; }
    public DateOnly? WarrantyExpiry { get; set; }
    public DateOnly? InsuranceExpiry { get; set; }
    public DateOnly? LastInspection { get; set; }
    public int? MaintenanceIntervalHours { get; set; }
    public int? MaintenanceIntervalDays { get; set; }
    public DateOnly? NextMaintenanceDate { get; set; }
    public decimal? NextMaintenanceHours { get; set; }
    public bool MaintenanceLocked { get; set; }
    public string? QrCode { get; set; }
    [MaxLength(50)] public string? RfidReaderId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public class EquipmentFile
{
    public int FileId { get; set; }
    public int EquipmentId { get; set; }
    public string FileType { get; set; } = null!; // Image / Manual / Attachment
    public string FileUrl { get; set; } = null!;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}

// ---------- TRAINING & LICENSES ----------
public class TrainingCourse
{
    public int CourseId { get; set; }
    [MaxLength(150)] public string CourseName { get; set; } = null!;
    public string? Provider { get; set; }
    public int? ValidityMonths { get; set; }
}

public class EquipmentTypeTrainingRequirement
{
    public int EquipmentTypeId { get; set; }
    public int CourseId { get; set; }
    public TrainingCourse Course { get; set; } = null!;
}

public class EmployeeTraining
{
    public int EmployeeTrainingId { get; set; }
    public int EmployeeId { get; set; }
    public int CourseId { get; set; }
    public TrainingCourse Course { get; set; } = null!;
    public DateOnly TrainingDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public string? CertificateUrl { get; set; }
    [NotMapped] public bool IsValid => ExpiryDate is null || ExpiryDate >= DateOnly.FromDateTime(DateTime.UtcNow);
}

public class LicenseType { public int LicenseTypeId { get; set; } [MaxLength(100)] public string LicenseTypeName { get; set; } = null!; }

public class EquipmentTypeLicenseRequirement
{
    public int EquipmentTypeId { get; set; }
    public int LicenseTypeId { get; set; }
    public LicenseType LicenseType { get; set; } = null!;
}

public class EmployeeLicense
{
    public int LicenseId { get; set; }
    public int EmployeeId { get; set; }
    public int LicenseTypeId { get; set; }
    public LicenseType LicenseType { get; set; } = null!;
    [MaxLength(60)] public string LicenseNumber { get; set; } = null!;
    public DateOnly IssueDate { get; set; }
    public DateOnly ExpiryDate { get; set; }
    public string? IssuingAuthority { get; set; }
    public string? FileUrl { get; set; }
    [NotMapped] public bool IsValid => ExpiryDate >= DateOnly.FromDateTime(DateTime.UtcNow);
}

// ---------- AUTHORIZATION ENGINE ----------
public class EquipmentAuthorization
{
    public int AuthorizationId { get; set; }
    public int EmployeeId { get; set; }
    public int? EquipmentId { get; set; }        // specific machine
    public int? EquipmentTypeId { get; set; }    // or machine class
    [MaxLength(20)] public string AuthType { get; set; } = "Permanent"; // Permanent/Temporary
    public DateTime ValidFrom { get; set; } = DateTime.UtcNow;
    public DateTime? ValidTo { get; set; }
    public int? ShiftId { get; set; }
    public Shift? Shift { get; set; }
    public int? LocationId { get; set; }
    public int? DepartmentId { get; set; }
    public int GrantedByUserId { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
}

// ---------- ACCESS EVENTS & USAGE ----------
public class AccessEvent
{
    public long EventId { get; set; }
    public DateTime EventAt { get; set; } = DateTime.UtcNow;
    [MaxLength(50)] public string RfidReaderId { get; set; } = null!;
    [MaxLength(50)] public string CardUid { get; set; } = null!;
    public int? CardId { get; set; }
    public int? EmployeeId { get; set; }
    public int? EquipmentId { get; set; }
    [MaxLength(10)] public string Result { get; set; } = null!;     // Granted / Denied
    [MaxLength(60)] public string? DenialReason { get; set; }
    public string? ValidationTrace { get; set; }                    // JSON of each gate
}

public class UsageSession
{
    public long SessionId { get; set; }
    public int EquipmentId { get; set; }
    public int EmployeeId { get; set; }
    public long StartEventId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public decimal? HoursAdded { get; set; }
    public decimal? FuelConsumed { get; set; }
    public decimal? GpsLat { get; set; }
    public decimal? GpsLng { get; set; }
    public int? ShiftId { get; set; }
    public string? Purpose { get; set; }
    public int? SupervisorId { get; set; }
}

// ---------- MAINTENANCE ----------
public class MaintenanceOrder
{
    public int MaintenanceId { get; set; }
    public int EquipmentId { get; set; }
    public Equipment Equipment { get; set; } = null!;
    [MaxLength(20)] public string MaintenanceType { get; set; } = null!; // Preventive/Corrective/Emergency/Predictive/Inspection
    public DateOnly? ScheduledDate { get; set; }
    public decimal? TargetHours { get; set; }
    [MaxLength(10)] public string Priority { get; set; } = "Medium";
    public int? TechnicianId { get; set; }
    public string? VendorName { get; set; }
    public decimal? Cost { get; set; }
    public DateOnly? CompletionDate { get; set; }
    [MaxLength(20)] public string Status { get; set; } = "Scheduled";   // Scheduled/InProgress/Completed/Cancelled/Overdue
    public string? Notes { get; set; }
    public int? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<MaintenancePart> Parts { get; set; } = new List<MaintenancePart>();
}

public class MaintenancePart
{
    public int PartUsageId { get; set; }
    public int MaintenanceId { get; set; }
    public string PartName { get; set; } = null!;
    public string? PartNumber { get; set; }
    public decimal Quantity { get; set; } = 1;
    public decimal? UnitCost { get; set; }
}

// ---------- INCIDENTS ----------
public class Incident
{
    public int IncidentId { get; set; }
    [MaxLength(20)] public string IncidentType { get; set; } = null!; // NearMiss/Accident/UnsafeAct/UnsafeCondition
    public DateTime IncidentDate { get; set; }
    public int? EquipmentId { get; set; }
    public int? EmployeeId { get; set; }
    public int? LocationId { get; set; }
    public string Description { get; set; } = null!;
    public string? RootCause { get; set; }
    public string? CorrectiveAction { get; set; }
    public int? InvestigatorId { get; set; }
    [MaxLength(20)] public string Status { get; set; } = "Open"; // Open/UnderInvestigation/PendingApproval/Closed
    public int? ApprovedByUserId { get; set; }
    public int ReportedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ---------- NOTIFICATIONS & AUDIT ----------
public class Notification
{
    public long NotificationId { get; set; }
    public int? RecipientUserId { get; set; }
    [MaxLength(20)] public string Channel { get; set; } = "InApp"; // Email/SMS/Push/InApp/Teams/WhatsApp
    public string? Subject { get; set; }
    public string Body { get; set; } = null!;
    public string? RelatedEntity { get; set; }
    public int? RelatedEntityId { get; set; }
    [MaxLength(20)] public string Status { get; set; } = "Pending"; // Pending/Sent/Failed/Read
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
    public DateTime? ReadAt { get; set; }
}

public class AuditLogEntry
{
    public long AuditId { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public int? UserId { get; set; }
    [MaxLength(60)] public string Action { get; set; } = null!;
    public string? Entity { get; set; }
    public string? EntityId { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    [MaxLength(45)] public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
