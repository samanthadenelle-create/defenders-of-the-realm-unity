# 2D Party Battle Design — classic Final Fantasy battles (resolving the 1-hero targeting gap)

> Owner catch (2026-05-30): *"in the active battle window it's one hero vs whatever — what good does
> logic targeting do if only 1?"* — **correct.** Targeting/role AI is meaningless with one attacker; it
> only earns its keep with **multiple units.** Owner direction: lean into **classic 2D Final Fantasy**
> (FF4–6 era) — a **party** battle, side-view, time gauges, Attack/Magic/Item menu, pick-your-target.
> A party makes targeting matter. Design on top of the **already-built `DeNelle.BattleATB` engine.**

---

## The contradiction, resolved

**Targeting AI ("focus healers → ranged → tanks") is a decision about which of MY units hits which of
THEIR units. One hero = nothing to coordinate = the logic is dead weight.** It only matters with multiple
autonomous-or-directed units. Where it actually matters:

| Mode | Units on your side | Targeting depth? |
|---|---|---|
| 1-hero ATB (today) | 1 | ❌ pointless — the gap |
| **2D party battle (this doc)** | **3–4 party members** | ✅ the FF core — role + target choice |
| Village defense | towers + pets + hero | ✅ (separate, real-time) |
| Async PvP arena (end-game) | an army | ✅ (the NS skill knob) |

**Fix = make the battle a PARTY** (FF is *never* a lone hero — that was the tell). Then targeting, roles,
and turn order are the gameplay.

---

## RECONCILE — the FF engine is ALREADY BUILT (do NOT rebuild)

`Assets/_Modules/BattleATB/` is a C# port of a full turn/ATB battle system — verified:
- **`Engine/BattleState.cs`** — battle state + `BattleUnit` (Hp, Alive, statuses, etc.).
- **`Engine/Turn.cs`** — turn/ATB time-gauge ordering.
- **`Engine/Actions.cs`** — `resolve*` family: **attack / ability / item / defend / rally / enemy-special**
  + `applyAction` dispatcher + the battle log ("X hits Y for N", "Y falls").
