<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 112 — The Ward-Tether: Relight the Marches, Earn the Range

**Status:** READY TO IMPLEMENT
**Date:** 2026-05-29
**Priority:** High — the exploration spine of Rung 3 (Defend + Explore); gates WO-110/111 node claims
**Scope:** Medium-large — new ward-stone system in `DeNelle.Environment`, reach enforcement, save field, ZoneManager + Arcane Tower hooks
**Depends on:** WO-107 (climate zones + ZoneManager + spawn-0..3), WO-110 (crystal mine site), WO-111 (resource-node pillar); narrative ratified in `docs/regions-narrative-and-npcs.md` §0
**Canon source:** `docs/narrative-bible.md` (ward-stones, the Arcane Tower, the Withering), `docs/regions-narrative-and-npcs.md` §0 (the ward-tether), `docs/NORTH_STAR.md` (Rung 3)

---

## Vision

The bible is firm: **the Keeper cannot leave the Heart for long without losing the bond.** That is
the one rule that should have made "Defend the Town" impossible to grow into "Defend **and Explore**."
The answer is already in canon — the **ward-stones**. The first Keepers planted them along the four
marches so the Heart's song could reach past the walls. A lit ward-stone is a place the Keeper can
stand and still hear home.

> **Relight a march's ward-stone, and you can walk that far.**

This single mechanic is the exploration ladder for Rung 3. You do not unlock a region with a flag —
you **earn the range one stone at a time**, pushing the song outward. Each relit ward extends how far
the Keeper may roam on that march, and the act of relighting a node-side ward is what **claims and
starts** the region's resource node (the WO-110/111 hook). The further out you push, the thinner the
song, the stronger the Withering — tonal dread scales with distance, exactly as the bible promises
("beyond the valley, things forget themselves"). And it is the same magic as the **Arcane Tower** at
home: relighting wards in the field is raising the ward-spire writ small.

The ward-tether unifies the core loop's spine: **BUILD → HARVEST → DEFEND → OFFLINE**, with
exploration as the thread that opens the world the harvest lives in.

---

## 1. The four marches

Reuse WO-107 geography exactly. Each march runs out from one cardinal gate, 80m to the zone center,
matching the existing `spawn-0..3` direction map.

| March | Direction | Gate | Spawn | Feeling on the dial (bible §1) |
|---|---|---|---|---|
| Goldfields | East | East Gate | `spawn-1` | Warm — the last open road (tutorial march) |
| Stoneback Ridge | West | West Gate | `spawn-2` | Neutral, cold, old (mid-game) |
| Mirewood | South | South Gate | `spawn-0` | Heavy, drowned (heaviest pressure) |
| Corrupted Ashwood | North | North Gate | `spawn-3` | Wrong, the front line (endgame; the forgetting march) |

---

## 2. Ward-stone data model — DESIGN ONLY

The real code is CLI's. These blocks illustrate intent and shape only.

### 2a. `Region` enum (extend, do not duplicate)

If WO-107's `ZoneManager` already exposes a region/march identifier, **reuse it**. If not, add a
single shared enum in `DeNelle.Environment`:

```csharp
namespace DeNelle.Environment
{
    public enum March { Goldfields, Stoneback, Mirewood, Ashwood } // East, West, South, North
}
```

### 2b. `WardStoneData` — ScriptableObject (the catalog entry)

Authoring-time definition for one ward-stone: which march, how far out it sits, what reach it grants
when lit, what it costs to relight, and whether it gates a resource node.

```csharp
using UnityEngine;

namespace DeNelle.Environment
{
    [CreateAssetMenu(menuName = "Defenders/Ward Stone", fileName = "WardStone_")]
    public class WardStoneData : ScriptableObject
    {
        public string  id;                 // stable save key, e.g. "ward_goldfields_1"
        public March   march;
        public int     order;              // 1 = closest, 2/3 = further out on this march

        public Vector3 worldPosition;      // placement (rides the architect rebake — §7)
        public float   reachRadiusGranted; // how far past the Heart the Keeper may range once lit

        // Relight cost — paid from GameState resources (§4)
        public int     coinCost;
        public int     crystalCost;

        // Resource-node hook (WO-110/111). Empty = pure reach extension.
        public string  unlocksNodeId;      // CollectionPoint id this ward claims when lit

        [TextArea] public string litFlavor;   // bible-tone line shown on relight
    }
}
```

### 2c. `WardStone` — runtime MonoBehaviour (the in-world object)

