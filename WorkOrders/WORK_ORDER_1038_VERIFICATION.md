# WO-1038 VERIFICATION — the undeclared-tag audit, checked against the tree

**Written:** 2026-08-17 (verification agent, read-only — an APK build held the tree; no code was edited)
**Verifies:** `WorkOrders/WORK_ORDER_1038_undeclared_tags_runtime_exceptions.md`
**Method:** §12 — every claim below is read from the file at the cited line, not inferred.
**Source of truth for declarations:** `ProjectSettings/TagManager.asset` lines 6–10, read this session.

---

## 0. VERDICT IN ONE LINE

> **The WO is RIGHT about the FACTS (all five tags are genuinely undeclared) and WRONG about the
> SEVERITY (its headline "each is a live `UnityException`" / "13 live exception sites" does not hold).**
> Across the whole tree there is **exactly ONE unguarded throw site**, and it is **dormant behind a
> dev-only flag that is OFF by default**. **The §1 live crash it was minted for is ALREADY FIXED in
> the working tree.** And the fix the WO asks for — *"the exact TagManager edit required"* — turns out
> to be **NO EDIT AT ALL**: adding any of these tags would make things strictly worse (§4).

| WO claim | verdict |
|---|---|
| TagManager declares exactly four tags | ✅ **TRUE** — `Tower`, `Building`, `HeartTarget`, `Player` |
| Five tags used in code are undeclared | ✅ **TRUE** — `HeroTarget`, `SpawnPoint`, `ScreenFlash`, `Pet`, `Enemy` |
| "each is a live `UnityException`" | ❌ **FALSE** — 1 of 5 can throw, and only under a dev flag |
| `HeroTarget` = "13 live exception sites" | ❌ **FALSE** — 13 sites is right, **all 13 are guarded**; 0 crash |
| `SpawnPoint` = the live crash in §1 | ❌ **ALREADY FIXED IN TREE** — zero live reads remain |
| `Enemy` is a LAYER not a tag; use a mask | ✅ **TRUE, and the mask is ALREADY there** — the tag line is dead code |
| `HeartTarget` declared but never used | ⚠️ **HALF TRUE** — never *read* by code, but it **is worn** by an authored object in the live hub scene |
| §4b: `Update()` flood, 42 captures | ⚠️ **CAUSE ALREADY REMOVED**; the throttling advice is still worth keeping |
| §5: no existing gate catches this | ✅ **TRUE** — no tag-declaration regression exists anywhere in `Assets/Editor` |

---

## 1. DECLARED TAGS TODAY

`ProjectSettings/TagManager.asset:6-10`:

```
tags:
  - Tower
  - Building
  - HeartTarget
  - Player
```

⚠ **Plus Unity's seven BUILT-IN tags, which are always valid and are NOT listed in that file:**
`Untagged`, `Respawn`, `Finish`, `EditorOnly`, `MainCamera`, `Player`, `GameController`.

> **Consequence the WO did not draw:** `Player` is **built-in**, so its row in `TagManager.asset` is
> **redundant** (harmless, but it is why 116 `Player` call sites were always safe even before anyone
> declared it). `MainCamera` — used at 10 sites — is likewise built-in and safe.
> **The list of tags this project actually adds is therefore THREE:** `Tower`, `Building`, `HeartTarget`.

---

## 2. THE TABLE — every tag literal in `Assets/**/*.cs`, with throw-or-silent per call

**Semantics used to classify (this is the distinction the WO collapsed):**

| API | on an undeclared tag |
|---|---|
| `GameObject.FindWithTag` / `FindGameObjectWithTag` / `FindGameObjectsWithTag` | **THROWS** `UnityException` |
| `Component.CompareTag` / `GameObject.CompareTag` | **THROWS** `UnityException` |
| `go.tag = "Undeclared"` | **THROWS** `UnityException` |
| `go.tag == "Undeclared"` (plain string compare) | **does NOT throw** — silently never matches |

