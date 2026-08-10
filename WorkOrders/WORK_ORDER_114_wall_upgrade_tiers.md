# WORK ORDER 114 — Wall Upgrade Tiers: Wood → Stone → Reinforced (the CoC Sink)

**Status:** CLOSED — SUPERSEDED by WO-904 (owner-approved sweep 2026-08-09: WO-904 explicitly cites this WO and owns walls/gates)
**Date:** 2026-05-29
**Priority:** High — closes the named North-Star gap (the upgrade sink paid from harvest)
**Scope:** Medium — WallSegment tier data + visual swap + upgrade interaction; rides the architect rebake
**Depends on:** WallSegment (built), EconomyService (built, DEF-78), GameState.WallLevel (already persisted)
**Coordinates with:** **WO-110 (Siege Warfare)** — also edits `WallSegment.cs`. See §6. **WO-110 lands first.**
**North Star:** UPGRADE walls (wood → stone → reinforced), paid from the haul — the Clash-of-Clans
                progression sink in the core loop (`docs/NORTH_STAR.md`, core-loop diagram + system-map
                row "Walls + upgrade tiers … tiers (wood→stone) are the gap").

---

## RECONCILE FIRST — what is ALREADY built (do NOT duplicate)

Per CLAUDE.md "reconcile, don't duplicate." This system is **more built than a greenfield spec assumes.**
Verified by inspection before writing:

