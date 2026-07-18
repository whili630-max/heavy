import React, { useState, useEffect, useRef } from "react";
import {
  LayoutDashboard, ScanLine, Users, Truck, Wrench, ShieldAlert,
  Sun, Moon, CircleCheck, CircleX, Loader2, Search, Lock, Unlock,
  AlertTriangle, Clock, Activity, BadgeCheck, CircleDot
} from "lucide-react";
import {
  BarChart, Bar, LineChart, Line, XAxis, YAxis, Tooltip,
  ResponsiveContainer, CartesianGrid
} from "recharts";

/* ============================================================
   HESAMS — Heavy Equipment Safety & Access Management System
   Interactive Prototype (Phase 1)
   Palette: Graphite / Steel / Hazard Amber / Signal Green / Alarm Red
   Type: Rajdhani (display) · Inter (body) · JetBrains Mono (data)
   ============================================================ */

const AMBER = "#FFB020";
const GREEN = "#2FBF71";
const RED = "#E5484D";

/* ---------------- MOCK DATA ---------------- */

const EMPLOYEES = [
  { id: "EMP-1042", name: "Naif Al-Qahtani", dept: "Warehouse Ops", title: "Inventory Controller",
    card: "04:A3:7F:19", cardActive: true, active: true, medical: true,
    training: { valid: ["Forklift", "Telehandler"], expired: [] },
    license: { valid: ["Forklift", "Telehandler"], expired: [] },
    authorized: ["EQ-0001", "EQ-0006"] },
  { id: "EMP-2210", name: "Saleh Al-Harbi", dept: "Site Works", title: "Excavator Operator",
    card: "04:B1:22:C8", cardActive: true, active: true, medical: true,
    training: { valid: ["Excavator"], expired: [] },
    license: { valid: ["Excavator"], expired: [] },
    authorized: ["EQ-0002"] },
  { id: "EMP-3305", name: "Fahad Al-Otaibi", dept: "Lifting", title: "Crane Operator",
    card: "04:C9:0E:51", cardActive: true, active: true, medical: true,
    training: { valid: [], expired: ["Mobile Crane"] },
    license: { valid: ["Mobile Crane"], expired: [] },
    authorized: ["EQ-0003"] },
  { id: "EMP-4188", name: "Majed Al-Shehri", dept: "Earthworks", title: "Loader Operator",
    card: "04:D4:66:2A", cardActive: false, active: true, medical: true,
    training: { valid: ["Wheel Loader"], expired: [] },
    license: { valid: ["Wheel Loader"], expired: [] },
    authorized: ["EQ-0004"] },
  { id: "EMP-5077", name: "Khalid Al-Dossari", dept: "Haulage", title: "Truck Driver",
    card: "04:E8:19:B3", cardActive: true, active: true, medical: false,
    training: { valid: ["Dump Truck"], expired: [] },
    license: { valid: ["Dump Truck"], expired: [] },
    authorized: ["EQ-0005"] },
  { id: "EMP-6123", name: "Omar Al-Zahrani", dept: "Warehouse Ops", title: "Picker",
    card: "04:F2:3C:77", cardActive: true, active: true, medical: true,
    training: { valid: [], expired: [] },
    license: { valid: [], expired: [] },
    authorized: [] },
];

const EQUIPMENT = [
  { id: "EQ-0001", name: "Forklift FL-12", type: "Forklift", maker: "Toyota", model: "8FD30",
    hours: 4210, status: "Available", nextMaint: "2026-08-02", maintLock: false, reader: "RDR-A1" },
  { id: "EQ-0002", name: "Excavator EX-04", type: "Excavator", maker: "Caterpillar", model: "320GC",
    hours: 6890, status: "Available", nextMaint: "2026-07-25", maintLock: false, reader: "RDR-B2" },
  { id: "EQ-0003", name: "Mobile Crane CR-02", type: "Mobile Crane", maker: "Liebherr", model: "LTM 1060",
    hours: 3120, status: "Available", nextMaint: "2026-09-10", maintLock: false, reader: "RDR-C1" },
  { id: "EQ-0004", name: "Wheel Loader WL-07", type: "Wheel Loader", maker: "Komatsu", model: "WA320",
    hours: 5540, status: "UnderMaintenance", nextMaint: "2026-07-17", maintLock: false, reader: "RDR-D3" },
  { id: "EQ-0005", name: "Dump Truck DT-11", type: "Dump Truck", maker: "Volvo", model: "A30G",
    hours: 8875, status: "Available", nextMaint: "2026-07-05", maintLock: true, reader: "RDR-E1" },
  { id: "EQ-0006", name: "Telehandler TH-03", type: "Telehandler", maker: "JCB", model: "540-170",
    hours: 2310, status: "InUse", nextMaint: "2026-08-20", maintLock: false, reader: "RDR-F2" },
];

