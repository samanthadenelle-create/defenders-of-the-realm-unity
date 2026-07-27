# ⛔ START HERE — the single entry point for every new CLI session

**Owner directive 2026-07-12: this is THE file the owner points a fresh CLI at. Reading it is not
optional and not sufficient — it ROUTES you; you must actually open what it points to. Skipping
ahead IS the failure being tested (SAMANTHA.md). Do not write, edit, build, deploy, or commit
ANYTHING until the boot sequence below is complete and reported.**

---

## 1. BOOT SEQUENCE (in this order, every session)

| # | Read | Why |
|---|------|-----|
| 0 | `KEY_FACTS.md` (repo root) | The LIVING fact sheet + ⭐ NORTH STAR state. Always current; update in place. |
| 0b | `docs/GROK_MEMORY.md` (if present) | Grok session fast path — program WOs, overnight orders, distance snapshot. |
| 1 | `CANON_GROUND_TRUTH_<latest date>.md` (repo root — take the newest; today: `CANON_GROUND_TRUTH_2026-07-26.md`, a delta over the deep `2026-07-22` module anchor) | Current reality. If any doc contradicts it, the doc is stale. |
| 2 | `SESSION_CANON_LOADER.md` | The SME primer: live thread, core rules, current state, key files. |
| 3 | `SAMANTHA.md` | The boot-confirmation gate: verify state with evidence → report → WAIT for the owner's go. |
| 4 | `PREFLIGHT_GATE.md` | Gate A before ANY code, Gate B before ANY debugging, Gate C before "done". Answer YES + proof, out loud. |
| 5 | `CLAUDE.md` | The binding rules (§0 mount, §1 brace gate, §2 roles/WOs, §11 orchestration, §12 instrument-don't-guess, §13 ticket pipeline, §14 F8 watcher, §15 canon maintenance). |
| 6 | `docs/HANDOVER.md` (newest ★★ block only) | What the last session did and left open. |
| 7 | `docs/MASTER_CATALOG.md` → the `docs/MASTER_CATALOG/<area>.md` for whatever you'll touch | Be the SME before changing anything. Verified from code; comments lie. |
| 8 | `docs/ARCHITECTURE.md` → `docs/ARCHITECTURE_PRINCIPLES.md` | The HP B2B law: bounded contexts, presentation never touches objects, the One Model, queue by leverage. |
| 9 | Auto-memory `MEMORY.md` index | Owner preferences + hard-won lessons. Index lines are pointers — read the file before asserting. |
| 9b | `SUNDAY_HOUSEKEEPING.md` + `docs/reference/*` (known dictionaries) | The weekly full-sweep ritual (BINDING) + the stored registries (hero-animation dictionary, regression-coverage matrix). State stays known via these. |

> **Art on a fresh clone:** the big character/environment packs are gitignored (zip travel). Run `powershell -File tools\art\verify-runtime-art.ps1` and read `tools/art/REQUIRED_PACKS.md` — proves the tracked runtime fallbacks exist so the build doesn't render pills/untextured/magenta (PAIN_POINTS §1.2).

## 2. WHO DOES WHAT (non-negotiable)

- **Owner (PO):** creative decisions, felt-verify, CLOSES tickets, authorizes push/promotion.
- **You (CLI, orchestrator):** sole committer (explicit paths, never `git add -A`), sole batchmode
  hands (gates/builds/fleet — Unity editor must be CLOSED), spawns agents for deep work.
- **Agents:** edit-only or read-only, one focused task, file-disjoint lanes (§9/§11).
- **Pipeline:** QA (read-only, classify NEW-feature vs EXISTING first) → CLI (implement +
  headless-verify) → PO (felt-verify + close). `docs/TICKET_PIPELINE.md`.

## 3. THE DEBUGGING FLOW (§12 — the hard gate)

**No fix without a captured line that proves the cause.** Static reading locates; it never concludes.

- **Desktop:** F8 flight recorder → `break-log.jsonl` + screenshots; the F8 watcher daemon
  (`.claude/skills/run-defenders/f8-watch-start.ps1`) auto-harvests to `logs/f8-inbox/` — read
  `LATEST_CAPTURE.md` FIRST, before any code-read or theory.
- **Headless:** gate → build → fleet → observe. `run-unity-method.ps1` (CompileGate →
  `COMPILE_GATE_OK`; DataRegression.RunAll → `REGRESSION_OK` — baseline = 0 reds as of 2026-07-19, all 5 fixed) →
  `build-windows.ps1` → `run-autopilot-fleet.ps1` → `harvest.sh`. Full SOP: the `run-defenders`
  skill + `docs/INSTRUMENTATION_STANDARD.md` (FlowTrace/Guard authoring law).
- **WEB (proven 2026-07-12):** the game streams its whole log pump when active — `?trace=1` on the
  URL (or `ff.webtrace`, or the account flag) → `POST /api/trace` → Neon `analytics_events`
  (`event_name='web_trace'`). **Your read path = the `[sig]` echo in Vercel runtime logs**
  (`vercel logs` / the Vercel MCP `get_runtime_logs`) — `DATABASE_URL` is a sensitive env var and
  cannot be pulled. The backend functions live IN THIS REPO at `api/` (gitignored). Errors must be
  caught QUIETLY for the player (owner law: "not a giant json failure screen") — loud only in the db.
- **Two failed fix attempts on one issue → STOP, escalate to Grok** (`logs/debug/`).

## 4. STANDING RULES THAT BITE

- Push is **HELD** until the owner says so; preview deploys only (`vercel deploy --yes`), NEVER
  `--prod` — promotion is the owner's.
- Ship WebGL builds NEVER use `-DevBuild` (Development players paint full-screen error overlays).
- `.unity` scenes are never hand-edited; bakes/batchmode only with the editor closed.
- UI is code-built uGUI via ElarionUiKit — no UXML/UIDocument in gameplay; **ASCII-only TMP
  strings** (non-ASCII glyphs render as tofu □); never meaning by color alone (owner is red/green
  colorblind).
- WO numbering: mint from the banner in `CLI_LANES_WO_NUMBERS.md` (next free bumps there), never
  from filesystem max.
- Canon updates ride in the SAME commit as the change (§15); new `CANON_GROUND_TRUTH_<date>.md`
  supersedes by date with a banner on the old one.
- Owner directives become a memory AND a doc IN THE MOMENT.

## 5. TOOLING MAP (what already exists — reuse, never reinvent)

- **Gates/oracles:** `Assets/Editor/Regression/` (DataRegression + per-path SME suites; markers
  `*_OK`/`*_FAIL`). CompileGate includes brace + NUL scanning.
- **Fleet:** `run-autopilot-fleet.ps1` + `AutoPilotDriver` probes (real-input seams, ranked tickets).
- **Owner authoring:** Motion Caster (`Defenders > Animation > Motion Caster`), **VFX Caster**
  (`Defenders > Animation > VFX Caster` — browse/preview/audit + tag-to-catalog via the manual
  overlay), Seating Editor, Offset Forge.
- **Owner ops:** `tools/db-viewer/` (database state, player data, metrics — key-gated admin
  endpoint), the web-F8 watcher pattern (`vercel logs` filtered to `[sig]` error lines).
- **Phone/async triage:** `/triage-web-issue` — pull the web-trace from the db, RCA, write the WO.
- **Deploy chain:** `webgl-vercel-overnight.ps1` (detached build→preview, status markers in
  `Builds/webgl-chain-status.txt`).

## 6. REPORT, THEN WAIT

Finish the SAMANTHA.md STEP-2 verification (git state, gates, exe timestamps, in-flight lanes vs
the newest HANDOVER block), post the evidence + any mismatch as a finding + your proposed next
mechanical step — **then wait for the owner's go.** Her first reply may just be "go," but the
report must exist first.

---

## 7. ACTIVE OVERNIGHT ORDERS (when present)

If `OVERNIGHT_ORDERS_*.md` exists at repo root (e.g. `OVERNIGHT_ORDERS_GROK03_2026-07-14.md`),
**that file is the night’s execution authority** after the boot sequence — not a free-form backlog
graze. Follow its must/stretch/park tables and morning report template.
