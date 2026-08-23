# WORK ORDER 1038 — FIVE undeclared tags are used in code; each is a live `UnityException`

**Status:** FIXED — AWAITING OWNER FELT-TEST TO CLOSE. Prior status: DONE — audit-verified as shipped (2026-08-21 backlog audit).
**Minted:** 2026-08-16 (UI seat) — provenance stack bumped 1038 → 1039 in the same edit
**Lane:** Project settings + tag call sites + a new regression. ⚠ Touches `ProjectSettings/TagManager.asset`.
**Priority:** **HIGH** — one is crashing in the owner's live session; the others are latent crashes
waiting for their code path to run.
**Provenance:** F8 **seq=2434 / 2435**, `Main_Castle_Overworld`:
`[Flow:Progression] spawn plans drop FAILED: UnityException: Tag: SpawnPoint is not defined.`

---

## 1. The live crash

```
UnityException: Tag: SpawnPoint is not defined.
  GameObject.FindGameObjectsWithTag(...)
  CastleDefensePlansService.ResolveGateSeat   :183
  CastleDefensePlansService.SpawnDrop         :132
  CastleDefensePlansService.Update            :123   (inside Guard.Try)
```

`Guard.Try` caught it, so it logged instead of taking down the frame — §12 instrumentation doing
exactly its job. But the drop **failed**: `ResolveGateSeat` never resolves, so `SpawnDrop` cannot place
the castle-defense-plans reward. **A progression reward is silently not being delivered.**

## 2. The audit — this is not one bug, it is a class

**`ProjectSettings/TagManager.asset` declares exactly FOUR tags:**

```
tags:
  - Tower
  - Building
  - HeartTarget
  - Player
```

**Every tag literal used across `Assets/_Modules`:**

| tag | uses | declared? | status |
|---|---|---|---|
| `Player` | **116** | ✅ | fine — canon §7's one-tag-per-GameObject hero tag |
| `HeroTarget` | **13** | ❌ **NO** | ⛔ **13 live exception sites** |
| `Tower` | 4 | ✅ | fine |
| `Building` | 4 | ✅ | fine |
| `SpawnPoint` | 1 | ❌ **NO** | ⛔ **the crash in §1** |
| `ScreenFlash` | 1 | ❌ **NO** | ⛔ latent |
| `Pet` | 1 | ❌ **NO** | ⛔ latent |
| `Enemy` | 1 | ❌ **NO** | ⛔ latent — ⚠ **`Enemy` is a LAYER, not a tag** (see the `layers:` block). Classic tag/layer confusion; the fix is probably a layer mask, not a new tag |
| `MainCamera` | 1 | ✅ | Unity built-in |
| `HeartTarget` | **0** | ✅ declared | ⚠ **declared but never used** — dead tag |

⚠ **`FindWithTag`, `FindGameObjectsWithTag` AND `CompareTag` all THROW on an undeclared tag** — they do
not return null or false. So each of these is an exception, not a quiet miss. Only `Guard.Try` coverage
is currently keeping them from being visible crashes; an uncovered site takes the frame.

## 3. ⛔ CANON DEFECT — `CLAUDE.md` §7 asserts a tag that does not exist

`CLAUDE.md` §7 currently reads:

> *"Enemy spawn tags: **`SpawnPoint`** — placed 12m outside each gate"*

**That is false.** The tag is not declared, so any code trusting §7 writes a call that throws — which is
exactly what `CastleDefensePlansService` did.

⚠ **And §7 ALREADY RECORDS THIS EXACT FAILURE ONCE:**

> *"Enemy AI finds the hero by **component** (`FindFirstObjectByType<HeroLocomotion>()`), NOT a
> `HeroTarget` tag — **that tag was never declared**"*

So canon knows `HeroTarget` was never declared **and there are still 13 call sites using it.** The
lesson was written down and the code was never swept. This ticket is the sweep.

**Required: correct `CLAUDE.md` §7 in the same commit** (§15 — a state change with no canon update is
an incomplete change). It must state what is actually declared, and the rule that follows from it.

## 4. The fix — per tag, decide DECLARE or REPLACE. Do not blanket-add five tags

For each undeclared tag, the right answer differs:

| tag | recommended |
|---|---|
| `SpawnPoint` | **Declare it** if spawn seats are genuinely authored objects in the scene — canon §7 says they are. ⚠ **Verify seats actually exist and carry it**; declaring a tag nothing wears makes `FindGameObjectsWithTag` return empty, turning a loud crash into a **silent** no-reward, which is worse |
| `HeroTarget` (×13) | **REPLACE with the component lookup** canon §7 already prescribes — `FindFirstObjectByType<HeroLocomotion>()`. Canon ruled this; the code never followed. ⚠ Do **not** declare `HeroTarget` — a GameObject has one tag and the hero's is `Player` (§7) |
| `Enemy` | **Almost certainly a LAYER mask**, not a tag — `Enemy` exists in `layers:`. Read the call site and fix the mechanism, don't add a duplicate tag |
| `Pet` / `ScreenFlash` | Read each site: declare if a real authored marker, otherwise replace with a component/direct reference |