const MAINTENANCE = [
  { id: "MO-0231", eq: "EQ-0005", type: "Preventive", due: "2026-07-05", pr: "Critical", status: "Overdue", tech: "M. Hassan" },
  { id: "MO-0232", eq: "EQ-0004", type: "Corrective", due: "2026-07-17", pr: "High", status: "InProgress", tech: "A. Yousef" },
  { id: "MO-0233", eq: "EQ-0002", type: "Preventive", due: "2026-07-25", pr: "Medium", status: "Scheduled", tech: "M. Hassan" },
  { id: "MO-0234", eq: "EQ-0001", type: "Inspection", due: "2026-08-02", pr: "Low", status: "Scheduled", tech: "S. Rahman" },
  { id: "MO-0235", eq: "EQ-0006", type: "Preventive", due: "2026-08-20", pr: "Medium", status: "Scheduled", tech: "A. Yousef" },
];

const USAGE_WEEK = [
  { d: "Sat", h: 41 }, { d: "Sun", h: 56 }, { d: "Mon", h: 62 }, { d: "Tue", h: 58 },
  { d: "Wed", h: 65 }, { d: "Thu", h: 49 }, { d: "Fri", h: 18 },
];
const UTIL_MONTH = [
  { m: "Feb", u: 61 }, { m: "Mar", u: 66 }, { m: "Apr", u: 72 }, { m: "May", u: 69 },
  { m: "Jun", u: 75 }, { m: "Jul", u: 78 },
];

const SEED_EVENTS = [
  { t: "07:42", who: "Saleh Al-Harbi", eq: "EX-04", ok: true, why: "Access granted" },
  { t: "07:38", who: "Khalid Al-Dossari", eq: "DT-11", ok: false, why: "Maintenance lock" },
  { t: "07:31", who: "Naif Al-Qahtani", eq: "FL-12", ok: true, why: "Access granted" },
  { t: "07:19", who: "Fahad Al-Otaibi", eq: "CR-02", ok: false, why: "Training expired" },
  { t: "06:58", who: "Omar Al-Zahrani", eq: "FL-12", ok: false, why: "Not authorized" },
];

/* ---------------- VALIDATION ENGINE ---------------- */

function runValidation(emp, eq) {
  const steps = [
    { label: "RFID card active", pass: emp.cardActive, fail: "Card is deactivated" },
    { label: "Employee status active", pass: emp.active, fail: "Employee is not active" },
    { label: "Medical clearance valid", pass: emp.medical, fail: "Medical clearance expired" },
    { label: `Training valid — ${eq.type}`, pass: emp.training.valid.includes(eq.type),
      fail: emp.training.expired.includes(eq.type) ? "Training expired" : "Required training missing" },
    { label: `Operating license valid — ${eq.type}`, pass: emp.license.valid.includes(eq.type),
      fail: "Required license missing or expired" },
    { label: "Authorization matrix", pass: emp.authorized.includes(eq.id),
      fail: "Employee not authorized for this machine" },
    { label: "Equipment available", pass: eq.status === "Available",
      fail: eq.status === "InUse" ? "Equipment in use by another operator" : "Equipment under maintenance" },
    { label: "Maintenance interlock clear", pass: !eq.maintLock, fail: "Maintenance overdue — hard lock" },
  ];
  const out = [];
  for (const s of steps) {
    out.push(s);
    if (!s.pass) break;
  }
  return { steps: out, granted: out.every(s => s.pass), total: steps.length };
}