⚠ **There is not a single `.tag == "..."` comparison against an undeclared tag in the tree.** Every
`.tag ==`/`.tag =` hit is an *assignment*, and all of them target declared or built-in tags — so the
"silent no-op" column of the brief is, in this repo, **empty**. The silence comes from a different
mechanism: a **caught** exception (§3).

### 2a. Undeclared tags

| tag | declared? | call site (file:line) | API | throws? | actually reaches a crash? | severity |
|---|---|---|---|---|---|---|
| **`HeroTarget`** | ❌ NO | `Assets/_Modules/Village/Camera/CinemachineCameraController.cs:104` | `SafeFindWithTag` | throws internally | **NO — caught** at `:121-122` | **NONE (waste)** |
| | | `Assets/_Modules/Village/World/SceneTransitionTrigger.cs:564` | `SafeFindWithTag` | throws internally | NO — caught at `:570-571` | NONE |
| | | `Assets/_Modules/Village/World/SceneLinkResolverHost.cs:213` | `SafeFindWithTag` | throws internally | NO — caught at `:219-220` | NONE |
| | | `Assets/_Modules/Village/World/LiftPlatform.cs:187` | `SafeFindWithTag` | throws internally | NO — caught at `:196-197` | NONE |
| | | `Assets/_Modules/Village/World/GateIntelHud.cs:146` | `SafeFindWithTag` | throws internally | NO — caught at `:155-156` | NONE |
| | | `Assets/_Modules/Village/World/Camps/CampProximityService.cs:68` | `SafeFindWithTag` | throws internally | NO — caught at `:106-107` | NONE |
| | | `Assets/_Modules/Village/Waves/KillComboTracker.cs:153` | `SafeFindWithTag` | throws internally | NO — caught at `:162-163` | NONE |
| | | `Assets/_Modules/Village/Hero/SmartMobileCamera.cs:785` | `SafeFindWithTag` | throws internally | NO — caught at `:864-865` | NONE |
| | | `Assets/_Modules/Village/Hero/CameraModeController.cs:425` | `SafeFindWithTag` | throws internally | NO — caught at `:431-432` | NONE |
| | | `Assets/_Modules/Village/Dungeon/PortalVFXController.cs:774` | `SafeFindWithTag` | throws internally | NO — caught at `:781-782` | NONE |
| | | `Assets/_Modules/Village/Items/ItemPickupMarker.cs:142` | `SafeFindWithTag` | throws internally | NO — caught at `:151-152` | NONE |
| | | `Assets/_Modules/Village/Diagnostics/CastleNavTopologyDiag.cs:100` | **raw** `FindWithTag` | throws | **NO — wrapped in `Guard.Try` at `:98`** | LOW (logs a Guard line) |
| | | `Assets/Editor/CastleGateNavVerify.cs:220` (loop var, `:216`) | `FindGameObjectsWithTag` | throws | **NO — `try/catch` at `:218,225`**, editor-only diag | NONE |
| | | `Assets/_Modules/Village/Buildings/TowerLoopDevHarness.cs:114` | ⚠ **COMMENT ONLY** — no call; already migrated to component lookup | — | — | none |
| **`SpawnPoint`** | ❌ NO | ⛔ **ZERO LIVE READS.** `Assets/_Modules/Village/Progression/CastleDefensePlansService.cs:263` is a **comment** describing the already-landed fix; the live code at `:272` is `FindObjectsByType<WaveSpawnPoint>` | — | — | **NO — fix already in tree** | **RESOLVED** |
| | | `Assets/Editor/CastleHubBuilder.cs:2595` — `go.tag = "SpawnPoint"` | assignment | throws | NO — `try{}catch{}` inline; **spawn left untagged** | LOW (dead write) |
| | | `Assets/Editor/Village2Playable.cs:711` — `go.tag = "SpawnPoint"` | assignment | throws | NO — `try/catch` + `Warn(...)` | LOW (dead write) |
| **`ScreenFlash`** | ❌ NO | `Assets/_Modules/Village/Dungeon/PortalVFXController.cs:854` | `SafeFindWithTag` | throws internally | NO — caught at `:781-782` | **MED (feature dead)** |
| **`Pet`** | ❌ NO | `Assets/_Modules/Village/World/PetHarvestBootstrap.cs:201` | **raw** `FindGameObjectsWithTag` | **THROWS** | ⛔ **YES — UNGUARDED.** *But* the only caller (`Build()`) returns at `:108` unless `PlaceholderNodesEnabled()` (`:71-79`) is true — **`SpawnPlaceholderNodes` is off by default (DEF-258)**; only `-spawnPlaceholderMineNodes` on the command line arms it | **THE ONLY REAL THROW — but dormant** |
| **`Enemy`** | ❌ NO (it is a **LAYER**, `TagManager.asset:19`) | `Assets/_Modules/Village/World/OutpostDefender.cs:166` | `CompareTag` | **THROWS** | **NO — UNREACHABLE.** `_enemyMask = LayerMask.GetMask("Enemy")` (`:58`) is non-zero because the layer *is* declared; the scan at `:140` is masked to it, so `:165` returns `true` for every hit before `:166` is evaluated | **LOW — dead code / landmine** |

