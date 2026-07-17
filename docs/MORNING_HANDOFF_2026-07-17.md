# Morning Handoff — 2026-07-17 (overnight autonomous session)

Point-in-time report (frozen; §15). Branch `wip/village2-and-f8-tickets`, all committed + **pushed** to origin.
Latest Windows build: `Builds/Windows/DefendersOfTheRealm.exe` (rebuilt overnight, run the .exe not a stale editor session).

---

## 1. What landed (all committed + pushed)

**Device/felt-test wave (your F8 flags):** dialogue box fills frame · Realm Store buy buttons fixed · tabbed Upgrade/Skills building panel · Echo swapped ice-wolf -> ethereal **Aether-Sprite** + magenta aura fixed · orphaned "vfx with no tower" self-clears · hub + **placed** arcane towers coloured · forge footprint right-sized · well scrapped · "Enter Elarion" gates removed · walk-into-walls fixed (NavMesh obstacles, archway passable) · death plays anim then **respawns in town** · Knight has his weapon.

**Overnight batch (commit 7dfa0e0d):** aura now a real soft glow (not white squares; fixes Echo + Heart-tree) · all 4 store panels one shared size · **out-of-battle "Repair All"** (hub button, gated on affordability, un-breaks broken towers) · **dungeon camera height capped** (was ~9u over the ceiling) · dungeon interior dressed with owned KayKit (5 placeholders -> real meshes) · dungeon-entrance portal has an arcane rune-ring VFX.

**Programs:** WC3 6-building upgrade system (tabbed panel + phase-1 perks + army-cap + auto-harvest, Village-tier raise wired) · Grok CoC charter WO-723 LOCKED · WO-724/725 done (flag-off) · roster program WO-732/733/734/735/737 done (+736 closing overnight).

---

## 2. Grok-related stories — pick up here

### Roster program (WO-732 -> 736) — CLOSING overnight
- 732 data / 733 unlock gate / 734 tier-copy / 735 troop visuals / 737 Obsidian Train layout = DONE. 736 (regression + canon) landing overnight.
- Barracks trains a 7-troop tier-unlocked roster (Footman+Archer default; Spearman T2 ... Echo Legionnaire T6).
- **Residual (owner-sourced art):** real Ranger mesh (Outrider), real Mage mesh (Battlemage), optional bow/staff icons, spearman spear prop. All future JSON `model`/`iconId` swaps — no code needed.

### CoC offense program (WO-723 -> 731) — NEXT
- **723 charter LOCKED** (Path A = Barracks army -> ArmyStorage -> RaidDeployController; entry = Arena Herald -> Path A camp-select; RESULT is binding law).
- **724 Barracks live** + **725 Herald entry** = implemented (flag-OFF; testers set PlayerPrefs `ff.barracks`/`ff.arena`=1).
- **NEXT = WO-726** (AI camp attack loop: Herald camp-select -> GoRaid RaidBase_* -> deploy trained army -> clear -> loot -> return). The tap-deploy plate (RaidDeployController) already exists; 726 is the wiring. This is the milestone that makes the offense loop *playable* — recommend it first when you're back (needs your felt-verify).
- Then 727 recipe AI settlements -> 728 raid economy -> 729 defend&watch -> 730 async PvP -> 731 felt-close + flag flips (PO closes).
- **Next free WO number = 738.**

### Building-upgrade phase-2 (docs/design/BUILDING_PERKS_DESIGN.md, WO-738+)
Model-swap for hub buildings (738), more perks/synergies, offline income, tower armor, new tower types, hero equipment/salvage, abilities. Model list: docs/design/BUILDING_TIER_MODEL_REQUIREMENTS.md (you own most; ~10-14 hero/tower meshes to buy, economy + Arcane-T3 first).

---

## 3. Needs YOUR eyes / your call (not blockers)

1. **Aether-Sprite (Echo) idle** — right model, but ships no idle clip, so it stands static. Drop a humanoid idle at `Resources/Pets/aether-sprite.controller` (you offered to rig) and it animates.
2. **Dungeon camera** — height-capped (math + trace verified). I did NOT build a one-off headed capture rig overnight for a deterministic fix — please felt-verify the framing; cap is tunable (`_maxHeightAboveHero`, ~7-11).
3. **3 dungeon props** with no owned mesh — fireplace, water puddle, rug — source later (JSON/prefab swap).
4. **Bryn + Hollow-One** dungeon characters — swapping capsules -> KayKit Rogue/Skeleton overnight (see §4); eyeball the result.
5. **DunGen** — verdict: not needed now (art-finish gap, you own the kit); revisit only if dungeons become a procedural-replayable pillar.
6. **Dev flags** still default-ON for testers (DevResourceTool, FlagButton) — flip OFF before any public release.

---

## 4. Also attempted overnight (see §5 for final status)
WO-736 close · dungeon Bryn/Hollow-One capsule -> rigged KayKit character swaps · L2/L3 arcane-spire albedo (so upgraded spires don't build-white, mirroring the L1 fix).

## 5. Final overnight status
(updated at end of overnight run — see the last commit + this section for what actually shipped vs deferred.)