```csharp
using UnityEngine;

namespace DeNelle.Environment
{
    /// <summary>
    /// One ward-stone in the field. Holds lit/unlit state, draws its glow,
    /// and reports its reach to the WardTetherService when lit.
    /// </summary>
    public class WardStone : MonoBehaviour
    {
        public WardStoneData data;
        public bool IsLit { get; private set; }

        public void SetLit(bool lit)
        {
            IsLit = lit;
            // toggle glow VFX / light / song-hum SFX via CoreServices (?. — cross-module)
            CoreServices.Audio?.PlaySfx(lit ? SfxId.WardLit : SfxId.WardDim);
            // resolve into the tether service so reach recomputes
            WardTetherService.Instance?.OnWardStateChanged(this);
        }
    }
}
```

### 2d. `WardTetherService` — the reach authority (singleton)

Owns the live set of lit wards, computes per-march reach, enforces the leash, and drives the
forgetting effect. Lives in `DeNelle.Environment`. Cross-module calls (HUD dim, Heart's voice) go
through `CoreServices` with null-conditional `?.` — never a direct HUD reference.

```csharp
namespace DeNelle.Environment
{
    public class WardTetherService : MonoBehaviour
    {
        public static WardTetherService Instance { get; private set; }

        // Per march: the radius granted by the FURTHEST lit ward on that march.
        public float ReachForMarch(March m) { /* max reachRadiusGranted of lit wards on m */ }

        // Furthest the Keeper may range right now, given their bearing from the Heart.
        public float CurrentReach(Vector3 keeperPos) { /* classify bearing → ReachForMarch */ }

        public void OnWardStateChanged(WardStone ward) { /* recompute, persist, refresh HUD */ }

        // Called each frame (or on a light tick) with the Keeper's distance from the Heart.
        public void EvaluateTether(Vector3 keeperPos) { /* drive forgetting (§3) */ }
    }
}
```

---

## 3. The reach system + the forgetting

**Reach = the furthest lit ward-stone on the march the Keeper is currently facing.** With no wards lit,
reach is the base "walls + a little" radius (the Heart's bare song). Light Goldfields ward #1 → the
east march opens to its reach radius. Light #2 further out → the east march opens further. Each march's
reach is independent — pushing east does not let you walk north.

**Past the edge — the forgetting (bible §0 / §5; ratified gentle + reversible):**

The leash is **not a wall.** The Keeper *can* step past the furthest lit ward — but the song goes thin,
and the world begins, gently, to forget them. This is a soft, fully reversible effect, never punishing:

| Distance past furthest lit ward | Effect |
|---|---|
| 0–6m (the fray edge) | HUD edges begin to desaturate; a low vignette creeps in |
| 6–14m | HUD dims further; minimap / readouts fade; the Heart's voice (ambient hum) quiets |
| 14m+ | Screen mutes toward grey; the Heart's voice falls silent; a soft "turn back" prompt |

- **No damage, no death, no hard stop, no timer that kills you.** The forgetting only *removes warmth* —
  it never harms. The bible's rule is dread, not punishment.
- **Fully reversible the instant the Keeper steps back inside reach** — or relights a further ward. The
  HUD warms back up, the hum returns. Stepping back in is the whole point: it teaches that the song is
  what keeps you yourself.
- Drive it through `CoreServices.Hud?.SetForgettingLevel(0..1)` and an ambient-hum fade — **never a
  direct `DeNelle.HUD` reference from `DeNelle.Environment`.** If `IVillageHud` lacks a dim hook, note
  it as a one-line Core interface add for CLI (passive display only; HUD never reads Environment back).

The Ashwood (north) is the march where this bites hardest — the song is thinnest there by design, so
the forgetting onset distances above can be **halved on the north march** to sell the front-line dread.

---

## 4. Relight interaction

Relighting a ward-stone is a small, earned beat — proximity + cost + a short defend-the-ritual.

1. **Approach.** The Keeper must be within ~3m of an unlit `WardStone` (it shows a dim "relight"
   affordance — a cold, unlit glow).
2. **Pay the song's price.** Relighting costs resources from `GameState` (coins + a few crystals,
   per `WardStoneData`). The cost rises with `order` (further wards cost more — thinner song, more to
   carry). Deduct via `GameStateService.Instance.State.Resources` (same path `CrystalMine` uses).
3. **The ritual beat (short defend).** Lighting a ward calls a brief **hold-the-ward** wave — a small,
   timed trickle of enemies from that march's spawn point while the stone kindles (~15–25s). Keep it
   light: this is a punctuation beat, not a full wave. Reuse `WaveManager`'s spawn path; do **not**
   fork a new spawner. If the Keeper survives the kindle, the ward lights; if they retreat, the ward
   stays cold and the spend is refunded (or held in escrow — owner's call, default: refund).
4. **Light + claim.** On success: `WardStone.SetLit(true)` → reach recomputes → the lit flavor line
   shows (bible-tone) → and if `data.unlocksNodeId` is set, **the matching CollectionPoint is claimed
   and its mine begins/enables harvest** (the WO-110/111 hook — see §6).

---

## 5. Progression — depth per march

Each march holds **2–3 ward-stones** in a line out from its gate, gating exploration depth like a
ladder:

| March | Wards | Layout (out from gate) |
|---|---|---|
| Goldfields (E) | 2 | #1 at ~40m (opens the road), #2 at ~75m (the node-ward — claims the grain/gold node) |
| Stoneback (W) | 3 | #1 ~35m, #2 ~60m, #3 ~80m at the seam (node-ward — the rich crystal/cold-iron node) |
| Mirewood (S) | 3 | #1 ~35m on a dry hummock, #2 ~58m, #3 ~80m (node-ward; heaviest pressure) |
| Ashwood (N) | 3 | #1 ~35m, #2 ~58m, #3 ~80m at the last warden's stand (node-ward; endgame) |

Tuning intent (the danger curve, bible §0):
- **Cost scales with `order`** — each further ward costs more than the last.
- **The kindle wave scales with `order` and march** — further/north wards summon a harder hold-beat.
- **Goldfields is the tutorial march** (2 wards, cheapest, lightest kindle) — where Maeren explains the
  ward-tether in plain terms.
- A march's resource node is gated behind that march's **final** ward — you must push to the edge to
  claim the prize.

---

## 6. Resource-node hook (WO-110 / WO-111)

The ward-tether is *the reason resource nodes have a reason.* Claiming a node = raising a ward beside it.

- The **node-ward** on each march carries `unlocksNodeId` pointing at a `CollectionPoint` (WO-111 §Phase 2)
  or the WO-110 crystal site.
- On relight, `WardTetherService` calls into the node system to **mark that CollectionPoint claimed**
  and **start/enable its mine harvest** (per WO-111 Phase 3 auto-harvest). Until the node-ward is lit,
  the node shows only a cold "out of reach" state — no build affordance, no harvest.
- This means: **push the march → light the node-ward → the node turns on.** Exploration and the harvest
  economy are one motion. Do **not** duplicate node state — read/write the existing CollectionPoint/mine
  state; the ward only flips the "claimed/active" gate.
- If the mine is later destroyed (WO-111's destructible-node tension), the **ward stays lit** (reach is
  not lost) — only the harvest stops until rebuilt. Reach and harvest are decoupled.

---

## 7. World placement — rides the architect rebake

Ward-stones are scene objects, so their placement is a **`VillageSceneBuilder` concern** — and that file
is the serialization bottleneck (CLAUDE.md §9). **Do not create a second placement path.**

- Add a `BuildWardStones(Transform exteriorRoot)` step to `VillageSceneBuilder`, called from
  `BuildVillage()` **after** `BuildClimateZones()` (WO-107) so wards sit inside their zones.
- Placement reads the `WardStoneData` assets (positions in §5) and instantiates a ward prefab per stone,
  wiring its `WardStone.data` reference.
- This placement work **must ride the next architect-lane rebake** — it does not get its own bake, and
  it must not land in the same window as another VillageSceneBuilder edit. Queue it as a bake line in
  the architect lane alongside WO-107 zones. UI does not fire batchmode; CLI owns the bake.
- **Never hand-edit `Village.unity`** — wards appear only via the builder rebake.

---

## 8. Arcane Tower tie-in

The ward-stones *are* the Arcane Tower's lore: *"Built by the first Keepers. Holds the ward-stones that
answer your call."* (bible §, building flavor). Reflect this in-fiction and in-system:

- The Arcane Tower at home is the **hearth ward** — the source the field wards are kindled from. On the
  Tower's panel, surface a small **"Wards of the Marches"** readout: per-march, how many stones lit / how
  far the song now reaches. This is passive display (HUD-side), fed by `WardTetherService` through
  `CoreServices` — the Tower reads the tether, never the reverse.
- Future hook (note only, not in scope): upgrading the Arcane Tower could raise base reach a little or
  lower relight cost — the "raise the ward-spire at home" mirror of relighting in the field.

---

## 9. Save / persistence

Lit ward-stones must persist across sessions (reach is earned progress).

- Add a serialized field to the persistent player save — wherever the existing offline/save-sync state
  lives (the same store `GameStateService.Instance.State` exposes; e.g. a `List<string> LitWardIds` or a
  `HashSet<string>` of ward `id`s on the save model).
- On load: `WardTetherService` reads `LitWardIds`, calls `SetLit(true)` on each matching `WardStone`,
  recomputes reach **before** the first tether evaluation (so the player doesn't briefly "forget" on
  spawn).
- On relight: add the id and persist via the existing save path (do not invent a new save file).
- This also lets offline accrual (WO-111 Phase 5) trust that claimed node-wards stayed lit while away.

---

## Files to Create / Edit

| File | Action |
|---|---|
| `Assets/_Modules/Environment/March.cs` | **Create** (or reuse WO-107's region enum if present) |
| `Assets/_Modules/Environment/WardStoneData.cs` | **Create** — ScriptableObject |
| `Assets/_Modules/Environment/WardStone.cs` | **Create** — runtime in-world ward |
| `Assets/_Modules/Environment/WardTetherService.cs` | **Create** — reach authority + forgetting driver |
| `Assets/_Modules/Core/HUD/IVillageHud.cs` | **Edit (if needed)** — add `SetForgettingLevel(float)` + wards readout hook |
| `Assets/_Modules/Core/Audio/SfxId.cs` (or equivalent) | **Edit (if needed)** — add `WardLit` / `WardDim` ids |
| `Assets/Editor/VillageSceneBuilder.cs` | **Edit** — add `BuildWardStones()`, call after `BuildClimateZones()` — **rides architect rebake, single-touch** |
| Save model (`GameState` save struct) | **Edit** — add `LitWardIds` persisted field |
| `Assets/_Data/WardStones/*.asset` | **Create** — the 2–3 ward data assets per march (§5) |
| `Assets/Scenes/Village.unity` | Rebuilt via builder — **do NOT hand-edit** |

**Assembly discipline:** new code lives in `DeNelle.Environment` (alongside `ZoneManager`). Environment
may reference `DeNelle.Core` only; all HUD/Audio calls go through `CoreServices` with `?.`. Never
reference `DeNelle.HUD` directly. Any new `.cs` that implements `IDamageableStructure` (if a ward is ever
made attackable — not in this WO) needs `using DeNelle.Core.Combat;`.

---

## Acceptance Criteria

- [ ] `WardStoneData` ScriptableObject authorable; assets created for 2 Goldfields + 3 each for Stoneback/Mirewood/Ashwood wards
- [ ] `WardStone` toggles lit/unlit, shows glow + plays ward SFX, reports to the tether service
- [ ] `WardTetherService` computes per-march reach = furthest lit ward on that march; marches are independent
- [ ] Relight requires proximity (~3m) + resource cost (rising with `order`, paid from `GameState`)
- [ ] Relight triggers a short hold-the-ward kindle wave via `WaveManager` (no new spawner); retreat refunds
- [ ] Stepping past the furthest lit ward triggers the forgetting (HUD dim + Heart's voice fade), scaling with distance
- [ ] Forgetting is gentle: no damage, no death, no hard wall — fully reversible on stepping back in or relighting further
- [ ] Ashwood (north) forgetting onset is tighter (thinner song) than the other marches
- [ ] Lighting a node-ward claims its CollectionPoint and enables harvest (WO-110/111) without duplicating node state
- [ ] Mine destruction stops harvest but does NOT extinguish the ward (reach decoupled from harvest)
- [ ] Lit ward ids persist in the save and restore on load before the first tether evaluation
- [ ] Arcane Tower surfaces a passive "Wards of the Marches" readout fed via `CoreServices` (Tower reads tether, never reverse)
- [ ] Ward placement added to `VillageSceneBuilder` and appears only via rebake — `Village.unity` not hand-edited
- [ ] Brace balance check passes on every `.cs` touched; cross-module calls use `?.`

---

## Do NOT touch

- **Do NOT hand-edit `Village.unity`** — ward placement appears only via the `VillageSceneBuilder` rebake.
- **Do NOT create a second ward-placement path** — placement rides the architect-lane rebake; only one
  agent/branch touches `VillageSceneBuilder` at a time (CLAUDE.md §9).
- **Do NOT fire any bake/batchmode from UI** — queue the rebake as a CLI work-order line in the architect lane.
- **Do NOT reference `DeNelle.HUD` from `DeNelle.Environment`** — forgetting + readouts go through `CoreServices` / `IVillageHud`.
- **Do NOT fork `WaveManager`** for the kindle beat — reuse the existing spawn path with a small timed trickle.
- **Do NOT duplicate CollectionPoint / mine state** (WO-110/111) — the ward only flips the claimed/active gate.
- **Do NOT make the forgetting punishing** — no damage, no death, no timed kill, no hard wall. Gentle + reversible is canon.
- Do not touch ATB, WalletService, monetization, or clan code.
