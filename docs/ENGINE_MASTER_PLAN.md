# Engine Master Plan — scope + foundation-first build order

> Owner directive (2026-05-30): **let the architecture determine the scope — how wide, how many
> systems — and build that FIRST.** This consolidates `CHARACTER_REFACTOR_PLAN.md` (WO-106…118) and
> `WORLD_ENGINE_ARCHITECTURE.md` (WO-119…128) into one scope + a foundation-first sequence. Vision:
> `CHARACTER_ARCHITECTURE.md`, business: `NORTH_STAR.md`.

## The scope the architecture determined (how wide)

Everything is a **typed `def` + a controller, dispatched generically.** The engine spans:

| Domain | Systems |
|---|---|
| **Actors** | `Character` substrate (nav/anim/health/VFX) · `Brain` (PlayerInput / EnemyAI / Pet) · `Equipment` (Weapon/Armor/Skin → `ActionSet`) · action verb (`DoAction`) |
| **World** | `NavSurface` (visual ⊥ navigable) · Terrain/biome/mountain · Weather · Structure (walls/stairs/towers) |
| **Engine core** | `EngineDispatcher` (typed registry of `IBuildHandler`) · `Def` / Catalog / Repo data model · `CharacterFactory` + world builders |
| **Player-side** | Input scheme (LeanTouch / KB / pad) · Camera (follow + **pan** modes, all heroes inherit) · dynamic HUD binding · build-mode (catalog palette + 90° rotate + ghost-snap; base sections + owned cosmetics) |
| **Already built → REUSE** | NavMesh bake · `VFXManager` · `WeatherManager`/`SkyProgressionController` · Monetization (`Wallet`/`PackStore`/`Cosmetics`/`BattlePass`) |

**~5 domains, ~18 systems** — but the world/engine/player pieces are mostly **thin façades over
things that already exist** (NavMesh, VFX, weather, monetization, and the rampart's proven
visual⊥nav decouple). The real new build is **the seams, not the systems.**

## Build the FOUNDATION first (the skeleton everything hangs on)

**Phase 0** of both plans — pure contracts + empty base abstractions. **No behavior change**, so it
ships green without touching the running game, and it **defines every seam the rest plugs into:**

1. **Core enums** — `CharacterType`, `ActionType`, `WorldDefType`.
2. **Core interfaces** — `IBrain`, `IBuildHandler`/`IController`, `Def` base (`IDamageable` already exists).
3. **Base abstractions (skeletons)** — `Character`, `Brain`, `NavSurface`, `EngineDispatcher`
   (typed registry), the `Def` / Catalog / Repo data model.

These compile + ship green while **nothing uses them yet** — the skeleton stands, the seams are
proven to fit, and 23 WOs turn from "a pile" into "slots." Land **WO-106 + WO-119 together** as the
single engine skeleton.

## Then, and only then

- **Adapt existing** to the contracts, one at a time — `Enemy → Character` first (per the plan),
  then Hero, Pet; the world builders → `IBuildHandler`s. Old component stays authoritative until
  in-play parity is confirmed (never big-bang).
- **Fill in features** on the established seams — equipment/`DoAction`, dynamic HUD, build-mode,
  camera modes, terrain/weather handlers.

## Guardrails (carried from both plans)
- Never big-bang: every phase leaves the game playable.
- `DoAction` routes VFX through `VFXManager` ONLY (else it double-fires the DEF hit-feel stack).
- Cosmetics swap the **catalog (look)**, never the **repo (behavior)** — structurally cosmetic-only.
- `VillageSceneBuilder.cs` is a single-touch serialization bottleneck — one editor/branch at a time.
- Runtime player edits carve `NavMeshObstacle` (no per-placement rebake); rebake on build-mode exit.

## How the engine hardens (antifragile — the operating principle)

Every scenario that *invalidates* the engine becomes a **new constraint on the tool, not a one-off
patch.** Because there is one code path, **one constraint protects the entire content space** — every
def, every composition, every player creation — retroactively and forever:

- A monolith bugfix fixes **one instance**; an engine constraint fixes the **whole class, everywhere,
  for good.**
- The space of valid content only ever **grows**; "can a player break X?" converges to "the engine has
  a guard for that."
- The project's hard-won lessons (mount-sync, `DoAction`-VFX-once, cosmetic-only, singleton-dedup,
  NavMeshObstacle-carve, single-touch builder) stop being tribal knowledge and become **enforced
  constraints** — structural guarantees, not docs to remember.

Paired with *test-in-catalog → works-everywhere*: **content scales, testing stays flat, reliability
ratchets up monotonically.** The engine gets *stronger every time it's challenged.* That is the floor
the whole player-created-world vision stands on.

## Deferred north-stars (kept open by the architecture, NOT current scope)

- **Cross-platform persistence (mobile ↔ PC ↔ console).** Build on phone, continue on console — the
  realm is a platform-agnostic **def-list**, Unity is multi-target, and `InputScheme` swaps controls,
  so this is a *build target + sync layer later*, **NOT a rewrite.** Rule: **do not build it now
  (scope); do not architect *against* it** — the data-driven foundation keeps the door open for free.
  Largely untried for this genre (CoC mobile-siloed; Valheim PC; Minecraft cross-plays but isn't
  build-and-defend) — a real differentiator worth keeping reachable.

## Sequence
1. **Foundation** — Phase 0 contracts + skeletons (WO-106 + WO-119), landed as the engine skeleton.
2. **Adapt** — existing entities + world builders onto the contracts.
3. **Features** — on the seams: equipment/HUD/build-mode/camera/terrain/weather.
