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

## 5. Final overnight status — ALL DELIVERED (commits 7dfa0e0d + 14953276, pushed)

Everything attempted overnight LANDED, gated, built, and pushed. Two fresh Windows builds cut.
- **WO-736 roster close** — DONE. New `TroopRosterRegression` oracle wired into `DataRegression`; ran headless: `[troop-roster] TROOP_ROSTER_OK` (7 ids, unlock ladder, models+icons, tier copy, gate all pass). Roster program **732–737 COMPLETE**. Canon one-liner in `PIPELINE_STATE.md`.
- **Bryn + Hollow-One** — DONE + baked. Bryn = KayKit Rogue_Hooded (idle controller); Hollow-One = Skeleton_Mage (code-proven `SkeletonHumanoid` rig — won't T-pose). Dungeon rebuilt: "Rigged KayKit character bodies: 2". Eyeball in Play.
- **L2/L3 arcane spire albedo** — DONE. `upgradeTexturePath` mechanism + flat `ArcaneSpire_2/3_Albedo` so upgraded spires don't build-white (upgrade + reload covered).

### Pre-existing regression reds (NOT from tonight — flagging for your awareness)
`DataRegression.RunAll` shows 8 non-roster reds, all pre-existing / fail-by-design. Worth a look when convenient (none block the build; all predate this session):
1. **DATAWEB dual-copy DRIFT** (real, worth fixing): `armor.json` (StreamingAssets 20130B vs Resources 12897B, + version 1 vs 2), `weapons.json` (266592B vs 19765B — big drift), `daily-quests.json`, `stake-rewards.json`, `tower-perks.json`. Resources wins at runtime; the StreamingAssets copies are stale. (My files — troops/building-tiers/structures — are byte-identical, verified.)
2. **BUILD ECONOMY**: `fountain_healing` upgradeVisualPath L2/L3 models (`Structures/Fountain_L2/L3`) load NULL from Resources — the fountain's upgrade meshes are missing (same class as the spire albedo, different structure).
3. **CORESAVE / GLIMMER (fail-by-design)**: `GameState.Tribes`, `.Wards`, `.Arena` W/L, and pet active-slot have no persisted save field — they RESET on reload. Threading these through SaveSchema (+ version bump) clears the reds.
4. **COMBAT (fail-by-design)**: orc-raider has two stat blocks (spawner Hp 95 vs garrison Hp 170) — unify the Wildlands roster.
5. **arena prefab**: `ForestClearingArena` ground material binds no base texture (flat/untextured).
6. **VILLAGE ECONOMY**: plain `Grant(wood)` moves the shop pool but not the upgrade ledger — route income through `GrantSpendable` or unify the pools.
7. **HUDUI**: 5 pre-existing HUD failures (not detailed here).
8. **ui-obsidian WARN** (report-only): 4 hand-rolled UI files bypass ElarionUiKit — incl. the dev tools I added (FlagCaptureButton, ResourceDevTool). Report-only, not gating; worth kit-converting if we ever HardFailOnNew.

Also: `ProjectSettings/TagManager.asset` has a tolerated empty-tag-slot parse warning (pre-existing since 7/13, every build succeeds through it).

### What I deliberately did NOT do overnight (needs you)
- **WO-726 deploy loop** — the CoC milestone; needs your felt-verify, left as the clear next pickup.
- **Headed dungeon-camera capture rig** — not worth building for a deterministic fix; felt-verify the framing (cap is tunable).
- **Speculative features** (#24 Echo engagement, #25 talk-glow, #32 Quick Setup) — held for your direction.
- **The pre-existing regression reds above** — not mine; the DATAWEB drift + fountain models are the most worth a quick fix, but I didn't guess which JSON copy is canonical.
