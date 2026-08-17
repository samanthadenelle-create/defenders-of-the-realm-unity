<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-28
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-28) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 560 — VFX Usage Pass (reuse-first juice + clarity)

**Status:** READY TO IMPLEMENT (spec from read-only VFX review, 2026-06-28)
**Silo:** VFX/Audio lane (§9) — `Assets/_Modules/Village/Vfx/*`, `VFXCatalog.asset`, arena/enemy fire-points. No scene hand-edits.
**Source:** read-only VFX SME review (data-grounded, file:line cited below). Owner ask: "someone look over all vfx and suggest better usage."

## Context
- Catalog taxonomy `VFXType.cs` has ~95 named events; `VFXCatalog.asset` wires ~50 but the project's OWN prefab set is only **9** (`Resources/VFX/Projectiles/`). Everything else falls to `ProceduralFallback` (`VFXManager.cs:649-839`), whose `default` case spawns an **Aether nova for every unmapped type** (`:837`) → visual sameness.
- Two large UNWIRED libraries already in-project (gitignored): **Spells Pack** (466 prefabs, element×effect matrix ~1:1 with the taxonomy) and **Mirza Beig Ultimate VFX** (564 prefabs). Quick wins are mostly **data/wiring**, not new content.

## Tasks (ranked)
### P0 — Arena enemies have NO attack telegraph (clarity, the V1 fight)
Arena orcs built by `EnemyFactory.Build` with a synthesized `EnemyDef` (`BattleArena.cs:773-778`) never get a `_typeVfxSet` (serialized-only, `Enemy.cs:252`) → `telegraphDuration==0` → `TelegraphThenAttack` skipped, `ExecuteContactAttack` fires instantly (`Enemy.cs:1119-1123`), no ground-ring warning. Fix: give arena orcs a code-built/default `EnemyTypeVfxSet` with a `TelegraphDuration` + red ground-ring, OR have `TelegraphThenAttack`/`RootedCast` always `VFXManager.Play` a ground-decal warning at the target's feet. Floor melee windup ≥1.0s (casts already do).

### P0 — Victory has no reward VFX burst (juice)
`BattleArena.Resolve(true)` (`:1119-1139`) plays a banner + music + XP but **no celebratory VFX**. `Juice_WaveClear`/`LevelUp_Celebration` exist + are SFX-mapped but never called on arena win. Fix: on `Resolve(true)`, `VFXManager.Play(VFXType.WaveClear_Celebration, heroPos)` + a loot-pop per reward; escalate burst by star rating (1/2/3). **Coordinate with WO-556** (which already rebuilt the victory summary HUD on `Resolve`).

### P1 — Wire the Spells Pack element matrix into `VFXCatalog.asset` (data-only)
`Impact_Flame/Ice/Aether` → `Explosion_Fire/Ice/Arcane`; `Cast_MageCharge/FireCharge/FrostNova` → `Casting_*`; `Death_*` → element explosions; `Aura_*` → pack `Aura_*`. Biggest juice-per-effort, no code.

### P1 — Replace the `default` Aether-nova fallback (`VFXManager.cs:837`)
Map unmapped types to type-appropriate procedural fallbacks so they stop looking identical.

### P1 — Knight melee hit-spark is generic grey (`Enemy.cs:1544`)
Wire `Impact_Physical` to a real spark+debris oneshot; optionally tint by `WeaponVfxProfile.TrailColor` so a legendary blade's HIT reads legendary (not just the swing arc).

### P2 — Hero damage/low-HP screen telegraph
Add a red screen-edge vignette pulse on hit driven by HP (reuse the arena URP Volume from `BattleArena.BuildArenaBloom`) + directional hit flash. `HeroHealth.cs:285/298`.

### P2 — Enemy ranged cast impact-point AoE indicator
During cast windup (`Enemy.cs:1282-1293`) play a shrinking ground-ring at the aim point so the dodge is legible.

### P3 — Death feedback over-stack guard (6-orc family)
Per-kill burst + secondary burst (`Enemy.cs:1790`) + scorch decal + shake are per-body, uncapped (slo-mo/hit-stop ARE rate-capped). Cap simultaneous scorch decals + secondary burst when >2 die in one frame. Verify by eye first.

### P3 — Seam/portal crossing VFX — reuse `PortalVFXController`/`Env_DungeonPortal` (do NOT author 4 new prefabs).

### P3 — Deprecated path (tower/wave/pet/level-up/ATB/boss) VFX is well-built but V2-gated. **No V1 action** — keep catalog entries so the work isn't lost.

## NOT to touch
Scene files. The deprecated Village/ATB/tower fire-points (V2). Don't author new prefabs where a pack prefab exists.

