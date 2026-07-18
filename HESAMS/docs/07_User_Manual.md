# HESAMS — User Manual

## Signing in
Go to the HESAMS address, enter your username and password. Five wrong attempts lock the account for 15 minutes — contact your administrator if that happens. What you see depends on your role; buttons you don't have permission for aren't shown.

## Operators — starting a machine
1. Tap your card on the reader mounted on the machine.
2. **Green light, one beep** — the machine is unlocked. Start the engine.
3. **Red light, three beeps** — the machine stays locked and the screen shows why.
4. When you finish, tap the same card again. This closes your session and records the hours.

### Why a machine refused you
| Message | What it means | What to do |
|---------|---------------|------------|
| Card inactive or expired | Your card is deactivated or past its expiry | Contact HR to reissue |
| Card not found | The card isn't registered | Contact HR |
| Employee inactive | Your record is suspended or terminated | Contact HR |
| Medical expired | Your medical clearance lapsed | Renew and give it to HR |
| Training missing or expired | You lack a valid certificate for this machine type | Contact the training coordinator |
| License missing or expired | Your operating license lapsed | Renew it |
| Not authorized | You have no grant for this machine | Ask your supervisor |
| Equipment in use | Another operator has an open session | Wait or use another machine |
| Maintenance lock | Maintenance is overdue on this machine | Contact the maintenance manager — do not attempt to bypass |

Every refusal is logged and your supervisor is notified automatically. There is no manual override at the machine — this is intentional.

### If the network is down
The reader keeps a whitelist of operators pre-approved for that machine, refreshed every five minutes. If you're on it you can still start the machine; if not, you'll be refused until the connection returns. When in doubt, contact your supervisor rather than waiting at the machine.

## Dashboard
The tiles show fleet totals, active operators, denied attempts, expired licenses and trainings, maintenance due and overdue, and utilization. Charts cover daily usage and monthly utilization. The Live RFID Events list shows the most recent scans with the reason for each refusal. Use the date and department filters to narrow any view.

## Employees (HR)
Search by name or code. Open a record to see cards, trainings, licenses, authorizations, and documents. Set the medical clearance expiry — leave it empty when clearance isn't required for the role. Terminating an employee deactivates every card they hold immediately.

## RFID cards
- **Issue** — enter the UID read from the card and pick the holder. Permanent, temporary (needs an expiry), or replacement.
- **Deactivate** — always give a reason; it's kept in the card history.
- **Replace** — use this for a lost card. It marks the old one Lost and issues the new one in one step.
- **History** — every issue, deactivation, and replacement with who did it and when.

## Equipment
Each card shows status, operating hours, next maintenance, and its reader ID. The reader ID must match what's flashed on the gate unit — a mismatch means the machine will refuse every card with "unknown reader". A striped red bar means the machine is hard-locked for overdue maintenance.

## Training (coordinators)
Add courses with a validity period in months; certificate expiry is then calculated for you. The **training matrix** shows every active employee against every course: green valid, amber expiring within 30 days, red expired. Reminders go out at 30, 14, 7, and 1 day before expiry.

## Licenses
Record the number, type, issuing authority, and expiry, and upload the copy. The expiring list defaults to the next 30 days.

## Authorizations
Grant per machine or per machine class. Add restrictions only when you need them:
- **Shift** — the operator may only start during that shift (overnight shifts are handled correctly).
- **Location** — only machines at that site.
- **Temporary** — requires an end date and expires by itself.

Revoking takes effect on the operator's next tap.

## Maintenance
Create orders as preventive, corrective, emergency, predictive, or inspection. Add parts as you use them — the cost totals automatically when you complete the order. Reminders arrive at 30, 14, 7, and 1 day, on the due date, and once overdue.

When a machine goes overdue, or reaches its operating-hours threshold, HESAMS locks it. To clear the lock: complete the order first, then ask a maintenance manager to unlock it. The system refuses to unlock without a completed order in the last seven days. On unlock, the next schedule rolls forward automatically.

## Incidents
Report near misses, accidents, unsafe acts, and unsafe conditions, with attachments. The flow is Open → under investigation (root cause + corrective action) → pending approval → closed by a safety manager.

## Reports
Twelve reports, each with a date range, exportable as PDF, Excel, or CSV. Every export is recorded in the audit log with who exported what and when.

## Notifications
The bell shows in-app alerts; the same alerts also go to email and, where configured, SMS, push, Teams, or WhatsApp. Mark items read to clear them.
