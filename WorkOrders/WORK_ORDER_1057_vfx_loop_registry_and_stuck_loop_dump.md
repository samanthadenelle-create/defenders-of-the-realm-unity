**Status:** FIXED 2026-08-23 (57b2c4595) — registry already shipped in 590464bd2; the BUG REPORT FORM seam was missing its snapshot and now carries the loop table. FELT-TEST: submit a bug report, confirm the trace contains [Flow:Vfx] LOOPS. AWAITING OWNER CLOSE.

# WORK ORDER 1057 — "Random VFX stuck around" is currently UNANSWERABLE — give the loop pool names

**Minted:** 2026-08-22 (UI seat — Claude UI; UI-block banner bumped 1057 -> 1058 in the SAME edit)
**Assigned:** CLI implements. UI writes no `.cs` (CLAUDE.md §2).
**Lane:** VFX (CLAUDE.md §9 — parallel lane, no gameplay dependencies)
**Class:** INSTRUMENTATION first, then whatever defect it exposes.
**Source:** F8 capture **seq=3583**, `logs/f8-inbox/capture-20260822-111643-seq3583.md`,
scene `Main_Castle_Overworld`, 2026-08-22 11:16:43, `t=1931s` (**32 minutes into the session**).
Flag text: *"random vfx stuck around"*.
**Prior art:** WO-930 (owner felt-test 2026-08-08: *"even when I switched weapons from the flame blade
to the regular sword, the VFX stayed"*) · `VfxLoopBudget` · `GearAura`'s handle discipline.

---

## 0. One-line truth

**`VFXManager` knows HOW MANY loops are live. It does not know WHICH.** `_activeLoops` is an `int`
(`VFXManager.cs:200`). So a leaked loop is, by construction, **invisible** — the number climbs and
nothing can name the culprit. The owner can see a stuck effect on screen; the game cannot.

That is why this flag has no RCA attached, and why it will keep recurring until the instrument
exists. **This ticket builds the instrument. It does not guess at the leak.**

---

## 1. What the capture does and does not say

**Does:** a VFX was still running when it should not have been, 32 minutes into a session, in
`Main_Castle_Overworld`, out of combat (`wave=False battleLock=False pursuit=False -> Town`).

**Does not:** name it. **There is not a single `[Flow:Vfx]` line in the entire harvested tail.** The
harvest is working exactly as designed and there was simply nothing to harvest — which is the
finding, not a gap in the capture.

⛔ **Do not theorise from this capture.** It contains no line that could confirm or refute any
hypothesis about which loop is stuck. Per §12, static reading locates candidates and never concludes.

---

## 2. The structural defect — a counter cannot be audited

`VFXManager.cs:197-200`:

```csharp
// set) via ActiveOneshotCount(). _activeLoops stays an int (loops are held by long-lived
private int _activeLoops;
```

Everything the pool does runs off that scalar:

| Line | Behaviour |
|---|---|
| `:514-517` | refuses `PlayLoop` at the cap and logs `SKIPPED — active loops N/M` |
| `:547`, `:1510` | `_activeLoops++` |
| `:982` | `if (wasLoop) _activeLoops--` |
| `:1113` | `_activeLoops = Mathf.Max(0, _activeLoops - freed)` |

Two consequences, both live today:

1. **A leak is unnameable.** When the count is 18/24, nothing can say what the 18 are, who started
   them, or how long they have been running. Every future "stuck VFX" report is a fresh mystery.
2. **⚠ The count itself is not fully trustworthy.** `:1113` clamps with `Mathf.Max(0, ...)`. A clamp
   only matters if the value can go negative — i.e. if decrements can outrun increments — and if it
   ever does, the pool silently *under*-counts and the cap stops protecting anything. **The clamp
   hides the very drift it implies.** Whether that path is reachable is itself unknown for the same
   reason as (1): there is nothing to audit.

---

## 3. The fix — a keyed registry plus an F8 dump

### 3.1 Give every live loop an identity

Replace (or back) the int with a registry keyed by handle, each row carrying:

| Field | Why |
|---|---|
| `VFXType` / key | what is playing |
| **owner** (`GameObject` name + instance id) | *who* started it — the field that actually names a leak |
| `startedAt` (realtime) | **age is the leak signal** — a loop alive for 30 minutes in a 4-minute scene is the answer |
| world position | ties the log line to the thing the owner can see on screen |

`_activeLoops` becomes `registry.Count`, so the cap logic at `:514` is unchanged in behaviour and
the `Mathf.Max` clamp at `:1113` **can be deleted** — a registry cannot go negative, and removing the
clamp turns silent drift into a real error if the accounting is ever wrong.

### 3.2 Dump it on F8

The `BreakCaptureHarness` already harvests on every capture. Add a live-loop table to that harvest:

```
[Flow:Vfx] LOOPS 7/24
  Marker8_SafeZoneLoop   owner='RealmStoreBeacon'      age=1892s  pos=(12.0, 0.1, -4.0)   <-- age flags it
  HarvestAura            owner='lumberyard@4_2'        age=612s   pos=(...)
  ...
```

**Sorted by age, oldest first.** One read then names the leak, exactly as the FloorDiag dump named
the pink-floor cause in a single read (§12's founding lesson).

⛔ **This must be part of the AUTO-HARVEST, not a debug menu.** The owner presses F8 and moves on;
an instrument she has to go and invoke is one that will not be there when it matters.

### 3.3 Add the leak assert while you are in there

A loop older than a generous threshold in a scene that should have released it is reportable on its
own — `FlowTrace.Warn` once per handle, naming the owner. **That converts the next occurrence from an
owner F8 press into a self-report**, which is the whole point of §14.

---

## 4. Candidates — to CHECK against the dump, explicitly NOT conclusions

Once §3 lands, one session names the culprit. Until then these are the things to look at first, in
rough order of suspicion:

| # | Candidate | Why it is on the list |
|---:|---|---|
| 1 | **`RealmStoreBeacon`** (`Vfx/RealmStoreBeacon.cs`, `store.beacon.near`, Marker8 ring + shockwave) | **Shipped YESTERDAY** (WO-1052) and is a deliberately *persistent, proximity-gated* loop. Newest change in this exact space; suspicion by recency, nothing more |
| 2 | **`GearAura` weapon/body seats** | WO-930 is this symptom verbatim — *"switched weapons... the VFX stayed"*. It was hardened, but it is the known repeat offender |
| 3 | **`HarvestAura`** (attached from `CollectorStackView.cs:150` and `MineNode.cs:170`) | One per collector **and** per mine. In a built-out town this is the largest single population of persistent loops |
| 4 | **`EnemyAuraVFX`** on a despawned body | An aura outliving its enemy is the classic seam; note `PetDeployer.DespawnEcho` was the *first* despawn path in the game, so despawn coverage is young |
| 5 | `Poi_Landmark` / `Poi_NodeAura` | Already documented as cap victims in `VfxLoopBudget` |

⚠ **Candidate 1 is the newest code, not the likeliest by evidence.** Do not "fix" the beacon on
suspicion — WO-1052 shipped it deliberately with proximity gating and single-seat discipline, and
disabling it would remove a feature the owner asked for to chase a hunch. **Read the dump.**

---

## 5. What NOT to touch

- **`VfxLoopBudget` tiers** (village 24 / dungeon 48 / boss 32). Computed with six captures behind
  them. **A leak is not fixed by raising the ceiling** — that is the change that starved tower
  projectiles in the first place.
- **`GearAura` / `HeroHpStateAura` handle discipline.** Single-seat fields, every exit path stops
  them. That pattern is the model, not the problem.
- **The `RealmStoreBeacon` feature.** See §4.
- **`FlowTrace` calls.** Instrumentation is permanent (CLAUDE.md §12) — this ticket only ADDS.

---

## 6. Acceptance

1. An F8 capture in a live session carries a **`[Flow:Vfx] LOOPS n/m` table naming every live loop**
   with owner, age and position, sorted oldest-first.
2. `_activeLoops` is derived from the registry; the `Mathf.Max(0, ...)` clamp at `:1113` is **gone**,
   and a negative would now be a reported error rather than a silent floor.
3. Cap behaviour at `:514` is **unchanged** — same refusal, same message. Prove it: drive the pool to
   saturation and confirm the `SKIPPED` line still fires identically.
4. A loop that outlives its owner emits a **one-per-handle Warn naming that owner** (§3.3).
5. The dump costs nothing when no capture is taken — **no per-frame allocation**, no string building
   outside a capture.
6. **Then, and only then:** re-run the owner's scenario, read the dump, and open a follow-up ticket
   naming the actual leak. **This WO does not fix the leak; it makes the leak nameable.**
7. `COMPILE_GATE_OK`; brace-check every `.cs`.

---

## 7. Files

**Read first:** `logs/f8-inbox/capture-20260822-111643-seq3583.md` ·
`Assets/_Modules/Village/Vfx/VFXManager.cs` (`:197-200`, `:514-517`, `:547`, `:982`, `:1113`, `:1510`) ·
`Assets/_Modules/Village/Vfx/VfxLoopBudget.cs` (the six saturation captures) ·
`Assets/_Modules/Core/Diagnostics/BreakCaptureHarness.cs` (the harvest seam)

**Likely edit:** `VFXManager.cs` (registry) · `BreakCaptureHarness.cs` (dump into the harvest)

**Follow-up, to be opened AFTER the dump reads:** the actual stuck-loop defect.

---

# ★ TWO CONFIRMED STRANDED-LOOP SITES (CLI lane, 2026-08-22) — verified at source, NOT fixed

A full sweep of all 46 `IsLoop: 1` catalog keys against every `.cs` and every authored `.json`
found **exactly one genuinely discarded** loop handle (`HitSurface.cs:221` — closed by regenerating
the Hovl catalog so the five `PP_*Impacts` rows point at the one-shot mirrors). Every other loop key
is held by a named field with a release path.

**But two are held by a SINGLE release path that a torn-down projectile never reaches:**

| Key | Played at | Released ONLY by |
|---|---|---|
| `PP_FireBall` — the DEFAULT projectile for EVERY ranged enemy (`EnemyTypeVfxSet.DefaultProjectileVfxKey:97`) | `RangedAttackVFX.cs:248` | `arrive = () => { h?.StopSoft(); ... }` at `:181` |
| `icebasedprojectile_Projectile` (`DefenseTower.cs:1192`) | `DefenseTower.cs:975` -> `boltFx` | `boltFx?.StopSoft()` in the `Launch` arrival closure, `:983` |

**The teardown gap, read at source:** `ProjectileMover.Arrive()` (`ProjectileMover.cs:179-188`) is the
**ONLY** caller of `_onArrive`, and the class has **no timeout, no `OnDisable`, no `OnDestroy`**.
`HovlVfxFollower.LateUpdate:53` disarms on a destroyed target but does **NOT** stop the VFX or release
the loop slot. `PruneDestroyedFromSet` frees loops whose HOST died — and the host is the pooled
instance, which is **never destroyed**.

> **So a projectile torn down IN FLIGHT — scene unload, pool teardown, raid exit — strands its loop
> PERMANENTLY, mid-air, at 1 of the 24 global slots.** Every ranged enemy uses `PP_FireBall`, so this
> is not an edge case.

⛔ **Deliberately NOT fixed by the finding lane.** The fix is a `ProjectileMover` lifecycle change —
a timeout, or `OnDisable`/`OnDestroy` release — and that is POLICY, which this WO owns. A finder
should not quietly set loop policy.

**Also worth pinning here:** there is **no battle-exit or raid-exit VFX teardown anywhere in the
tree**. Anything a spawn site leaks is never swept, which is why the owner's session showed
`active loops 24/24 (cap hit)` 21 times with `SweepOneshots` never once reclaiming a slot.
