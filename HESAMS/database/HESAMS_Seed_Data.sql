/* HESAMS — Seed / demo data. Run AFTER HESAMS_Database_Schema.sql */
USE HESAMS;
GO

-- Permissions
INSERT INTO Permissions (PermissionCode, Module) VALUES
 ('employees.read','Employees'),('employees.write','Employees'),
 ('rfid.read','RFID'),('rfid.write','RFID'),
 ('equipment.read','Equipment'),('equipment.write','Equipment'),('equipment.unlock','Equipment'),
 ('training.read','Training'),('training.write','Training'),
 ('licenses.read','Licenses'),('licenses.write','Licenses'),
 ('authorizations.read','Authorization'),('authorizations.write','Authorization'),
 ('maintenance.read','Maintenance'),('maintenance.write','Maintenance'),('maintenance.unlock','Maintenance'),
 ('incidents.read','Incidents'),('incidents.write','Incidents'),('incidents.approve','Incidents'),
 ('reports.read','Reports'),('reports.export','Reports'),
 ('audit.read','Audit'),('users.admin','Security'),('dashboard.read','Dashboard');

-- Super Admin gets everything
INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT r.RoleId, p.PermissionId FROM Roles r CROSS JOIN Permissions p WHERE r.RoleName = 'SuperAdmin';

-- Maintenance Manager
INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT r.RoleId, p.PermissionId FROM Roles r, Permissions p
WHERE r.RoleName = 'MaintenanceManager' AND p.PermissionCode IN
 ('maintenance.read','maintenance.write','maintenance.unlock','equipment.read','equipment.write',
  'equipment.unlock','dashboard.read','reports.read','reports.export','employees.read');

-- Safety Manager
INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT r.RoleId, p.PermissionId FROM Roles r, Permissions p
WHERE r.RoleName = 'SafetyManager' AND p.PermissionCode IN
 ('incidents.read','incidents.write','incidents.approve','reports.read','reports.export',
  'audit.read','dashboard.read','employees.read','equipment.read','training.read','licenses.read');

-- Operations Manager
INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT r.RoleId, p.PermissionId FROM Roles r, Permissions p
WHERE r.RoleName = 'OperationsManager' AND p.PermissionCode IN
 ('equipment.read','equipment.write','authorizations.read','authorizations.write',
  'dashboard.read','reports.read','employees.read');

-- HR Administrator
INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT r.RoleId, p.PermissionId FROM Roles r, Permissions p
WHERE r.RoleName = 'HRAdministrator' AND p.PermissionCode IN
 ('employees.read','employees.write','rfid.read','rfid.write','licenses.read','dashboard.read');

-- Training Coordinator
INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT r.RoleId, p.PermissionId FROM Roles r, Permissions p
WHERE r.RoleName = 'TrainingCoordinator' AND p.PermissionCode IN
 ('training.read','training.write','employees.read','dashboard.read','reports.read');

-- Supervisor / Auditor / Operator / Guest
INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT r.RoleId, p.PermissionId FROM Roles r, Permissions p
WHERE r.RoleName = 'Supervisor' AND p.PermissionCode IN
 ('employees.read','equipment.read','incidents.read','incidents.write','dashboard.read','reports.read');
INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT r.RoleId, p.PermissionId FROM Roles r, Permissions p
WHERE r.RoleName = 'Auditor' AND p.PermissionCode IN
 ('audit.read','reports.read','reports.export','dashboard.read','employees.read','equipment.read',
  'maintenance.read','incidents.read','training.read','licenses.read','authorizations.read');
INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT r.RoleId, p.PermissionId FROM Roles r, Permissions p
WHERE r.RoleName IN ('EquipmentOperator','Guest') AND p.PermissionCode = 'dashboard.read';

-- Organization
INSERT INTO Companies (CompanyName) VALUES ('ASMO Supply Chain');
INSERT INTO Departments (CompanyId, DepartmentName) VALUES
 (1,'Warehouse Ops'),(1,'Site Works'),(1,'Lifting'),(1,'Earthworks'),(1,'Haulage'),(1,'Maintenance');
