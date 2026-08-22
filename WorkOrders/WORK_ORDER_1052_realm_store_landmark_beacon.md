**Status:** IMPLEMENTED 2026-08-21 — owner delegated the VFX review/pick; `store.beacon.near` is Marker8 Safe Zone Loop.

# WORK ORDER 1052 — Realm Store landmark beacon: make the storefront findable from anywhere in the hub

**Minted:** 2026-08-21 (UI seat — Claude UI; UI-block banner bumped 1052 -> 1053 in the SAME edit)
**Assigned:** CLI implements. UI authored the design; UI writes no `.cs` (CLAUDE.md §2).
**Lane:** VFX / World (CLAUDE.md §9 — VFX is a parallel lane with no gameplay dependencies)
**Class:** FEATURE (wayfinding). Not a defect.
**Owner request 2026-08-21:** *"can we add some special aura around the realm store so they always
know where it is and stands out"*
**Sibling:** WO-1050 (The Night Market) — this is the world-side half of the same job. The store
cannot earn a purchase from a player who never finds the door.

---

## 0. One-line truth

**An aura only works when the store is on screen.** The ask is *"always know where it is"*, and for
a player standing behind a wall the honest answer to that is not a particle loop — it is a bearing.
So this ships as **three layers**, and only the middle one is particles.

---

## 1. ⛔ READ THIS FIRST — the obvious implementation has ALREADY FAILED, on record

The naive build is "attach a persistent VFX loop to the storefront." **That is the exact thing that
is documented as broken in this repo**, and shipping it would reintroduce the owner's complaint by a
different route.

`VfxLoopBudget.cs:8-25` cites six F8 captures of the loop pool saturating and silently starving live
effects. Two of the named victims are **the POI markers themselves**:

```
capture-20260716-205819.md:99   Poi_NodeAura     <- skipped, cap hit
capture-20260716-210343.md:97   Poi_Landmark     <- skipped, cap hit
capture-20260730-175552.md:55   PlayKey('ArcherTower_Projectile') SKIPPED - active loops 20/20
```

**`Poi_Landmark` is the landmark-marker loop, and it has already gone missing under the cap.** A
permanent store beacon implemented as one more Family-A loop is a beacon that disappears on exactly
the busy evenings when the player most needs it — and it does so **silently**.

The ceiling is now scene-tiered (`village 24 / dungeon 48 / boss 32`) and `VfxAuraProximityCuller`
bounds the population, but **the village tier is 24 and it is the tier a phone spends the most
wall-clock time in**. A store beacon must not depend on winning that lottery.

**Therefore: the layer that guarantees findability must cost ZERO loop slots.** See §2.

---

## 2. The design — three layers, only one of which is particles

### Layer A — the always-on beacon (NO loop slot, never culled)

Built from **rendered geometry and a real light**, not from the particle pool, so it is outside
`_maxActiveLoops` entirely and cannot be starved:

- A **vertical light shaft / banner mast** rising above the storefront's roofline, tall enough to
  clear the surrounding buildings and read from across the hub.
- An **emissive sign element** on the building face itself, lit at its own base.
- One real Unity **`Light`** at the base, gently animated by a curve on its intensity — **animation
  by curve, not by particles**.

This is the layer that discharges the owner's *"always know where it is."* It is on in every scene
state, at every distance, under every load. **It is the requirement; the rest is polish.**

### Layer B — the near aura (particles, proximity-gated, priority-pinned)

The ground-level aura that makes the storefront feel special once you are near it:

- Plays **only inside the proximity ring**, via the existing `VfxAuraProximityCuller`. Outside the
  ring the loop is **stopped**, returning its slot to the pool — so the store is a good citizen of
  the village-24 budget rather than a permanent tenant of it.
- **Priority-pinned** so that when the pool does contract, the storefront is not the first thing
  dropped. If a priority concept does not exist on the culler, **say so and it becomes a separate
  ticket** — do not fake it with a larger radius.
