# Software Requirements Specification (SRS)
## Heavy Equipment Safety & Access Management System (HESAMS)
**Version 1.0 · July 2026**

---

## 1. Introduction

### 1.1 Purpose
HESAMS prevents unauthorized or unqualified personnel from starting heavy equipment. It enforces qualification, authorization, and maintenance rules at the point of ignition through RFID card authentication, and records every attempt for safety compliance and audit.

### 1.2 Scope
The system covers employee records, RFID card lifecycle, equipment registry, training and license tracking, an authorization engine, a hardware interlock via ESP32 gate units, preventive maintenance with automatic locking, incident management, notifications, reporting, and a full audit trail.

### 1.3 Definitions
| Term | Meaning |
|------|---------|
| Gate unit | ESP32 + RFID reader + relay mounted on a machine |
| Interlock chain | The ordered sequence of validation gates run on every scan |
| Maintenance lock | Hard block preventing operation when maintenance is overdue |
| Authorization | An explicit grant allowing an employee to operate a machine or class |
| Usage session | The period between a granted scan and engine shutdown |

---

## 2. Overall Description

### 2.1 Product perspective
Three-tier system: field layer (gate units) → application layer (ASP.NET Core API) → data layer (SQL Server), with a Next.js web client and Power BI analytics.

### 2.2 User classes
| Role | Primary needs |
|------|---------------|
| Super Admin | Full configuration, user management |
| Safety Manager | Incidents, safety KPIs, denied attempts |
| Maintenance Manager | Schedules, orders, the only role that clears maintenance locks |
| Operations Manager | Utilization, assignments |
| Supervisor | Team activity, denial alerts for their people |
| Equipment Operator | Operates assigned machines; sees own qualifications |
| HR Administrator | Employee records, documents |
| Training Coordinator | Courses, certificates, training matrix |
| Auditor | Read-only access to logs and reports |
| Guest | Read-only dashboard |

### 2.3 Operating environment
Server: Windows Server / Linux, .NET 8, SQL Server 2019+, hosted on Azure App Service, IIS, or Docker. Client: modern browsers, responsive to mobile. Field: 802.11 Wi-Fi or LTE at each machine; gate units tolerate network loss.

### 2.4 Constraints
- The relay controls the **starter-enable circuit only**, never a safety-critical stop circuit.
- Fail-safe default: relay de-energized (locked) on power-up, reset, or fault.
- Validation must complete in under 500 ms at the reader.

### 2.5 Assumptions
Every operator holds one active card; every machine has one gate unit with a unique reader ID; card UIDs are globally unique.

---

## 3. Functional Requirements

### FR-1 Authentication & RBAC
| ID | Requirement | Priority |
|----|-------------|----------|
| FR-1.1 | Users log in with username/password; passwords stored with bcrypt | Must |
| FR-1.2 | JWT access token (15 min) plus rotating refresh token (7 days) | Must |
| FR-1.3 | Account locks for 15 minutes after 5 failed attempts | Must |
| FR-1.4 | Permissions are code-based (`equipment.update`) and mapped to roles | Must |
| FR-1.5 | Optional TOTP multi-factor authentication | Should |
| FR-1.6 | Session timeout on inactivity | Must |

### FR-2 Employee management
| ID | Requirement |
|----|-------------|
| FR-2.1 | Store code, name, photo, department, company, job title, contacts, nationality, status |
| FR-2.2 | Track medical clearance expiry; null means not required |
| FR-2.3 | Store emergency contact and supervisor relationship |
| FR-2.4 | Upload employee documents with expiry dates |
| FR-2.5 | Termination is a soft delete and deactivates all cards automatically |

### FR-3 RFID card management
| ID | Requirement |
|----|-------------|
| FR-3.1 | Issue a card by UID to an employee; reject duplicate active UIDs |
| FR-3.2 | Support permanent, temporary (with expiry), and replacement cards |
| FR-3.3 | Deactivate a card with a recorded reason |
| FR-3.4 | Replace a lost card: mark old as Lost and issue the new one atomically |
| FR-3.5 | Full card assignment history is queryable |

### FR-4 Equipment management
| ID | Requirement |
|----|-------------|
| FR-4.1 | Store code, name, type, manufacturer, model, year, VIN, engine number, location |
| FR-4.2 | Track operating hours, fuel type, current operator, supervisor |
| FR-4.3 | Track purchase, warranty, insurance, inspection dates |
| FR-4.4 | Configure maintenance interval by days and/or operating hours |
| FR-4.5 | Bind exactly one gate unit (`RfidReaderId`) per machine |
| FR-4.6 | Attach images, manuals, and documents |

