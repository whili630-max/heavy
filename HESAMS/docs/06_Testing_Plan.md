# HESAMS — Testing Plan

## 1. Scope & strategy
Four levels: unit (validation logic), integration (API + database), hardware-in-the-loop (gate units), and acceptance (role-based UAT). The interlock chain is the safety-critical path and carries the highest coverage bar: **every gate must have a passing and a failing test.**

## 2. Unit tests — the interlock chain

| ID | Scenario | Expected |
|----|----------|----------|
| UT-01 | All gates pass | `unlock = true`, session opened, machine InUse |
| UT-02 | Card status Inactive | Denied `CardInactiveOrExpired`; chain stops at gate 1 |
| UT-03 | Card expired (`ExpiresAt` past) | Denied `CardInactiveOrExpired` |
| UT-04 | Unknown card UID | Denied `CardNotFound` |
| UT-05 | Employee Terminated | Denied `EmployeeInactive` |
| UT-06 | Medical clearance expired yesterday | Denied `MedicalExpired` |
| UT-07 | Medical clearance NULL (not required) | Gate passes |
| UT-08 | Required training expired | Denied `TrainingMissingOrExpired` |
| UT-09 | Training never taken | Denied `TrainingMissingOrExpired` |
| UT-10 | Training with NULL expiry | Gate passes |
| UT-11 | License expired | Denied `LicenseMissingOrExpired` |
| UT-12 | Authorization for a different machine | Denied `NotAuthorized` |
| UT-13 | Type-scoped authorization, matching class | Granted |
| UT-14 | Temporary authorization, `ValidTo` past | Denied `NotAuthorized` |
| UT-15 | Shift-restricted auth, scan inside shift | Granted |
| UT-16 | Shift-restricted auth, scan outside shift | Denied `NotAuthorized` |
| UT-17 | Overnight shift (22:00–06:00), scan at 02:00 | Granted — wrap-around logic |
| UT-18 | Location-restricted auth, machine elsewhere | Denied `NotAuthorized` |
| UT-19 | Machine already InUse | Denied `EquipmentInUse` |
| UT-20 | `MaintenanceLocked = true` | Denied `MaintenanceLock` |
| UT-21 | Machine UnderMaintenance | Denied `MaintenanceLock` |
| UT-22 | Unknown reader ID | Denied `UnknownReader` |
| UT-23 | Any denial | Event persisted, supervisor notified, relay stays locked |
| UT-24 | Chain stops at first failure | Trace contains no gates after the failure |

## 3. Integration tests

| ID | Scenario | Expected |
|----|----------|----------|
| IT-01 | Login with valid credentials | 200 + JWT carrying `perm` claims |
| IT-02 | 5 wrong passwords | Account locked 15 min |
| IT-03 | Operator calls `PUT /equipment/{id}` | 403 |
| IT-04 | Refresh token reused after rotation | 401 |
| IT-05 | Issue duplicate active card UID | 409 |
| IT-06 | Replace lost card | Old Lost, new Active, two history rows |
| IT-07 | Terminate employee | All cards Inactive |
| IT-08 | Unlock without a completed order | 409 |
| IT-09 | Complete order then unlock | Lock cleared, schedule rolled forward |
| IT-10 | Temporary auth without `ValidTo` | 400 |
| IT-11 | Approve an incident that was never investigated | 409 |
| IT-12 | Any write endpoint | Audit row with old/new JSON and IP |
| IT-13 | Report export, all 12 keys × 4 formats | 200, correct content type |
| IT-14 | End session | Hours added to machine, status Available |

## 4. Scheduler tests

| ID | Scenario | Expected |
|----|----------|----------|
| SC-01 | Machine due in 30/14/7/1 days | One reminder per offset, no duplicates |
| SC-02 | Machine due today | "Due today" notification |
| SC-03 | Machine one day overdue | `MaintenanceLocked = 1` + lock notification |
| SC-04 | Operating hours reach threshold | `MaintenanceLocked = 1` |
| SC-05 | Already-locked machine, next cycle | No duplicate notification |
| SC-06 | Scheduled order past its date | Status → Overdue |

## 5. Hardware-in-the-loop

| ID | Scenario | Expected |
|----|----------|----------|
| HW-01 | Valid card | Green LED, one beep, relay closes < 500 ms |
| HW-02 | Invalid card | Red LED, three beeps, relay stays open |
| HW-03 | Power cycle while running | Relay opens (fail-safe), stays open until a new scan |
| HW-04 | Wi-Fi lost, cached card | Granted via cache, logged as offline |
| HW-05 | Wi-Fi lost, uncached card | Denied |
| HW-06 | Network restored | Whitelist syncs within 5 min |
| HW-07 | Second card while running | Refused, "machine in use" |
| HW-08 | Same card tapped again | Session closes, hours posted |
| HW-09 | Server returns 500 | Relay stays locked (fail-safe, not fail-open) |
| HW-10 | 24 h offline soak | Cache still valid, no memory leak |

## 6. Performance

| ID | Target |
|----|--------|
| PF-01 | `/access/validate` p95 < 500 ms with 10k employees / 1k machines |
| PF-02 | 500 concurrent gate units, no errors |
| PF-03 | Dashboard KPIs < 2 s |
| PF-04 | 5,000-row PDF export < 10 s |
| PF-05 | 1M `AccessEvents` rows — RFID activity report still < 5 s |

## 7. Security

| ID | Scenario | Expected |
|----|----------|----------|
| SE-01 | SQL injection in search params | Parameterized, no injection (EF Core) |
| SE-02 | Expired JWT | 401 |
| SE-03 | Tampered JWT signature | 401 |
| SE-04 | Missing device token on `/access/validate` | 401 |
| SE-05 | `perm` claim removed from token | 403 |
| SE-06 | HTTP request | 307 → HTTPS |
| SE-07 | Attempt to update the audit log via API | No endpoint exists; DB grants deny it |
| SE-08 | Password stored | bcrypt hash, never plaintext |

## 8. Acceptance (UAT) — by role

| ID | Role | Scenario |
|----|------|----------|
| UA-01 | Operator | Tap card, start machine, tap out; usage appears in history |
| UA-02 | Supervisor | Receive a denial alert within one minute |
| UA-03 | Training Coordinator | Add a certificate; the matrix updates; the operator can now start |
| UA-04 | Maintenance Manager | Complete an order and clear a lock |
| UA-05 | Safety Manager | Report → investigate → approve an incident |
| UA-06 | Auditor | Export unauthorized attempts to PDF; export is itself audited |
| UA-07 | HR Administrator | Terminate an employee; their card stops working immediately |
| UA-08 | Guest | Read-only dashboard; no write buttons rendered |

## 9. Exit criteria
- 100% of UT-01…UT-24 passing (safety-critical, no exceptions)
- ≥ 85% line coverage on `AccessValidationService` and `MaintenanceSchedulerService`
- All HW tests passing on production hardware, including every fail-safe case
- Zero high or critical findings open from the security pass
- All UAT scenarios signed off by the corresponding role owner
