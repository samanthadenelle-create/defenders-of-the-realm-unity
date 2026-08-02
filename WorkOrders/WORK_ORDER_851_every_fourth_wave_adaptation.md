# WORK ORDER 851 — Every-4th-wave BOSS encounters (+ statistical adaptation)

**Status:** READY TO IMPLEMENT (specced 2026-08-02). **Lane:** Combat/AI + Waves + Audio/UI.
**Origin:** owner design conversation 2026-08-02, sparked by a Gemini/Grok proposal for
AI-driven adaptive enemies. **No AI library, no cloud call, no ML model — statistics only.**

---

## 0. Owner rulings

| # | Ruling | Source |
|---|---|---|
| R1 | Use **statistics**, not an AI/ML library | *"we can use statistics to determine how to pivot right?"* |
| R2 | Change **enemy types / strategy** by play style | *"Maybe change the enemy types or strategy based on standard play style?"* |
| R3 | Cadence = **every 4th wave** (4, 8, 12, 16, 20) | *"Couldnt we use these on the every 4 level enemies"* |
| R4 | Those waves are **BOSS enemies** — we already structure them larger | *"make them boss enemies we already structure them as larger"* |
| R5 | Give them **the same flair as the wave-20 boss: HP bar + boss music** | *"same flair as the lvl 20 boss with a hit point bar and boss music"*, *"same as with synth"* |

**Design read:** R3+R4+R5 together solve the problem the feasibility pass exposed — that
adaptation aimed at "bosses" would land on content that is either rarely reached (Syndrath,
wave 20) or does not READ as a boss (waves 5/6 are plain `Enemy` with pinned HP: no bar, no
phases, no music). Making every 4th wave a *presented* boss creates a recurring, legible
moment for the adaptation to live in. The cadence teaches itself; the flair makes it read as
intentional rather than as the game quietly punishing good play.

---

## 1. Verified state (read-only pass, 2026-08-02 — cite before arguing)

### Already exists — free to use
- **Larger enemies (R4 confirmed).** `EnemyFactory.cs:35 sizeScale`, applied to collider +
  capsule (`:58-60`, `:309-310`); the tree already ships a 4x wight and 3-5x giants
  (`:269`, `:280`). Boss scale is a parameter, not new work.
- **Ground bosses already spawn on 5/6/12/18.** `WaveManager.cs:1330-1336` reads
  `wave.Boss` / `wave.BossHp` from `waves.json` and pins HP via `Enemy.cs:663 OverrideMaxHp`.
- **Apex boss path.** `WaveManager.cs:1342` → `:1605 SpawnApexBoss`; `DragonBoss.cs:158`
  (phase machine `:120`, thresholds `:302-305`, `Configure` `:429`).
- **`WaveCompositionBuilder.Build(waveId, catalog, seedSalt)`** (`WaveCompositionBuilder.cs:161`)
  is **pure + deterministic** — seeds at `:170`, restores `Random.state` at `:260`. Family
  pools `BrutePool :305` / `CasterPool :312` / `ElitePool :318` are spread round-robin by
  `AddVaried :333` — that round-robin IS the bias hook. `_smartComposition` is serialized **1**
  in both live hubs, so this is the path that actually runs.

### Needs work — the real cost of R5
1. **`BossHealthBar` is hard-typed to `DragonBoss`** — `BossHealthBar.cs:69 private DragonBoss _dragon`,
   `:109 FindFirstObjectByType<DragonBoss>()`, `:143 ShowFor(DragonBoss)`.
   **OWNER RULING (R6): generalize the bar so it is driven by JSON, not by a type.**
   *"would be better to generalize the hitbar so we can pass json to it"*. So the bar becomes a
   catalog-driven presenter, matching how every other surface in this project is authored:
   - A **`boss-bars.json` dual-copy catalog** row per boss: display name, phase thresholds,
     optional subtitle/telegraph, mix hints. Authoring a new boss's bar becomes a JSON edit,
     not a code change — the same win as `repo.npcModel` (WO-818) and the Echo balance catalog.
   - A tiny runtime binding (`IBossPresence`-shaped: Hp, MaxHp, IsAlive, DisplayName) so the bar
     can drive **any** boss — `DragonBoss` today, a wave-boss `Enemy` next — without knowing the
     concrete type. Phase pips come from the JSON thresholds instead of DragonBoss's constants.
   - Syndrath keeps his exact wave-20 presentation: his current constants become his JSON row.
     `DragonBoss` itself stays read-only where possible — the bar was deliberately written to
     touch only its public surface (`BossHealthBar.cs:25`), and that discipline holds.