- **`Engine/Targeting.cs`** — target selection (the thing that's useless at party-size 1, gold at 3–4).
- **`Engine/Ai.cs`** — enemy AI. **`CombatantDefSO.cs`** — unit defs. **`Rng.cs`** (seeded — deterministic,
  unit-tested). **`BattleScaling.cs`**, **`Combat.cs`** (damage). UI + State folders + Tests.
- **`BattleController.cs` / `ATBCombatManager.cs`** — the runtime drivers; **`AtbCombatantSwapper.cs`** —
  enemy model swap.

**So the battle ENGINE exists and is FF-shaped.** The gap is three things on top: **(1) a real party**,
**(2) 2D side-view presentation**, **(3) wiring it as the world/dungeon encounter resolver.** This is a
*completion + reskin + connect* job, not a new engine. (NS flags ATB "keep/park/cut" — owner = **KEEP**,
and this is what makes it worth keeping.)

---

## What to build

### 1. The PARTY (this is what makes targeting matter)
- Player side = **3–4 `BattleUnit`s**, not one. Sources that already exist:
  - **The hero** (always).
  - **Pets/companions as party members** — you already have a Pet system + roster. The Pet Home
    collection *becomes your battle party* — a beautiful tie-in (collect pets → field them in battle).
  - Future: recruited allies (encounter NPCs, WO encounter system).
- **Roles** (the codex already has them: tank/DPS/healer/caster) → party composition is a real choice:
  bring a healer or more DPS? Now `Targeting.cs` matters — focus-fire the enemy healer, protect your own.
- **Player authors the party** (who's in it, their gear/+1 perks from crafting) — the build-identity knob.

### 2. 2D side-view presentation (the classic FF look)
- **Side-view battle screen:** party on one side, enemies on the other (FF4–6 layout). 2D/billboarded
  sprites or flat-facing 3D — the engine is presentation-agnostic; this is a **view** over `BattleState`.
- **ATB time gauges** per unit (the bars filling — `Turn.cs` drives the order; the UI shows the gauge).
- **Command menu** when a unit's gauge fills: **Attack / Magic(Ability) / Item / Defend** (Actions.cs
  already resolves all of these) → then **pick a target** (Targeting.cs).
- **The battle log + damage numbers** (Actions.cs already emits the log lines).
- Code-built UI (no UXML — repo rule). This is the **biggest new work**: the screen, gauges, menu, target
  selector, sprites — all a *view* on the existing engine.

### 3. Connect it as the ENCOUNTER resolver (ties to the encounter system)
- World/dungeon **random encounters** (see `ENCOUNTER_SYSTEM_DESIGN.md`) can resolve into a **2D party
  battle** — a classic-FF "you were ambushed!" transition into the battle screen, then back to the world.
  (Reuse the `DungeonController`/scene-transition pattern — battle screen as an overlay/scene.)
- Some encounters → real-time (the open-world hero), some → the 2D battle screen (FF-style set-piece).
  Owner/creative decides which encounters trigger which. The battle screen is a **mode**, reachable from
  the world, not a disconnected side area.

---

## Why this is the right move
- **Resolves the targeting contradiction** — a party is the *only* context where the role/target logic the
  NS describes is real gameplay. The engine's `Targeting.cs`/`Ai.cs` finally have a reason to exist.
- **It's genuinely Final Fantasy** — party + ATB gauges + Attack/Magic/Item + pick-target + side-view = the
  FF4–6 battle, which is what the owner is leaning into.
- **Reuses the most code of anything** — the battle *engine* is fully ported + tested; this is party-ify +
  reskin 2D + connect. High payoff, contained scope.
- **Ties the loops together** — your **collected pets become your battle party**; your **crafted +1 gear**
  (DEFENSE_DEPTH_ANALYSIS) equips them; **encounters** (ENCOUNTER_SYSTEM) trigger battles. The cozy
  collection loop and the combat loop meet on the battle screen.
- **Foothold for the arena** — party + targeting + role AI is the same brain the async-PvP army needs;
  building it here is a down-payment on the end-game.

## Open questions for owner / creative
- **Party size** — 3 or 4? (FF classic = 4; 3 is leaner for mobile.)
- **Pets-as-party** — is the battle party drawn from your collected pets, dedicated companions, or both?
- **Which encounters → 2D battle** vs real-time? (Set-piece/dungeon = 2D battle; roadside ambush =
  real-time? Or all encounters → the battle screen? Creative call.)
- **ATB flavor** — active (gauges fill in real time, pressure) or wait (pauses on menu, classic/relaxed)?
  FF offered both; mobile-friendly = a wait/active toggle.
- **Keep/park check** — NS flagged ATB keep/park/cut; owner = KEEP. Confirm it's worth the 2D-view build
  now vs after the core base-building loop (the keystone). Recommend: **design now, build after the
  vision keystone (build mode) lands** — unless the 2D battle IS the near-term showcase.

## Build shape
- **Engine: reuse as-is** (`DeNelle.BattleATB.Engine` — party is just N player `BattleUnit`s; it already
  supports multiple units + targeting).
- **New: the 2D battle VIEW** (`BattleATB/UI` — side-view screen, gauges, command menu, target selector,
  sprites) — code-built.
- **New: party assembly** (pull hero + chosen pets/companions into the `BattleState` player side, with
  their gear/perks).
- **New: encounter→battle transition** (enter battle from a world encounter, return on win/flee).
- Reuses: Pet system (party), crafting/+1 gear (equip), encounter system (trigger), scene-transition (enter/exit).

🤖 Design doc (UI lane). Reconciled to the BUILT `DeNelle.BattleATB` engine (BattleState/Turn/Actions/
Targeting/Ai/CombatantDef/Rng — all ported + tested), the Pet system, crafting (DEFENSE_DEPTH_ANALYSIS),
and the encounter system. No code/scene/bake.