| Piece | State | Where |
|---|---|---|
| `GameState.WallLevel` (`int`, 0..3) | **BUILT + persisted** | `Assets/_Modules/Core/State/GameState.cs:77-78` (field #11) |
| WallLevel save round-trip | **BUILT** | `SaveSchema.cs:112` (`wallLevel`), `GameStateService.cs:282/335/528` (load/sync/reset) |
| WallLevel tests | **BUILT** | `SaveLoadRoundTripTest.cs:255/440`, `ResetCarveOutTest.cs:120/157` |
| `WallSegment` damage / repair / `IDamageableStructure` | **BUILT** | `Assets/_Modules/Village/Walls/WallSegment.cs` |
| `WallSegment.Configure(data, height)` per-tier height hook | **BUILT** (height param already threaded) | same file, line 100 |
| Economy (Wood/Stone/Iron/Crystals, `CanAfford`/`TrySpend`) | **BUILT** | `Assets/_Modules/Village/EconomyService.cs` |
| Polyperfect wood + stone wall meshes | **EXIST in catalog** | see §3 |

> **What is genuinely missing (this WO's job):** the *tier concept itself* — WallSegment does NOT yet
> read `WallLevel`, has no per-tier HP multiplier, and never swaps its mesh. The **persistence field
> and the height hook are already there and inert.** We are wiring an existing dangling field, not
> adding a new save slot. **Do not add a second wall-level field** — reuse `GameState.WallLevel`.

> **Reconcile note on `WALL_MAX_LEVEL`:** the GameState comment references a `WALL_MAX_LEVEL (3)`
> constant. Grep found **no live constant by that name** — only the inline range comment and a test
> that sets `WallLevel = 3`. CLI should add the constant to `DeNelle.Village` (or reuse an existing
> `Constants`) rather than hard-code `3`. 3 tiers (Wood=0, Stone=1, Reinforced=2) + an implicit
> "max" — keep the 0..2 index = 3 tiers reading; the persisted "level" is the tier index.

---

## Vision

The player hauls Wood and Stone from the harvest nodes (WO-110/111 mines). Walls are the first place
that haul goes: **a wood palisade is cheap but crumbles; a stone wall costs the haul but holds; a
reinforced wall is the late-game flex that turns back a siege.** This is the Clash-of-Clans upgrade
sink — the reason to keep harvesting after the first wave is survived. It ties directly to WO-110:
a trebuchet that one-shots a wood wall takes 5× the hits on a reinforced one, so **every tier the
player buys visibly changes how a siege plays out.** The wall you upgraded is the wall that saves
your Heart.

Tier is **global** (one `WallLevel` for the whole ring), matching the existing persisted field and
the CoC "upgrade your walls" mental model — not per-segment micromanagement. (Per-segment tiering is
explicitly out of scope; see "Do NOT touch.")

---

## 1. Tier Data Model — `WallTier` (DeNelle.Core.Data)

Three tiers, rising HP multiplier + a polyperfect mesh per tier. Mirror the TowerData pattern
(a small authoring SO with an array of level entries — see `TowerData.upgrades[]`).

**Path:** `Assets/_Modules/Core/Data/WallTierData.cs` (new, `DeNelle.Core.Data`)

> ILLUSTRATIVE DESIGN ONLY — CLI owns the final compile.

```csharp
using UnityEngine;
using DeNelle.Village; // ResourceCost lives in DeNelle.Village (EconomyService.cs)

namespace DeNelle.Core.Data
{
    /// <summary>Authoring data for the global wall upgrade ladder (wood -> stone -> reinforced).</summary>
    [CreateAssetMenu(menuName = "Defenders/Wall Tier Data", fileName = "WallTierData")]
    public class WallTierData : ScriptableObject
    {
        // Exactly 3 entries: [0]=Wood (fresh/level 0), [1]=Stone, [2]=Reinforced.
        public WallTier[] tiers = new WallTier[3];
    }

    [System.Serializable]
    public class WallTier
    {
        public string tierName       = "Wood Palisade";
        public float  hpMultiplier   = 1f;     // Wood 1.0x, Stone 2.5x, Reinforced 5.0x (matches WO-110 §5)
        public float  wallHeight     = 3f;     // fed into WallSegment.Configure(data, height)

        [Header("Visual (polyperfect)")]
        public GameObject straightPrefab;       // null -> code placeholder + LogWarning (CLAUDE.md §4)
        public GameObject cornerPrefab;

        [Header("Upgrade cost INTO this tier (from the harvest)")]
        // ResourceCost is the existing struct (Wood/Stone/Iron/Crystals).
        // Tier 0 (Wood) cost is ignored — it is the free starting tier.
        public ResourceCost upgradeCost;
    }
}
```

**Recommended tuning** (owner to confirm — illustrative):

| Tier | Index (`WallLevel`) | HP mult | Height | Upgrade cost (into this tier) |
|---|---|---|---|---|
| Wood Palisade | 0 | 1.0× | 3.0m | — (starting tier) |
| Stone Wall | 1 | 2.5× | 3.5m | 150 Wood, 200 Stone |
| Reinforced Wall | 2 | 5.0× | 4.0m | 300 Stone, 120 Iron, 40 Crystals |

> HP multipliers (1.0/2.5/5.0) are taken verbatim from **WO-110 §5** so the two systems agree on the
> number a siege has to chew through. If WO-110 hard-codes them on `WallSegment`, this WO replaces the
> hard-code with `tier.hpMultiplier` (see §6).

---

## 2. WallSegment — read tier (ADDITIVE on top of WO-110's HP)

`WallSegment` already implements `IDamageableStructure` and (after WO-110) owns `maxWallHp` /
`currentWallHp` / `Breach()`. This WO adds **only** the tier multiplier + a mesh-swap entry point.
It must NOT re-define the HP/damage model — that is WO-110's.

> ILLUSTRATIVE DESIGN ONLY — additive members; do not restate WO-110's HP code.

```csharp
// added to WallSegment (DeNelle.Village) — additive to WO-110's HP fields
[Header("Tier (WO-114)")]
[SerializeField] private int _tierLevel; // 0 wood / 1 stone / 2 reinforced — mirrors GameState.WallLevel

public int TierLevel => _tierLevel;

/// <summary>
/// Applies a wall tier: scales max HP by the tier multiplier and swaps the visual.
/// Called by VillageController at build time and by the upgrade flow at runtime.
/// </summary>
public void ApplyTier(WallTier tier, float baseMaxHp)
{
    if (tier == null) return;
    _tierLevel = Mathf.Clamp(_tierLevel, 0, 2);
    // WO-110 owns maxWallHp/currentWallHp. Scale, preserving current damage ratio.
    float ratio = (maxWallHp > 0f) ? currentWallHp / maxWallHp : 1f;
    maxWallHp   = baseMaxHp * tier.hpMultiplier;
    currentWallHp = maxWallHp * ratio; // an upgrade refills proportionally, not for free
    _height = tier.wallHeight;
    RebuildCollider();
    // Visual swap is handled by the architect rebake / VisualSwap helper (see §5).
}
```

> **Cross-module rule:** `WallTier` is in `DeNelle.Core.Data`; `WallSegment` is in `DeNelle.Village`.
> Village → Core is allowed. `using DeNelle.Core.Combat;` is already present (IDamageableStructure).
> Add `using DeNelle.Core.Data;`. Use `?.`/null-guards on the tier arg (done above).

---

## 3. Polyperfect Wall Meshes (verified in `docs/polyperfect-asset-catalog.md`)

Confirmed present (catalog §1, "Stone wall system"):

| Tier | Straight mesh | Corner mesh |
|---|---|---|
| 0 Wood | `Wall_Wood_Horizontal_3x3m` | `Wall_Wood_Horizontal_Corner` |
| 1 Stone | `Wall_Stone_3x3_A` (or `Wall_Medieval_Stone`) | `Wall_Stone_Corner_A` |
| 2 Reinforced | `Wall_Stone_3x3_C` (battle-worn variant) **or** `Wall_Medieval_Stone` re-tinted darker | `Wall_Stone_Corner_C` |

> No dedicated "reinforced" mesh exists in the pack. Reinforced reuses the stone mesh with a
> battle-worn variant (`_C`) and/or a darker material tint — call it out so the owner can swap in a
> bespoke asset later. **Per CLAUDE.md §4:** always use the `_M` tier prefab path
> (`_M/Prefabs_M/<Category>_M/`); on a missing prefab `Debug.LogWarning` (NOT error) and fall back to
> the current code-built placeholder box — the pack may not be imported on a fresh clone.

---

## 4. Upgrade Interaction + Cost Curve (mirror the Tower upgrade UX)

The wall upgrade should feel like the tower upgrade the player already knows
(`TowerUpgradeButton` → `EconomyService.CanAfford`/`TrySpend` → visual swap).

**Path:** `Assets/_Modules/Village/Walls/WallUpgradeController.cs` (new, `DeNelle.Village`)

Behaviour:
1. The player taps a wall section (or a dedicated "Upgrade Walls" HUD button — reuse the build-menu
   affordance). Because tier is **global**, one upgrade promotes the whole ring.
2. Read current `GameState.WallLevel` (via `CoreServices` / the GameState service the towers use).
   If already at max (2) → show "Max tier" and bail.
3. Look up `nextTier = WallTierData.tiers[WallLevel + 1]`. Gate the button on
   `EconomyService.Instance?.CanAfford(nextTier.upgradeCost)`.
4. On confirm: `EconomyService.Instance?.TrySpend(nextTier.upgradeCost)`; if it returns true, set
   `WallLevel += 1`, persist via the GameState service, then call `ApplyTier` on **every** live
   `WallSegment` and trigger the visual swap (§5).
5. Fire `CoreServices.Audio?.Play(SfxId.Upgrade)` and a small VFX, mirroring the tower upgrade juice.

> ILLUSTRATIVE DESIGN ONLY:

```csharp
public bool TryUpgradeWalls()
{
    int level = _gameState != null ? _gameState.WallLevel : 0;
    if (level >= 2) return false;                          // already reinforced
    WallTier next = _tierData != null ? _tierData.tiers[level + 1] : null;
    if (next == null) return false;
    if (EconomyService.Instance == null) return false;
    if (!EconomyService.Instance.CanAfford(next.upgradeCost)) return false;
    if (!EconomyService.Instance.TrySpend(next.upgradeCost)) return false;

    _gameState.WallLevel = level + 1;
    _stateService?.MarkDirty();                            // reuse the towers' persist path
    foreach (var seg in FindObjectsByType<WallSegment>(FindObjectsSortMode.None))
        seg.ApplyTier(next, _baseSegmentMaxHp);
    CoreServices.Audio?.Play(SfxId.Upgrade);
    return true;
}
```

**Cost-curve principle:** each tier costs *roughly the haul of one wave's worth of mining* more than
the last — a smooth ramp, not a wall. Wood is free (the starting palisade). Stone is a Wood+Stone
sink (early-game). Reinforced pulls in Iron + a little Crystal so it stays a mid/late goal. Tune in
the SO; do not bake numbers in code.

---

## 5. Visual Tier Swap on the Rebake (architect lane)

The wall ring is placed by **VillageSceneBuilder** (the serialization bottleneck — single-touch).
On a fresh scene build the builder must read the persisted `WallLevel` and instantiate the matching
tier prefab per segment, then call `WallSegment.Configure(data, tier.wallHeight)` and
`ApplyTier(tier, baseMaxHp)`.

- **Seeding rule:** wall placement/seeding rides the architect rebake. Do NOT hand-edit `Village.unity`
  (CLAUDE.md §3) — the tier-aware placement goes into the builder and ships on the next
  `Defenders > Week 3 > Build Village Scene` bake. **Queue the bake in a work order for CLI; UI does
  not fire batchmode.**
- **Runtime swap (no rebake):** when the player upgrades mid-session, the new mesh must appear without
  a scene rebuild. Provide a `WallVisualSwap` helper (or a method on WallSegment) that destroys the
  current child mesh and instantiates the new tier prefab as a child, preserving transform/collider.
  This is the only path that mutates the scene at runtime — it does not touch the `.unity` asset.
- Architect-lane WO already in flight (WO-107) touches `VillageSceneBuilder`. **Coordinate the
  builder edit through a single work order** so two agents don't both touch the bottleneck file.

---

## 6. Coordination with WO-110 (Siege Warfare) — ORDERING (read carefully)

**Both WOs edit `Assets/_Modules/Village/Walls/WallSegment.cs`. They must not clobber each other.**

- **WO-110 lands FIRST.** It introduces the HP model (`maxWallHp`, `currentWallHp`, `Breach()`,
  `OnBreached`) and the damage-decal hooks. WO-110 §2 explicitly *anticipates* this WO ("Tier
  multipliers (set by wall upgrade system, WO-111 wall tiers) — Wood 1.0×, Stone 2.5×, Reinforced
  5.0×") — note WO-110 calls it "WO-111"; the owner renumbered it to **WO-114**. Same system.
- **WO-114 (this) is purely ADDITIVE on top.** It adds `_tierLevel`, `ApplyTier()`, and the mesh
  swap. It **reads** WO-110's `maxWallHp`/`currentWallHp` to scale them — it does not redefine them.
- **If WO-114 is somehow scheduled before WO-110:** CLI must include a minimal `maxWallHp` /
  `currentWallHp` field stub in WallSegment so `ApplyTier` compiles, and WO-110 then layers its
  damage/breach logic on top. **Preferred order is WO-110 → WO-114** to avoid the stub.
- **Reconcile conflict to flag:** the *current* `WallSegment.cs` uses a 0–100 `_damage` accumulator
  (`ApplyContactDamage`, `IsDestroyed` at 100). WO-110 replaces this with an HP-down-to-zero model.
  **WO-114 assumes the WO-110 HP model is in place.** If WO-110 has NOT landed, the 0–100 model has no
  per-tier scaling concept — so `ApplyTier` should scale a `_baseMaxHp` the tier owner provides, and
  CLI maps that onto whichever damage model is live. This is the one place the two specs touch; keep
  the multiplier as the single shared contract (1.0 / 2.5 / 5.0).

---

## 7. Persistence (reuse the existing field — do NOT add a new one)

- Tier is stored in **`GameState.WallLevel`** (already a persisted save field, #11, round-tripped by
  `SaveSchema`/`GameStateService`, covered by `SaveLoadRoundTripTest` + `ResetCarveOutTest`).
- **No new GameState field, no SaveSchema bump, no migrator change** — the slot exists and is inert.
  This WO's only persistence work is to *write* `WallLevel` on upgrade (via the towers' existing
  `MarkDirty`/save path) and *read* it on scene build (§5).
- `WallLevel` already resets to 0 on New Game (`GameStateService.cs:528`) and survives load — so a
  reinforced wall persists across sessions, and a new game starts on wood. Nothing to add.
- Add the `WALL_MAX_LEVEL` constant (value 3 tiers / max index 2) to `DeNelle.Village` Constants so
  the magic number in the GameState comment becomes real (reconcile note, top of doc).

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/_Modules/Core/Data/WallTierData.cs` | **Create** — `WallTierData` SO + `WallTier` entry |
| `Assets/_Modules/Village/Walls/WallSegment.cs` | **Edit (ADDITIVE)** — add `_tierLevel`, `ApplyTier()`; reads WO-110 HP fields. Coordinate ordering — WO-110 first |
| `Assets/_Modules/Village/Walls/WallUpgradeController.cs` | **Create** — global upgrade interaction, mirrors `TowerUpgradeButton` |
| `Assets/_Modules/Village/Walls/WallVisualSwap.cs` | **Create** — runtime tier mesh swap (no `.unity` edit) |
| `Assets/Editor/VillageSceneBuilder.cs` | **Edit** — tier-aware wall placement at build; reads `WallLevel`. SINGLE-TOUCH — coordinate w/ WO-107 |
| `Assets/_Modules/Village/Constants` (or new `WallConstants`) | **Edit/Create** — `WALL_MAX_LEVEL` (= 3 tiers) |
| Create a `WallTierData.asset` + assign polyperfect prefabs | **Create** (CLI, in editor) |
| `Assets/Scenes/Village.unity` | Rebuilt via builder — **do NOT hand-edit** (CLAUDE.md §3) |

---

## Do NOT touch

- **Do NOT add a new wall-level save field** — reuse `GameState.WallLevel` (already persisted).
- **Do NOT redefine WallSegment's HP/damage model** — that belongs to WO-110. Stay additive.
- **Do NOT hand-edit `Village.unity`** — placement rides the architect rebake (CLAUDE.md §3).
- **Do NOT fire any batchmode/bake from UI** — queue the bake in a CLI work order.
- **Do NOT implement per-segment tiering** — tier is global (one `WallLevel`). Out of scope.
- **Do NOT bump SaveSchema / write a migrator** — the field and round-trip already exist.
- **Do NOT touch WaveManager, ATB, Wallet, or monetization code** — unrelated lanes.

---

## Acceptance Criteria

- [ ] `WallTierData` SO compiles in `DeNelle.Core.Data` with exactly 3 tiers (Wood/Stone/Reinforced)
- [ ] HP multipliers are 1.0× / 2.5× / 5.0× and AGREE with WO-110 §5 (single shared contract)
- [ ] `WallSegment.ApplyTier()` scales max HP and preserves the current damage ratio (no free refill)
- [ ] Upgrade is GLOBAL: one tap promotes the whole ring; cost spent via `EconomyService.TrySpend`
- [ ] Upgrade button is gated on `CanAfford` and shows "Max tier" at Reinforced
- [ ] Upgraded tier persists across save/load via existing `GameState.WallLevel` (no new field)
- [ ] New Game resets walls to Wood (existing reset path, unchanged)
- [ ] Wall mesh visibly swaps wood → stone → reinforced (runtime swap + on rebake), polyperfect `_M` prefabs
- [ ] Missing prefab → `Debug.LogWarning` + code-placeholder fallback (CLAUDE.md §4), never an error
- [ ] A reinforced wall survives ~5× the trebuchet hits of a wood wall (verifies WO-110 integration)
- [ ] Brace-balance check passes on every `.cs` file edited (CLAUDE.md §1)
- [ ] `using DeNelle.Core.Data;` present in WallSegment; `?.` on all cross-module service calls
- [ ] Rebake required — queue for CLI AFTER WO-110 lands; do not bake with the editor open
