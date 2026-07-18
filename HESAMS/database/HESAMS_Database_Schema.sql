/* =============================================================
   HESAMS — Heavy Equipment Safety & Access Management System
   Microsoft SQL Server Database Schema (3NF)
   Version 1.0 — Phase 1
   =============================================================
   Order of creation respects foreign-key dependencies.
   ============================================================= */

CREATE DATABASE HESAMS;
GO
USE HESAMS;
GO

/* ---------- 1. SECURITY & RBAC ---------- */

CREATE TABLE Roles (
    RoleId          INT IDENTITY(1,1) PRIMARY KEY,
    RoleName        NVARCHAR(50) NOT NULL UNIQUE,   -- SuperAdmin, SafetyManager, ...
    Description     NVARCHAR(250) NULL,
    IsSystemRole    BIT NOT NULL DEFAULT 0
);

CREATE TABLE Permissions (
    PermissionId    INT IDENTITY(1,1) PRIMARY KEY,
    PermissionCode  NVARCHAR(100) NOT NULL UNIQUE,  -- e.g. equipment.update
    Module          NVARCHAR(50) NOT NULL,
    Description     NVARCHAR(250) NULL
);

CREATE TABLE RolePermissions (
    RoleId          INT NOT NULL REFERENCES Roles(RoleId),
    PermissionId    INT NOT NULL REFERENCES Permissions(PermissionId),
    PRIMARY KEY (RoleId, PermissionId)
);