⚠ **Prefer component lookups over tags generally** — canon §7's `HeroTarget` ruling is precedent: a
GameObject has exactly **one** tag, so tags do not compose and cannot express two roles at once. Every
tag added is a future collision.

## 4b. ⚠ FOLD IN — the error fires from `Update()` and is FLOODING the F8 queue

`CastleDefensePlansService.Update():123` retries every frame it is eligible, so the throw repeats
forever. Measured 2026-08-16: **42 identical captures in a single drain window**, then 2 more within
minutes (seq 2482/2483). It will bury every other signal for the rest of the owner's session — exactly
how the WO-1022 GUID flood buried two `STEP-STUCK` lines and the repair-surface capture for a full day.

**Fix the flood as well as the cause:** the failure is a **permanent, unchanging condition** (a tag
either exists or does not), so re-reporting it per frame adds zero information after the first.

- Use **`FlowTrace.Once`** for a condition that cannot change at runtime, or **`FlowTrace.Throttle`**
  (~1/sec) if the site must keep retrying — both are the §12-sanctioned helpers for hot loops
- ⛔ **Do NOT silence it and do NOT remove the `Guard.Try`** — §12: instrumentation is permanent. The
  goal is **one loud report**, not zero
- Also consider whether the service should **stop retrying** once it has proven the tag is undefined —
  a per-frame retry of an impossible lookup is a real (if small) cost on top of the noise

### ★ THIS IS THE THIRD INSTANCE OF ONE PATTERN THIS SESSION — worth a standing rule

| # | site | nature |
|---|---|---|
| 1 | `[Flow:MagentaGuard]` (WO-1022 §6) | guard **SUCCEEDS** at hiding a placeholder, logs at **error**, 16 captures |
| 2 | `HeartAuraController.DescribeParticle` (WO-1025 §2c) | audit reads `mat.mainTexture` unguarded → Unity **error**, 4 per scene load |
| 3 | **this** | permanent condition re-reported every frame, **42+ captures** |

**The rule these three imply:** *error severity is a budget spent on the owner's attention.* An
instrument may log freely, but **F8 captures on error**, so anything at error severity in a hot loop or
on a succeeding path is spending that budget on noise — and the cost lands as **buried real signals**,
which is measurable damage, not a style nit.

**Recommend adding this to `docs/INSTRUMENTATION_STANDARD.md`** (the *method* doc that §12 points at):
error severity is reserved for a **new, actionable, non-repeating** condition; use `Once`/`Throttle`
for anything recurrent, and `Warn` for a guard that succeeded. ⚠ File that as its own small ticket if
the owner agrees — do **not** silently widen this WO into a canon edit.

## 5. The oracle — this is what stops it recurring

**A regression that fails when any tag literal used in code is not declared in `TagManager.asset`.**

- Scan `Assets/**/*.cs` for `FindWithTag` / `FindGameObjectsWithTag` / `FindGameObjectWithTag` /
  `CompareTag` string literals
- Assert each is in `TagManager.asset` **or** is a Unity built-in (`Untagged`, `Respawn`, `Finish`,
  `EditorOnly`, `MainCamera`, `Player`, `GameController`)
- Optionally warn on declared-but-unused (`HeartTarget`)

⚠ **No existing gate catches this.** `COMPILE_GATE_OK` cannot — tag names are runtime strings, so this
compiles clean and fails only when the code path runs. That is precisely why `HeroTarget` survived from
a written-down canon ruling to 13 live sites. **A compile gate will never find the sixth one; this
regression will.**

## 6. Acceptance criteria

- [ ] `[Flow:Progression] spawn plans drop FAILED` no longer fires; the drop **actually places**
- [ ] All five undeclared tags resolved per §4, each decision recorded in the RESULT
- [ ] Zero `UnityException: Tag ... is not defined` across a full headless run
- [ ] The §5 regression exists and **FAILS on a deliberately-introduced bad tag** — prove the red, then
      restore
- [ ] `CLAUDE.md` §7 corrected in the same commit (§3)
- [ ] ⚠ If `SpawnPoint` is declared: seats verified to **exist and wear it** — no silent-empty result
- [ ] `Guard.Try` coverage left intact — §12; it is what surfaced this

## 7. Verify

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` including the new case
2. Headless run of `Main_Castle_Overworld` through a wave + a plans drop — grep for
   `Tag:.*is not defined`, require zero
3. Owner felt-verifies the reward actually arrives + closes (§13)

> **AUDIT 2026-08-21 (agent fleet, read-only):** FIXED. Evidence: `CastleDefensePlansService.cs:262-272; 5bc773833` — spawn throw removed. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
