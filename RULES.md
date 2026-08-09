# RULES — the one page. Read this, then read what it points at.

**This file is an INDEX of the binding rules. It is NOT their source.**
Every rule below is one checkable line plus a pointer to the doc that owns it. The pointer is the
authority; this page is the map. **Nothing here may be quoted as the reason a rule exists** — open
the source. If this page and a source doc ever disagree, **the source doc wins and this page is the
bug** (fix it in the same breath, §15).

Deliberately no deep content is copied here. A copied rule is a future contradiction: the copy drifts,
and then nobody knows which one is binding. That is exactly why "read the rules" needed a single target
instead of an eighth long doc.

**Scope:** binding on every seat — CLI, UI, every spawned agent, every session, forever.
*(Implements WO-938; the numbering banner is `CLI_LANES_WO_NUMBERS.md`.)*

---

## Precedence — when two sources disagree

Not invented here; each line is stated by the doc named.

1. **The owner's live ruling** beats every document.
2. **The newest `CANON_GROUND_TRUTH_<date>.md`** beats any other doc on current state — *(SESSION_CANON_LOADER.md, top banner; CLAUDE.md §15)*.
3. **The CODE beats the comments, and beats every doc, on what the software does** — *(CLAUDE.md, mandatory-first-step)*.
4. **The MARKER FILE beats any doc** on gate/suite/test results; never restate a count from prose — *(CLAUDE.md §8; docs/HANDOVER.md)*.
5. **The `CLI_LANES_WO_NUMBERS.md` banner is the SOLE work-order numbering authority** — no other file, ever — *(CLAUDE.md §2)*.
6. **`BOARD.html` is derived from `WorkOrders/*.md`**; the WO files are the data — *(docs/BOARD.md §1)*.
7. Anything else: **the source doc over this page.**

---

## ★ THE FIVE THAT GET VIOLATED MOST ★

These are not the five most important. They are the five the docs themselves record as **repeatedly
broken**. Answer yes to all five before anything else.

1. **INSTRUMENT FIRST — never inference-fix.** No code edit on a non-trivial bug until you can cite a
   CAPTURED LINE that proves the cause. Static reading locates candidates; it never concludes.
   *(CLAUDE.md §12 — the HARD GATE; memory `never-inference-fix`. Forged after 3 wasted cycles on the
   "pink floor"; one headless dump named it in a single read.)*
2. **MINT WO NUMBERS ONLY FROM THE BANNER, AND BUMP YOUR OWN ROW IN THE SAME EDIT.** Never copy a
   number out of any doc. *(CLAUDE.md §2 — five two-seat collisions in one day on 2026-08-02, including
   by the CLI. The mint written without the banner bump IS the collision.)*