- **Single-seat handle discipline**, copied from `GearAura`: one field, not a collection, so "the
  old aura is still running under the new one" is not representable. **Every exit path stops it** —
  ring exit, `OnDisable`, `OnDestroy`, scene unload.

### Layer C — the bearing (this is what actually answers "always")

Register the storefront as a compass objective. `HudCompassWidget` already takes an
**`ObjectiveProvider`** delegate — *"world pos of the nearest objective / region-gate seam (the
navigation cue the owner asked for)"* — so the seam exists and this is a registration, not a system.

**Without Layer C the request is not satisfied.** A player facing away from the store sees no aura
of any kind; the compass is the only surface that can say *"it is behind you and to the left."*

⚠ **One judgement call, flagged:** the compass objective slot is described as *nearest* objective.
If the store would displace a live quest objective, it must not — it should be a **second, distinct
pin class**, or yield. **This needs the CLI to check the provider's contract before wiring**, and if
it turns out to be single-slot, that is a scoped follow-up, not something to force.

---

## 3. Greyscale is the acceptance channel (owner is red/green colourblind)

`EnemyAuraVFX` already sets the standard in this codebase and it is the right one: its three auras
are *"separated by MOTION and SHAPE, never by hue"* — twitching-and-tight vs roiling-and-wide vs
streaking-and-trailing.

The store beacon must be distinguishable **with all colour removed** from the two landmarks it will
share a skyline with:

| Landmark | Reads as |
|---|---|
| **The Heart of Elarion** | the world tree at centre — organic, broad, slow |
| **Dungeon portals** | `DungeonPortal` / `PortalStructure` — a threshold, pulsing inward |
| **Realm Store (this WO)** | **a steady VERTICAL column with a slow upward drift** — the tallest, thinnest and most regular silhouette of the three; the only one that reads as a *mast* rather than a *mass* or a *doorway* |

**Rhythm separates them as much as shape:** the Heart breathes slowly and irregularly, a portal
pulses, and the store's mast is **metronomic** — a lit sign, not a living thing. A player who cannot
see hue can still name which is which.

⛔ **Do not ask the owner to approve a hue.** Ask about behaviour. The greyscale capture is the gate.

---

## 4. ⛔ What NOT to touch

- **The store's furniture status.** `RealmStoreVendor.cs` records the PROD-003 owner ruling: the
  Realm Store is **baked hub furniture like the Heart, with NO catalog row, and must never get
  one** — because a catalog row makes it sellable, movable, damageable and placeable, and *"a raid
  takes revenue OFFLINE."* **The beacon attaches to the baked furniture. It does not make the store
  a structure, and it must not introduce a catalog row as a convenience for hanging VFX on.**
- **The door.** `RealmStoreVendor` opens `PanelId.RealmStore` through `PackStoreBootstrap`, via
  `TalkPromptRegistry` -> the HUD TALK button. **Do not add a second interaction prompt.** WO-416
  retired the bottom-centre "Talk:" element as redundant clutter and raising a mobile interact
  button here would put it straight back.
- **`VfxLoopBudget` tiers.** Village 24 is a computed budget with six captures behind it. **Do not
  raise a tier to make room for this beacon** — that is the change that starved tower projectiles.
- **`MarketplaceInteractor`.** It is the older walk-up path and its header still documents a UXML
  setup that cannot work in player builds. **Out of scope — log it, do not fix it here.**
- **The store panel itself.** That is WO-1050.

---

## 5. Acceptance

1. The store is identifiable from **the far side of the hub**, at the default camera pitch, with
   Layer B's particles **forcibly disabled**. (This is the test that proves Layer A stands alone.)
2. Layer A consumes **zero** Family-A loop slots. Prove it by reading the active-loop count with the
   beacon on and off — the numbers must match.
3. Layer B starts on ring entry and **stops on every exit path**. Assert the handle is null after
   ring exit, `OnDisable`, `OnDestroy` and a scene unload.