INSERT INTO Locations (LocationName, Site) VALUES
 ('Yard A','Riyadh Central'),('Yard B','Riyadh Central'),('Workshop','Riyadh Central');
INSERT INTO Shifts (ShiftName, StartTime, EndTime) VALUES
 ('Day','06:00','18:00'),('Night','18:00','06:00');

-- Catalogs
INSERT INTO EquipmentTypes (TypeName) VALUES
 ('Forklift'),('Excavator'),('Mobile Crane'),('Wheel Loader'),('Dump Truck'),('Telehandler');
INSERT INTO TrainingCourses (CourseName, Provider, ValidityMonths) VALUES
 ('Forklift Safe Operation','SafetyPro KSA',24),
 ('Excavator Operation','SafetyPro KSA',24),
 ('Mobile Crane Operation','Lifting Institute',12),
 ('Wheel Loader Operation','SafetyPro KSA',24),
 ('Dump Truck Operation','SafetyPro KSA',36),
 ('Telehandler Operation','SafetyPro KSA',24),
 ('Site Safety Induction','Internal HSE',12);
INSERT INTO LicenseTypes (LicenseTypeName) VALUES
 ('Forklift License'),('Heavy Equipment License'),('Crane Operator License'),('Heavy Vehicle License');

-- Requirement matrix: every type needs its course + site induction
INSERT INTO EquipmentTypeTrainingRequirements (EquipmentTypeId, CourseId) VALUES
 (1,1),(1,7),(2,2),(2,7),(3,3),(3,7),(4,4),(4,7),(5,5),(5,7),(6,6),(6,7);
INSERT INTO EquipmentTypeLicenseRequirements (EquipmentTypeId, LicenseTypeId) VALUES
 (1,1),(2,2),(3,3),(4,2),(5,4),(6,1);

-- Employees
INSERT INTO Employees (EmployeeCode, FullName, CompanyId, DepartmentId, JobTitle, Status, MedicalClearanceExpiry) VALUES
 ('EMP-1000','Site Administrator',1,6,'System Administrator','Active',NULL),
 ('EMP-1042','Operator One',1,1,'Forklift Operator','Active','2027-01-15'),
 ('EMP-2210','Operator Two',1,2,'Excavator Operator','Active','2027-03-01'),
 ('EMP-3305','Operator Three',1,3,'Crane Operator','Active','2027-02-10'),
 ('EMP-4188','Operator Four',1,4,'Loader Operator','Active','2026-12-20'),
 ('EMP-5077','Operator Five',1,5,'Truck Driver','Active','2026-06-01'),  -- medical expired
 ('EMP-9001','Maintenance Tech',1,6,'Senior Technician','Active',NULL);

-- Default admin user. Password: Admin@123  (CHANGE ON FIRST LOGIN)
INSERT INTO Users (Username, Email, PasswordHash, RoleId, EmployeeId, IsActive)
SELECT 'admin','admin@company.com',
       '$2a$11$N9qo8uLOickgx2ZMRZoMyeIjZAgcfl7p92ldGxad68LJZdL17lhWy',
       RoleId, 1, 1 FROM Roles WHERE RoleName='SuperAdmin';

-- RFID cards
INSERT INTO RfidCards (CardUid, EmployeeId, CardType, Status) VALUES
 ('04:A3:7F:19',2,'Permanent','Active'),
 ('04:B1:22:C8',3,'Permanent','Active'),
 ('04:C9:0E:51',4,'Permanent','Active'),
 ('04:D4:66:2A',5,'Permanent','Inactive'),   -- deactivated card demo
 ('04:E8:19:B3',6,'Permanent','Active'),
 ('04:F2:3C:77',7,'Permanent','Active');

