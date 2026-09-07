# DeNelle Studios - Session Canon Loader

> **THE LIVE ANCHOR IS THE ONE ROOT `CANON_GROUND_TRUTH_<date>.md` WITHOUT A `SUPERSEDED` BANNER -
> never a filename copied into a doc.** That is the rule; newest-by-date is only the usual way it
> comes out. Find it: `ls CANON_GROUND_TRUTH_*.md | sort | tail -1` (PowerShell:
> `Get-ChildItem CANON_GROUND_TRUTH_*.md | Sort-Object Name | Select-Object -Last 1`), then confirm
> its first lines carry no `SUPERSEDED` banner. Read it first, then this file.
>
> ⚠ **Root holds TWO of these files and only ONE is the anchor (WO-1482, 2026-09-07).**
> `CANON_GROUND_TRUTH_2026-07-22.md` is kept at root as the deep **module** anchor many docs cite; it
> is bannered `SUPERSEDED` and is history, not guidance. Sorting by date happens to skip it today, but
> **judge by the banner, not by the sort.** The 19 older anchors live under `docs/_archive/root/`. Per CLAUDE.md section 15 that anchor WINS over this file on any conflict. This file
> holds only rules and pointers: **it states no numbers, no versions, no branch names, by design.**

*(The stacked dated banners that used to sit here are history, moved verbatim to
`docs/_archive/SESSION_CANON_LOADER_history_2026-09-06.md`. They are not guidance.)*

---

## 1. Core rules - each one is owned by a CLAUDE.md section; read the section, not a summary

1. **Be the SME before you touch anything.** `docs/MASTER_CATALOG.md` plus the
   `docs/MASTER_CATALOG/<area>.md` for what you will touch, verified from the code - comments lie.
   *(CLAUDE.md mandatory-first-step, section 0.)*
2. **Answer the preflight gate, unprompted.** Gate A before any code, Gate B before any debugging,
   Gate C before "done". YES plus a one-line proof to every item, or STOP. *(`PREFLIGHT_GATE.md`,
   binding via the CLAUDE.md preflight directive.)*
3. **Never write `.cs` through a bash redirect or the Linux mount.** Write/Edit tools on the Windows
   path only; the repo root is machine-dependent and is never hardcoded. *(CLAUDE.md section 0.)*
4. **Brace-balance and NUL-check every `.cs` you edited before reporting done.** *(section 1.)*
5. **Roles are fixed:** the UI seat never writes code, the CLI seat writes and gates all of it and is
   the sole committer, the owner is PO and closes tickets. *(section 2.)*
6. **Mint WO numbers only from the `CLI_LANES_WO_NUMBERS.md` banner, and bump that banner in the same
   edit as the mint.** Never from the filesystem max, never from a number copied into another doc.
   *(section 2.)*
7. **Never hand-edit a `.unity` scene; never bake with the editor open.** *(section 3.)*
7b. **Architecture law:** one bounded context per component, the One Model (capability is a property on
   the entry, never hard-coded per type or tag), presentation is a separate layer that never touches the
   objects, MVVM strict (the VM holds all logic and state, the View is a dumb skin), changes arrive
   flag-gated, and structural refactors are never smuggled into player-facing work.
   *(CLAUDE.md "Architecture law (BINDING)", spec in `docs/ARCHITECTURE_PRINCIPLES.md`.)*
8. **The `.asmdef` files are the dependency authority.** The one hard invariant: `DeNelle.HUD` never
   references `DeNelle.Village` in either direction; cross-module calls go through `CoreServices` with
   `?.`. *(section 5.)*
9. **Naming and design canon is owner-ruled, not inferred** - village name, hero tag, spawn resolution
   by component (the `SpawnPoint` tag does not exist), Echo affinity as a match bonus, passive Echo
   repair, the action-bar face rules. *(section 7 - and read the live constants it points at, never a
   count restated in prose.)*
10. **Parallel lanes are file-disjoint silos; one agent per file.** *(section 9.)*
11. **Orchestrate, do not solo-dig.** Fan out read-only diagnosis freely; fan out edit-only agents on
    disjoint lanes; the orchestrator batch-gates once and commits by explicit path. The pipeline never
    idles while READY tickets exist. *(section 11.)*
12. **Never guess. Prove it, or say you have not.** Every factual statement traces to something read or
    measured this session; "probably" is an admission of a guess. And follow the documented procedure
    to the end - deviating needs the owner's explicit permission in advance. *(section 11B.)*