2. **There is NO boss music track**, and **OWNER RULING (R7): reuse the least-used existing
   track rather than sourcing new audio** — *"use whatever is used the least"*.
   Measured across `Assets/` (excluding the audio module's own registry/mapping tables and tests):
   | track | gameplay call sites |
   |---|---|
   | **Raid** | **1** — `RaidGarrisonSpawner.cs:156` (and it has **no `MusicTrackRegistry` mix row** at all) |
   | Title | 1 — `StoryIntroController.cs:163` |
   | Arena | 2 — `BattleArena.cs:532`, `ArenaMode.cs:191` |
   | Defeat | 2 — `GameOverScreen.cs:228`, `BattleArena.cs:1930` |
   ⇒ **Raid's clip is the least-worked asset.** Add a `Boss` enum row to BOTH enums
   (`Assets/_Modules/Audio/MusicTrack.cs:28` and the Core mirror
   `Assets/_Modules/Core/Audio/MusicTrack.cs:10` — they must stay in sync) and point it at the
   existing Raid clip, with its own registry mix row. **Do NOT repurpose the `Raid` enum value
   itself** — `RaidGarrisonSpawner` still uses it, and silently changing what Raid plays would
   be a behaviour change nobody asked for. Reuse the ASSET, add the ROW.
   Swapping in a bespoke boss track later is then a one-line registry change.

### FICTIONAL today — cut from scope, do NOT spec against these
- **`dodgeBias` (left/right): no dodge action exists anywhere.** The only movement ability is a
  **forward-only** blink (`HeroAbilities.cs:1012`). Unmeasurable. **CUT.**
- **Elemental resistances: element is DISCARDED before damage math** —
  `EnemyDamageable.cs:121-123` forwards raw damage; `Enemy.cs:1928/1954` takes no element. And
  the hero's basic melee is hardcoded `DamageElement.None` (`PlayerAttackController.cs:562`),
  so a fire resist would be **literally unfeelable for a Knight**. New system *and* a feedback
  dead end. **CUT.**
- **`rangedShare`: no ranged basic attack exists** — the split would only restate the player's
  class. **CUT.**
- **`topDeathCause`: death records a POSITION, never an identity** (`HeroHealth.cs:461`;
  `OnDied`/`OnDeath` parameterless `:204`/`:679`). **DEFER.**

### Landmines
- **`TacticalData` archetypes are STATIC SINGLETONS** (`EnemyBrain.cs:264-346`). Mutating one
  leaks to every enemy of that archetype for the session. A per-boss override MUST
  `ScriptableObject.CreateInstance` a fresh copy.
- **`Enemy.ApplyWaveScaling` (`Enemy.cs:634`) is multiplicative and compounding** — never reuse
  it as a setter. Add an absolute `SetMoveSpeed(float)` if speed must be set.
- **Do NOT re-add `enemies[]` batches to `waves.json`** — retired 2026-07-30 (WO-783 D1);
  `WaveAuthoringLiveRegression.cs:62` FAILS the gate if live-looking batches reappear. All
  roster work goes through `WaveCompositionBuilder`.

---

## 2. Scope

### Phase A — the boss WAVE (delivers the felt moment on its own)
1. Every wave where `waveId % 4 == 0` is a boss wave (4, 8, 12, 16, 20). Wave 20 keeps
   Syndrath; 12 keeps its authored `necromancer`; 4/8/16 gain one.
2. The boss is an `Enemy` at boss `sizeScale` with pinned HP (existing `OverrideMaxHp`).
3. **JSON-driven `BossHealthBar`** (R6): `boss-bars.json` dual-copy catalog + a type-agnostic
   binding, so the wave boss gets Syndrath's bar and a future boss is a JSON row, not a code edit.
4. `MusicTrack.Boss` in BOTH enums + a registry mix row, pointed at the existing **Raid** clip
   (R7 — least-used asset; the `Raid` enum value itself is left alone).
5. A short ASCII intro telegraph naming the boss.

### Phase B — the statistical adaptation (rides ON the boss wave)
6. `PlayerCombatProfile` — session-scoped, pure C#, in `DeNelle.Core` with a real namespace
   (NOT a loose global-namespace script; Unity cannot serialize a raw `Dictionary`, and our
   persistence is Newtonsoft via `SaveSchema`, so any persisted field is `[JsonProperty]`).
   Fields limited to what is genuinely measurable today:
   - `MeleeSwings` — one call site, `PlayerAttackController.cs:562`
   - `SpellCasts` — `HeroAbilities.cs:400 TryCast` / `:459 TryCastExtra`
   - `AvgEngagementRange` — free from the existing `OverlapSphere` at `PlayerAttackController.cs:533`
   - `SampleCount`; derived `MeleeShare` / `SpellShare`
7. `BossCounterTable` — authored dual-copy JSON with **hard caps**, emitting a **roster bias**
   (per-family weight multipliers) + a **telegraph** string. Rules evaluated deterministically.
8. Bias applied ONLY on boss waves, inside `WaveCompositionBuilder` AFTER the existing seed so
   determinism holds.

### OUT (v1)
Resistances, dodge bias, ranged share, death-cause; per-enemy `TacticalData` overrides (defer
to v2 — see the singleton trap); persisting the profile (that is v37 + a `MigrateToV37` step;
`SaveMigrator.cs:484` requires the top step to equal `SaveSchema.CurrentVersion`). Any
cloud/LLM/TFLite provider — if ever wanted it authors the JSON **between sessions** behind a
provider seam, never in the combat loop.

## 3. Guardrails (these are the feel)
1. **Cap every multiplier.** Bias changes a wave's texture; it must never invalidate a build.
2. **Composition over resistance** — send different enemies, don't nerf the player's answer.
   (Also the only option that exists — see §1.)
3. **Telegraph, or it is rubber-banding.** Cadence + bar + music + line = "the world answered";
   silent stat drift = "the game cheats". ASCII text, never meaning by colour alone.

## 4. Acceptance
- [ ] Waves 4/8/12/16/20 spawn a presented boss: boss-scale body, HP bar, boss music, intro line
- [ ] Non-multiples of 4 are **byte-identical** to today's composition (the no-regression pin)
- [ ] Same `waveId` + same profile ⇒ identical roster every run (fleet-replayable)
- [ ] `BossHealthBar` drives BOTH `DragonBoss` and a wave boss through the interface; Syndrath's
      wave-20 behaviour is unchanged
- [ ] Both `MusicTrack` enums stay in sync; a registry mix row exists for `Boss`
- [ ] `waves.json` gains no `enemies[]` batches (gate law)
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK` + EditMode + `UI_CAPTURE_OK` (bar PNG opened)