-- Equipment (reader IDs must match firmware READER_ID)
INSERT INTO Equipment (EquipmentCode, EquipmentName, EquipmentTypeId, Manufacturer, Model, ModelYear,
                       LocationId, DepartmentId, Status, OperatingHours, FuelType,
                       MaintenanceIntervalHours, MaintenanceIntervalDays, NextMaintenanceDate,
                       NextMaintenanceHours, MaintenanceLocked, RfidReaderId) VALUES
 ('EQ-0001','Forklift FL-12',1,'Toyota','8FD30',2021,1,1,'Available',4210,'Diesel',500,180,'2026-08-02',4500,0,'RDR-A1'),
 ('EQ-0002','Excavator EX-04',2,'Caterpillar','320GC',2020,2,2,'Available',6890,'Diesel',500,180,'2026-07-25',7000,0,'RDR-B2'),
 ('EQ-0003','Mobile Crane CR-02',3,'Liebherr','LTM 1060',2019,1,3,'Available',3120,'Diesel',250,90,'2026-09-10',3300,0,'RDR-C1'),
 ('EQ-0004','Wheel Loader WL-07',4,'Komatsu','WA320',2022,2,4,'UnderMaintenance',5540,'Diesel',500,180,'2026-07-17',5700,0,'RDR-D3'),
 ('EQ-0005','Dump Truck DT-11',5,'Volvo','A30G',2018,2,5,'Available',8875,'Diesel',500,180,'2026-07-05',8800,1,'RDR-E1'), -- locked: overdue
 ('EQ-0006','Telehandler TH-03',6,'JCB','540-170',2023,1,1,'Available',2310,'Diesel',500,180,'2026-08-20',2600,0,'RDR-F2');

-- Trainings (employee 4 has an EXPIRED crane certificate -> denial demo)
INSERT INTO EmployeeTrainings (EmployeeId, CourseId, TrainingDate, ExpiryDate) VALUES
 (2,1,'2025-02-01','2027-02-01'),(2,6,'2025-02-01','2027-02-01'),(2,7,'2026-01-10','2027-01-10'),
 (3,2,'2025-03-01','2027-03-01'),(3,7,'2026-01-10','2027-01-10'),
 (4,3,'2024-01-01','2025-01-01'),(4,7,'2026-01-10','2027-01-10'),
 (5,4,'2025-05-01','2027-05-01'),(5,7,'2026-01-10','2027-01-10'),
 (6,5,'2024-06-01','2027-06-01'),(6,7,'2026-01-10','2027-01-10');

-- Licenses
INSERT INTO EmployeeLicenses (EmployeeId, LicenseTypeId, LicenseNumber, IssueDate, ExpiryDate, IssuingAuthority) VALUES
 (2,1,'FL-88231','2024-01-10','2028-01-10','Saudi Traffic Dept'),
 (3,2,'HE-44120','2024-02-15','2028-02-15','Saudi Traffic Dept'),
 (4,3,'CR-90112','2024-03-20','2027-03-20','Lifting Authority'),
 (5,2,'HE-51009','2024-04-05','2028-04-05','Saudi Traffic Dept'),
 (6,4,'HV-22740','2023-11-01','2027-11-01','Saudi Traffic Dept');

-- Authorizations
INSERT INTO Authorizations (EmployeeId, EquipmentId, AuthType, GrantedByUserId, IsActive) VALUES
 (2,1,'Permanent',1,1),(2,6,'Permanent',1,1),
 (3,2,'Permanent',1,1),(4,3,'Permanent',1,1),
 (5,4,'Permanent',1,1),(6,5,'Permanent',1,1);
-- Employee 7 has NO authorization -> "NotAuthorized" demo

-- Maintenance orders
INSERT INTO MaintenanceOrders (EquipmentId, MaintenanceType, ScheduledDate, Priority, TechnicianId, Status) VALUES
 (5,'Preventive','2026-07-05','Critical',7,'Overdue'),
 (4,'Corrective','2026-07-17','High',7,'InProgress'),
 (2,'Preventive','2026-07-25','Medium',7,'Scheduled'),
 (1,'Inspection','2026-08-02','Low',7,'Scheduled'),
 (6,'Preventive','2026-08-20','Medium',7,'Scheduled');
GO

/* Demo matrix (after seeding):
   04:A3:7F:19 on RDR-A1  -> GRANTED
   04:C9:0E:51 on RDR-C1  -> DENIED  TrainingMissingOrExpired
   04:D4:66:2A on RDR-D3  -> DENIED  CardInactiveOrExpired
   04:E8:19:B3 on RDR-E1  -> DENIED  MedicalExpired
   04:F2:3C:77 on RDR-A1  -> DENIED  NotAuthorized
*/