3. **READ THE ALREADY-HARVESTED CAPTURE BEFORE YOU THEORISE.** F8 inbox / break-log / screenshots first
   — spawning a code-reader before reading the harvest is the banned failure. *(CLAUDE.md §14: "you have
   the answers yet choose not to look".)*
4. **COMMIT BY EXPLICIT PATH, ONE COMMITTER, NEVER `git add -A`.** The tree is shared by multiple
   sessions and agents. *(CLAUDE.md §11; memory `sole-git-committer` — two committers duel on
   `.git/index.lock` and produce false "pushed".)*
5. **UPDATE CANON IN THE SAME BREATH AS THE CHANGE.** A state change with no canon update is an
   incomplete change; if deferred, leave a dated `STALE:` flag. *(CLAUDE.md §15 — the rule that exists
   because one fleet-scale audit of 1090 files already had to happen once.)*

---

# A. Before you touch anything

6. **Answer PREFLIGHT GATE A out loud, unprompted, before your first edit of a session.** One NO or one
   unproven YES = stop. → `PREFLIGHT_GATE.md` Gate A · CLAUDE.md preflight banner
7. **Load `SESSION_CANON_LOADER.md` every session, before anything else.** → CLAUDE.md read-first
8. **Read `docs/MASTER_CATALOG.md` + the `docs/MASTER_CATALOG/<area>.md` for what you are about to
   touch.** Mandatory first step, every session. → CLAUDE.md mandatory-first-step
9. **Read the newest `CANON_GROUND_TRUTH_<date>.md` and confirm nothing there contradicts your plan.**
   → `PREFLIGHT_GATE.md` A2 · CLAUDE.md §15
10. **Be the SME BEFORE you change anything — verified from the CODE, not the comments.** No fixing,
    building, or claiming-fixed on assumptions. → CLAUDE.md mandatory-first-step
11. **Never assert a fact you have not opened at source this session.** Read-before-assert applies to
    code AND docs. → SESSION_CANON_LOADER.md (Day-1 boot) · memory `assert-only-what-you-read-at-source`
12. **Check the README index system before grepping or exploring** — `PROJECT_INDEX.md`,
    `Assets/README.md`, `Assets/_Modules/README.md`, `docs/README.md`. → CLAUDE.md Navigation
13. **Confirm the system does not already exist. Extend it; never greenfield a duplicate.**
    → `PREFLIGHT_GATE.md` A3 · memory `dont-greenfield`
14. **Delegate the deep dig to agents; your hands stay on gates and commits.** Do not solo-charge what
    an agent should go deep on. → CLAUDE.md §11 · `PREFLIGHT_GATE.md` A4
15. **Run file-disjoint lanes in parallel; one agent per shared file.** `VillageSceneBuilder.cs` is a
    serialization bottleneck — one toucher at a time. → CLAUDE.md §9
16. **Ambiguous ticket (no repro / screen / stack) bounces back for detail. Never work blind.**
    → CLAUDE.md §11

# B. Architecture law — the shape the code must take

17. **Decision lens: what is RIGHT, not what is easy. When they diverge, name the divergence out loud.**
    → `docs/ARCHITECTURE_PRINCIPLES.md` §0
18. **Bounded context per component — one job, deliberately limited scope, never reaches outside its
    lane.** → `docs/ARCHITECTURE_PRINCIPLES.md` §1
19. **Presentation is a separate layer that NEVER touches the objects.** Nothing about how a thing looks
    lives on the thing. → `docs/ARCHITECTURE_PRINCIPLES.md` §2 (the most-violated principle, per the doc)
20. **One Model: capability is a property on the entry; never hard-code per type/tag. Every system is a
    READER of the collection.** → `docs/ARCHITECTURE_PRINCIPLES.md` §2b
21. **POOL by default, and ONE pool/owner per concern.** Anything spawned more than once comes from a
    pool. → `docs/ARCHITECTURE_PRINCIPLES.md` §2b.1, §2b.2
22. **Structural work ships with the tests that prove behavior was preserved** — tests are the permission
    gate. → `docs/ARCHITECTURE_PRINCIPLES.md` §2c
23. **Queue by leverage: player-felt vs holistic. NEVER smuggle a structural refactor into player-facing
    work.** → `docs/ARCHITECTURE_PRINCIPLES.md` §3 · CLAUDE.md architecture banner
24. **Derive orientation/grip/seat/scale from mesh bounds + asset name — never a guessed Euler, never
    identity. A `manual=true` correction is canon and is never overwritten.**
    → `docs/ARCHITECTURE_PRINCIPLES.md` §4 · `docs/WEAPON_ARMOR_ORIENT_LOGIC.md`
25. **MVVM strict: the VM holds all logic/state; the View is a dumb skin that reads no game state.**
    → SESSION_CANON_LOADER.md Core Rules · `docs/UI_MVVM_BINDING_MAP.md`
26. **Behaviour changes are flag-gated.** → SESSION_CANON_LOADER.md Core Rules

# C. Writing code

27. **Write/Edit on the Windows path ONLY. Never `cat >`, `echo >>`, or any bash redirect into a `.cs`.**
    The Linux mount does not sync reliably and silently garbles files. → CLAUDE.md §0
28. **Brace-balance check every `.cs` you touched, before reporting done.** → CLAUDE.md §1
29. **No NUL bytes in any `.cs`** — the compile gate scans for them and withholds the marker.
    → CLAUDE.md §1 (WO-434)
30. **Respect the assembly boundaries: Village → Core only, HUD → Core only, never Village ↔ HUD.**
    Cross-module calls go through `CoreServices`. → CLAUDE.md §5
31. **`using DeNelle.Core.Combat;` in any file implementing `IDamageableStructure`.** → CLAUDE.md §6, §10
32. **Null-conditional (`?.`) on every cross-module service call.** → CLAUDE.md §10
33. **No new `System.Reflection` in bridge scripts.** → CLAUDE.md §10
34. **Never use UXML — it does not work in builds. UI is code-built.** → CLAUDE.md §8
35. **Write the instrumentation IN as you author the method, not after a bug** — flow entry, every branch
    taken, every fallback, every resolve, the render/commit seam.
    → `docs/INSTRUMENTATION_STANDARD.md` §2
36. **No silent failures. Every `catch` logs; every fallback is a `Warn`; every empty/skip/early-return is
    traced.** "Shows nothing, no error" must be impossible. → `docs/INSTRUMENTATION_STANDARD.md` §2 ·
    CLAUDE.md §12.2
37. **Any loop that builds a list/grid/screen from N objects uses `Guard.TryEach`** — one bad object never
    blanks a screen. → `docs/INSTRUMENTATION_STANDARD.md` §3
38. **Real failures use `Fail` (error level → break-log). Never downgrade one to `Warn` to keep the log
    clean.** → `docs/INSTRUMENTATION_STANDARD.md` §5
39. **Hot-path logs use `Throttle`/`Once` or a guarded call.** → `docs/INSTRUMENTATION_STANDARD.md` §1.3
40. **Regression code lives in the nested editor asmdef, never in a runtime assembly.**
    → `docs/INSTRUMENTATION_STANDARD.md` §4
41. **Instrument existing code ON TOUCH, never as a big-bang sweep.**
    → `docs/INSTRUMENTATION_STANDARD.md` §6
42. **One thing at a time, fully verified before the next. Deliver complete — no piecemeal.**
    → SESSION_CANON_LOADER.md Core Rules

# D. Debugging — the hard gate (most-violated; give it weight)

43. **Answer PREFLIGHT GATE B the moment a bug appears.** → `PREFLIGHT_GATE.md` Gate B
44. **No code edit until CAPTURED DATA proves the cause.** Instrument → run (prefer headless) → read the
    trace → fix the step the data names. → CLAUDE.md §12 ★
45. **Static code-reading LOCATES candidates; it NEVER CONCLUDES the cause.** An inferred root is a guess.
    → CLAUDE.md §12
46. **Instrumenting is the OPENING move, unprompted — not a fallback after a guess fails.**
    → CLAUDE.md §12
47. **Read the already-harvested F8 capture FIRST** — before any code-read, any agent, any theory.
    → CLAUDE.md §14
48. **Cite the exact proving line.** If you cannot point at it, you have not earned the edit.
    → `PREFLIGHT_GATE.md` B9 · memory `never-inference-fix`
49. **Split every "shows nothing" into data-empty vs built-but-invisible vs threw-and-skipped from the
    trace, before touching code.** → CLAUDE.md §12.3 · `PREFLIGHT_GATE.md` B10
50. **Prefer headless capture to self-serve** — AutoPilot fleet, `break-log.jsonl`, on-load dumps —
    before asking the owner to retest. → CLAUDE.md §12.4
51. **Two failed fix attempts on the same issue → STOP and escalate with logs. Do not solo-iterate a
    third time.** → `PREFLIGHT_GATE.md` B11 · memory `two-failure-escalate-to-grok`
52. **Every RCA hand-off carries a PROOF section: the verbatim captured line, its source, and what it
    proves.** Never a narrative-only RCA. → `docs/TICKET_PIPELINE.md` §0
53. **Keep the F8 watcher running and poll its inbox every turn; ack after triage.** The owner is NEVER
    the bug detector. → CLAUDE.md §14
54. **"Works on my machine" / "it doesn't stop the demo" / "probably just noise" are BANNED answers.**
    → `docs/ARCHITECTURE_PRINCIPLES.md` §5
55. **Verify proactively — audit the flow before you are asked and before you call it done.**
    → `docs/ARCHITECTURE_PRINCIPLES.md` §5

# E. Scenes, assets + naming canon

56. **NEVER hand-edit a curated `.unity` scene.** Rebuild through its builder. → CLAUDE.md §3 ·
    memory `owner-prefs-scenes`, `dungeon-scene-shared-tree-corruption`
57. **Never run a bake while the Unity editor is open** (project lock). → CLAUDE.md §3
58. **UI does not fire batchmode. Bake/build commands go to CLI in a work order.** → CLAUDE.md §3
59. **Polyperfect: use the `_M` quality tier only, check `docs/polyperfect-asset-catalog.md` before
    naming a prefab, and `LogWarning` (never error) on a missing one** — the pack is gitignored.
    → CLAUDE.md §4
60. **Use the canon names.** Elarion (never "Avalon"); hero tag `Player`; home hub
    `Main_Castle_Overworld`; player-facing strings from `canon-strings.json`. → CLAUDE.md §7
61. **Enemy AI finds the hero by component, not by a tag.** → CLAUDE.md §7

# F. Work orders + the board

62. **Take every new WO number from the `CLI_LANES_WO_NUMBERS.md` banner — the SOLE authority — and bump
    your seat's own banner row IN THE SAME EDIT as the mint.** → CLAUDE.md §2 ★
63. **Never copy a WO number out of any other doc** (not the filesystem max, not a backlog doc, not a
    handover). Every copy goes stale. → CLAUDE.md §2 · docs/HANDOVER.md
64. **The two seats mint from DISJOINT blocks; collisions resolve first-on-disk-and-referenced-wins.**
    → CLAUDE.md §2 · the banner's block table (**read the block ranges off the banner — see Conflict C-1**)
65. **Save work orders to `WorkOrders/WORK_ORDER_NNN_short_name.md` with files-to-edit, acceptance
    criteria, and what NOT to touch; mark `Status: READY TO IMPLEMENT` when the spec is complete.**
    → CLAUDE.md §2
66. **The `**Status:**` line must contain one canonical keyword.** `Unlabeled` is a defect in the WO file,
    not a category. → `docs/BOARD.md` §3
67. **Flip the status line AND write the `.RESULT.md` in the SAME COMMIT as the work.** A deferred flip is
    a board that lies until later. → `docs/BOARD.md` §2 · CLAUDE.md §2
68. **A RESULT file is written by the seat that verified the work — never fabricated to clear debt.**
    → docs/HANDOVER.md
69. **Regenerate `BOARD.html` (`python tools/board_build.py`) at session boot and before any board read.
    Never hand-edit it** — it is generated output. → `docs/BOARD.md` §2
70. **Never mirror to Notion — no writes, no reads — whatever older docs say.** → `docs/BOARD.md` §2 ·
    CLAUDE.md §2 · memory `notion-retired-board-is-derived`
71. **A new status keyword requires editing `tools/board_build.py` AND `docs/BOARD.md` §3 in the same
    commit.** → `docs/BOARD.md` §4

# G. Committing

72. **There is exactly ONE committer (the CLI/lead seat). No second session or agent commits or pushes.**
    → CLAUDE.md §11 · `docs/TICKET_PIPELINE.md` §7
73. **Stage by EXPLICIT PATH. Never `git add -A`. Never blind-replace a file.** → CLAUDE.md §11 ★
74. **Review every diff before staging — the tree is shared, and mount-garble is a live risk (§0).**
    → CLAUDE.md §11
75. **Commit local; PUSH ONLY after the owner felt-verifies, or a regression proves it.**
    → CLAUDE.md §11 · `PREFLIGHT_GATE.md` C15 · `docs/TICKET_PIPELINE.md` §7
76. **Other sessions write and signal "ready"; the one committer reconciles by path.** → CLAUDE.md §11

# H. Gates

77. **Answer PREFLIGHT GATE C before you say DONE.** → `PREFLIGHT_GATE.md` Gate C
78. **Pre-ship gates are `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` + `UI_CAPTURE_OK` — and you
    OPEN the PNGs.** → CLAUDE.md §8
79. **The MARKER is the evidence, not the exit code.** A batchmode run can exit 0 on a refusal or a FAIL —
    verify the marker, the log's freshness, and its size. → CLAUDE.md §8 · docs/HANDOVER.md ·
    memory `gates-report-success-without-proving-it`
80. **The three entry points emit DISTINCT markers** — `DataRegression.RunAll` → `REGRESSION_OK`,
    `RegressionSuite.RunAll` → `CHECKIN_SUITE_OK`, `SessionRegression.RunAll` → `SESSION_GUARDS_OK`.
    Do not read one as another. → CLAUDE.md §8
81. **NEVER restate a suite/test count from a doc — read it off the marker file, and check its date.**
    → CLAUDE.md §8 · docs/HANDOVER.md
82. **Never claim fixed on faith. Prove it with captured data, a headless run, or a regression.**
    → `PREFLIGHT_GATE.md` C13 · `docs/TICKET_PIPELINE.md` §5
83. **Headless markers cannot see geometry, orientation, or feel — that class of defect needs eyes**
    (UI capture, device screencap, or the owner). → docs/HANDOVER.md 2026-08-09 ·
    memory `headless-screenshot-verify-ui-before-build`

# I. Roles

84. **UI NEVER writes or edits `.cs` — no exceptions.** It does RCA, specs/work orders, narrative,
    mockups, board grooming. Code it wants goes to CLI as a spec. → CLAUDE.md §2
85. **CLI writes and build-verifies ALL code, owns batchmode, and is the sole git committer.**
    → CLAUDE.md §2 · `docs/TICKET_PIPELINE.md` §3
86. **The owner is the PO: she routes, felt-verifies after deploy, and CLOSES. CLI does not close.**
    → `docs/TICKET_PIPELINE.md` §1, §6
87. **QA triage is READ-ONLY: never edits, never gates, never commits.** → `docs/TICKET_PIPELINE.md` §2
88. **QA classifies NEW FEATURE vs EXISTING before any fix.** A not-yet-built function goes back to PO as
    a spec — never RCA-"fixed". → `docs/TICKET_PIPELINE.md` §2, §3
89. **Role separation is non-negotiable: QA doesn't write, CLI doesn't classify-triage, PO closes.**
    → CLAUDE.md §13 · `docs/TICKET_PIPELINE.md` §1
90. **Log every hand-off** (who → who, why) on the ticket. → `docs/TICKET_PIPELINE.md` §4
91. **The lead session is the ORCHESTRATOR: triage flow-first, fan out focused single-task agents in
    parallel, batch-gate once, commit by lane.** → CLAUDE.md §11
92. **Propagate this methodology to every agent you spawn, every session.** → CLAUDE.md §12
93. **Give the real architectural read — the why, the tradeoff, the failure mode — and let the owner
    decide. Never quietly pick easy and present it as the answer.**
    → `docs/ARCHITECTURE_PRINCIPLES.md` §0

# J. Canon maintenance

94. **Any change to architecture/state/canon updates the load-bearing doc in the SAME commit — or gets a
    one-line dated `STALE:` flag naming what is now wrong.** → CLAUDE.md §15 ★
95. **Keep exactly ONE current `CANON_GROUND_TRUTH_<date>.md`; supersede the old one by date.**
    → CLAUDE.md §15
96. **Keep the load-bearing set green:** `SESSION_CANON_LOADER.md`, `docs/HANDOVER.md`,
    `PIPELINE_STATE.md`, `docs/MASTER_CATALOG.md`, `PROJECT_INDEX.md`, the relevant `docs/*ARCHITECTURE*`,
    `CLAUDE.md`. → CLAUDE.md §15
97. **Dated point-in-time ledgers are FROZEN: banner them `⚠ SUPERSEDED <date>`, never rewrite the body.**
    → CLAUDE.md §15
98. **An undated WO asserting current state is STALE — date it or banner it.** → CLAUDE.md §15
99. **Weekly 5-minute audit: skim the load-bearing set against the anchor; fix or flag.**
    → CLAUDE.md §15 · `SUNDAY_HOUSEKEEPING.md`
100. **Never guess in a doc.** Canon updates are sourced from HEAD / the working tree / verified captures —
     §12 discipline applies to documentation too. → CLAUDE.md §15
101. **Keep the README index system and `docs/MASTER_CATALOG.md` current when you add, move, or change
     systems.** → CLAUDE.md Navigation, mandatory-first-step
102. **Never say "I'll mark it" — write the memory AND the doc in the moment.**
     → SESSION_CANON_LOADER.md Day-1 boot

---

## ⚠ CONFLICTS BETWEEN SOURCES — open, needing an owner ruling

Found while indexing. **No winner is picked here.** Each is a place where two binding docs say different
things, so a seat following one is provably breaking the other.

**C-1 — WO numbering: which block belongs to the UI seat.**
`CLAUDE.md` §2 states two blocks: "main line → CLI" and "**860–899** reserved → the UI seat."
`CLI_LANES_WO_NUMBERS.md` records an **owner ruling dated 2026-08-07** that **860–899 is CLOSED (full at
899)** and the UI seat moved to **1000–1099** (banner table: UI seat next free 1012). Both docs are
binding; they name different ranges.
*Note:* CLAUDE.md itself names the banner the SOLE authority, which arguably self-resolves the operative
number — but §2's text is stale and is exactly the kind of copied number that caused the 08-02 collisions.
**Owner call: correct CLAUDE.md §2 to point at the banner without restating a range.**

**C-2 — Which board is "the shared board".** Three answers live in binding docs:
`CLAUDE.md` §2 (Completing work orders) says "**UI marks the matching Linear issue as Done**";
`CLAUDE.md` §2 (board paragraph) + `docs/BOARD.md` say the board is **`BOARD.html`, derived from
`WorkOrders/*.md`**, with Notion retired; `CLAUDE.md` §13 + `docs/TICKET_PIPELINE.md` say the shared board
is **the Task list**. Linear predates both retirements (history: Linear → Notion → derived board), so the
Linear line reads as dead text — but it is still in a binding section. Whether the *ticket* board (Task
list) and the *work-order* board (BOARD.html) are one board or two is never stated.
**Owner call: delete the Linear line, and state whether tickets and WOs share one board.**

**C-3 — The project's Windows path.** `CLAUDE.md` §0 says the project's home is **`C:\EoA\`**;
`PREFLIGHT_GATE.md` B11 says to write escalation logs to **`C:\eoa\logs\debug\`**. On this machine
`C:\EoA` **does not exist** — the repo is **`D:\eoa`** (and `D:\eoa\logs\debug` exists). Both doc paths are
stale, which makes §0's mount-vs-Windows rule read against a path that isn't there.
**Owner call: repoint both to `D:\eoa`.**

**C-4 — CLAUDE.md §3 names a scene CLAUDE.md §7 says is deleted.** §3: "NEVER hand-edit `Village.unity` —
always rebuild via `Defenders > Week 3 > Build Village Scene` / `VillageSceneBuilder.BuildVillage`." §7:
"`Village.unity` + `OuterWorld.unity` are **DELETED** from the tree." The general rule (never hand-edit a
`.unity`; rebuild through its builder) is unaffected, but the named scene and menu path are stale and there
is no stated builder for the scenes that replaced it.
**Owner call: restate §3 generically and name the current builders.**

**C-5 — What to strip when a system stabilises.** `CLAUDE.md` §12 closes with "Set
`FlowTrace.Enabled=false` (or strip calls) once a system is proven stable."
`docs/INSTRUMENTATION_STANDARD.md` §1.4 is narrower: on graduation, mute/strip the **`Step` breadcrumbs
only** and **KEEP every `Warn`/`Fail` and every `Guard`** — those are the permanent no-silent-failure net,
not scaffolding. A seat reading §12 alone can legitimately strip the net.
**Owner call: point §12's closing line at §1.4 instead of restating it.**

---

*Maintained per CLAUDE.md §15: if you change a rule at its source, fix the one line here in the same
commit. If you find yourself pasting explanation into this file, stop — that content belongs in the
source doc, and the copy is the drift.*
