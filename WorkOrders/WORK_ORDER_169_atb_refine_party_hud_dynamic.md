<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 169 — ATB Refinement: party-of-4, dynamic HUD, data-driven (top-to-bottom)

**Status: READY TO IMPLEMENT (phased)**
**Priority:** High — ATB is the 2D party-battle screen (BATTLE_2D_PARTY_DESIGN); the engine is ready, the
Unity layer is the gap. Aligns ATB with the project's dynamic/data-driven direction.
**Date:** 2026-05-30
**Lane:** Combat — `DeNelle.BattleATB` (engine + controller + UI). Code only; no VillageSceneBuilder; no bake.
**Source:** full top-to-bottom ATB audit (2026-05-30). Owner ask: review ALL of ATB, refine for HUD + make
it dynamic as everything builds off dynamic object collections now.

---

## ⚠ LIVE STATE 2026-05-31 (owner screenshot — "not sure where to start")
The ATB screen **runs**: HP+ATB bars, "choose an action," Attack/Skills/Item bar, battle log, the hero
renders (Blaise the chibi wizard). What looks broken is exactly the known gaps below — **don't panic, it's
these specific items, not a rewrite:**
1. **Enemies render as PURPLE CAPSULES** — the `AtbCombatantSwapper` (capsule→model swap) isn't running or
   the enemy def isn't mapped to a mesh, so placeholders show. **Highest-visible fix:** make the swapper
   put the real enemy model (Skeleton etc.) on the combatant. (Reuse the village enemy meshes / the
   `MapToEngineDef` mapping — see §P2 data-driven + the swapper.)
2. **1-hero-vs-1, not the party** — controller still builds zero pets/party (§P0). Surface the party.
3. **Layout is hero-LEFT / enemy-RIGHT — WRONG.** Per the locked FF layout it must be **enemies LEFT,
   heroes RIGHT** (see Layout section). Flip it.
4. **No 2D retro VFX/anim yet** — that's WO-170 (separate, pairs with this).
**Start order:** (a) enemy model swap [kills the capsules — biggest visual win], (b) FF layout flip
(enemies left/heroes right), (c) surface the party (§P0), (d) dynamic per-unit HUD (§P1). Then WO-170 juice.

## Audit verdict (the headline)
**The ATB ENGINE is production-grade — deterministic, bit-for-bit RNG-tested, party-ready (N vs N), and
internally table-driven. DO NOT rebuild it.** The refinement is **entirely the Unity layer**: the
controller hard-wires 1-hero-vs-1-enemy and builds **zero pets**, the HUD renders **2 fixed cards** instead
of dynamic per-unit collections, and the designer-facing `CombatantDefSO` data-driven path **exists but is
never connected** to the engine. Fix the layer, keep the core.

### Preserve as-is (do NOT touch)
- All of `Engine/` (Actions/Ai/Combat/Targeting/Turn/BattleState/Scaling/Types) — the math.
- `Rng.cs` + `RngGoldenVectorTest.cs` — bit-for-bit verified; any refactor MUST keep RNG draw order
  identical (respect the inline `F-CMB-1`/`F-ACT-*` order-significant flags).
- `BattleState.Units` = single `List<BattleUnit>` — already the right multi-unit structure (party of N real).
- `ATBRuntimeState` store + its `UnityEvent` observable pattern + clone-at-boundary discipline — exactly
  the state/view separation we want. Build the new HUD ON this.
- `CombatantDefSO` converters (`ToEnemyDef`/etc.) — sound scaffolding; connect it, don't rewrite.

---

## P0 DECISION — LOCKED (owner 2026-05-30): per-member command/AI toggle

**Each party member is individually set to PLAYER-COMMANDED or AI — the player's choice, per member,
changeable anytime in Settings.** Not a fixed "4 controlled" vs "1+3 AI" — a **per-unit control mode** the
player tunes. This resolves the decision and serves both player types (tactician commands all; casual sets
some/all to AI and watches).

**Mechanics:**
- Add a **per-member `ControlMode { Player, AI }`** (on `BattleUnit` / the party-member record), default
  sensible (the Keeper/hero = Player; new recruits default Player but flippable).
- **As each character is added as playable** (joins the party — Bram, Nessa, spirit per
  PARTY_OF_FOUR_STORYLINE), the player can **command them or set them to AI** right then, and **always
  change it later in Settings** (a party-control settings panel: each member → Command / Auto).
- **Engine change:** `Turn.IsPlayerControlled` (`Turn.cs:85`) reads the unit's `ControlMode` instead of
  `Kind == Hero`. A `Player`-mode unit prompts the input UI on its turn; an `AI`-mode unit runs the
  existing AI policy (`Ai.cs` `ChoosePetAction`/archetype logic — already there, reuse for any member).