### 2b. Declared / built-in tags (all safe — listed for completeness)

| tag | declared? | sites | note |
|---|---|---|---|
| `Player` | ✅ (built-in **and** listed) | ~116 | canon §7 hero tag. Set at `HeroControlEnsurer.cs:327, 512, 644`; `DungeonController.cs:289`; `DungeonBaker.cs:1209` |
| `MainCamera` | ✅ built-in | 10 writes + `HeroControlEnsurer.cs` `CompareTag` | safe |
| `Tower` | ✅ | `BuildModeController.cs:785, 1575`; `TowerPlacementSystem.cs` ×2; set at `Tower.cs:517` (already `try/catch` + `FlowTrace.Warn`) | safe |
| `Building` | ✅ | `BuildModeController.cs:785, 1575`; `TowerPlacementSystem.cs` ×2 | safe |
| `HeartTarget` | ✅ | **0 code reads.** Written at `Assets/Editor/CastleHubBuilder.cs:2417` (`try{}`), and **authored into the live scene**: `Assets/Scenes/Main_Castle_Overworld.unity:12551` `m_TagString: HeartTarget` (also `MainCastle_Hall.unity:4217`) | ⚠ correction to the WO: not a dead tag, an **unread** one |
| `Untagged` | ✅ built-in | `Village2PlaceGateCrossings.cs:225` | safe |

---

## 3. ★ THE FINDING THE WO MISSED — the codebase already solved this, sixteen times

Twelve of the thirteen `HeroTarget` sites do not call Unity directly. They call a **private static local
helper**, copy-pasted **verbatim into sixteen separate files**:

```csharp
private static GameObject SafeFindWithTag(string tag)
{
    try { return GameObject.FindWithTag(tag); }
    catch (UnityEngine.UnityException) { return null; }
}
```

Confirmed identical at: `SceneTransitionTrigger.cs:568`, `SceneLinkResolverHost.cs:217`,
`NodeDiscoverySystem.cs:379`, `LiftPlatform.cs:194`, `GateIntelHud.cs:153`,
`DungeonWorldPortalSpawner.cs:1291`, `CampProximityService.cs:104`, `KillComboTracker.cs:160`,
`CinemachineCameraController.cs:119`, `VfxAuraProximityCuller.cs:339`, `PoiCalloutSystem.cs:307`,
`ArenaHeraldSpawner.cs:447`, `PortalVFXController.cs:779`, `ItemPickupMarker.cs:149`,
`CameraModeController.cs:429`, `SmartMobileCamera.cs:862`.

**Three consequences, and the middle one is the real ticket:**

1. **The crash story is wrong.** Someone already swept this — the WO-450 comment at
   `TowerLoopDevHarness.cs:113-116` names it explicitly. The sweep the WO asks for **has happened**.