### FR-5 Training & licenses
| ID | Requirement |
|----|-------------|
| FR-5.1 | Define courses with validity in months; expiry computed automatically |
| FR-5.2 | Map required courses and license types to each equipment type |
| FR-5.3 | Upload certificates and license copies |
| FR-5.4 | Produce a training matrix (employees × courses) with Valid / Expiring / Expired |
| FR-5.5 | Renewal reminders at 30 / 14 / 7 / 1 days before expiry |

### FR-6 Authorization engine
| ID | Requirement |
|----|-------------|
| FR-6.1 | Grant per specific machine or per equipment class |
| FR-6.2 | One employee → many machines; one machine → many employees |
| FR-6.3 | Permanent or temporary (temporary requires an end date) |
| FR-6.4 | Optional restriction by shift, location, and department |
| FR-6.5 | Revocation is immediate and takes effect at the next scan |

### FR-7 Access validation (the interlock chain)
| ID | Requirement |
|----|-------------|
| FR-7.1 | On scan, run gates in order: card active → employee active → medical → training → license → authorization → equipment available → maintenance clear |
| FR-7.2 | First failure stops the chain and returns a specific denial reason |
| FR-7.3 | Unlock the relay only when every gate passes |
| FR-7.4 | Persist every scan with the full gate trace as JSON |
| FR-7.5 | Open a usage session and set the machine to InUse on success |
| FR-7.6 | Notify the supervisor on every denial |
| FR-7.7 | On network loss the gate unit falls back to a cached whitelist synced every 5 minutes |
| FR-7.8 | Tapping the same card again ends the session and logs hours |

### FR-8 Maintenance management
| ID | Requirement |
|----|-------------|
| FR-8.1 | Support preventive, corrective, emergency, predictive, inspection orders |
| FR-8.2 | Store scheduled date, hour target, priority, technician, vendor, parts, cost |
| FR-8.3 | Reminders at 30 / 14 / 7 / 1 days, due today, and overdue |
| FR-8.4 | Trigger a hard lock when a machine passes its due date or hour threshold |
| FR-8.5 | Only `maintenance.unlock` may clear a lock, and only after a completed order within 7 days |
| FR-8.6 | Rolling the schedule forward happens automatically on unlock |

### FR-9 Usage history
| ID | Requirement |
|----|-------------|
| FR-9.1 | Record operator, machine, start, end, duration, hours, fuel, shift, purpose |
| FR-9.2 | Optional GPS coordinates per session |
| FR-9.3 | Operating hours accumulate onto the machine record on session close |

### FR-10 Incidents
| ID | Requirement |
|----|-------------|
| FR-10.1 | Record near miss, accident, unsafe act, unsafe condition |
| FR-10.2 | Workflow: Open → UnderInvestigation → PendingApproval → Closed |
| FR-10.3 | Approval restricted to `incidents.approve` |
| FR-10.4 | Attach evidence files |

### FR-11 Notifications
| ID | Requirement |
|----|-------------|
| FR-11.1 | Channels: in-app, email, SMS, push, Teams, WhatsApp (pluggable) |
| FR-11.2 | Every notification persists with status Pending/Sent/Failed/Read |
| FR-11.3 | Templates with placeholder substitution |
| FR-11.4 | Escalation when a critical alert is unread beyond its threshold |

### FR-12 Reports
Twelve reports — employee qualification, RFID activity, equipment utilization, operator history, maintenance schedule, maintenance cost, license expiry, training expiry, unauthorized attempts, downtime, incidents, safety KPI — each exportable as JSON, CSV, XLSX, and PDF.

### FR-13 Audit log
Every login, logout, scan, authorization change, record update, unlock, and export is recorded with timestamp, user, IP, user agent, and old/new values as JSON. The audit log is append-only.

---

## 4. Non-Functional Requirements

### 4.1 Performance
| ID | Requirement |
|----|-------------|
| NFR-1.1 | Validation endpoint responds in < 500 ms at p95 |
| NFR-1.2 | Dashboard KPI load < 2 s with 10,000 employees and 1,000 machines |
| NFR-1.3 | Support 500 concurrent gate units and 200 concurrent web users |
| NFR-1.4 | Reports of 5,000 rows export in < 10 s |

