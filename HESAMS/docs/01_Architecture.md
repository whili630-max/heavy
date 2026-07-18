# HESAMS — System Architecture & Delivery Roadmap (Phase 1)

## 1. System Architecture

```
 ┌────────────────────────────  FIELD LAYER  ────────────────────────────┐
 │  ESP32 + Industrial RFID Reader + Relay  (one unit per machine)       │
 │  • Reads card UID → POST /api/access/validate (HTTPS, device token)   │
 │  • Local whitelist cache for offline fallback (sync every 5 min)      │
 │  • Relay closes ignition circuit only on "Granted"                    │
 └───────────────────────────────────┬───────────────────────────────────┘
                                     │ REST / MQTT (TLS)
 ┌───────────────────────────────────▼───────────────────────────────────┐
 │                    ASP.NET Core Web API (Azure / IIS / Docker)        │
 │  AuthN: JWT + refresh tokens + optional MFA                           │
 │  AuthZ: RBAC (role → permission codes)                                │
 │  Core services:                                                       │
 │   • Access Validation Engine (the 9-gate interlock chain)             │
 │   • Authorization Engine (machine / type / shift / location scopes)   │
 │   • Maintenance Scheduler + Lock service (Hangfire background jobs)   │
 │   • Notification service (Email, SMS, Push, Teams, WhatsApp)          │
 │   • Audit interceptor (old/new value JSON on every write)             │
 │  Docs: Swagger / OpenAPI                                              │
 └───────────────────────────────────┬───────────────────────────────────┘
                                     │ EF Core
 ┌───────────────────────────────────▼───────────────────────────────────┐
 │       Microsoft SQL Server (3NF — see HESAMS_Database_Schema.sql)     │
 └───────────────────────────────────┬───────────────────────────────────┘
                                     │
 ┌───────────────────────────────────▼───────────────────────────────────┐
 │   Next.js + TypeScript + Tailwind frontend  •  Power BI (analytics)   │
 └───────────────────────────────────────────────────────────────────────┘
```

## 2. Access Validation Engine (the interlock chain)

Executed server-side in a single transaction, in this order. First failure
stops the chain, returns `Denied` + reason, logs the event, and notifies the
supervisor:

1. Card exists & status = Active & not expired
2. Employee exists & status = Active
3. Medical clearance not expired (skipped if not required)
4. All required trainings for the equipment type are valid
5. Required license type is valid
6. Active authorization matching machine/type + shift + location + validity window
7. Equipment status = Available (not InUse / OutOfService)
8. Equipment not under maintenance and `MaintenanceLocked = 0`
9. → Grant, open relay, create AccessEvent + UsageSession, set status = InUse

Target response time: < 500 ms; ESP32 falls back to local cache when offline.

## 3. Maintenance automation

- Hangfire recurring job (hourly): compares `NextMaintenanceDate` /
  `NextMaintenanceHours` against today and current `OperatingHours`.
- Generates reminders at T-30 / T-14 / T-7 / T-1 / due / overdue via the
  Notification service.
- On overdue past threshold → sets `MaintenanceLocked = 1` (hard interlock);
  only MaintenanceManager or SuperAdmin can clear it after a completed
  MaintenanceOrder is recorded.

## 4. Security baseline

HTTPS everywhere • JWT (15 min) + refresh rotation • bcrypt password hashing +
policy (length, history, lockout) • per-device tokens for ESP32 units •
column-level encryption for sensitive PII • session timeout • full audit log
with IP + old/new values • optional TOTP MFA.

## 5. Delivery roadmap

| Phase | Scope | Output |
|-------|-------|--------|
| 1 ✅ | DB schema (3NF), architecture, interactive UI prototype | this package |
| 2 | SRS (functional + non-functional), use-case & ER diagrams | documents |
| 3 | ASP.NET Core API: auth, RBAC, employees, RFID, equipment | source code + Swagger |
| 4 | Validation engine + authorization engine + audit interceptor | source code + tests |
| 5 | Maintenance module + scheduler + notification channels | source code |
| 6 | Training, licenses, incidents, reports (PDF/Excel/CSV) | source code |
| 7 | Next.js production frontend (from the Phase-1 prototype) | source code |
| 8 | ESP32 firmware + relay wiring guide + offline cache | firmware + guide |
| 9 | Deployment (Docker/Azure), testing plan, user & admin manuals | docs |

Each phase is deliverable in this chat — say which phase to run next.