4. With the village pool driven to saturation, **the store is still findable** (Layers A and C are
   unaffected; B may legitimately be culled).
5. The compass carries a store bearing when the store is off-screen, **without displacing a live
   quest objective**.
6. **Greyscale capture:** with hue removed, the store beacon is nameable against the Heart and a
   dungeon portal in the same frame.
7. No new catalog row. No second interact prompt. No tier raise.
8. Captures opened, not just taken: far/mid/near approach, off-screen bearing, pool-saturated, and
   the greyscale pass.

---

## 6. OWNER DELEGATION RESOLVED — VFX pick (2026-08-21)

The owner explicitly asked the CLI to review the available VFX prefabs and add the best fit. That
instruction resolves the earlier hold. The chosen mapping is:

| Hook | Pick | Why |
|---|---|---|
| `store.beacon.near` | `Assets/Resources/VFX/Markers/Marker8_SafeZoneLoop.prefab` | A regular ground ring plus a restrained shockwave. In greyscale it reads as a deliberate safe/store destination, not the Heart's organic cloud, a portal threshold, an enemy aura, fire damage, or a combat cast. It is a real looping prefab, tracked in Resources, and needs only one pooled seat. |

The audit rejected the other plausible tracked families: boss phase/fire auras communicate danger;
healing/acceleration auras communicate a buff; `Aura_TalentNode` is already the node identity;
`Poi_Landmark` duplicates fortress silhouette and is actually a burst-derived row; top-down attacks
are one-shots and read as incoming combat. Marker8 is the only candidate whose *shape* says
"destination" without borrowing another system's meaning.

The earlier held text below is retained as decision history and is superseded by this owner
delegation.

**Standing rule (owner practice, memory `vfx-map-owner-tags-no-creative-pick`): the owner tags the
VFX key in the Caster; the CLI maps key -> named hook verbatim, and never picks or substitutes.**

So this WO deliberately specifies **the hook, the layer, the lifecycle and the budget** — and stops
at the art. What is needed from the owner is one tag:

| Hook | What it needs | Status |
|---|---|---|
| `store.beacon.near` | the Layer B ground aura key | **AWAITING OWNER TAG** |

Two notes to make the tag easy, neither of which is a pick:

- **`Poi_Landmark` already exists** as a landmark-marker key in the catalog (it is one of the
  effects named in the saturation captures). If the owner wants it, it is already there — but
  **whether to reuse it or tag a bespoke key is her call**, and reusing it means the store shares a
  silhouette with every other POI, which cuts against "stands out."
- **Layer A is geometry and a light, not a Caster key.** It needs no tag and is not blocked by this
  section — **implementation can start on Layers A and C immediately.**

⛔ **Do not substitute a "close enough" particle asset to unblock Layer B.** Ship A and C, leave B's
hook wired and dormant, and let the tag land it. A held hook is not a stall; a wrong pick is a
felt-test rejection.

---

## 7. Files

**Create:** a beacon component under `Assets/_Modules/Village/Vfx/` (single-seat handle discipline
per `GearAura`), attached to the baked Realm Store furniture.

**Edit:** the hub scene's baked store furniture **via the scene builder, never by hand**
(CLAUDE.md §3 — `Village.unity` corruption history; the hub is `Main_Castle_Overworld`) ·
the compass objective registration seam.

**Read, do not edit:** `VfxLoopBudget.cs` (the tiers and the six captures) ·
`VfxAuraProximityCuller` · `GearAura.cs` (the handle discipline to copy) ·
`EnemyAuraVFX.cs` (the greyscale standard to copy) · `RealmStoreVendor.cs` (the PROD-003 ruling) ·
`HudCompassWidget.cs` (the `ObjectiveProvider` seam)

**Separate tickets, named not folded:** (a) a priority concept on `VfxAuraProximityCuller`, if it
does not already have one; (b) `MarketplaceInteractor`'s stale UXML header; (c) a second compass pin
class, if `ObjectiveProvider` proves single-slot.