2. ⛔ **This is a §12 SILENT-FAILURE FARM.** The helper catches and returns `null` **with no
   `FlowTrace.Warn`, no `Guard`, no log of any kind.** CLAUDE.md §12.2 is unambiguous: *"a catch that
   swallows without logging is forbidden."* Sixteen copies of a forbidden pattern is a bigger, more
   durable defect than the tag itself — it is precisely why `HeroTarget` survived from a written-down
   canon ruling to thirteen call sites **without anyone noticing**: the code was *engineered* not to
   tell anyone. **The tags are the symptom; the mute catch is the disease.**
3. **Sixteen copies of one helper is duplicate authority** — this repo's dominant bug class. Every
   site pays exception-construction cost on a path that provably can never succeed, and some are in
   refresh loops (`PortalVFXController.cs:769-774` on a `HeroRefreshInterval` timer,
   `CampProximityService.cs:63-68`, `KillComboTracker.cs:151-153`).

---

## 4. ⛔ THE EXACT `TagManager.asset` EDIT REQUIRED — **NONE. DO NOT ADD ANY TAG.**

The brief asked for the exact edit so the fix is mechanical. **The verified answer is that the correct
edit is the empty edit**, and here is the proof rather than the opinion:

> **Grepped every authored asset for `m_TagString:` matching the five undeclared tags.
> Result: ZERO objects in the entire project wear `Pet`, `ScreenFlash`, `SpawnPoint`, `HeroTarget`, or
> `Enemy`.** (The only non-default `m_TagString` hits in `Assets/` are two `HeartTarget` rows —
> `Main_Castle_Overworld.unity:12551` and `MainCastle_Hall.unity:4217`.)

That fact triggers the WO's own §4 warning, for **all five**:

> *"declaring a tag nothing wears makes `FindGameObjectsWithTag` return empty, turning a loud crash
> into a **silent** no-reward, which is worse"*

Adding any of these five would convert a caught-and-recoverable condition into a **permanently empty
result that no gate can ever detect**. So:

```yaml
# ProjectSettings/TagManager.asset — REQUIRED CHANGE: (none)
  tags:
  - Tower
  - Building
  - HeartTarget
  - Player
```

**⚠ `ProjectSettings/TagManager.asset` must NOT be touched by this ticket.** That also removes the
WO's stated lane risk (*"⚠ Touches `ProjectSettings/TagManager.asset`"*) entirely.

### 4a. What to do instead, per tag — all code-side, all cheap

