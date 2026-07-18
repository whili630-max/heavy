# HESAMS — System Maintenance Manual

For the team that keeps HESAMS itself running.

## Architecture recap
Field (ESP32) → API (.NET 8) → SQL Server. The web client and Power BI are read consumers. The only component in the safety path is the API's `AccessValidationService`; treat changes to it as safety-critical.

## Code layout
```
backend/HESAMS.Api
  Models/Entities.cs             all EF entities
  Data/HesamsDbContext.cs        mappings, indexes
  Services/
    AccessValidationService.cs   the interlock chain  ← safety-critical
    MaintenanceSchedulerService.cs hourly background job
    NotificationService.cs       multi-channel dispatch
    TokenService.cs              JWT + refresh rotation
    AuditService.cs              audit writes
  Controllers/                   one per module
frontend/                        Next.js client
firmware/esp32_rfid_gate/        gate unit
database/                        schema + seed
```

## Changing the interlock
1. Add the gate to `AccessValidationService.ValidateAsync` **in the correct order** — cheap checks first, and never after the unlock decision.
2. Add its denial reason to the documented list.
3. Add a passing and a failing unit test (UT series).
4. Update the user manual's refusal table — operators need to understand the new reason.
5. Never add a bypass. If a business case demands an override, model it as a time-boxed temporary authorization with an audit trail, not a skip.

## Database migrations
```bash
dotnet ef migrations add <Name>
dotnet ef database update
```
Review the generated SQL before applying to production. Never drop a column from `AccessEvents` or `AuditLog` — archive instead.

## Data growth
`AccessEvents`, `UsageSessions`, and `AuditLog` grow fastest. At 1,000 machines with ~20 scans/day each, expect ~7M `AccessEvents` rows a year. Partition by `EventAt` (monthly) and archive partitions older than your retention period to a warehouse. Keep the filtered index on denied events — it's what the safety dashboard uses.

## Background job
`MaintenanceSchedulerService` runs hourly in-process. On multi-instance deployments, run it on a single instance (or move it to Hangfire with a distributed lock) so reminders aren't duplicated. It's idempotent for locks but not for notifications.

## Monitoring
| Signal | Threshold | Meaning |
|--------|-----------|---------|
| `/access/validate` p95 | > 500 ms | Index or connection pool problem |
| Gate unit last-seen | > 15 min | Unit down — the machine is now cache-only |
| Denial rate | sudden spike | Expired batch of certificates, or a config error |
| Notification Failed count | > 0 | SMTP/gateway problem |
| Locked equipment count | rising | Maintenance backlog |
| Failed logins | spike | Possible credential attack |

## Firmware updates
Test on a bench unit first. The relay must default LOW on boot — verify this after every firmware change, before deploying to a machine. Roll out to one machine, observe for a shift, then fleet-wide.

## Backup and DR
Nightly full + 15-min log backups; geo-redundant storage. RPO 15 min, RTO 4 h. Quarterly restore drill: restore to a staging server, point a bench gate unit at it, run one grant and one deny.

## Known constraints
- One active card per employee (business rule, enforced in the controller not the schema).
- Offline cache holds 64 UIDs per unit; larger crews need a bigger `MAX_CACHE` or an SD card.
- `Equipment.OperatingHours` is a cached aggregate; if you edit it manually, the hour-based maintenance trigger shifts with it.