### 4.2 Reliability & availability
| ID | Requirement |
|----|-------------|
| NFR-2.1 | 99.5% uptime for the API |
| NFR-2.2 | Gate units operate offline for at least 24 hours on cached whitelist |
| NFR-2.3 | Relay defaults to locked on any fault, reset, or power loss |
| NFR-2.4 | Nightly full backup, 15-minute transaction log backups |

### 4.3 Security
| ID | Requirement |
|----|-------------|
| NFR-3.1 | TLS 1.2+ on all traffic; HTTP redirects to HTTPS |
| NFR-3.2 | Passwords bcrypt-hashed with per-user salt |
| NFR-3.3 | Gate units authenticate with a per-device token, rotatable without reflashing |
| NFR-3.4 | Least privilege enforced by permission codes on every endpoint |
| NFR-3.5 | Sensitive PII encrypted at rest (TDE plus column encryption) |
| NFR-3.6 | Audit log immutable to application users |

### 4.4 Usability
| ID | Requirement |
|----|-------------|
| NFR-4.1 | Responsive from 360 px to desktop |
| NFR-4.2 | Dark and light modes |
| NFR-4.3 | WCAG 2.1 AA: contrast, keyboard focus, reduced motion |
| NFR-4.4 | Denial reasons stated in plain language at the machine |
| NFR-4.5 | Arabic and English interface support |

### 4.5 Maintainability & portability
Layered architecture, EF Core migrations, OpenAPI documentation, Docker images for API and web, deployable to Azure, IIS, or on-premise Kubernetes.

---

## 5. Use Cases

### UC-1 Start equipment
**Actor:** Equipment Operator
**Precondition:** Operator holds an active card; machine has a gate unit.
1. Operator taps card on the reader.
2. Gate unit posts reader ID and card UID to the API.
3. System runs the eight gates in order.
4. All pass → system unlocks the relay, opens a usage session, sets the machine InUse.
5. Operator starts the engine.
**Alternate 4a:** Any gate fails → relay stays locked, reason displayed, attempt logged, supervisor notified.
**Alternate 2a:** Network unreachable → gate unit checks its cached whitelist; grants only if the UID is present, otherwise denies.

### UC-2 Issue a replacement card
**Actor:** HR Administrator (`rfid.write`)
1. Administrator opens the employee's card record and selects Replace.
2. Enters the new UID and reason.
3. System marks the old card Lost, issues the replacement, writes two history entries, and logs the audit entry.

### UC-3 Grant temporary authorization
**Actor:** Operations Manager (`authorizations.write`)
1. Manager selects an employee and a machine or class.
2. Chooses Temporary, sets the end date, optionally restricts to a shift or location.
3. System validates that an end date exists and saves the grant.
4. The grant takes effect on the next scan and expires automatically.

### UC-4 Clear a maintenance lock
**Actor:** Maintenance Manager (`maintenance.unlock`)
**Precondition:** The machine is locked; a maintenance order exists.
1. Technician completes the order with cost and parts.
2. Manager requests unlock.
3. System verifies a completed order within 7 days; refuses otherwise.
4. System clears the lock, returns the machine to Available, rolls the next schedule forward, and writes an audit entry.

### UC-5 Investigate an incident
**Actor:** Safety Manager
1. Supervisor reports a near miss with attachments.
2. Investigator records root cause and corrective action → status PendingApproval.
3. Safety Manager approves → status Closed, approver recorded.

### UC-6 Export a compliance report
**Actor:** Auditor (`reports.read`)
1. Auditor selects Unauthorized Attempts, sets a date range, chooses PDF.
2. System generates the document and records the export in the audit log.

---

## 6. Traceability

| Requirement | Implementation |
|-------------|----------------|
| FR-1.x | `AuthController`, `TokenService`, `PermissionCatalog` |
| FR-2.x | `EmployeesController`, `Employee`, `EmployeeDocument` |
| FR-3.x | `RfidCardsController`, `RfidCard`, `CardAssignmentLog` |
| FR-4.x | `EquipmentController`, `Equipment`, `EquipmentFile` |
| FR-5.x | `TrainingsController`, `LicensesController` |
| FR-6.x | `AuthorizationsController`, `EquipmentAuthorization` |
| FR-7.x | `AccessValidationService`, `AccessController`, ESP32 firmware |
| FR-8.x | `MaintenanceController`, `MaintenanceSchedulerService` |
| FR-9.x | `UsageSession`, `AccessValidationService.EndSessionAsync` |
| FR-10.x | `IncidentsController` |
| FR-11.x | `NotificationService`, `NotificationsController` |
| FR-12.x | `ReportsController` |
| FR-13.x | `AuditService`, `AuditController` |
