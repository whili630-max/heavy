# HESAMS — Future Enhancement Roadmap

## Near term (3–6 months)
- **Mobile app for supervisors** — denial alerts with one-tap context, offline incident reporting from the field.
- **Bulk import** — CSV onboarding for employees, equipment, and certificates during rollout.
- **Arabic-first UI** — full RTL layout, not just translated strings.
- **Photo capture at the gate** — a camera on the reader records who actually tapped, closing the card-sharing gap that RFID alone can't.
- **Escalation rules engine** — configurable: unread critical alert → supervisor → manager after N minutes.

## Medium term (6–12 months)
- **Telematics integration** — pull real operating hours, fuel, and fault codes from CAN bus / OEM APIs (Cat Product Link, Komatsu Komtrax) instead of estimating from session duration.
- **GPS geofencing** — deny operation outside the authorized work zone; feeds the location gate with live data rather than a static record.
- **Predictive maintenance** — model failure risk from hours, fault codes, and history; open orders before the calendar says so.
- **Pre-start inspection checklist** — operator completes a digital walkaround on a tablet at the gate before the relay opens.
- **Contractor portal** — third-party companies manage their own operators' certificates, with your safety team approving.
- **Power BI embedded** — analytics inside HESAMS rather than a separate tool.

## Long term (12+ months)
- **Biometric second factor** — fingerprint or facial verification at the gate; card + biometric eliminates card sharing entirely.
- **Fatigue and impairment detection** — camera-based drowsiness monitoring tied to the session, alerting supervisors in real time.
- **Digital twin** — per-machine model combining usage, maintenance, and incident history for lifecycle costing and replacement decisions.
- **Multi-site federation** — one tenant per site with a group-level safety dashboard; certificates recognized across sites by policy.
- **Regulatory export packs** — one-click evidence bundles formatted for OSHA / Saudi HCIS / ISO 45001 audits.
- **Offline-first mesh** — gate units gossip whitelist updates peer-to-peer so a single unit with connectivity keeps a whole yard current.

## Deliberately not planned
- **Manual override at the machine.** Every request for one is a request to defeat the safety case. Model exceptions as time-boxed temporary authorizations with an audit trail instead.
- **Fail-open on server error.** The relay stays locked when the system is unsure. A stopped machine is a recoverable problem; an unqualified operator on a running machine is not.