CREATE TABLE Users (
    UserId          INT IDENTITY(1,1) PRIMARY KEY,
    Username        NVARCHAR(100) NOT NULL UNIQUE,
    Email           NVARCHAR(150) NOT NULL UNIQUE,
    PasswordHash    NVARCHAR(500) NOT NULL,
    RoleId          INT NOT NULL REFERENCES Roles(RoleId),
    EmployeeId      INT NULL,                        -- FK added after Employees
    IsActive        BIT NOT NULL DEFAULT 1,
    MfaEnabled      BIT NOT NULL DEFAULT 0,
    LastLoginAt     DATETIME2 NULL,
    FailedAttempts  INT NOT NULL DEFAULT 0,
    LockedUntil     DATETIME2 NULL,
    CreatedAt       DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

/* ---------- 2. ORGANIZATION ---------- */

CREATE TABLE Companies (
    CompanyId       INT IDENTITY(1,1) PRIMARY KEY,
    CompanyName     NVARCHAR(150) NOT NULL UNIQUE
);

CREATE TABLE Departments (
    DepartmentId    INT IDENTITY(1,1) PRIMARY KEY,
    CompanyId       INT NOT NULL REFERENCES Companies(CompanyId),
    DepartmentName  NVARCHAR(150) NOT NULL,
    CONSTRAINT UQ_Dept UNIQUE (CompanyId, DepartmentName)
);

CREATE TABLE Locations (
    LocationId      INT IDENTITY(1,1) PRIMARY KEY,
    LocationName    NVARCHAR(150) NOT NULL,
    Site            NVARCHAR(150) NULL,
    GpsLat          DECIMAL(9,6) NULL,
    GpsLng          DECIMAL(9,6) NULL
);

CREATE TABLE Shifts (
    ShiftId         INT IDENTITY(1,1) PRIMARY KEY,
    ShiftName       NVARCHAR(50) NOT NULL,           -- Day / Night / A / B
    StartTime       TIME NOT NULL,
    EndTime         TIME NOT NULL
);

/* ---------- 3. EMPLOYEES ---------- */

CREATE TABLE Employees (
    EmployeeId      INT IDENTITY(1,1) PRIMARY KEY,
    EmployeeCode    NVARCHAR(30) NOT NULL UNIQUE,    -- badge / HR number
    FullName        NVARCHAR(150) NOT NULL,
    PhotoUrl        NVARCHAR(400) NULL,
    CompanyId       INT NOT NULL REFERENCES Companies(CompanyId),
    DepartmentId    INT NOT NULL REFERENCES Departments(DepartmentId),
    JobTitle        NVARCHAR(100) NULL,
    Email           NVARCHAR(150) NULL,
    Phone           NVARCHAR(30)  NULL,
    Nationality     NVARCHAR(60)  NULL,
    Status          NVARCHAR(20) NOT NULL DEFAULT 'Active'
                    CHECK (Status IN ('Active','Suspended','OnLeave','Terminated')),
    MedicalClearanceExpiry DATE NULL,                -- NULL = not required
    EmergencyContactName  NVARCHAR(150) NULL,
    EmergencyContactPhone NVARCHAR(30) NULL,
    SupervisorId    INT NULL REFERENCES Employees(EmployeeId),
    Notes           NVARCHAR(MAX) NULL,
    CreatedAt       DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt       DATETIME2 NULL
);

ALTER TABLE Users
    ADD CONSTRAINT FK_Users_Employee FOREIGN KEY (EmployeeId) REFERENCES Employees(EmployeeId);

CREATE TABLE EmployeeDocuments (
    DocumentId      INT IDENTITY(1,1) PRIMARY KEY,
    EmployeeId      INT NOT NULL REFERENCES Employees(EmployeeId),
    DocumentType    NVARCHAR(50) NOT NULL,           -- Iqama, Passport, Contract...
    FileUrl         NVARCHAR(400) NOT NULL,
    ExpiryDate      DATE NULL,
    UploadedAt      DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

/* ---------- 4. RFID CARDS ---------- */

CREATE TABLE RfidCards (
    CardId          INT IDENTITY(1,1) PRIMARY KEY,
    CardUid         NVARCHAR(50) NOT NULL UNIQUE,    -- hardware UID
    EmployeeId      INT NULL REFERENCES Employees(EmployeeId),
    CardType        NVARCHAR(20) NOT NULL DEFAULT 'Permanent'
                    CHECK (CardType IN ('Permanent','Temporary','Replacement')),
    Status          NVARCHAR(20) NOT NULL DEFAULT 'Active'
                    CHECK (Status IN ('Active','Inactive','Lost','Expired','Blocked')),
    IssuedAt        DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    ExpiresAt       DATETIME2 NULL,
    IssuedByUserId  INT NULL REFERENCES Users(UserId)
);

CREATE TABLE CardAssignmentLog (
    LogId           BIGINT IDENTITY(1,1) PRIMARY KEY,
    CardId          INT NOT NULL REFERENCES RfidCards(CardId),
    EmployeeId      INT NULL REFERENCES Employees(EmployeeId),
    Action          NVARCHAR(30) NOT NULL,           -- Issued/Deactivated/Replaced/Assigned
    Reason          NVARCHAR(250) NULL,
    ActionByUserId  INT NULL REFERENCES Users(UserId),
    ActionAt        DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

/* ---------- 5. EQUIPMENT ---------- */

CREATE TABLE EquipmentTypes (
    EquipmentTypeId INT IDENTITY(1,1) PRIMARY KEY,
    TypeName        NVARCHAR(100) NOT NULL UNIQUE    -- Forklift, Excavator, Crane...
);

CREATE TABLE Equipment (
    EquipmentId     INT IDENTITY(1,1) PRIMARY KEY,
    EquipmentCode   NVARCHAR(30) NOT NULL UNIQUE,    -- asset tag
    EquipmentName   NVARCHAR(150) NOT NULL,
    EquipmentTypeId INT NOT NULL REFERENCES EquipmentTypes(EquipmentTypeId),
    Manufacturer    NVARCHAR(100) NULL,
    Model           NVARCHAR(100) NULL,
    ModelYear       SMALLINT NULL,
    VinSerial       NVARCHAR(100) NULL UNIQUE,
    EngineNumber    NVARCHAR(100) NULL,
    LocationId      INT NULL REFERENCES Locations(LocationId),
    DepartmentId    INT NULL REFERENCES Departments(DepartmentId),
    Status          NVARCHAR(25) NOT NULL DEFAULT 'Available'
                    CHECK (Status IN ('Available','InUse','UnderMaintenance','Locked','OutOfService')),
    OperatingHours  DECIMAL(10,1) NOT NULL DEFAULT 0,
    FuelType        NVARCHAR(30) NULL,
    CurrentOperatorId  INT NULL REFERENCES Employees(EmployeeId),
    SupervisorId    INT NULL REFERENCES Employees(EmployeeId),
    PurchaseDate    DATE NULL,
    WarrantyExpiry  DATE NULL,
    InsuranceExpiry DATE NULL,
    LastInspection  DATE NULL,
    MaintenanceIntervalHours INT NULL,               -- hour-based PM
    MaintenanceIntervalDays  INT NULL,               -- calendar-based PM
    NextMaintenanceDate      DATE NULL,
    NextMaintenanceHours     DECIMAL(10,1) NULL,
    MaintenanceLocked BIT NOT NULL DEFAULT 0,        -- hard interlock
    QrCode          NVARCHAR(200) NULL,
    RfidReaderId    NVARCHAR(50) NULL UNIQUE,        -- ESP32 device id
    CreatedAt       DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt       DATETIME2 NULL
);

CREATE TABLE EquipmentFiles (
    FileId          INT IDENTITY(1,1) PRIMARY KEY,
    EquipmentId     INT NOT NULL REFERENCES Equipment(EquipmentId),
    FileType        NVARCHAR(30) NOT NULL,           -- Image / Manual / Attachment
    FileUrl         NVARCHAR(400) NOT NULL,
    UploadedAt      DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

/* ---------- 6. TRAINING ---------- */

CREATE TABLE TrainingCourses (
    CourseId        INT IDENTITY(1,1) PRIMARY KEY,
    CourseName      NVARCHAR(150) NOT NULL,
    Provider        NVARCHAR(150) NULL,
    ValidityMonths  INT NULL                          -- NULL = never expires
);

/* which equipment types require which courses */
CREATE TABLE EquipmentTypeTrainingRequirements (
    EquipmentTypeId INT NOT NULL REFERENCES EquipmentTypes(EquipmentTypeId),
    CourseId        INT NOT NULL REFERENCES TrainingCourses(CourseId),
    PRIMARY KEY (EquipmentTypeId, CourseId)
);

CREATE TABLE EmployeeTrainings (
    EmployeeTrainingId INT IDENTITY(1,1) PRIMARY KEY,
    EmployeeId      INT NOT NULL REFERENCES Employees(EmployeeId),
    CourseId        INT NOT NULL REFERENCES TrainingCourses(CourseId),
    TrainingDate    DATE NOT NULL,
    ExpiryDate      DATE NULL,
    CertificateUrl  NVARCHAR(400) NULL,
    Status          AS (CASE WHEN ExpiryDate IS NULL THEN 'Valid'
                             WHEN ExpiryDate >= CAST(GETDATE() AS DATE) THEN 'Valid'
                             ELSE 'Expired' END)
);

/* ---------- 7. LICENSES ---------- */

CREATE TABLE LicenseTypes (
    LicenseTypeId   INT IDENTITY(1,1) PRIMARY KEY,
    LicenseTypeName NVARCHAR(100) NOT NULL UNIQUE    -- Heavy Equipment, Crane, Forklift
);

CREATE TABLE EquipmentTypeLicenseRequirements (
    EquipmentTypeId INT NOT NULL REFERENCES EquipmentTypes(EquipmentTypeId),
    LicenseTypeId   INT NOT NULL REFERENCES LicenseTypes(LicenseTypeId),
    PRIMARY KEY (EquipmentTypeId, LicenseTypeId)
);

CREATE TABLE EmployeeLicenses (
    LicenseId       INT IDENTITY(1,1) PRIMARY KEY,
    EmployeeId      INT NOT NULL REFERENCES Employees(EmployeeId),
    LicenseTypeId   INT NOT NULL REFERENCES LicenseTypes(LicenseTypeId),
    LicenseNumber   NVARCHAR(60) NOT NULL,
    IssueDate       DATE NOT NULL,
    ExpiryDate      DATE NOT NULL,
    IssuingAuthority NVARCHAR(150) NULL,
    FileUrl         NVARCHAR(400) NULL
);

/* ---------- 8. AUTHORIZATION ENGINE ---------- */

CREATE TABLE Authorizations (
    AuthorizationId INT IDENTITY(1,1) PRIMARY KEY,
    EmployeeId      INT NOT NULL REFERENCES Employees(EmployeeId),
    /* one of the scopes below — exactly one must be non-null */
    EquipmentId     INT NULL REFERENCES Equipment(EquipmentId),      -- specific machine
    EquipmentTypeId INT NULL REFERENCES EquipmentTypes(EquipmentTypeId), -- machine class
    AuthType        NVARCHAR(20) NOT NULL DEFAULT 'Permanent'
                    CHECK (AuthType IN ('Permanent','Temporary')),
    ValidFrom       DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    ValidTo         DATETIME2 NULL,                  -- NULL = open-ended
    ShiftId         INT NULL REFERENCES Shifts(ShiftId),      -- shift restriction
    LocationId      INT NULL REFERENCES Locations(LocationId),-- location restriction
    DepartmentId    INT NULL REFERENCES Departments(DepartmentId),
    GrantedByUserId INT NOT NULL REFERENCES Users(UserId),
    IsActive        BIT NOT NULL DEFAULT 1,
    Notes           NVARCHAR(250) NULL,
    CONSTRAINT CK_Auth_Scope CHECK (EquipmentId IS NOT NULL OR EquipmentTypeId IS NOT NULL)
);

/* ---------- 9. ACCESS EVENTS (RFID SCANS) ---------- */

CREATE TABLE AccessEvents (
    EventId         BIGINT IDENTITY(1,1) PRIMARY KEY,
    EventAt         DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    RfidReaderId    NVARCHAR(50) NOT NULL,
    CardUid         NVARCHAR(50) NOT NULL,
    CardId          INT NULL REFERENCES RfidCards(CardId),
    EmployeeId      INT NULL REFERENCES Employees(EmployeeId),
    EquipmentId     INT NULL REFERENCES Equipment(EquipmentId),
    Result          NVARCHAR(10) NOT NULL CHECK (Result IN ('Granted','Denied')),
    DenialReason    NVARCHAR(60) NULL,               -- CardInactive, TrainingExpired, ...
    ValidationTrace NVARCHAR(MAX) NULL               -- JSON of each check
);

CREATE TABLE UsageSessions (
    SessionId       BIGINT IDENTITY(1,1) PRIMARY KEY,
    EquipmentId     INT NOT NULL REFERENCES Equipment(EquipmentId),
    EmployeeId      INT NOT NULL REFERENCES Employees(EmployeeId),
    StartEventId    BIGINT NOT NULL REFERENCES AccessEvents(EventId),
    StartTime       DATETIME2 NOT NULL,
    EndTime         DATETIME2 NULL,
    DurationMinutes AS (DATEDIFF(MINUTE, StartTime, EndTime)),
    HoursAdded      DECIMAL(8,1) NULL,
    FuelConsumed    DECIMAL(8,2) NULL,
    GpsLat          DECIMAL(9,6) NULL,
    GpsLng          DECIMAL(9,6) NULL,
    ShiftId         INT NULL REFERENCES Shifts(ShiftId),
    Purpose         NVARCHAR(250) NULL,
    SupervisorId    INT NULL REFERENCES Employees(EmployeeId)
);

/* ---------- 10. MAINTENANCE ---------- */

CREATE TABLE MaintenanceOrders (
    MaintenanceId   INT IDENTITY(1,1) PRIMARY KEY,
    EquipmentId     INT NOT NULL REFERENCES Equipment(EquipmentId),
    MaintenanceType NVARCHAR(20) NOT NULL
                    CHECK (MaintenanceType IN ('Preventive','Corrective','Emergency','Predictive','Inspection')),
    ScheduledDate   DATE NULL,
    TargetHours     DECIMAL(10,1) NULL,
    Priority        NVARCHAR(10) NOT NULL DEFAULT 'Medium'
                    CHECK (Priority IN ('Low','Medium','High','Critical')),
    TechnicianId    INT NULL REFERENCES Employees(EmployeeId),
    VendorName      NVARCHAR(150) NULL,
    Cost            DECIMAL(12,2) NULL,
    CompletionDate  DATE NULL,
    Status          NVARCHAR(20) NOT NULL DEFAULT 'Scheduled'
                    CHECK (Status IN ('Scheduled','InProgress','Completed','Cancelled','Overdue')),
    Notes           NVARCHAR(MAX) NULL,
    CreatedByUserId INT NULL REFERENCES Users(UserId),
    CreatedAt       DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE TABLE MaintenanceParts (
    PartUsageId     INT IDENTITY(1,1) PRIMARY KEY,
    MaintenanceId   INT NOT NULL REFERENCES MaintenanceOrders(MaintenanceId),
    PartName        NVARCHAR(150) NOT NULL,
    PartNumber      NVARCHAR(80) NULL,
    Quantity        DECIMAL(10,2) NOT NULL DEFAULT 1,
    UnitCost        DECIMAL(12,2) NULL
);

CREATE TABLE MaintenanceAttachments (
    AttachmentId    INT IDENTITY(1,1) PRIMARY KEY,
    MaintenanceId   INT NOT NULL REFERENCES MaintenanceOrders(MaintenanceId),
    FileUrl         NVARCHAR(400) NOT NULL,
    UploadedAt      DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

/* ---------- 11. INCIDENTS ---------- */

CREATE TABLE Incidents (
    IncidentId      INT IDENTITY(1,1) PRIMARY KEY,
    IncidentType    NVARCHAR(20) NOT NULL
                    CHECK (IncidentType IN ('NearMiss','Accident','UnsafeAct','UnsafeCondition')),
    IncidentDate    DATETIME2 NOT NULL,
    EquipmentId     INT NULL REFERENCES Equipment(EquipmentId),
    EmployeeId      INT NULL REFERENCES Employees(EmployeeId),
    LocationId      INT NULL REFERENCES Locations(LocationId),
    Description     NVARCHAR(MAX) NOT NULL,
    RootCause       NVARCHAR(MAX) NULL,
    CorrectiveAction NVARCHAR(MAX) NULL,
    InvestigatorId  INT NULL REFERENCES Employees(EmployeeId),
    Status          NVARCHAR(20) NOT NULL DEFAULT 'Open'
                    CHECK (Status IN ('Open','UnderInvestigation','PendingApproval','Closed')),
    ApprovedByUserId INT NULL REFERENCES Users(UserId),
    ReportedByUserId INT NOT NULL REFERENCES Users(UserId),
    CreatedAt       DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

/* ---------- 12. NOTIFICATIONS ---------- */

CREATE TABLE NotificationTemplates (
    TemplateId      INT IDENTITY(1,1) PRIMARY KEY,
    TemplateCode    NVARCHAR(60) NOT NULL UNIQUE,    -- MAINT_DUE_7D, LICENSE_EXP_30D...
    Channel         NVARCHAR(20) NOT NULL CHECK (Channel IN ('Email','SMS','Push','InApp','Teams','WhatsApp')),
    SubjectTemplate NVARCHAR(250) NULL,
    BodyTemplate    NVARCHAR(MAX) NOT NULL
);

CREATE TABLE Notifications (
    NotificationId  BIGINT IDENTITY(1,1) PRIMARY KEY,
    TemplateId      INT NULL REFERENCES NotificationTemplates(TemplateId),
    RecipientUserId INT NULL REFERENCES Users(UserId),
    Channel         NVARCHAR(20) NOT NULL,
    Subject         NVARCHAR(250) NULL,
    Body            NVARCHAR(MAX) NOT NULL,
    RelatedEntity   NVARCHAR(40) NULL,               -- Equipment / License / Training
    RelatedEntityId INT NULL,
    Status          NVARCHAR(20) NOT NULL DEFAULT 'Pending'
                    CHECK (Status IN ('Pending','Sent','Failed','Read')),
    CreatedAt       DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    SentAt          DATETIME2 NULL,
    ReadAt          DATETIME2 NULL
);

/* ---------- 13. AUDIT LOG ---------- */

CREATE TABLE AuditLog (
    AuditId         BIGINT IDENTITY(1,1) PRIMARY KEY,
    OccurredAt      DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    UserId          INT NULL REFERENCES Users(UserId),
    Action          NVARCHAR(60) NOT NULL,           -- Login, RfidScan, Update, Delete...
    Entity          NVARCHAR(60) NULL,
    EntityId        NVARCHAR(40) NULL,
    OldValue        NVARCHAR(MAX) NULL,              -- JSON
    NewValue        NVARCHAR(MAX) NULL,              -- JSON
    IpAddress       NVARCHAR(45) NULL,
    UserAgent       NVARCHAR(300) NULL
);

/* ---------- 14. INDEXES ---------- */

CREATE INDEX IX_Employees_Department   ON Employees(DepartmentId);
CREATE INDEX IX_Employees_Status       ON Employees(Status);
CREATE INDEX IX_RfidCards_Employee     ON RfidCards(EmployeeId) WHERE Status = 'Active';
CREATE INDEX IX_Equipment_Status       ON Equipment(Status);
CREATE INDEX IX_Equipment_NextMaint    ON Equipment(NextMaintenanceDate);
CREATE INDEX IX_EmpTrain_Employee      ON EmployeeTrainings(EmployeeId, CourseId, ExpiryDate);
CREATE INDEX IX_EmpLicense_Employee    ON EmployeeLicenses(EmployeeId, LicenseTypeId, ExpiryDate);
CREATE INDEX IX_Auth_Employee          ON Authorizations(EmployeeId) WHERE IsActive = 1;
CREATE INDEX IX_Auth_Equipment         ON Authorizations(EquipmentId) WHERE IsActive = 1;
CREATE INDEX IX_AccessEvents_Time      ON AccessEvents(EventAt DESC);
CREATE INDEX IX_AccessEvents_Equipment ON AccessEvents(EquipmentId, EventAt DESC);
CREATE INDEX IX_AccessEvents_Denied    ON AccessEvents(Result, EventAt DESC) WHERE Result = 'Denied';
CREATE INDEX IX_Usage_Equipment        ON UsageSessions(EquipmentId, StartTime DESC);
CREATE INDEX IX_Maint_Equipment        ON MaintenanceOrders(EquipmentId, Status);
CREATE INDEX IX_Maint_Scheduled        ON MaintenanceOrders(ScheduledDate) WHERE Status IN ('Scheduled','Overdue');
CREATE INDEX IX_Audit_Time             ON AuditLog(OccurredAt DESC);
CREATE INDEX IX_Audit_User             ON AuditLog(UserId, OccurredAt DESC);
CREATE INDEX IX_Notif_Recipient        ON Notifications(RecipientUserId, Status);
GO

/* ---------- 15. SEED — ROLES ---------- */

INSERT INTO Roles (RoleName, Description, IsSystemRole) VALUES
 ('SuperAdmin','Full system control',1),
 ('SafetyManager','Safety oversight, incidents, KPIs',1),
 ('MaintenanceManager','Maintenance planning and unlock authority',1),
 ('OperationsManager','Operations oversight',1),
 ('Supervisor','Team and equipment supervision',1),
 ('EquipmentOperator','Operates assigned equipment',1),
 ('HRAdministrator','Employee records management',1),
 ('TrainingCoordinator','Training records management',1),
 ('Auditor','Read access to logs and reports',1),
 ('Guest','Read-only access',1);
GO
