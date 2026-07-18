# HESAMS — API Reference

Base URL: `https://hesams.company.com/api`
Interactive docs: `/swagger` (generated from the same source).

**Auth:** all endpoints require `Authorization: Bearer <access token>` unless marked *(device)*, which use `X-Device-Token` instead.
**Errors:** `{ "error": "message" }` with 400 / 401 / 403 / 404 / 409.
**Paging:** `?page=1&pageSize=25` → `{ total, page, pageSize, items }`.

## Authentication
| Method | Path | Permission | Description |
|--------|------|-----------|-------------|
| POST | `/auth/login` | — | `{username,password}` → `{access,refresh,role}`. Locks 15 min after 5 failures. |
| POST | `/auth/refresh` | — | Rotates the refresh token. |
| POST | `/auth/logout` | — | Revokes the refresh token. |

## Access control (gate units)
| Method | Path | Permission | Description |
|--------|------|-----------|-------------|
| POST | `/access/validate` | *(device)* | `{readerId,cardUid}` → `{unlock,reason,eventId,trace}`. Runs the eight gates. |
| POST | `/access/end-session` | *(device)* | `{readerId,hoursAdded,fuelConsumed}` closes the session, returns the machine to Available. |
| GET | `/access/whitelist?readerId=` | *(device)* | Pre-approved card UIDs for the offline cache. |

## Employees
| Method | Path | Permission |
|--------|------|-----------|
| GET | `/employees?search=&status=&departmentId=` | `employees.read` |
| GET | `/employees/{id}` | `employees.read` |
| POST | `/employees` | `employees.write` |
| PUT | `/employees/{id}` | `employees.write` |
| DELETE | `/employees/{id}` | `users.admin` — soft delete, deactivates cards |

## RFID cards
| Method | Path | Permission |
|--------|------|-----------|
| GET | `/rfid-cards?status=&employeeId=` | `rfid.read` |
| GET | `/rfid-cards/{id}/history` | `rfid.read` |
| POST | `/rfid-cards/issue` | `rfid.write` — 409 on duplicate active UID |
| POST | `/rfid-cards/{id}/deactivate` | `rfid.write` |
| POST | `/rfid-cards/{id}/replace` | `rfid.write` — marks old Lost, issues new |

## Equipment
| Method | Path | Permission |
|--------|------|-----------|
| GET | `/equipment?search=&status=&typeId=` | `equipment.read` |
| GET | `/equipment/{id}` | `equipment.read` |
| GET | `/equipment/{id}/usage?from=&to=` | `equipment.read` |
| POST | `/equipment` | `equipment.write` |
| PUT | `/equipment/{id}` | `equipment.write` |
| POST | `/equipment/{id}/unlock` | `maintenance.unlock` — 409 unless a Completed order exists within 7 days |

## Authorizations
| Method | Path | Permission |
|--------|------|-----------|
| GET | `/authorizations?employeeId=&equipmentId=&activeOnly=` | `authorizations.read` |
| POST | `/authorizations` | `authorizations.write` — needs EquipmentId or EquipmentTypeId; Temporary needs ValidTo |
| DELETE | `/authorizations/{id}` | `authorizations.write` — revoke, effective next scan |

## Training & licenses
| Method | Path | Permission |
|--------|------|-----------|
| GET/POST | `/trainings/courses` | `training.read` / `training.write` |
| POST | `/trainings/records` | `training.write` — expiry auto-computed from course validity |
| GET | `/trainings/matrix?departmentId=` | `training.read` |
| GET | `/trainings/expiring?days=30` | `training.read` |
| GET/POST | `/licenses` | `licenses.read` / `licenses.write` |
| GET | `/licenses/expiring?days=30` | `licenses.read` |

## Maintenance
| Method | Path | Permission |
|--------|------|-----------|
| GET | `/maintenance?status=&equipmentId=&type=` | `maintenance.read` |
| POST | `/maintenance` | `maintenance.write` |
| POST | `/maintenance/{id}/complete` | `maintenance.write` — cost defaults to sum of parts |
| POST | `/maintenance/{id}/parts` | `maintenance.write` |

## Incidents
| Method | Path | Permission |
|--------|------|-----------|
| GET | `/incidents?type=&status=&from=&to=` | `incidents.read` |
| POST | `/incidents` | `incidents.write` |
| POST | `/incidents/{id}/investigate` | `incidents.write` → PendingApproval |
| POST | `/incidents/{id}/approve` | `incidents.approve` → Closed |

## Dashboard
| Method | Path | Permission |
|--------|------|-----------|
| GET | `/dashboard/kpis` | `dashboard.read` — all executive tiles in one call |
| GET | `/dashboard/live-events?take=20` | `dashboard.read` |
| GET | `/dashboard/daily-usage?days=7` | `dashboard.read` |
| GET | `/dashboard/safety-kpi?days=30` | `dashboard.read` — compliance rate + denial breakdown |

## Reports
`GET /reports/{key}?format=json|csv|xlsx|pdf&from=&to=` — permission `reports.read`.

Keys: `employee-qualification`, `rfid-activity`, `equipment-utilization`, `operator-history`, `maintenance-schedule`, `maintenance-cost`, `license-expiry`, `training-expiry`, `unauthorized-attempts`, `downtime`, `incidents`, `safety-kpi`.

## Audit & notifications
| Method | Path | Permission |
|--------|------|-----------|
| GET | `/audit?action=&entity=&userId=&from=&to=` | `audit.read` |
| GET | `/notifications?unreadOnly=` | any authenticated |
| POST | `/notifications/{id}/read` | any authenticated |

## Permission codes
`employees.read/write` · `rfid.read/write` · `equipment.read/write/unlock` · `training.read/write` · `licenses.read/write` · `authorizations.read/write` · `maintenance.read/write/unlock` · `incidents.read/write/approve` · `reports.read/export` · `audit.read` · `users.admin` · `dashboard.read`

## Sample: successful scan
```json
POST /api/access/validate
{ "readerId": "RDR-A1", "cardUid": "04:A3:7F:19" }

200 OK
{
  "unlock": true,
  "reason": null,
  "eventId": 88213,
  "trace": [
    { "gate": "CardActive", "passed": true, "reason": null },
    { "gate": "EmployeeActive", "passed": true, "reason": null },
    { "gate": "MedicalValid", "passed": true, "reason": null },
    { "gate": "TrainingValid", "passed": true, "reason": null },
    { "gate": "LicenseValid", "passed": true, "reason": null },
    { "gate": "Authorized", "passed": true, "reason": null },
    { "gate": "EquipmentAvailable", "passed": true, "reason": null },
    { "gate": "MaintenanceClear", "passed": true, "reason": null }
  ]
}
```

## Sample: denied scan
```json
200 OK
{
  "unlock": false,
  "reason": "TrainingMissingOrExpired",
  "eventId": 88214,
  "trace": [
    { "gate": "CardActive", "passed": true, "reason": null },
    { "gate": "EmployeeActive", "passed": true, "reason": null },
    { "gate": "MedicalValid", "passed": true, "reason": null },
    { "gate": "TrainingValid", "passed": false, "reason": "TrainingMissingOrExpired" }
  ]
}
```
The chain stops at the first failure — later gates are never evaluated, and the supervisor is notified automatically.