- Unique member ids (`hero`/`bram`/`nessa`/`spirit` or `member-0..3`) — fix the hard-coded `Id="hero"`
  (`BattleState.cs:60`); `BattleSetup` carries the multi-member party + each one's default control mode
  (currently single `HeroClass`/`HeroName`, `Types.cs:414`).
- Persist the per-member control mode in settings/GameState (so the player's preference sticks).

> This is elegant: the engine already has both paths (player input for heroes, AI for pets) — this just
> makes **which path a member uses a per-member, settings-togglable flag** instead of hard-tied to
> Hero-vs-Pet. Minimal engine change, maximal player flexibility. All four roles present; the player
> decides how much they micro.

---

## Phases

### P0 — surface the party the engine already supports (highest leverage, smallest change)
1. `BattleController.BuildSetup`: populate the party from real game state — **today it builds
   `Pets = new List<PartyPetSpec>()` (zero pets, `:229`)**, forcing 1v1 despite a party-ready engine.
   Feed the actual party (Keeper + companions/pets per PARTY_OF_FOUR + the player's collected pets).
2. Per the P0 decision: enable multiple controlled members (ids, `IsPlayerControlled`, `BattleSetup`).

### P1 — DYNAMIC HUD (the owner's core ask: HUD reads dynamic collections, not fixed cards)
3. `BattleController.Render` (`:492`) currently renders `FirstUnit(Hero)` + `FirstUnit(Enemy)` into **two
   cached cards** (`_heroCard`/`_enemyCard`, `:85-92`) — pets + enemies 2..N are invisible. **Rebuild as a
   data-bound list:** one reusable combatant-card template **instantiated per `BattleUnit`**, keyed by unit
   `Id`, re-bound on `OnBattleChanged`. Iterate `_runtimeState.Party()` / `.Enemies()` — the observable
   selectors already exist. The HUD becomes a **dynamic collection view** (matches the whole-game direction).
4. Add a real **target picker** + **ability/item pickers** — today it auto-targets `LowestHpEnemy` (`:375`)
   and auto-casts "best ability" (`:417`), bypassing player agency and not scaling to N foes/allies.
   `ResolveTargets` already supports all target modes — surface the choice in UI.
5. **Drop/fix the dead UXML** — `BattleHUD.uxml` doesn't clone in builds (CLAUDE.md §8); the code-fallback
   HUD is the real UI. Commit to **code-built** for the multi-card layout; don't maintain a dead 2-card uxml.

### Layout + style — classic Final Fantasy (owner 2026-05-30, locked)
- **Side-view, FF-styled:** **ENEMIES on the LEFT, HEROES (the party) on the RIGHT**, facing each other
  across the battle screen. Party members stacked on the right with their command menu; enemies arrayed
  on the left as targets. Classic FF4–6 staging.
- ATB time-gauge per unit; command menu (Attack / Magic / Item / Defend) on the active party member's turn;
  battle log; HP/MP bars. Code-built. (The full FF screen from BATTLE_2D_PARTY_DESIGN.)

### HUD cleanup + Skills/spell menu MUST work (owner 2026-05-31)
From the live screenshot, three concrete fixes on the battle HUD:
1. **Clean the HUD** — tighten the layout: combatant cards (HP+ATB) tidy in the FF corners, a clean
   command bar (Attack / Skills / Item), readable battle log, themed to match the game (ties WO-175/178
   UI polish). No sprawling/placeholder spacing.
2. **Skills opens the SPELL LIST — it currently goes nowhere.** Blaise is a **caster**; tapping **Skills**
   must open the hero's **ability/spell menu** (the caster's spells — Arcane Bolt / Frost Nova / Heal /
   Meteor, etc., the same Q/W/E/R kit from the village HUD), each showing cost + a pick. Right now Skills
   has **no options** — wire it to the hero's ability set (the engine's `HERO_ABILITIES` per class). A
   caster with no spell menu is the core miss. After picking a spell → target per the rules below.
3. **Item** likewise opens the item list (potions etc.), not a hard-coded single use.

### Targeting rules (owner 2026-05-30, locked)
- **Single-target abilities → the player picks ONE target individually** (tap/select an enemy on the left,
  or an ally on the right for heals/buffs). A clear target cursor/highlight on the selected unit.
- **AoE abilities → default to ALL valid targets** (all enemies for an offensive AoE, all allies for a
  party heal/buff) — no per-target pick needed; the ability auto-selects its whole valid set.
