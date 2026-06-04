# WORK ORDER 113 — Arcane Tower as a Buildable Tower Type

**Status:** READY TO IMPLEMENT
**Date:** 2026-05-29
**Priority:** High — completes the buildable tower roster; unlocks the §9 support-aura imbuements
**Scope:** Medium — one new TowerData asset + build-palette/roster entry + prefab wiring + imbue authoring. **Additive only.**
**Depends on:**
- **WO-86** — ScriptableObject data architecture (TowerData). **DONE** — this WO authors a new asset against that architecture, no schema change.
- **WO-108** — player build mode / `BuildPaletteUI` (the palette the Arcane Tower joins).
- **WO-109** — buildable tower roster (Ground / Wall / Corner Bastion) + two-tier patterns. The Arcane Tower is the fourth, orthogonal type.
- **`docs/tower-empowerment-spec.md`** — primary source. §10 (Arcane Tower as a buildable type), §3.1 (Mana Surge), §9 (Wardlight / Consecrate / Rally). Owner-ratified.
**North Star:** The magic/support pillar of the roster — the tower you build to *enable a base*, whose Imbued ceiling turns a cluster into a sanctuary, a kill zone, or a fast gun-line.

---

## Vision

The Arcane Tower is the **ward-stone made buildable**. In the narrative bible it is the
"stone spire raised by the first Keepers… it houses the defensive ward-stones, and they
answer your call" (`narrative-bible.md` §6, §7.13). Raising one inside the walls is the
player physically planting a ward-stone — the at-home half of the ward-tether established
in `regions-narrative-and-npcs.md` §0, where relighting wards in the field extends the
Heart's reach. One Aether ward-craft, two contexts: the village spire and the marches'
ward-stones.

Mechanically it is the roster's **magic / support tower**. WO-109 defines three tiers by
*placement and proportion* (Ground / Wall / Corner). The Arcane Tower is defined by
**role**, not placement — it is the only buildable whose identity is its Aether element
and whose top tier is the support-aura set (Wardlight / Consecrate / Rally) plus its
offensive Mana Surge imbuement. Its reason to exist is its **imbue ceiling**, not its
raw Level-1 output.

---

## 1. Roster Reconciliation (with WO-109)

The Arcane Tower slots in as a fourth, orthogonal type — same structural family as the
Ground Tower (ground-built spire), distinct in role and mesh.

| Type | Prefab | Placement | Role | Source |
|---|---|---|---|---|
| Ground Tower | `Tower_Medieval_Big` | Ground, standalone | Long-range, high-HP workhorse | WO-109 |
| Wall Tower | `Tower_Medieval_Wood` | Wall top only | Mid-range, cheap, elevated | WO-109 |
| Corner Bastion | `Tower_Castle_Round` | Corner positions | Auto-placed, not player-built | WO-109 |
| **Arcane Tower** | **`Tower_Castle_Square`** | **Ground, standalone** | **Magic / support — ward-stone spire** | **THIS WO** |

**Mesh-collision note (resolved):** WO-109's Ground Tower reuses `Tower_Medieval_Big`,
which the polyperfect catalog labels as the "ArcaneTower building." To keep the buildable
Arcane Tower visually distinct from the Ground Tower, this WO assigns it a **different
mesh** — `Tower_Castle_Square` (catalog: "Main corner tower — square keep"; verified
present, currently unassigned to any building, so claiming it creates no conflict).

---

## 2. TowerData Asset Definition

Create a new `TowerData` ScriptableObject asset (no code change to `TowerData.cs` — uses
the existing WO-86 architecture). Stat values below are **DESIGN targets — owner to tune**;
real values land in the inspector when CLI authors the asset.