| tag | action | why |
|---|---|---|
| `SpawnPoint` | **NOTHING — already fixed.** Optionally delete the two dead editor writes (`CastleHubBuilder.cs:2595`, `Village2Playable.cs:711`), which throw-and-catch on every bake and tag nothing | `CastleDefensePlansService.cs:272` resolves by `WaveSpawnPoint` component; a component lookup cannot throw on project settings |
| `HeroTarget` (×13) | **Delete the fallback at all 13 sites.** Nothing wears the tag, so the branch is provably unreachable — it is pure cost. Canon §7 already prescribes `FindFirstObjectByType<HeroLocomotion>()`; most sites already try `Player` first and only need the second clause removed | Canon §7 forbids declaring it (one tag per GameObject; the hero's is `Player`) |
| `ScreenFlash` | **Decide, don't declare.** `PortalVFXController.ScreenFlashRoutine()` (`:851-858`) is **permanently dead** — nothing wears the tag, so `flash == null` always and it `yield break`s at `:855`. Either wire a real serialized reference to the flash `Image`, or delete the routine | Declaring it gives an empty find forever — the §4 trap |
| `Pet` | **Delete line 201 and fall straight through to the existing component-name fallback at `:204-205`**, which the file already wrote and which currently never runs | Removes the tree's only unguarded throw; the decoupling comment at `:199-200` is satisfied by the fallback alone |
| `Enemy` | **Delete `OutpostDefender.cs:166`.** The layer mask at `:165` already covers it and provably short-circuits first | Confirms the WO's instinct — the mechanism is a mask, and it is already in place |

### 4b. The change that actually prevents recurrence — kill the mute catch

Replace the sixteen copies of `SafeFindWithTag` with **one** helper that **logs**, in
`Assets/_Modules/Core/Diagnostics/` next to `FlowTrace.cs` / `Guard.cs` (assembly `DeNelle.Core`, which
every one of the sixteen files can already reference):

```csharp
// DeNelle.Core.Diagnostics — the ONE undeclared-tag-safe find. Logs once per tag,
// never silently. CLAUDE.md §12.2: a catch that swallows without logging is forbidden.
public static GameObject FindWithTagSafe(string tag)
{
    try { return GameObject.FindWithTag(tag); }
    catch (UnityException e)
    {
        FlowTrace.Once("Tags", "undeclared-" + tag,
            $"tag '{tag}' is NOT declared in TagManager.asset — lookup skipped: {e.Message}");
        return null;
    }
}
```

⚠ **`FlowTrace.Once`, not `Warn`/`Fail`** — this is the WO's own §4b lesson applied to itself: the
condition is permanent and unchangeable at runtime, so one report is the whole information content.
This turns the disease of §3 into a single loud line and satisfies §12 without spending the owner's
F8 attention budget. **⛔ Do not "clean up" by deleting the catch — §12 forbids stripping instrumentation.**

---

## 5. §4b — the flood: cause already gone, advice still worth banking

The 42-captures-per-drain flood is **already resolved**, because its cause was the `SpawnPoint` read
that no longer exists. `CastleDefensePlansService.Update()` has additionally been rewritten to the
correct §12 shape and is worth copying as the house pattern — every early return now NAMES its reason
through `FlowTrace.Throttle` with a heartbeat, at `:138`, `:163`, `:174`, and the spawn still runs
inside `Guard.Try` at `:181`.

**The standing rule the WO proposes for `docs/INSTRUMENTATION_STANDARD.md` is sound and this audit is a
fourth data point for it** — but per the WO's own instruction, file it as its own small ticket rather
than widening this one.

---

## 6. §5 — the oracle. Still needed, and now it can be stronger

**Confirmed: no such gate exists.** Grepping `Assets/Editor/**/*.cs` for `TagManager` / `IsTagDefined`
returns only scene builders and `StructureTargetableRegression.cs` (which mentions it in prose). A
compile gate structurally cannot catch this — tag names are runtime strings.

Proposed: `Assets/Editor/Regression/TagDeclarationRegression.cs`, assembly **`DeNelle.EditorRegression`**
(the assembly `StructureTargetableRegression.cs:4` documents itself as living in), marker
**`TAG_DECLARATION_OK` / `TAG_DECLARATION_FAIL`** — distinct per canon §8, never a shared
`REGRESSION_OK`.

**Registration** — one line in `Assets/Editor/Regression/DataRegression.cs`, copying the exact
neighbour idiom at `:547`:

```csharp
// --- WO-1038: no code may reference a tag that TagManager.asset does not declare. A compile
// gate structurally cannot catch this (tag names are runtime strings), which is how 'HeroTarget'
// survived from a written-down canon ruling to 13 call sites. Also pins the inverse: a DECLARED
// tag with no wearer, which turns a loud crash into a silent empty result (WO-1038 §4). ---
DeNelle.Core.Diagnostics.Guard.Try("Regression", "tag-declaration suite", () => { if (!DeNelle.Editor.Regression.TagDeclarationRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[tag-declaration] " + r); });
```

**Cases — case 2 is the one this verification proves is necessary:**

| # | case | asserts |
|---|---|---|
| 1 | `[declared]` | every string literal passed to `CompareTag` / `FindWithTag` / `FindGameObjectWithTag` / `FindGameObjectsWithTag` / assigned to `.tag` in `Assets/**/*.cs` is in `TagManager.asset` **or** is one of the seven built-ins (`Untagged`, `Respawn`, `Finish`, `EditorOnly`, `MainCamera`, `Player`, `GameController`) |
| 2 | `[no-mute-catch]` | ⭐ **no `catch` around a tag API returns without logging.** Source-lint for `catch (...UnityException) { return null; }` with no `FlowTrace`/`Guard`/`Debug` in the block. **This is the case that would have caught the real defect** — case 1 alone would have gone red for months and been "fixed" by wrapping in another mute catch |
| 3 | `[worn]` | every **declared** tag is worn by at least one authored object (`m_TagString:` in a `.unity`/`.prefab`) **or** assigned by code — the §4 silent-empty trap, in gate form |
| 4 | `[single-helper]` | only ONE `SafeFindWithTag`-shaped helper exists in the tree (guards §3's re-duplication) |

⚠ Per the WO's acceptance criterion, **prove the red**: introduce `CompareTag("NotATag")` in a scratch
file, watch case 1 fail, then restore. Do the same for case 2 by muting the new helper's catch.

Lint precedent to copy: `StructureTargetableRegression.cs` describes the source-lint approach and names
`UiMvvmConformanceRegression` as "the house style" for it (`:28-29`).

---

## 7. Canon (§3 of the WO) — the §7 correction, restated from evidence

The WO is right that `CLAUDE.md` §7's *"Enemy spawn tags: `SpawnPoint` — placed 12m outside each gate"*
is false and must be corrected in the same commit as the fix (§15). Evidence-backed replacement text:

> **Enemy spawn seats are found BY COMPONENT — `FindObjectsByType<WaveSpawnPoint>()`.** There is no
> `SpawnPoint` tag and there must not be one; `CastleHubBuilder.PlaceCastleSpawnPoints` seats each
> marker 12 m outside its gate and each marker carries `WaveSpawnPoint.GatePosition`.
> **⛔ THE PROJECT DECLARES EXACTLY THREE TAGS — `Tower`, `Building`, `HeartTarget` — plus Unity's
> built-ins (`Player`, `MainCamera`, `Untagged`, `Respawn`, `Finish`, `EditorOnly`, `GameController`).
> Do not add a fourth.** A GameObject has one tag, so tags do not compose; every new tag is a future
> collision, and a tag no object wears converts a loud crash into an undetectable silent empty.
> **Resolve by component.** Pinned by `TagDeclarationRegression`.

⚠ The existing §7 line *"that tag was never declared"* about `HeroTarget` should stay — but note in the
same breath that the thirteen call sites were **guarded, not swept**, so the fallback still needs deleting.

---

## 8. Revised acceptance criteria

- [ ] ⛔ `ProjectSettings/TagManager.asset` is **unchanged** — diff proves zero tags added (§4)
- [ ] `PetHarvestBootstrap.cs:201` no longer calls `FindGameObjectsWithTag("Pet")` — the tree's only unguarded throw
- [ ] `OutpostDefender.cs:166` deleted; a masked-scan behavioural test still finds a hostile
- [ ] All 13 `HeroTarget` fallbacks deleted; hero resolution still succeeds headless in `Main_Castle_Overworld`
- [ ] `ScreenFlash` decided — wired to a real reference or the routine deleted; **not** declared
- [ ] The sixteen mute `SafeFindWithTag` copies replaced by ONE logging helper in `DeNelle.Core.Diagnostics` (§4b)
- [ ] `TagDeclarationRegression` exists, emits `TAG_DECLARATION_OK`, and **its case 2 red was proven then restored**
- [ ] `CLAUDE.md` §7 corrected per §7 above, same commit
- [ ] Headless run: zero `Tag:.*is not defined`

---

## 9. What could NOT be verified this session

- **No headless run was possible** — the Unity lock was held by the running APK build, so the "zero
  `Tag: ... is not defined` across a full run" criterion is asserted from source, not from a captured
  log. ⚠ §12: **this is a static read and therefore LOCATES rather than CONCLUDES.** Everything above
  is a source-level proof of *reachability*; a headless pass is still owed before closing.
- Whether the `SpawnPoint` fix in `CastleDefensePlansService` has been **committed** or is another
  seat's uncommitted working-tree change (memory `other-seats-commit-ungated`) — `git` was off-limits.
  **Check `git log -S"WaveSpawnPoint" -- Assets/_Modules/Village/Progression/` before assuming HEAD has it.**
- Runtime tag assignment from **Addressables/asset-bundle content or Timeline**, which a `m_TagString:`
  grep over `Assets/` would not see. Low risk — the five tags appear nowhere else in the tree.