- The engine already supports this: `TargetMode` has `SingleEnemy / AllEnemies / SingleAlly / AllAllies /
  RandomEnemies / Self` and `ResolveTargets` maps each. **Wire the UI to it:** an ability's `TargetMode`
  decides whether the player gets a **single-target picker** (Single*) or it **auto-targets all** (All*).
  AoE = all-by-default; single = individual selection. Don't force a pick for AoE; don't auto-pick for single.

### P2 — data-driven (connect the SO path that's built-but-unwired)
6. The engine reads hard-coded static tables in `Engine/Defs.cs` (`HERO_ABILITIES`/`HERO_STATS`/`PET_*`/
   `ENEMY_DEFS`/`ITEM_DEFS`/`STATUS_BLUEPRINTS`). The SO mirrors (`CombatantDefSO.cs`) exist but **nothing
   consumes them.** Introduce a **registry/`Defs` facade** with a swappable backing source: load
   `EnemyDefSO`/`AbilityDefSO`/`HeroStatsSO`/`PetStatsSO` (Resources/addressable/catalog) → expose the same
   `IReadOnlyDictionary` shape `Defs` provides. **Keep the static tables as the golden/test fallback** so
   the RNG/determinism tests still pin exact values. This makes ATB combatants a **dynamic collection** like
   the rest of the game (catalog/registry pattern).
7. Replace `MapToEngineDef`'s hard-coded `if`-ladder (`BattleController.cs:301`, string `.Contains`
   heuristics mapping village enemies → engine defs) with a **data-driven id→def mapping asset.**

### P3 — polish
8. Lift magic tuning (`CRIT_CHANCE`/`CRIT_MULT`, ATB muls, element matrix `Combat.cs:30`, AI probabilities
   `Ai.cs:41`, bond curves `BattleState.cs:26`) into a `BattleTuningSO` (low priority — values are
   test-anchored; do AFTER the determinism tests are updated to read the SO).
9. Wire or remove the dead ATB-timer feedback (`ATBCombatManager.TurnProgress` / `onEnemyAutoAttack` not
   subscribed/rendered).

---

## Constraints
- **Engine math + RNG draw order: untouchable.** Any data-driven refactor preserves exact draw order
  (golden test must still pass). Static tables stay as the test fallback.
- Build on `ATBRuntimeState` (don't replace the store); HUD = subscriber rendering dynamic collections.
- Code-built UI (no UXML). `DeNelle.BattleATB` assembly. Brace-gate. No bake.

## Acceptance criteria
1. ATB battle runs with a **party of up to 4** vs N enemies — not 1v1; controller feeds a real party (no more zero-pets). **Each member has a per-unit `ControlMode` (Player/AI), set when they join and changeable in Settings**; `IsPlayerControlled` reads it (not Hero-vs-Pet). Player-mode members prompt input; AI-mode members run the existing AI policy. Mode persists.
2. HUD renders **one card per unit dynamically** (party + all enemies), bound to `ATBRuntimeState` collections, re-bound on change — no fixed 2-card layout.
3. **FF layout:** side-view, **enemies LEFT / heroes RIGHT**, facing off; per-unit ATB gauges; command menu on the active member's turn.
3b. **Targeting:** single-target abilities → player **picks one** individually (target cursor); AoE abilities → **auto-target ALL** valid (all enemies / all allies) by default, no pick forced. Driven by the ability's `TargetMode` → `ResolveTargets` (no forced auto-target for single; no forced pick for AoE).
3c. **HUD clean + Skills/Item menus work:** command bar tidy + themed; **Skills opens the caster's spell list** (hero `HERO_ABILITIES` — Arcane Bolt/Frost Nova/Heal/Meteor etc., with cost), Item opens the item list — neither is empty or a hard-coded single action. Pick a spell → then target per 3b.
4. Combatants/abilities/enemies load from **SO data via a registry/`Defs` facade**; static tables remain as the test golden fallback; RNG golden test still passes.
5. `MapToEngineDef` replaced with a data-driven mapping; no hard-coded enemy `if`-ladder.
6. Code-built HUD (dead UXML dropped/fixed); engine math + RNG untouched; brace balance; no bake.

## Done checklist (CLAUDE.md §10)
- [ ] Per-member `ControlMode` (Player/AI) — set on join, toggle in Settings, persisted; `IsPlayerControlled` reads it; party surfaced (controller feeds real party, no zero-pets)
- [ ] Dynamic per-unit HUD bound to ATBRuntimeState collections; target/ability/item pickers
- [ ] SO registry/Defs facade wired; static tables kept as test fallback; RNG golden test passes
- [ ] MapToEngineDef data-driven; code-built UI; engine/RNG untouched
- [ ] Brace balance; no bake
- [ ] `WORK_ORDER_169_atb_refine_party_hud_dynamic.RESULT.md` when complete