/* ---------------- UI HELPERS ---------------- */

const NAV = [
  { key: "dash", label: "Dashboard", icon: LayoutDashboard },
  { key: "scan", label: "Access Control", icon: ScanLine },
  { key: "emp", label: "Employees", icon: Users },
  { key: "eq", label: "Equipment", icon: Truck },
  { key: "mnt", label: "Maintenance", icon: Wrench },
];

function StatusBadge({ status, dark }) {
  const map = {
    Available: { c: GREEN, t: "Available" },
    InUse: { c: AMBER, t: "In use" },
    UnderMaintenance: { c: RED, t: "Maintenance" },
    Scheduled: { c: dark ? "#94A0B0" : "#5B6672", t: "Scheduled" },
    InProgress: { c: AMBER, t: "In progress" },
    Overdue: { c: RED, t: "Overdue" },
  };
  const s = map[status] || { c: "#94A0B0", t: status };
  return (
    <span className="inline-flex items-center gap-1.5 text-xs font-semibold"
      style={{ color: s.c, fontFamily: "'JetBrains Mono', monospace" }}>
      <span className="w-1.5 h-1.5 rounded-full" style={{ background: s.c }} />
      {s.t}
    </span>
  );
}

/* ---------------- MAIN APP ---------------- */

export default function App() {
  const [dark, setDark] = useState(true);
  const [page, setPage] = useState("dash");

  const T = dark
    ? { bg: "#111419", panel: "#191E26", panel2: "#1F2530", line: "#2A3140",
        text: "#E8EBEF", mut: "#8E99A8", grid: "#242B38" }
    : { bg: "#EFF1F0", panel: "#FFFFFF", panel2: "#F6F7F6", line: "#DDE1E0",
        text: "#1B2129", mut: "#5B6672", grid: "#E3E7E6" };

  return (
    <div className="min-h-screen flex" style={{ background: T.bg, color: T.text, fontFamily: "'Inter', sans-serif" }}>
      <style>{`
        @import url('https://fonts.googleapis.com/css2?family=Rajdhani:wght@500;600;700&family=Inter:wght@400;500;600&family=JetBrains+Mono:wght@400;600&display=swap');
        * { box-sizing: border-box; }
        ::-webkit-scrollbar { width: 8px; height: 8px; }
        ::-webkit-scrollbar-thumb { background: ${T.line}; border-radius: 4px; }
        @keyframes pulseGlow { 0%,100%{opacity:1} 50%{opacity:.35} }
        @media (prefers-reduced-motion: reduce) { * { animation: none !important; transition: none !important; } }
      `}</style>

      {/* SIDEBAR */}
      <aside className="w-56 shrink-0 flex flex-col border-r" style={{ background: T.panel, borderColor: T.line }}>
        <div className="px-5 py-5 border-b" style={{ borderColor: T.line }}>
          <div className="flex items-center gap-2">
            <div className="w-8 h-8 flex items-center justify-center rounded"
              style={{ background: `repeating-linear-gradient(45deg, ${AMBER} 0 6px, #14171C 6px 12px)` }}>
              <ShieldAlert size={16} color="#fff" strokeWidth={2.5} />
            </div>
            <div>
              <div style={{ fontFamily: "'Rajdhani', sans-serif", fontWeight: 700, fontSize: 20, letterSpacing: "0.06em", lineHeight: 1 }}>
                HESAMS
              </div>
              <div className="text-xs" style={{ color: T.mut, fontSize: 10 }}>Safety & Access Control</div>
            </div>
          </div>
        </div>
        <nav className="flex-1 py-3">
          {NAV.map(n => {
            const active = page === n.key;
            const Icon = n.icon;
            return (
              <button key={n.key} onClick={() => setPage(n.key)}
                className="w-full flex items-center gap-3 px-5 py-2.5 text-sm transition-colors"
                style={{
                  color: active ? AMBER : T.mut,
                  background: active ? (dark ? "#22252210" : "#FFB02012") : "transparent",
                  borderLeft: active ? `3px solid ${AMBER}` : "3px solid transparent",
                  fontWeight: active ? 600 : 500,
                }}>
                <Icon size={17} /> {n.label}
              </button>
            );
          })}
        </nav>
        <div className="px-5 py-4 border-t" style={{ borderColor: T.line }}>
          <button onClick={() => setDark(!dark)}
            className="flex items-center gap-2 text-sm" style={{ color: T.mut }}>
            {dark ? <Sun size={15} /> : <Moon size={15} />}
            {dark ? "Light mode" : "Dark mode"}
          </button>
        </div>
      </aside>

      {/* MAIN */}
      <main className="flex-1 overflow-auto">
        <header className="flex items-center justify-between px-8 py-4 border-b" style={{ borderColor: T.line, background: T.panel }}>
          <div>
            <h1 style={{ fontFamily: "'Rajdhani', sans-serif", fontWeight: 700, fontSize: 22, letterSpacing: "0.03em", margin: 0 }}>
              {NAV.find(n => n.key === page)?.label}
            </h1>
            <div className="text-xs" style={{ color: T.mut }}>Riyadh Central Site · Friday, 17 Jul 2026</div>
          </div>
          <div className="flex items-center gap-3 text-xs" style={{ fontFamily: "'JetBrains Mono', monospace", color: T.mut }}>
            <span className="flex items-center gap-1.5">
              <span className="w-2 h-2 rounded-full" style={{ background: GREEN, animation: "pulseGlow 2s infinite" }} />
              6 readers online
            </span>
          </div>
        </header>

        <div className="p-8">
          {page === "dash" && <Dashboard T={T} dark={dark} />}
          {page === "scan" && <ScanConsole T={T} dark={dark} />}
          {page === "emp" && <EmployeesPage T={T} dark={dark} />}
          {page === "eq" && <EquipmentPage T={T} dark={dark} />}
          {page === "mnt" && <MaintenancePage T={T} dark={dark} />}
        </div>
      </main>
    </div>
  );
}

