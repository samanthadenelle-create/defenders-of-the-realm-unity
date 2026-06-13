# Instrumentation Standard — write it observable-first

**Status:** BINDING. Operationalizes `CLAUDE.md §12` (the *rule*: instrument, don't guess)
and `docs/ARCHITECTURE_PRINCIPLES.md` (the *lens*). This doc is the *method* — how you
write instrumented code from the first line.

**Scope:** every new method in gameplay / service / data code. The rule:
**a failure must be a logged line, never a silent blank.** You do not add instrumentation
*after* a bug — you write it in, then toggle or strip it later. Owner framing:
*"always easy to have a helper clean it up later, or leave it in and turn on/off as needed."*

This standard introduces **no new helpers**. It is the authoring discipline for the four
that already exist:
- `Assets/_Modules/Core/Diagnostics/FlowTrace.cs` — `Step/Warn/Fail/Throttle/Once/Measure`, `[Flow:<system>]` tags, runtime `Enabled` + per-category gating.
- `Assets/_Modules/Core/Diagnostics/Guard.cs` — error factory: `Try`/`TryEach`. The always-on safety net.
- `Assets/_Modules/Core/Diagnostics/BreakCaptureHarness.cs` — F8 flight recorder → `break-log.jsonl` + screenshots.
- `Assets/Editor/Regression/DataRegression.cs` — headless "real object in, real response out" regression.

---

## 1. Toggle & lifecycle architecture

Three independent controls, smallest blast radius first.

### 1.1 Runtime master switch
`FlowTrace.Enabled` — global on/off, flippable at runtime / dev panel. Leave **on** while a
system stabilises; flip **off** when proven stable. Short-circuits every call before the string.

### 1.2 Per-system granularity
When stabilising "Store" you don't want "Seam"/"Enemy"/"Roster" flooding the log. The
category is the existing first arg, so **no call site changes** — control it at the console / dev panel:

```csharp
FlowTrace.Enabled = true;            // master
FlowTrace.Only("Store", "Seam");     // allow-list: mute everything else
FlowTrace.Mute("Enemy");             // or deny just the noisy one
FlowTrace.AllOn();                    // clear filters
```

The gate is O(1) (a `null` allow-list means "all"). Default state = all categories on,
so this never changes shipped behaviour.

### 1.3 Zero-cost-when-off — the hybrid rule
Two mechanisms at different stages:

- **Runtime toggle** (`Enabled` / category filter) — for **dev iteration**. Note: in C# the
  interpolated string is built *before* the call, so a hot-path log still allocates even when
  disabled. **Rule:** in `Update()` / per-mob / per-frame loops, use `Throttle` (guards
  internally, ~1/sec) or guard the call yourself:
  ```csharp
  if (FlowTrace.Enabled) FlowTrace.Step("Seam", $"dist={Vector3.Distance(a,b):F2}");
  ```
  For cold paths (entry / branch / fallback — a handful of hits) do **not** guard; the
  allocation is irrelevant and the guard hurts readability.

- **Compile-strip** (`[Conditional("ENABLE_FLOWTRACE")]`) — for the **ship build** (WebGL/mobile).
  When the define is absent the compiler removes the call site *and its string args* at zero
  runtime cost. **PENDING — not yet enabled** (see §1.6); flip it on only after a debugging
  cycle's data is harvested, and only paired with the Guard decoupling already in place.

  | Build | `ENABLE_FLOWTRACE` | FlowTrace |
  |---|---|---|
  | Editor / dev iteration | defined | compiled in; runtime toggle + category filter govern |
  | Desktop playtest (.exe) | defined | same; feeds `break-log.jsonl` via BreakCaptureHarness |
  | WebGL / mobile ship | not defined | every `FlowTrace.*` call stripped at compile |

  `Guard.Try` is **never** `[Conditional]` — it changes control flow (the safety net) and
  always runs. Only the pure-logging `FlowTrace` entry points are strippable. `Measure`
  returns a `Scope` (non-void), so it can't be `[Conditional]`; it stays on the runtime guard.

### 1.4 The strip path (clean it up later)
- Every line is `[Flow:<system>]`. To audit/strip a stabilised system:
  `Grep "FlowTrace\.\w+\(\"Store\""` → every Store trace in one query.
- **One-folder delete:** the whole diagnostic layer is `Assets/_Modules/Core/Diagnostics/`.
  Static, `DeNelle.Core`-local, no cross-module coupling to unwind.
- **Promotion rule — "proven stable":** a system graduates when (a) its headless
  `DataRegression` check is green, and (b) it survives owner F8 playtests with no new
  `[BREAK]` / `[Flow:*] *FAILED` lines for that system. On graduation: **mute/strip the
  `Step` breadcrumbs**, but **keep every `Warn`/`Fail` and every `Guard`** — those are the
  permanent no-silent-failure net, not scaffolding.

### 1.5 Mobile / WebGL / perf
Mirror `BreakCaptureHarness`'s own rule (it disables itself on WebGL):
- **Ships to players:** `Guard` (control-flow safety) + the `[BREAK]` console line +
  `EventTracker` telemetry. Silent-failure insurance — must survive.
- **Editor / dev-build only:** all `FlowTrace.Step/Throttle/Once/Measure` verbosity, file +
  screenshot capture, the F8 flag UI.

### 1.6 Current toggle state (keep this current)
- `FlowTrace.Enabled = true`, all categories on. Traces compiled in everywhere.
- `Guard.Report` logs **error-level directly** (decoupled from the strippable `FlowTrace.Fail`),
  so guard failures survive even once FlowTrace is compile-stripped.
- `[Conditional("ENABLE_FLOWTRACE")]` is **NOT yet applied** — applying it without defining
  `ENABLE_FLOWTRACE` in the editor + desktop dev configs would silence all FlowTrace output
  immediately. Enable as a deliberate step (add the attribute **and** the per-platform define
  together) once active debugging on the current systems is done.

---

## 2. Where to instrument when writing — the authoring checklist

Bake these in as you write the method, not after a bug. **Canonical trace points:**

1. **Flow entry** — `Step` once you're in a meaningful flow.
2. **Each decision branch** — `Step`/`Warn` on the branch *taken* (log the path, not the possibilities).
3. **Every fallback / default** — `Warn`. A fallback means "the data wasn't what I expected." Never silent.
4. **Resource / service resolution** — `Step` on resolve; `Warn`/`Fail` on null/missing.
5. **Render / commit** — `Step` at the point the result is handed to presentation (count built / "committed N to HUD"). This is the split point for *data-empty vs built-but-invisible*.
6. **Perf-sensitive blocks** — `using var t = FlowTrace.Measure("Sys","what", warnAboveMs:16f)`.

**No-silent-failure rule (non-negotiable):** every `catch` logs; every fallback is a `Warn`;
every empty/skip/early-return is traced. "Shows nothing, no error" must be impossible.

### Before / after

Before (silent — the bug you can't see):
```csharp
void PopulateStore(VendorDef vendor)
{
    var rows = new List<RowView>();
    foreach (var def in GearCatalog.AllWeapons())
        rows.Add(BuildRow(def));   // throws on def #3 -> whole list lost, no log
    _hud.ShowRows(rows);           // shows blank; you guess for an hour
}
```

After (written to standard):
```csharp
void PopulateStore(VendorDef vendor)
{
    FlowTrace.Step("Store", $"PopulateStore vendor='{vendor?.id ?? "<null>"}'");          // (1)
    if (vendor == null) { FlowTrace.Warn("Store", "null vendor -> empty store"); return; } // (3)+(6)

    var weapons = GearCatalog.AllWeapons();
    if (weapons.Count == 0)
        FlowTrace.Warn("Store", "catalog returned 0 weapons (data-empty, not a render bug)"); // (6) the split

    var rows = new List<RowView>();
    using (FlowTrace.Measure("Store", "build rows", warnAboveMs: 8f))                       // (6)
    {
        var r = Guard.TryEach("Store", "build weapon row", weapons,                         // (2)+(3)
            def => rows.Add(BuildRow(def)));
        FlowTrace.Step("Store", $"built {r.built} rows, {r.failed} failed");                // (5)
    }
    _hud.ShowRows(rows);   // HUD reads state; Store never touches HUD internals (ARCH §2)
}
```

One F8 run on the "after" version tells you exactly which failure class hit —
data-empty, built-but-invisible, or threw-and-skipped — with no second guess.

---

## 3. Guard usage standard

| Situation | Use |
|---|---|
| "Run this; if it throws, log + carry on" (void) | `Guard.Try("Sys","what", () => …)` → `bool ok` |
| Need a value, safe default on throw | `Guard.Try("Sys","what", () => Parse(json), fallback: null)` |
| **Building a list/grid/screen from N objects** | `Guard.TryEach("Sys","what", items, perItem)` → `(built, failed)` |
| Must **branch on exception type**, or do **cleanup** (dispose / rollback / release a pooled object) | a real `try/catch` — and the `catch` **must** call `FlowTrace.Fail` |

**Load-bearing rule:** *one bad object must never blank a whole list or screen.* Any loop
that builds presentation from a collection uses `TryEach`, full stop.

Hand `try/catch` — the catch is never empty:
```csharp
try { stream = Open(path); Parse(stream); }
catch (FileNotFoundException) { FlowTrace.Warn("Save", $"no save at {path} -> new game"); } // branch
catch (Exception ex) { FlowTrace.Fail("Save", $"load failed: {ex.Message}"); throw; }         // log then rethrow
finally { stream?.Dispose(); }                                                                 // cleanup = why not Guard
```

---

## 4. Regression arm — generalize DataRegression

`DataRegression.RunAll` is the template: **real object in → assert real response → one marker.**

```csharp
public static void RunAll()
{
    var failures = new List<string>();
    SomeCatalog.Reload();                                   // fresh read through the REAL game path
    var objects = new List<Foo>(SomeCatalog.All());
    if (objects.Count == 0) failures.Add("Foo deserialized to 0 (mapping break)");
    foreach (var f in objects)
        if (string.IsNullOrEmpty(f?.id)) failures.Add($"Foo '{f}' missing id (blank row)");
    if (failures.Count == 0) Debug.Log("REGRESSION_OK");
    else Debug.LogError($"REGRESSION_FAIL: {failures.Count}\n - " + string.Join("\n - ", failures));
}
```

Run headless: `run-unity-method.ps1 -Method DeNelle.Editor.DataRegression.RunAll -LogName data-regression.log`.
`REGRESSION_FAIL` uses `Debug.LogError` **on purpose** so it also lands in `break-log.jsonl`.

**Headless vs play-mode/F8:**
- **Headless `DataRegression`** — anything decidable from **data + logic**: catalog mapping,
  capability composition, service resolution, save round-trip, pricing. Self-serve before
  asking for a retest (§12.4).
- **Play-mode / owner F8** — needs the running scene, physics, rendering, input, or
  **subjective** judgment ("feels off", "ugly", wrong placement). That's what F8 flagging is for.

**Asmdef boundary:** regression code lives in its own nested editor asmdef
(`Assets/Editor/Regression/DeNelle.EditorRegression.asmdef`, `includePlatforms: ["Editor"]`)
that **references** the runtime assemblies but is **referenced by none** — exercises real game
code, ships in no build, respects the `DeNelle.Editor` no-Village reflection boundary. New
regressions go here; never put regression code in a runtime assembly.

**Gate:** the `REGRESSION_OK` marker is a pre-commit / CI-able gate — fail the commit on
`REGRESSION_FAIL`. The cheapest behavior-preserving check that runs without the editor open.

---

## 5. Conventions

- **System tag:** short PascalCase noun for a bounded context — `Store`, `Seam`, `Enemy`,
  `Roster`, `Save`, `Hud`, `Wave`. One tag per bounded context; reuse existing tags
  (don't invent `StoreUI` when `Store` exists).
- **Message format:** `<verb/what> <key=value …>`. Lead with the action, follow with the
  discriminating state: `"PopulateStore vendor='general' rows=7"`. Render nulls as `<null>`.
- **Log-level mapping (do not deviate):**

  | Call | Unity level | Why |
  |---|---|---|
  | `Step` | `Debug.Log` | breadcrumb; not a problem |
  | `Warn` | `Debug.LogWarning` | fallback / anomaly; a soft problem |
  | `Fail` | `Debug.LogError` | **error level → caught by `BreakCaptureHarness` → `break-log.jsonl` + screenshot** |

- **Never downgrade a true failure to `Warn`** to "keep the log clean" — that hides it from
  the flight recorder. Real failures use `Fail`.

---

## 6. Adoption

- **New code → write-to-standard.** The §2 checklist is part of authoring; a method missing
  its flow/branch/fallback traces and `Guard` on risky ops is incomplete, like a missing null check.
- **Existing code → instrument-on-touch, NOT a big-bang sweep** (ARCH §3: queue by leverage,
  no smuggled refactors). Bring a method to standard *as part of* the change that touches it.
  A dedicated "instrument system X" task is allowed only when X is the actively-failing system (§12).

**PR / authoring checklist (paste into PR template):**
- [ ] Flow entry, every branch taken, and every fallback are traced (`Step`/`Warn`).
- [ ] No silent `catch`; every empty/skip path is traced.
- [ ] List/screen population uses `Guard.TryEach`.
- [ ] Real failures use `Fail` (error-level → break-log), not downgraded to `Warn`.
- [ ] Hot-path logs are `Throttle`/`Once` or guarded against string-building.
- [ ] Tags reuse existing system names; messages carry the discriminating value.
- [ ] If data/logic-decidable, a `DataRegression` check exists/updated (`REGRESSION_OK`).
- [ ] Presentation reads state; instrumentation did not push the object to touch the HUD (ARCH §2).

---

## 7. Architecture fit (HP B2B)

- **Presentation stays separate (ARCH §2):** trace point (5) logs on the *producer* side at
  the seam ("built N rows, committed to HUD") — it does not make the object reach into HUD
  internals. `FlowTrace`/`Guard` are `DeNelle.Core` statics, callable from any layer with no
  reference edge (same as `BreakCaptureHarness`).
- **No smuggled refactors (ARCH §3):** §6 splits new-code (authoring) from existing-code
  (on-touch only), keeping the standard inside the leverage rule.
- **What's right, not easy (ARCH §0):** the `[Conditional]` compile-strip changes shipped
  behaviour; it is the *right* path (true zero-cost on ship) but is held until a debugging
  cycle is done and is paired with the `Guard` decoupling so the safety net survives. Owner
  flips it with eyes open (§1.6).