13. **Instrument, do not guess - the hard gate.** No code edit on a non-trivial bug until captured data
    proves the cause. FlowTrace/Guard first, run it (prefer headless), let the data name the dead step.
    Static reading locates candidates; it never concludes. **Never strip instrumentation** - flag it off
    at most. *(section 12, method in `docs/INSTRUMENTATION_STANDARD.md`.)*
14. **Ticket pipeline:** QA does read-only RCA and classifies NEW-feature vs EXISTING, CLI implements and
    headless-verifies, PO felt-verifies and closes. *(section 13, spec in `docs/TICKET_PIPELINE.md`.)*
15. **F8 captures are surfaced by hooks and triaged live; the owner is never the bug detector.** The
    inbox is a QUEUE - ack exactly one capture at a time until it reports no capture. *(section 14.)*
16. **Canon updates ride in the same commit as the change**, or the doc gets a dated `STALE:` banner.
    Duplicated state is the bug; point at the thing that knows. *(section 15.)*
17. **Content ships from the R2 CDN, not in the APK.** Bundle names are content-hashed, so every content
    build needs its own push through `tools\r2-ship.ps1`; judge by the marker on a fresh log, never an
    exit code; never distribute through raw `adb install`. *(section 16.)*
18. **Reporting-done checklist is run line by line, not from memory.** *(section 10.)*

### Day-1 boot posture (the reminder the owner should never have to give)
Turn one, unprompted, before your first task reply: read and become SME (rule 1), take the
verify-delegate-instrument-first posture (rules 11-13), hold the line rather than please (park
off-focus work into a WO), and write the memory AND the doc in the moment rather than promising to.
Reading this file is not doing it. The full boot route is `START_HERE.md`, executed via
`CLI_SESSION_PLAYBOOK.md`, with the report-then-wait gate in `SAMANTHA.md`.

---

## 2. Current state - pointers only. Every value below is read, never quoted.

| What you need | Where it actually lives |
|---|---|
| Reality anchor | the one ROOT `CANON_GROUND_TRUTH_*.md` with no `SUPERSEDED` banner (`ls CANON_GROUND_TRUTH_*.md \| sort \| tail -1`, then check its banner); archived ones are in `docs/_archive/root/` |
| Branch, HEAD, unpushed count | `git status -sb`, `git rev-list --count origin/<branch>..HEAD` |
| Build stamp (`bundleVersion`, `AndroidBundleVersionCode`) | `ProjectSettings/ProjectSettings.asset` |
| Live public store release | `publishing/SUBMIT_CHECKLIST.md` (read at source; it is not the tester build) |
| Save schema version | `SaveSchema.CurrentVersion`, `Assets/_Modules/Core/State/SaveSchema.cs` |
| Assembly count and dependencies | `find Assets/_Modules -name '*.asmdef'` and the files themselves |
| Action-bar face maximum | `HudActionBarModel.MaxVisibleFaces` plus its pinning regressions |
| Next free WO number | the `CLI_LANES_WO_NUMBERS.md` banner rows, cross-checked against `WorkOrders/` |
| Board / ticket status | `BOARD.html`, regenerated by `python tools/board_build.py` |
| Home hub scene | `SceneRouter.CastleCandidates` (flag-dependent on `FeatureFlags.MergedWorld`) |
| The live P0 list | the get-well plan the newest anchor marks LIVE, in its stated order (the anchor names it) |
| Owner rulings and living facts | `KEY_FACTS.md` |
| How to run the machine | `docs/CLI_OPERATIONS_RUNBOOK.md` |

### The four gate markers
Judge every gate by its MARKER on a **fresh** log under `Builds/`, never by an exit code - this repo's
runners exit 0 on refusals. The four, and the logs the current anchor read them from:

| Marker (prefix; read the counts off the log) | Log, as of the 2026-09-06 anchor |
|---|---|
| `COMPILE_GATE_OK` | `Builds/cg-lanes2.log` |
| `REGRESSION_OK` | `Builds/reg-final2.log` |
| `UI_CAPTURE_OK` | `Builds/ui-capture.log` |
| `R2_PARITY_OK` | `Builds/r2-parity.log` |

Log filenames vary per run - the anchor's gate table names the current ones. **A marker alone is not a
pass:** compare each log's mtime against HEAD's commit time, and re-run anything that predates HEAD.
`Builds/r2-parity.log` is UTF-16LE; decode it before concluding the marker is absent. Open the capture
PNGs; a compile-green build never proves a panel looks right.

---

## 3. Key files (each one verified present on 2026-09-06)