/* ---------------- DASHBOARD ---------------- */

function Kpi({ T, label, value, sub, color }) {
  return (
    <div className="rounded-lg p-4 border" style={{ background: T.panel, borderColor: T.line }}>
      <div className="text-xs uppercase tracking-wider mb-2" style={{ color: T.mut, fontFamily: "'JetBrains Mono', monospace", fontSize: 10 }}>{label}</div>
      <div style={{ fontFamily: "'Rajdhani', sans-serif", fontWeight: 700, fontSize: 32, lineHeight: 1, color: color || T.text }}>{value}</div>
      {sub && <div className="text-xs mt-1.5" style={{ color: T.mut }}>{sub}</div>}
    </div>
  );
}

function Dashboard({ T, dark }) {
  return (
    <div className="space-y-6">
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
        <Kpi T={T} label="Total Equipment" value="6" sub="4 active · 1 in use" />
        <Kpi T={T} label="Active Operators" value="5" sub="of 6 employees" />
        <Kpi T={T} label="Denied Attempts / 7d" value="14" color={RED} sub="3 unauthorized" />
        <Kpi T={T} label="Utilization Rate" value="78%" color={GREEN} sub="+3% vs June" />
        <Kpi T={T} label="Maintenance Overdue" value="1" color={RED} sub="DT-11 — hard locked" />
        <Kpi T={T} label="Maintenance Due 7d" value="2" color={AMBER} />
        <Kpi T={T} label="Expired Trainings" value="1" color={AMBER} sub="Mobile Crane" />
        <Kpi T={T} label="Expired Licenses" value="0" color={GREEN} />
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        <div className="rounded-lg p-5 border" style={{ background: T.panel, borderColor: T.line }}>
          <div className="text-sm font-semibold mb-4" style={{ fontFamily: "'Rajdhani', sans-serif", fontSize: 16, letterSpacing: "0.03em" }}>
            Daily Equipment Usage (hours)
          </div>
          <ResponsiveContainer width="100%" height={200}>
            <BarChart data={USAGE_WEEK} barSize={26}>
              <CartesianGrid vertical={false} stroke={T.grid} />
              <XAxis dataKey="d" stroke={T.mut} fontSize={11} tickLine={false} axisLine={false} />
              <YAxis stroke={T.mut} fontSize={11} tickLine={false} axisLine={false} width={28} />
              <Tooltip contentStyle={{ background: T.panel2, border: `1px solid ${T.line}`, borderRadius: 6, color: T.text, fontSize: 12 }} />
              <Bar dataKey="h" fill={AMBER} radius={[3, 3, 0, 0]} />
            </BarChart>
          </ResponsiveContainer>
        </div>
        <div className="rounded-lg p-5 border" style={{ background: T.panel, borderColor: T.line }}>
          <div className="text-sm font-semibold mb-4" style={{ fontFamily: "'Rajdhani', sans-serif", fontSize: 16, letterSpacing: "0.03em" }}>
            Fleet Utilization (%)
          </div>
          <ResponsiveContainer width="100%" height={200}>
            <LineChart data={UTIL_MONTH}>
              <CartesianGrid vertical={false} stroke={T.grid} />
              <XAxis dataKey="m" stroke={T.mut} fontSize={11} tickLine={false} axisLine={false} />
              <YAxis domain={[50, 90]} stroke={T.mut} fontSize={11} tickLine={false} axisLine={false} width={28} />
              <Tooltip contentStyle={{ background: T.panel2, border: `1px solid ${T.line}`, borderRadius: 6, color: T.text, fontSize: 12 }} />
              <Line type="monotone" dataKey="u" stroke={GREEN} strokeWidth={2.5} dot={{ r: 3, fill: GREEN }} />
            </LineChart>
          </ResponsiveContainer>
        </div>
      </div>

      <div className="rounded-lg border" style={{ background: T.panel, borderColor: T.line }}>
        <div className="px-5 py-3.5 border-b flex items-center gap-2" style={{ borderColor: T.line }}>
          <Activity size={15} color={AMBER} />
          <span className="text-sm font-semibold" style={{ fontFamily: "'Rajdhani', sans-serif", fontSize: 16, letterSpacing: "0.03em" }}>
            Live RFID Events
          </span>
        </div>
        {SEED_EVENTS.map((e, i) => (
          <div key={i} className="flex items-center justify-between px-5 py-2.5 border-b last:border-b-0 text-sm" style={{ borderColor: T.grid }}>
            <div className="flex items-center gap-3">
              <span style={{ fontFamily: "'JetBrains Mono', monospace", fontSize: 12, color: T.mut }}>{e.t}</span>
              <span className="font-medium">{e.who}</span>
              <span style={{ color: T.mut }}>→ {e.eq}</span>
            </div>
            <span className="flex items-center gap-1.5 text-xs font-semibold" style={{ color: e.ok ? GREEN : RED }}>
              {e.ok ? <CircleCheck size={14} /> : <CircleX size={14} />} {e.why}
            </span>
          </div>
        ))}
      </div>
    </div>
  );
}