| Field | Value | Note |
|---|---|---|
| Asset name | `ArcaneTower` (asset) / display "Arcane Tower" | Lives in the same TowerData folder as the other tower assets |
| Element | **Aether** | White→violet muzzle glow; ward-stone bolt identity (`elemental-codex.md` §4) |
| Base bolt behavior | Single-target ward-stone bolt at nearest enemy | Pooled Aether projectile via the existing `TowerCombat` fire loop — `hitBall2.prefab` (pale violet), impact `hitRing2-solid` / `distortedShockwave-light`. Reliable damage dealer, **not** a specialist. |
| Base range | ~7 m (moderate) | Between Wall Tower (short) and Ground Tower (long). Inherits WO-109 elevation bonus only if ground placement Y > 2.5 m (it won't — ground-only). |
| Cooldown / fire rate | ~1.2 s per shot (moderate) | Deliberately middling — its value is the imbue ceiling, not raw DPS |
| HP | ~600 (moderate, landmark) | Higher than a Wall Tower, below a max Ground Tower — it is a built spire meant to anchor a cluster |
| Upgrade levels | **3** (MaxLevel unchanged) | Standard L1→L2→L3 stat ramp on the existing 3-entry TowerData arrays. **Do NOT change MaxLevel.** |
| Build cost | **120 gold** | Between Wall Tower (50) and Ground Tower (~150). Base build stays affordable; power is in imbue. |
| Footprint | **2×2 grid plots** | Larger than Wall Tower (1×1); a landmark support structure, and the bigger footprint discourages spam. |
| Placement | **Ground only** (`BuildZone.Ground`) | A raised spire, structurally a ground build like the Ground Tower — not a wall-top tower. |

> The Arcane Tower implements `IDamageableStructure` like every other tower — CLI confirms
> `using DeNelle.Core.Combat;` is present on the tower component (CLAUDE.md §6). No new
> interface work; the existing `Tower` / `TowerCombat` components are reused unchanged.

---

## 3. Prefab Wiring

**Primary:** `Tower_Castle_Square` (square keep — reads as a built, magical spire,
distinct from the round Corner Bastion and the `_Big` Ground Tower). Use the `_M` quality
tier per CLAUDE.md §4: `Assets/polyperfect/Low Poly Ultimate Pack/_M/Prefabs_M/<Category>_M/Tower_Castle_Square...`.

**Fallback (per CLAUDE.md §4):** if `Tower_Castle_Square` is not present in the imported
pack, fall back to `Tower_Medieval_Big` and emit a `Debug.LogWarning` (not error — the
pack may not be imported):

```csharp
// DESIGN — illustrative resolution, CLI to finalize against the real loader
var prefab = LoadArcanePrefab("Tower_Castle_Square");
if (prefab == null)
{
    Debug.LogWarning("[ArcaneTower] Prefab 'Tower_Castle_Square' missing — " +
                     "using 'Tower_Medieval_Big' placeholder. Re-import polyperfect pack.");
    prefab = LoadArcanePrefab("Tower_Medieval_Big");
}
```

---

## 4. Build Roster / Palette Wiring

Add an `arcane_tower` entry to the WO-108 build palette (`BuildPaletteUI.items[]`),
mirroring WO-109's `BuildableItem` shape. It sits in the palette **next to the Ground
Tower and Wall Tower** as the magic/support option.

```csharp
// DESIGN — illustrative, mirrors WO-109's BuildableItem. CLI to match the real struct.
new BuildableItem
{
    id              = "arcane_tower",
    displayName     = "Arcane Tower",
    prefab          = /* resolved per §3, with Tower_Medieval_Big fallback */,
    footprint       = new Vector2Int(2, 2),
    goldCost        = 120,
    zoneRestriction = BuildZone.Ground   // ground-only, like the Ground Tower
}
```

**TowerDataSeeder:** the seeder that registers/seeds the buildable TowerData assets (the
same path WO-109's towers are seeded through) gains an entry pointing the `arcane_tower`
palette id at the new `ArcaneTower` TowerData asset. This is the single place the palette
id, prefab, and stat asset are tied together — add the Arcane Tower row there so it seeds
alongside the existing roster.

---

## 5. Imbuement Options (Level 3 → Imbued tier)

The Arcane Tower is the **single home for the Aether support-aura set**. At Level 3 the
player may imbue it with **exactly ONE** of four abilities (an empowered tower picks one;
the choice is irreversible per the empowerment spec §1). All four come from the **existing
empowerment system** — this WO does **not** redesign empowerment, it only authors which
imbuements the Arcane Tower offers. See `tower-empowerment-spec.md` §3.1 and §9 for full
mechanics, stats, and VFX.

| Imbuement | Source | One-line role | Suggested crystal cost |
|---|---|---|---|
| **Mana Surge** | spec §3.1 | Every 5th shot becomes a 3-bolt 30° burst (offense) | 8 |
| **Wardlight** | spec §9a | Mend + 20% damage-soak ward aura over friendly structures (incl. the Heart) | 10 |
| **Consecrate** | spec §9b | +25% vulnerability aura — enemies in radius take more from all sources | 10 |
| **Rally** | spec §9c | +30% fire-rate haste aura over friendly towers | 10 |

- The player chooses **ONE** imbuement when they imbue the tower; it cannot be changed.
- Authored on the Arcane Tower TowerData as the empowerment options per spec §9e — each is
  an existing `EmpowermentAbility` enum value + the authoring data already defined in the
  empowerment spec §5.2. **No new enum work in this WO** (the enum entries are owned by the
  empowerment implementation ticket; this WO consumes them).
- Player-facing copy presents imbue as the **"Imbued" tier / "Level 4"** per spec §9d —
  **UI/copy only, `MaxLevel` stays 3.** The "Imbue" button label and "Imbued — Lv 4" badge
  are the empowerment system's existing display strings; the Arcane Tower inherits them.

> Crystal costs are suggestions from the spec — **owner to tune in spec §11**. This WO
> authors whatever the owner ratifies.

---

## 6. Ward-Tether Lore Tie

The Arcane Tower is the **mechanical bridge** between the Defend pillar (towers in the
village) and the Explore pillar (ward-stones in the regions). `regions-narrative-and-npcs.md`
§0 states that building/relighting wards in the field is the *same magic* as raising the
ward-spire at home — the buildable Arcane Tower is the at-home half. When the ward-stone
relight/reach system (flagged WO-112 in the regions doc) is built, field ward-stones can
reuse the Arcane Tower's ward-bolt + aura behavior.

**Build-menu / first-build copy hook** (tone-matched to `narrative-bible.md` §7.2):

> *"Raise a ward-stone. The first Keepers planted these to carry the Heart's song past the
> walls. Yours will answer the same way."*

Wire this string into the build-menu entry / first-build tutorial copy for the Arcane Tower.

---

## 7. Files to Create / Edit

| File | Action | Type |
|---|---|---|
| `Assets/_Modules/.../TowerData/ArcaneTower.asset` (new TowerData asset) | **Create** — Aether element, ward-stone bolt, range/cooldown/HP per §2, 3-level ramp, the four §5 imbuement options authored on it | Asset (CLI authors in inspector) |
| `Assets/_Modules/Village/BuildMode/BuildPaletteUI.cs` | **Edit** — add `arcane_tower` `BuildableItem` (2×2, 120 gold, `BuildZone.Ground`) per §4 | `.cs` |
| TowerDataSeeder (the seeder that registers buildable TowerData — same path WO-109 uses) | **Edit** — add the Arcane Tower row tying `arcane_tower` id → prefab → `ArcaneTower` asset | `.cs` |
| Prefab resolution path (the loader used by the palette/seeder) | **Edit/Confirm** — `Tower_Castle_Square` with `Tower_Medieval_Big` fallback + `Debug.LogWarning` per §3 | `.cs` |
| Build-menu copy / first-build tutorial string table | **Edit** — wire the §6 ward-stone copy hook | copy/`.cs` |

> CLI confirms the exact file paths for the seeder, palette struct, and prefab loader
> against the WO-108/WO-109 implementation before editing. Everything here is **additive** —
> no non-additive edits to existing tower code, no `MaxLevel` change.

---

## 8. Acceptance Criteria

- [ ] An "Arcane Tower" entry appears in the build palette next to the Ground and Wall towers, ground-only (`BuildZone.Ground`).
- [ ] Building one costs **120 gold** and occupies a **2×2** footprint.
- [ ] The placed tower uses the `Tower_Castle_Square` mesh (or `Tower_Medieval_Big` + `Debug.LogWarning` if the pack prefab is missing — warning, not error).
- [ ] The tower fires single-target Aether ward-stone bolts (`hitBall2` violet) at the nearest enemy at L1–L3.
- [ ] It upgrades L1→L2→L3 on the standard stat ramp; **`MaxLevel` is still 3** (no 4th upgrade level introduced).
- [ ] At Level 3 the player may imbue it with **exactly one** of: Mana Surge / Wardlight / Consecrate / Rally — the choice is one-time and irreversible.
- [ ] Imbue UI presents as the **"Imbued" tier / "Level 4"** (copy only); the tower's internal `_currentLevel` never exceeds 3.
- [ ] The build-menu / first-build copy surfaces the ward-stone lore hook (§6).
- [ ] Brace-balance check passes on every `.cs` file CLI edits (CLAUDE.md §1).
- [ ] Build-verifies in batchmode with no new errors (CLI owns the build).

---

## 9. Do NOT Touch

- **Do NOT change `MaxLevel`** (stays 3). The Imbued tier is the existing empowerment
  prestige lane presented as "Level 4" in copy — never a real 4th upgrade level.
- **Do NOT redesign the empowerment system.** Mana Surge / Wardlight / Consecrate / Rally
  come from the existing system (`tower-empowerment-spec.md` §3.1, §9). This WO only
  *authors* which imbuements the Arcane Tower offers.
- **Do NOT make non-additive edits to existing tower code.** No changes to the `Tower` /
  `TowerCombat` MaxLevel logic, the other towers' TowerData assets, or the WO-109 roster
  entries. The Arcane Tower is purely additive.
- **Do NOT touch `VillageSceneBuilder.cs`** — this is a Combat/AI + UI task (palette,
  TowerData, seeder), not a scene-build task. Per CLAUDE.md §9 it runs in parallel with the
  World/Environment lane and does **not** ride the VillageSceneBuilder single-touch
  bottleneck. If any seeding/placement *does* end up needing VillageSceneBuilder, it must be
  split into a separate WO that rides the architect lane.
- **Do NOT hand-edit `Village.unity`** (CLAUDE.md §3).
- **Do NOT touch** WaveManager, ATB internals, HUD, or monetization.
- **Do NOT add `System.Reflection`** in any bridge script; use `CoreServices` + null-
  conditional (`?.`) for all cross-module calls (CLAUDE.md §10).

---

## 10. Notes for CLI

- This is a **DESIGN** spec — the code blocks above are illustrative, not final. Match the
  real `BuildableItem` struct, seeder signature, and prefab-loader from the WO-108/WO-109
  implementation.
- Cross-assembly: Village → Core only. The Arcane Tower's tower component needs
  `using DeNelle.Core.Combat;` for `IDamageableStructure`.
- The four imbuement effects (Mana Surge burst, the three §9 auras) are implemented by the
  empowerment-system ticket, not here. This WO is complete once the Arcane Tower can be
  built, upgraded to L3, and offered the four imbuement options with correct costs.
