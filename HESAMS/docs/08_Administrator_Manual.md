# HESAMS — Administrator Manual

## Roles and permissions
Ten roles ship as system roles. Permissions are codes (`equipment.write`, `maintenance.unlock`) mapped to roles in `RolePermissions`; the API enforces one code per endpoint and the token carries them as `perm` claims. To change what a role can do, edit the mapping — never edit the code check.

Recommended baseline:

| Role | Codes |
|------|-------|
| Super Admin | all |
| Safety Manager | `incidents.*`, `reports.*`, `dashboard.read`, `audit.read`, read on everything |
| Maintenance Manager | `maintenance.*`, `equipment.read/write/unlock`, `dashboard.read`, `reports.read` |
| Operations Manager | `equipment.read/write`, `authorizations.*`, `dashboard.read`, `reports.read` |
| Supervisor | `employees.read`, `equipment.read`, `incidents.write`, `dashboard.read` |
| Equipment Operator | own record read, `dashboard.read` |
| HR Administrator | `employees.*`, `rfid.*`, `licenses.read` |
| Training Coordinator | `training.*`, `employees.read` |
| Auditor | `audit.read`, `reports.*`, read everywhere |
| Guest | `dashboard.read` |

**Separation of duty:** the role that completes maintenance should not be the role that clears the lock. Keep `maintenance.unlock` on managers only.

## Onboarding a new machine
1. Create the equipment record; set type, intervals (days and/or hours), location, supervisor.
2. Assign a unique `RfidReaderId`.
3. Flash the gate unit with the same reader ID and a fresh device token.
4. Map required trainings and licenses to the equipment **type** (not the machine) — this is what the interlock checks.
5. Grant authorizations.
6. Test with a known-good card and a known-bad card before releasing the machine to work.

## Onboarding a new operator
1. HR creates the employee, sets department, supervisor, medical expiry.
2. Training coordinator records certificates.
3. HR records the license.
4. HR issues the card.
5. Operations grants authorization (machine or class; add shift/location limits if needed).
6. Operator taps to verify.

Miss any step and the interlock will refuse them — the denial reason tells you which step is missing.

## Device token management
Each gate unit authenticates with `X-Device-Token`. Rotate quarterly and immediately if a unit is stolen or a machine is sold. Tokens are per-device: revoking one doesn't affect the fleet. Rotation doesn't require reflashing if you provision tokens over the air; otherwise reflash the affected unit.

## Maintenance locks
The scheduler runs hourly and locks any machine past its due date or hour threshold. Only `maintenance.unlock` clears it, and only with a Completed order dated within seven days. If a lock is wrong (bad interval data), fix the interval and complete a corrective order — don't disable the scheduler.

## Notification channels
In-app and email work out of the box (SMTP settings in `appsettings.json`). SMS, push, Teams, and WhatsApp plug in by implementing `INotificationService.QueueAsync` against the provider gateway and registering it in DI. Every message is persisted regardless of channel, so failures are visible in the Notifications table.

## Audit log
Append-only. Revoke UPDATE and DELETE on `AuditLog` from the application's SQL account — the API has no endpoint to modify it, but the database should enforce this too. Retain at least as long as your regulator requires; archive to cold storage rather than deleting.

## Backups and recovery
Nightly full, 15-minute log backups. Test a restore quarterly. Recovery order: database → API → web → verify one gate unit end to end.

## Routine tasks
| Cadence | Task |
|---------|------|
| Daily | Review denied attempts; investigate repeats from the same operator |
| Weekly | Review expiring trainings and licenses (30-day window) |
| Weekly | Confirm every gate unit reported in — a silent unit is a blind machine |
| Monthly | Review temporary authorizations still active |
| Quarterly | Rotate device tokens; test a backup restore; review role mappings |
| Annually | Review the interlock rules with the safety committee |

## Troubleshooting
| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| Every card refused with "unknown reader" | `RfidReaderId` mismatch | Align the record with the flashed value |
| One operator always refused | Missing training/license/authorization | Read the denial reason; it names the gate |
| Machine won't unlock after maintenance | No Completed order within 7 days | Complete the order, then unlock |
| Reader offline | Wi-Fi coverage or power | Check the unit; it should still grant cached operators |
| No email notifications | SMTP settings or relay block | Check Notifications table for Failed status |
| Slow validation | Missing indexes after a restore | Rebuild the indexes in the schema script |