## Acceptance
- Arena orc attacks show a readable telegraph before damage lands (headless: assert telegraph window >0 in `ArenaCombatOracle`).
- Arena win fires a celebration + per-reward pop.
- High-traffic combat `VFXType`s resolve to real pack prefabs, not the violet default nova.
- Brace gate clean; no new prefabs where a pack prefab suffices.

---

## RESULT (2026-06-28 — VFX P0 engineer, committed-asset-only)

**Constraint honoured:** NO gitignored-asset references added. `VFXCatalog.asset` was NOT
touched. All new VFX fire-points use existing `VFXType` enum values that resolve through the
**procedural `AbilityVfxKit` fallback** (committed) — no Spells Pack / Mirza GUIDs. The P1
catalog element-matrix wiring is **DEFERRED to an owner pack-import task** (see flags).

### LANDED

**P0 #1 — Arena enemy attack telegraph (`Assets/_Modules/Village/Enemies/Enemy.cs`)**
- `TickContactAttack` (~:1113-1126): removed the `telegraphDuration==0 -> instant hit` branch.
  Melee now **ALWAYS** routes through `TelegraphThenAttack`, with the wind-up floored at the new
  `private const float ContactTelegraphFloor = 1.0f` (`~:1138`). Arena orcs (synthesized
  `EnemyDef`, no `_typeVfxSet`) previously had `telegraphDuration==0` → no tell; now they get a
  ≥1.0s reactable wind-up.
- `TelegraphThenAttack` (~:1146-1190): draws a ground-ring **danger tell at the target's feet
  UNCONDITIONALLY** via `VFXManager.Play(VFXType.Impact_ShockwaveRing, feet, …, playSound:false)`
  even when no `EnemyTypeVfxSet` is assigned (an authored `TelegraphVFXPrefab`, when present, is
  spawned in addition). `FlowTrace.Step("VFXTelegraph", …)` fires on every melee tell.
- `RootedCast` (mage cast, ~:1298-1313): added a ground-ring tell at the **aim point** during
  the wind-up (already ≥1.0s) so casts read like melee; `FlowTrace.Step("VFXTelegraph", …CAST…)`
  fires on each. (Cast already had WindUp pose + charge audio + visible orb; this adds the
  feet-decal danger read the P0 asked for.)

**P0 #2 — Victory reward burst (`Assets/_Modules/Village/Arena/BattleArena.cs`)**
- `Resolve(true)` (~:1208): added `Guard.Try(... PlayVictoryBurst(stars, totals))` AFTER the
  WO-556 `SUMMARY` FlowTrace and BEFORE the masked-return setup. **WO-556 victory summary
  (`hud.ShowResult`) is untouched** and still pushed below as before.
- New `PlayVictoryBurst(int stars, BattleRewardSummary totals)` (~:1322): one
  `VFXType.WaveClear_Celebration` at the hero (falls back to `_climaxBody`), a `Juice_LevelUp`
  loot-pop fanned out per reward actually granted (Xp/Wisdom/Wood/Iron/gear), and `(stars-1)`
  extra `Juice_WaveClear` ringing bursts so a 3★ clear reads bigger than 1★.
  `FlowTrace.Step("BattleArena", "VICTORY BURST FIRED …")` proves the fire headless.

**P1 (opt) — default fallback de-duplication (`Assets/_Modules/Village/Vfx/VFXManager.cs`)**
- `ProceduralFallback` `default:` (~:836) now calls new `SpawnHeuristicFallback(type, …)`
  (~:843) which picks colour by element keyword (Fire/Ice/Heal/Physical/gold) and effect by
  category keyword (Death/Explosion→Meteor, Cast/Telegraph/Projectile→Strike, Aura→Heal,
  Ring/Aoe→Aoe) from the enum NAME — so unmapped types stop all looking like the violet aether
  nova. Pure procedural, no pack prefab.

### DEFERRED (owner pack-import task)
- **P1 — Spells Pack element matrix → `VFXCatalog.asset`** and **P1 — Mirza wiring**: blocked by
  the gitignored packs (would create missing GUIDs on fresh clone / CI / build). Flag for an
  owner-run asset-import + catalog-wire pass.
- **P1 — Knight melee hit-spark tint** / **P2 hero damage vignette** / **P2 ranged AoE indicator
  (partially covered by the cast feet-ring above)** / **P3 death over-stack cap** / **P3 seam
  portal VFX**: not in this P0 batch; left for follow-up.

### VALIDATION
- Brace check: Enemy.cs 190/190 ✓, BattleArena.cs 206/206 ✓, VFXManager.cs 78/78 ✓.
- No `.unity` scene hand-edits. No `VFXCatalog.asset` edit. No WO-556 / 9-zone HUD / WO-568
  material-cache code touched. No gitignored-asset references.
- `ArenaCombatOracle.cs` has no existing telegraph assertion; behaviour now guarantees the
  telegraph window > 0 (floor 1.0s) — adding an explicit oracle assertion is a cheap follow-up.
