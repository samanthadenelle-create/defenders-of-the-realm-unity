# Vision Gap Analysis — connecting NORTH_STAR to where we are (2026-05-30)

> Owner asked: *"look at the North Star and where we are, determine what I'm missing to start connecting
> them as a vision, closing the gap."* This is that map — what the vision needs, what exists, and the
> **one missing keystone** plus the connective tissue, in build order. Design/analysis only.

---

## The one-line test

North Star fantasy: **"Build your own stronghold, claim and defend the resource nodes around it, and
grow it even while you're away."** Read that sentence as a checklist against the repo:

| Vision clause | In the repo? | Gap |
|---|---|---|
| **"Build your own** stronghold" | ❌ **NO player build mode** (verified: no `BuildMode`/`PlacementGrid`/`BaseLayout` files) | **THE keystone gap** |
| "stronghold" (walls/towers/buildings) | ✅ built (VillageSceneBuilder authors it) | but *designer*-built, not *player*-built — the inversion the NS calls "the drift" |
| "**claim** … resource nodes" | 🟡 nodes exist (`MineNode`); claim-by-settlement = **spec'd (WO-159), not built** | building |
| "**defend** the nodes" | 🟡 waves/enemies built; roaming-node-raids = **spec'd (WO-160), not built** | building |
| "resource nodes **around it**" | ✅ OuterWorld + regions + nodes baked (WO-142) | landed |
| "grow it **even while away**" (offline) | ❌ offline accrual **not built** (no OfflineAccrual/AutoHarvest in code) | gap |

**The verdict matches the North Star's own diagnosis: the bones are built, but the spine verb — CREATE
(player build mode) — and the loop's *connective tissue* (claim, auto-harvest, offline, upgrade-from-
haul) are the gap.** Everything else is supporting cast that already exists.

---

## What you're missing — in priority order (the gap, closed)

### 🔑 GAP 1 — Player Build Mode (THE keystone; nothing else completes the vision without it)
The North Star says it plainly: *"let the player do what VillageSceneBuilder does."* It does **not exist**
in code yet. This is rung 5 of the ladder and the CREATE verb. Everything — node settlements (WO-159),
the arena defense you author, "your layout" — **hangs off this one system.**
- **Good news (NS §"what getting back on vision means"):** the primitives exist — `BuildMenu` places
  buildings, walls are modular, the catalog data model + `StructureFactory` (WO-148) are built, the
  polyperfect palette (WO-101) was stocked *for this*. The build-mode architecture is already spec'd
  (`docs/build-mode-architecture.md` / WO-108) — `PlacedStructureData` + a `BaseLayout` in GameState +
  a runtime loader that's the twin of VillageSceneBuilder.
- **What's missing = the actual implementation of WO-108:** the `PlacementGrid`, `BuildModeController`
  (enter/place/move/rotate/sell), and `BaseLayout` persistence. **This is the single highest-leverage
  thing to build. It is the heart.** → *promote WO-108 to the top of the build queue after the playtest
  blockers.*

### 🔗 GAP 2 — Offline accrual (the "grow while away" clause — currently absent)
The fantasy's last clause ("grow it even while you're away") has **no implementation** (no offline-harvest
code found). Mines/settlements/pets must accrue resources up to a cap while the app is closed, then pay
out on return. Spec'd as WO-115 (referenced) but not built. **Without it the loop is missing its idle
hook — the retention spine of the whole genre.** → build after the harvest nodes work.

### 🔗 GAP 3 — The loop's connective tissue (each half-there, none closed end-to-end)
The core loop is `BUILD → HARVEST → UPGRADE → DEFEND → OFFLINE → repeat`. Today these exist as
**disconnected pieces**, not a closed cycle:
- **Wall tiers** (wood→stone→reinforced, paid from haul) — WO-151 spec'd, not built. The CoC upgrade sink.
- **Auto-harvest** nodes → settlement (WO-159) → wallet — spec'd, building.
- **Claim/defend nodes** (WO-159/160) — spec'd, building.
- **`ThreatLevel`/zone foundation** (WO-164) — the shared dial all of the above read — **not built yet**;
  it's the under-layer the world features need.
→ **The connective work is mostly spec'd; it needs building + wiring into ONE closed loop you can play
through start to finish.** Right now you can't do a full lap of the core loop in one sitting — that's the
real "gap" felt in play.

### 🟣 GAP 4 — The end-game that monetizes (Arena / async PvP — designed, 0% built)
The North Star's revenue engine is the **Challenge Arena (async PvP raids)** — you author both your base
(needs GAP 1) and your attack AI (`FindBestTarget` → group tactics). **None exists** (no Arena/raid/
leaderboard code; `FindBestTarget` not implemented). This is correctly *later* (rungs 6+/end-game) — it
**depends on build mode existing first** — but it's the thing the business model rests on, so it should be
the **known destination** every combat-AI decision routes toward (the smart-targeting scorer is the cheap
first tier).

---

## What you are NOT missing (so you don't over-build)
- Combat, waves, `EnemyBrain`, towers, walls, pets, economy/`GameState`, save-sync, VFX, HUD, clans/chat,
  the wallet/currency rail, the catalog data model, the two-scene world + regions + nodes, hero animation,
  the castle — **all built.** The North Star is right: *"the hard systems are already built."*
- Monetization stack (wallet/packs/cosmetics/referral/promo) — built; it's the layer the arena *feeds*,
  not a thing to build now.
- Dungeons engine (`DungeonController`) — built; D2–D11 are *content* (designed), not engine gaps.

---

## The closing-the-gap sequence (vision → playable lap → end-game)

**Phase A — make ONE full lap of the core loop playable (closes GAPs 1–3, the heart):**
1. **Playtest blockers first** (queue Tier 1: gates/camera/veins/errors) — so the village is playable at all.
2. **`ThreatLevel`/zone foundation (WO-164)** — the dial the world reads.
3. **Player Build Mode (WO-108)** — THE keystone. Place/arrange your own walls/towers/mines.
4. **Wall tiers + harvest auto-collect + claim (WO-151/159/111)** — the BUILD→HARVEST→UPGRADE→DEFEND tissue.
5. **Offline accrual (WO-115)** — the "grow while away" clause.
   → **Milestone: a player can build a base, harvest+defend nodes, upgrade from the haul, close the app,
   and come back richer.** *That is the North Star fantasy, minimally complete — one playable lap.*

**Phase B — depth + world (the Explore rung, mostly spec'd):**
6. Region enemies + tribes + crystal mines + dungeon portals (WO-155/160/153/165) + dungeon content (D2–D11).

**Phase C — the end-game that pays (the destination):**
7. Smart targeting (`FindBestTarget`) → group tactics → **Challenge Arena async PvP** (WO-future) →
   leaderboards → tournaments. Needs Phase A's build mode. The monetization flywheel turns here.

---

## The single sentence answer to "what am I missing?"

> **You're missing the CREATE verb — player build mode — and the wiring that closes BUILD→HARVEST→
> UPGRADE→DEFEND→OFFLINE into one playable lap. Almost every supporting system is already built; the gap
> is the heart (build mode) plus connective tissue that's mostly already spec'd. Build WO-108 next after
> the playtest blockers, then wire the loop closed — that's the moment the pile of systems becomes the
> game in the North Star.**

🤖 Analysis (UI lane). Verified against NORTH_STAR.md + repo (no BuildMode/PlacementGrid/BaseLayout/
offline/arena code present). No code/scene/bake.