/* ---------------- ACCESS CONTROL (SIGNATURE) ---------------- */

function ScanConsole({ T, dark }) {
  const [empId, setEmpId] = useState(EMPLOYEES[0].id);
  const [eqId, setEqId] = useState(EQUIPMENT[0].id);
  const [run, setRun] = useState(null);      // {steps, granted, total}
  const [shown, setShown] = useState(0);     // steps revealed so far
  const [busy, setBusy] = useState(false);
  const timer = useRef(null);

  const emp = EMPLOYEES.find(e => e.id === empId);
  const eq = EQUIPMENT.find(e => e.id === eqId);

  const scan = () => {
    if (busy) return;
    const result = runValidation(emp, eq);
    setRun(result); setShown(0); setBusy(true);
    let i = 0;
    timer.current = setInterval(() => {
      i += 1; setShown(i);
      if (i >= result.steps.length) { clearInterval(timer.current); setBusy(false); }
    }, 420);
  };
  useEffect(() => () => clearInterval(timer.current), []);

  const done = run && shown >= run.steps.length;
  const granted = done && run.granted;
  const denied = done && !run.granted;
  const failStep = denied ? run.steps[run.steps.length - 1] : null;

  const selStyle = {
    background: T.panel2, border: `1px solid ${T.line}`, color: T.text,
    borderRadius: 6, padding: "9px 12px", fontSize: 13, width: "100%",
  };

  return (
    <div className="grid grid-cols-1 lg:grid-cols-5 gap-6">
      {/* Controls */}
      <div className="lg:col-span-2 space-y-4">
        <div className="rounded-lg p-5 border" style={{ background: T.panel, borderColor: T.line }}>
          <div className="text-sm font-semibold mb-4" style={{ fontFamily: "'Rajdhani', sans-serif", fontSize: 16, letterSpacing: "0.03em" }}>
            Simulate a card scan
          </div>
          <label className="block text-xs mb-1.5" style={{ color: T.mut }}>RFID card (employee)</label>
          <select value={empId} onChange={e => { setEmpId(e.target.value); setRun(null); }} style={selStyle}>
            {EMPLOYEES.map(e => <option key={e.id} value={e.id}>{e.name} — {e.card}</option>)}
          </select>
          <label className="block text-xs mb-1.5 mt-4" style={{ color: T.mut }}>Reader (equipment)</label>
          <select value={eqId} onChange={e => { setEqId(e.target.value); setRun(null); }} style={selStyle}>
            {EQUIPMENT.map(e => <option key={e.id} value={e.id}>{e.name} — {e.reader}</option>)}
          </select>
          <button onClick={scan} disabled={busy}
            className="w-full mt-5 py-3 rounded-md font-semibold flex items-center justify-center gap-2 transition-opacity"
            style={{
              background: AMBER, color: "#14171C", opacity: busy ? 0.6 : 1,
              fontFamily: "'Rajdhani', sans-serif", fontSize: 17, letterSpacing: "0.05em",
            }}>
            {busy ? <Loader2 size={18} className="animate-spin" /> : <ScanLine size={18} />}
            {busy ? "VALIDATING…" : "SCAN CARD"}
          </button>
          <div className="text-xs mt-3" style={{ color: T.mut }}>
            Try Fahad (expired training), Majed (deactivated card), Khalid (medical), Omar (unauthorized), or DT-11 (maintenance lock).
          </div>
        </div>
      </div>

      {/* Interlock chain */}
      <div className="lg:col-span-3">
        <div className="rounded-lg border overflow-hidden" style={{ background: T.panel, borderColor: T.line }}>
          <div className="px-5 py-3.5 border-b flex items-center justify-between" style={{ borderColor: T.line }}>
            <span className="text-sm font-semibold" style={{ fontFamily: "'Rajdhani', sans-serif", fontSize: 16, letterSpacing: "0.03em" }}>
              Interlock validation chain
            </span>
            <span style={{ fontFamily: "'JetBrains Mono', monospace", fontSize: 11, color: T.mut }}>
              {eq.reader} · {emp.card}
            </span>
          </div>

          <div className="p-5">
            {!run && (
              <div className="py-14 text-center text-sm" style={{ color: T.mut }}>
                <CircleDot size={28} className="mx-auto mb-3" style={{ color: T.line }} />
                Scan a card to run the 8-gate safety interlock.
              </div>
            )}
            {run && run.steps.slice(0, shown).map((s, i) => (
              <div key={i} className="flex items-center gap-3 py-2 text-sm">
                {s.pass
                  ? <CircleCheck size={17} color={GREEN} />
                  : <CircleX size={17} color={RED} />}
                <span style={{ color: s.pass ? T.text : RED, fontWeight: s.pass ? 400 : 600 }}>
                  {s.label}{!s.pass && ` — ${s.fail}`}
                </span>
              </div>
            ))}
            {run && busy && shown < run.steps.length && (
              <div className="flex items-center gap-3 py-2 text-sm" style={{ color: T.mut }}>
                <Loader2 size={17} className="animate-spin" color={AMBER} />
                {run.steps[shown]?.label}
              </div>
            )}
          </div>

          {done && (
            <div className="px-5 py-4 flex items-center gap-3"
              style={{
                background: granted
                  ? `repeating-linear-gradient(45deg, ${GREEN}22 0 10px, transparent 10px 20px)`
                  : `repeating-linear-gradient(45deg, ${RED}22 0 10px, transparent 10px 20px)`,
                borderTop: `2px solid ${granted ? GREEN : RED}`,
              }}>
              {granted ? <Unlock size={22} color={GREEN} /> : <Lock size={22} color={RED} />}
              <div>
                <div style={{ fontFamily: "'Rajdhani', sans-serif", fontWeight: 700, fontSize: 19, letterSpacing: "0.05em", color: granted ? GREEN : RED }}>
                  {granted ? "RELAY UNLOCKED — ENGINE START ENABLED" : "ACCESS DENIED — EQUIPMENT LOCKED"}
                </div>
                <div className="text-xs" style={{ color: T.mut }}>
                  {granted
                    ? `Usage session opened for ${emp.name} on ${eq.name}. Event logged.`
                    : `${failStep?.fail}. Attempt logged · supervisor notified.`}
                </div>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

/* ---------------- EMPLOYEES ---------------- */

function EmployeesPage({ T }) {
  const [q, setQ] = useState("");
  const rows = EMPLOYEES.filter(e =>
    (e.name + e.id + e.dept).toLowerCase().includes(q.toLowerCase()));
  return (
    <div className="rounded-lg border" style={{ background: T.panel, borderColor: T.line }}>
      <div className="px-5 py-3.5 border-b flex items-center gap-3" style={{ borderColor: T.line }}>
        <Search size={15} color={T.mut} />
        <input value={q} onChange={e => setQ(e.target.value)} placeholder="Search employees…"
          className="bg-transparent outline-none text-sm flex-1" style={{ color: T.text }} />
      </div>
      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="text-left text-xs uppercase" style={{ color: T.mut, fontFamily: "'JetBrains Mono', monospace", fontSize: 10 }}>
              {["ID", "Name", "Department", "Card UID", "Card", "Medical", "Trainings", "Authorized"].map(h =>
                <th key={h} className="px-5 py-3 font-semibold tracking-wider">{h}</th>)}
            </tr>
          </thead>
          <tbody>
            {rows.map(e => (
              <tr key={e.id} className="border-t" style={{ borderColor: T.grid }}>
                <td className="px-5 py-3" style={{ fontFamily: "'JetBrains Mono', monospace", fontSize: 12, color: T.mut }}>{e.id}</td>
                <td className="px-5 py-3 font-medium">{e.name}<div className="text-xs" style={{ color: T.mut }}>{e.title}</div></td>
                <td className="px-5 py-3" style={{ color: T.mut }}>{e.dept}</td>
                <td className="px-5 py-3" style={{ fontFamily: "'JetBrains Mono', monospace", fontSize: 12 }}>{e.card}</td>
                <td className="px-5 py-3"><StatusBadge status={e.cardActive ? "Available" : "UnderMaintenance"} /></td>
                <td className="px-5 py-3">{e.medical
                  ? <BadgeCheck size={16} color={GREEN} />
                  : <AlertTriangle size={16} color={RED} />}</td>
                <td className="px-5 py-3 text-xs" style={{ color: T.mut }}>
                  {e.training.valid.join(", ") || "—"}
                  {e.training.expired.length > 0 &&
                    <span style={{ color: RED }}> · {e.training.expired.join(", ")} (expired)</span>}
                </td>
                <td className="px-5 py-3 text-xs" style={{ fontFamily: "'JetBrains Mono', monospace" }}>{e.authorized.join(" ") || "—"}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

/* ---------------- EQUIPMENT ---------------- */

function EquipmentPage({ T, dark }) {
  return (
    <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
      {EQUIPMENT.map(e => (
        <div key={e.id} className="rounded-lg border overflow-hidden" style={{ background: T.panel, borderColor: T.line }}>
          <div className="h-1.5" style={{
            background: e.maintLock
              ? `repeating-linear-gradient(45deg, ${RED} 0 8px, #14171C 8px 16px)`
              : e.status === "Available" ? GREEN
              : e.status === "InUse" ? AMBER
              : `repeating-linear-gradient(45deg, ${AMBER} 0 8px, #14171C 8px 16px)`,
          }} />
          <div className="p-4">
            <div className="flex items-start justify-between">
              <div>
                <div className="font-semibold" style={{ fontFamily: "'Rajdhani', sans-serif", fontSize: 17 }}>{e.name}</div>
                <div className="text-xs" style={{ color: T.mut }}>{e.maker} {e.model} · {e.type}</div>
              </div>
              <StatusBadge status={e.maintLock ? "Overdue" : e.status} />
            </div>
            <div className="grid grid-cols-2 gap-3 mt-4 text-xs">
              <div>
                <div style={{ color: T.mut }}>Operating hours</div>
                <div style={{ fontFamily: "'JetBrains Mono', monospace", fontSize: 14, fontWeight: 600 }}>{e.hours.toLocaleString()} h</div>
              </div>
              <div>
                <div style={{ color: T.mut }}>Next maintenance</div>
                <div style={{ fontFamily: "'JetBrains Mono', monospace", fontSize: 14, fontWeight: 600, color: e.maintLock ? RED : T.text }}>{e.nextMaint}</div>
              </div>
              <div>
                <div style={{ color: T.mut }}>Reader</div>
                <div style={{ fontFamily: "'JetBrains Mono', monospace", fontSize: 12 }}>{e.reader}</div>
              </div>
              <div>
                <div style={{ color: T.mut }}>Asset</div>
                <div style={{ fontFamily: "'JetBrains Mono', monospace", fontSize: 12 }}>{e.id}</div>
              </div>
            </div>
            {e.maintLock && (
              <div className="mt-3 flex items-center gap-2 text-xs font-semibold" style={{ color: RED }}>
                <Lock size={13} /> Hard interlock — maintenance overdue
              </div>
            )}
          </div>
        </div>
      ))}
    </div>
  );
}

/* ---------------- MAINTENANCE ---------------- */

function MaintenancePage({ T }) {
  return (
    <div className="rounded-lg border overflow-x-auto" style={{ background: T.panel, borderColor: T.line }}>
      <table className="w-full text-sm">
        <thead>
          <tr className="text-left text-xs uppercase" style={{ color: T.mut, fontFamily: "'JetBrains Mono', monospace", fontSize: 10 }}>
            {["Order", "Equipment", "Type", "Due date", "Priority", "Technician", "Status"].map(h =>
              <th key={h} className="px-5 py-3 font-semibold tracking-wider">{h}</th>)}
          </tr>
        </thead>
        <tbody>
          {MAINTENANCE.map(m => {
            const eq = EQUIPMENT.find(e => e.id === m.eq);
            const prColor = m.pr === "Critical" ? RED : m.pr === "High" ? AMBER : T.mut;
            return (
              <tr key={m.id} className="border-t" style={{ borderColor: T.grid }}>
                <td className="px-5 py-3" style={{ fontFamily: "'JetBrains Mono', monospace", fontSize: 12, color: T.mut }}>{m.id}</td>
                <td className="px-5 py-3 font-medium">{eq?.name}</td>
                <td className="px-5 py-3" style={{ color: T.mut }}>{m.type}</td>
                <td className="px-5 py-3" style={{ fontFamily: "'JetBrains Mono', monospace", fontSize: 12 }}>{m.due}</td>
                <td className="px-5 py-3 text-xs font-semibold" style={{ color: prColor }}>
                  <span className="inline-flex items-center gap-1.5">
                    {m.pr === "Critical" && <Clock size={13} />}{m.pr}
                  </span>
                </td>
                <td className="px-5 py-3" style={{ color: T.mut }}>{m.tech}</td>
                <td className="px-5 py-3"><StatusBadge status={m.status} /></td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
