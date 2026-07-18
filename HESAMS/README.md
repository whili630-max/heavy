# HESAMS — Heavy Equipment Safety & Access Management System

RFID-gated ignition interlock for heavy equipment. A card only starts a machine
when the operator is active, medically cleared, trained, licensed, authorized for
that machine, and the machine itself is free and not overdue for maintenance.
Every attempt — granted or refused — is logged with the full reasoning trace.

## What's in the box

```
HESAMS/
├── docs/
│   ├── 01_Architecture.md                     system architecture + delivery roadmap
│   ├── 02_SRS.md                              SRS: functional, non-functional, use cases
│   ├── 03_ER_Diagram_and_Data_Dictionary.md   ER model (Mermaid), 3NF notes, data dictionary
│   ├── 04_API_Documentation.md                full REST reference
│   ├── 05_Deployment_Guide.md                 SQL / .NET / Docker / Azure / IIS / ESP32
│   ├── 06_Testing_Plan.md                     unit, integration, hardware, security, UAT
│   ├── 07_User_Manual.md                      operators, HR, training, maintenance, safety
│   ├── 08_Administrator_Manual.md             roles, onboarding, tokens, troubleshooting
│   ├── 09_Maintenance_Manual.md               for the team running HESAMS itself
│   └── 10_Roadmap.md                          future enhancements
├── database/
│   ├── HESAMS_Database_Schema.sql             SQL Server, 3NF, indexes, constraints
│   └── HESAMS_Seed_Data.sql                   roles, permissions, demo fleet + crew
├── backend/
│   ├── HESAMS.Api/                            ASP.NET Core 8 Web API
│   │   ├── Models/Entities.cs                 all EF Core entities
│   │   ├── Data/HesamsDbContext.cs            mappings, indexes
│   │   ├── Services/
│   │   │   ├── AccessValidationService.cs     ← the 8-gate interlock (safety-critical)
│   │   │   ├── MaintenanceSchedulerService.cs hourly reminders + hard lock
│   │   │   ├── NotificationService.cs         in-app, email, pluggable SMS/Teams/WhatsApp
│   │   │   ├── TokenService.cs                JWT + rotating refresh
│   │   │   └── AuditService.cs                old/new value audit writes
│   │   ├── Controllers/                       14 controllers, one per module
│   │   └── Program.cs                         JWT, RBAC policies, Swagger, CORS
│   └── HESAMS.Tests/                          xUnit — every gate, pass and fail
├── frontend/
│   └── HESAMS_Prototype.jsx                   React UI: dashboard + live scan simulator
├── firmware/esp32_rfid_gate/
│   └── esp32_rfid_gate.ino                    ESP32 + MFRC522 + relay, offline cache
├── deploy/Dockerfile.api
├── docker-compose.yml
└── .env.example
```

## Quick start

```bash
# 1. Database
sqlcmd -S localhost -i database/HESAMS_Database_Schema.sql
sqlcmd -S localhost -d HESAMS -i database/HESAMS_Seed_Data.sql

# 2. API
cd backend/HESAMS.Api && dotnet restore && dotnet run
# Swagger at https://localhost:5001/swagger

# 3. Tests
cd backend/HESAMS.Tests && dotnet test

# Or everything at once:
cp .env.example .env   # set SA_PASSWORD and JWT_KEY first
docker compose up -d --build
```

Default login: `admin` / `Admin@123` — **change it on first login.**

## The interlock chain

Every scan runs these gates in order. The first failure stops the chain, logs the
attempt with its reason, notifies the supervisor, and leaves the relay locked.

| # | Gate | Denial reason |
|---|------|---------------|
| 0 | Reader is registered to a machine | `UnknownReader` |
| 1 | Card exists, active, not expired | `CardNotFound` / `CardInactiveOrExpired` |
| 2 | Employee active | `EmployeeInactive` |
| 3 | Medical clearance valid (skipped if not required) | `MedicalExpired` |
| 4 | All trainings required by the machine's type are valid | `TrainingMissingOrExpired` |
| 5 | Required license valid | `LicenseMissingOrExpired` |
| 6 | Authorization matches machine/class, window, shift, location | `NotAuthorized` |
| 7 | Machine available | `EquipmentInUse` / `EquipmentUnavailable` |
| 8 | Maintenance interlock clear | `MaintenanceLock` |

## Try the demo data

Seeded to exercise each failure path:

| Card | Reader | Result |
|------|--------|--------|
| `04:A3:7F:19` | `RDR-A1` | Granted |
| `04:C9:0E:51` | `RDR-C1` | Denied — training expired |
| `04:D4:66:2A` | `RDR-D3` | Denied — card deactivated |
| `04:E8:19:B3` | `RDR-E1` | Denied — medical expired |
| `04:F2:3C:77` | `RDR-A1` | Denied — not authorized |
| any | `RDR-E1` | Denied — maintenance lock (DT-11 is overdue) |

```bash
curl -k -X POST https://localhost:5001/api/access/validate \
  -H "Content-Type: application/json" \
  -d '{"readerId":"RDR-A1","cardUid":"04:A3:7F:19"}'
```

## Safety design rules

These are deliberate and shouldn't be "optimized" away:

1. **Fail-safe, never fail-open.** Relay defaults locked on boot, reset, power loss,
   server error, or timeout. An unqualified operator on a running machine is worse
   than a stopped machine.
2. **No manual override at the gate.** Exceptions are modeled as time-boxed
   temporary authorizations with an audit trail, not a bypass button.
3. **Separation of duty.** Completing maintenance and clearing the lock are different
   permissions. The system refuses to unlock without a completed order in 7 days.
4. **The relay controls the starter-enable circuit only** — never a safety stop circuit.
5. **Every decision is explainable.** The full gate trace is stored as JSON on the event,
   so any refusal can be reconstructed months later.

## Stack

React / Next.js / TypeScript / Tailwind · ASP.NET Core 8 · SQL Server (3NF) · JWT +
permission-code RBAC · ESP32 + MFRC522 + relay · QuestPDF / ClosedXML exports ·
Power BI · Azure / IIS / Docker.