**Boot and law**
- `START_HERE.md` - the single entry point; it routes, it does not substitute for what it points at
- `CLI_SESSION_PLAYBOOK.md` - the session executed step by step, with a receipt at each step
- `SAMANTHA.md` - the boot-confirmation gate: verify with evidence, report, wait
- `PREFLIGHT_GATE.md` - Gates A / B / C
- `CLAUDE.md` - the binding rules; every rule in section 1 above points into it
- `KEY_FACTS.md` - the living fact sheet and north-star state

**Procedure and access**
- `docs/CLI_OPERATIONS_RUNBOOK.md` - startup, seat model, board, every gate command and marker, builds,
  R2, Firebase, Vercel, the DB, F8, commit and push discipline. CLAUDE.md is the law; this is the how.
- `docs/ACCESS_AND_SECRETS.md` - what is public vs secret; read before claiming prod is unreachable
- `docs/INSTRUMENTATION_STANDARD.md` - FlowTrace/Guard authoring law
- `docs/TICKET_PIPELINE.md` and `docs/BOARD.md` - ticket lifecycle and the derived board
- `SUNDAY_HOUSEKEEPING.md` - the weekly full-sweep ritual
- `publishing/SUBMIT_CHECKLIST.md` - the store submission procedure, executed as written
- `tools/board_build.py` - regenerates `BOARD.html` from `WorkOrders/*.md`
- `tools/r2-ship.ps1` - the one sanctioned R2 push-and-verify path

**Architecture and design**
- `docs/ARCHITECTURE.md` (hub) and `docs/ARCHITECTURE_PRINCIPLES.md` (the HP B2B law)
- `docs/MASTER_CATALOG.md` and `docs/MASTER_CATALOG/<area>.md`
- `docs/COMBAT_PIVOT_NORTHSTAR.md` - the single-hero pivot; supersedes all party-of-four canon
- `docs/UI_MVVM_BINDING_MAP.md` and `docs/UI_BLINK_TEMPLATE_CANON.md` - the master-frame UI formula
- `docs/PATH_TO_V1.md`
- `docs/PROD022_TUNABLE_FLAGS.md` - the tunable contract; four sources change in the same commit

**Numbering, board, history**
- `CLI_LANES_WO_NUMBERS.md` - the sole WO-numbering authority
- `BOARD.html` - the derived board (Linear, Notion and the task list are all retired)
- `docs/HANDOVER.md` - newest block only
- `docs/GROK_MEMORY.md` - the Grok fast path
- `docs/_archive/SESSION_CANON_LOADER_history_2026-09-06.md` - this file's retired banner stack

---

## 4. What changed since the last loader (2026-09-06)

The 09-06 Sunday sweep replaced this loader's dated banner stack with pointers, and produced the
documents below. Read them at source; nothing from them is restated here.

- `CANON_GROUND_TRUTH_2026-09-06.md` - the new anchor, superseding the 09-03 one. Every number on it
  was read at source, and anything unprovable from this repo says NOT PROVEN instead of being tidied
  into a fact. Its closing lesson is the reason this loader was rewritten: **every stale line fixed
  that day was a copied number**, and the cure is deleting the copy, not improving it.
- `docs/GET_WELL_PLAN_2026-09-06.md` - **LIVE.** The sequencing document; its section 1 is the P0 list.
- `docs/READY_RCA_2026-09-06.md` - root causes behind the READY backlog, each claim with its measuring
  line.
- `docs/GROWTH_RCA_2026-09-06.md` - why nobody is arriving: the public build is far behind the tester
  build, no analytics on the landing project, a deep link as the only call to action.
- `docs/PREREQUISITE_REGISTRY_2026-09-06.md` - the unlock/prerequisite registry.
- `docs/RAID_BALANCE_AUDIT_2026-09-06.md` - the raid balance pass.
- `docs/ART_REQUEST_2026-09-06_manage_tab_portraits.md` and
  `docs/ART_DELIVERY_2026-09-06_manage_assets.md` - the Manage-tab art request and its delivery.

The anchor also records open discrepancies it names but does not resolve - see its tree-and-build and
WO-numbering sections before you trust a build stamp or mint a number.

---

> **Maintenance (CLAUDE.md section 15):** any commit that changes architecture or state updates the
> relevant canon doc in the same breath, and the newest `CANON_GROUND_TRUTH_<date>.md` is kept current.
> When you supersede an anchor, you do not re-stamp a filename into this file - there is no filename in
> it to re-stamp, and that is the point.

*Keep this file rules-and-pointers only. If you are about to write a number, a version, a count or a
branch name into it, write the path to the thing that knows instead.*
