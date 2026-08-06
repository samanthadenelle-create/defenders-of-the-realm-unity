# VFX Direction — Echoes of Elarion

**Dated 2026-08-05. Source-cited design registry, not a point-in-time report.**
Supersedes nothing; complements `docs/audits/AUDIT_vfx_2026-06-28.md` and WO-759 §8.
Every claim below was read at source. Findings §1 were independently re-verified by the
CLI against the captured logs before any work was authorised.

---

## 1. The loop-slot leak — VERIFIED P0

**`IsLoop` is a sticky manual UI toggle, never a read of the prefab.**
`VfxCasterWindow.cs` declares `_tagLoop` (~:107), renders it as a checkbox (~:1153),
**force-sets it true for role `Projectile`/`Aura`** (~:1172), writes it (~:1178).
Nothing ever inspects `rateOverTime` or bursts.

**Measured:** 95 of 135 rows in `Assets/Resources/VFX/HovlVfxCatalog.asset` are `IsLoop: 1`
(40 are 0). That set includes `PP_BigExplosion`, `PP_SmallExplosion`, `PP_TinyExplosion`,
`PP_EnergyExplosion`, `PP_DustExplosion`, `PP_MuzzleFlash`, `PP_MetalImpacts`,
`PP_FleshImpacts`, `PP_StoneImpacts`, `PP_EarthShatter`, `PP_ElectricalSparks` — all of
which WO-759 §4.3/§4.4 classifies as **BURST**.

**Why it leaks.** `VFXManager.Hovl.cs:283-288` increments `_activeLoops` and returns a
handle. Only the oneshot branch below (`:290-297`) registers a reclaim deadline. The one
loop reclaim, `PruneDestroyedFromSet` (`VFXManager.cs:973`), frees *destroyed* hosts —
**pooled objects are never destroyed.** So a fire-and-forget play of a loop-flagged burst
row leaks a slot permanently for the session. Cap: `_maxActiveLoops = 20`
(`VFXManager.cs:142`).

**The live tower path does exactly this.** `DefenseTower.CastKeyFor` (`:1099-1108`) returns
`PP_MuzzleFlash` for physical/default — the archer and ballista, the most common towers —
played with the handle discarded (`:1065`, `:1069`).

**Captured proving lines (CLI-verified 2026-08-05, six sessions, two dates):**

```
capture-20260730-175552.md:55  PlayKey('ArcherTower_Projectile')        SKIPPED - active loops 20/20 (cap hit).
capture-20260730-175447.md:21  PlayKey('ARcaneTower_Projectile')        SKIPPED - active loops 20/20 (cap hit).
capture-20260730-175729.md:54  PlayKey('ArcaneTower-Baselevel_Projectile') SKIPPED - active loops 20/20 (cap hit).
capture-20260730-215507.md:22  PlayKey('TreeofLifeAura_Aura')           SKIPPED - active loops 20/20 (cap hit).
capture-20260716-205819.md:99  PlayKey('Poi_NodeAura')                  SKIPPED - active loops 20/20 (cap hit).
capture-20260716-210343.md:97  PlayKey('Poi_Landmark')                  SKIPPED - active loops 20/20 (cap hit).
```

**A SECOND signature, not in the original finding** — the oneshot cap saturates too:
```
capture-20260716-205641.md:112 PlayOneshot('Impact_Aether') SKIPPED - active oneshots 40/40 (cap hit; combat VFX dropping). Counter-leak or too-low cap?
```
(also `-205656`, `-205705`). Different pool, same shape. **Open question — needs its own
diagnosis; do not assume the loop fix closes it.**

**What this means for the owner's complaint.** "The fire from the towers is horrible" is at
minimum partly this: after ~20 shots the tower renders **no** projectile, and simultaneously
starves the Tree of Life aura and every POI marker. **WO-870 as written makes it worse** —
it wires *more* fire-and-forget plays into the same leaking bucket. Its diagnosis is right;
only its sequence was wrong.

The codebase already knows this failure mode — it is written out at `Enemy.cs:1680-1685`.
The lesson was applied to one call site and never to the catalog.

---

## 2. HDR is off, so the shipped bloom cannot fire

- `Assets/Settings/DeNelle-URP.asset:26` — `m_SupportsHDR: 0`. Only URP asset in the project.
- `Assets/DefaultVolumeProfile.asset:356-361` — Bloom `threshold: 1.1`, `intensity: 2`.

In an LDR buffer every value clamps at 1.0, so a 1.1 threshold can never be crossed. Every
Hovl `HS_Blend_CG` material emits HDR luminance *specifically so bloom can halo it*; that
intent is discarded at the pipeline. The depth half of WO-759 §5.5 shipped
(`DeNelle-URP.asset:22` now `m_RequireDepthTexture: 1`); the HDR half did not.

**OWNER CALL — frame cost vs. the entire glow language.** The check: flip to 1, A/B one
Seeker capture of a tower shot. If too costly, drop the threshold below 1.0 (~0.8) and
accept duller non-selective glow — do **not** leave a threshold no pixel can reach.

---

## 3. Scale: every owner pick is at demo scale

122 of 122 rows in `Assets/Editor/VfxManualPicks.json` carry `scale: 1.0` (sole exception
`Cleave_Impact` at 1.15). `CANON_GROUND_TRUTH_2026-08-05.md:90-91` records this for
projectiles; `f359ece2` fixed it for tower projectiles by **deriving size from range**. The
same fix has not reached casts, impacts or auras.

**OWNER CALL:** derive scale from the gameplay quantity the effect describes (the proven
approach), or tag per-row?

---

## 4. The creative frame

Echoes of Elarion is played at arm's length, landscape, on a 2670x1200 phone. The player
drives exactly one body — the Knight. Everything else acts autonomously: towers auto-acquire
and auto-fire (`TowerCombat.cs:96`, `DefenseTower.cs:605`), troops fight their own fights,
the companion picks its own targets (`StoryCompanion.cs:733`), Echoes harvest while the app
is closed, the dragon flies its own arcs. Beneath the town is a dungeon ruled pitch dark by
design — ambient 0.05, a lantern the only light, oil the resource that buys sight
(`912b500f`; `Lantern.cs:162-166`).

**In a game where you control one unit among many autonomous ones, VFX is not decoration —
it is the only channel through which the autonomous half of the game reports to you.** A
tower that fires with no visible bolt has not fired. An enemy that winds up with no
telegraph is a random damage event.

**Therefore: readability under pressure, not spectacle.** Spectacle is rationed to the four
or five moments a session that are meant to *be* an event — the dragon's breath, a boss
death, a wave cleared, an Echo awakened, a third star. Everything else earns its pixels by
answering a question the player would otherwise guess at.

**The test an effect must pass:** a player who is not looking directly at it can still tell
what happened.

### Three constraints that are creative direction, not footnotes

**Red/green colourblind.** The default action palette (red hurts, green heals) is
unavailable. Meaning carries in **shape, motion vector and timing**: heal *rises*, damage
*stabs inward*, a telegraph is a ground ring that *grows*, a shield is a bubble that
*closes*, a slash is an *arc*. Two effects that must be distinguished have to differ in
silhouette at 20% opacity, not in hue. Already law (`SkillTree_VFX_Mapping.md:20`); the work
is holding it at prefab-pick time — systematically preferring each pack's ring/arc/bubble
families over its coloured-blob families.

**2670x1200, landscape, held.** Vertical space is scarce and the HUD owns the top and bottom
bands. Effects that grow **upward** — columns, fountains, rising sparkles — waste the axis
the game has least of and get cropped. **Ground rings, expanding shockwaves, horizontal arcs
and near-target sparks** are the correct vocabulary. This also argues against screen-filling
washes: at this density on a held device a full-screen wash reads as a frame drop, not a
payoff.

**Fill-rate.** The only real mobile cost is overdraw from large overlapping transparent
quads. The rule is *not* "fewer effects" — it is **smaller effects, more often, close to the
thing they describe.** Which is what readability wants anyway.

**A convergence worth naming:** the one VFX pack **tracked in git** — Lana Studio — is also
the pack whose vocabulary best matches this brief. Its largest categories are
`Top_down_attack/` (19 ground circles/dots/lines), `Range_attack/` (20 paired projectile+hit),
`Slash/` (12 arcs), `Burst/` (12 rings and poofs). Ground-plane, shape-carried, small.
**The safe pack and the right pack are the same pack.**

---

## 5. Two layers, two failure modes

| Layer | Path | Rows | Into gitignored art | On missing prefab |
|---|---|---|---|---|
| **A** | `VFXType` -> `VFXCatalog.asset` | 45 | 6 (13%), all Spells Pack | **degrades** to procedural (`VFXManager.cs:439`->`:469`) |
| **B** | `PlayKey` -> `HovlVfxCatalog.asset` | 135 | ~all (118 of 122 owner picks) | **goes dark, silently** (`VFXManager.Hovl.cs:210-215`) |

**Layer B is the active combat path** — towers, hero abilities, enemy casters, structure
burn, the Tree of Life aura. It also has **no `MinQuality` field at all**
(`HovlVfxCatalog.cs:39-65`), so it cannot be tiered down.

WO-785 is `READY TO IMPLEMENT` with **no RESULT file**. Its P2 (`verify-runtime-art.ps1
-Strict`) and P3 (`VFX_CATALOG_RESOLVE_OK` oracle) return zero grep hits — neither was built.
Only the count drifted, 117/121 -> 118/122.

### Recommended policy: divide by INFORMATION, not by cost

The question is not "how expensive is this effect" but **"if this does not render, does the
player lose information they need to act?"**

- **Tier M — MUST have a tracked fallback.** Telegraphs, impact confirmations, projectile
  travel bodies, deaths, state/threshold changes, low-oil warning. *A telegraph you cannot
  see is a dead player.*
- **Tier D — may degrade to nothing.** Ambient loops, residual burn, decorative auras,
  secondary layers of a multi-layer stack, celebration polish.

**Mechanism:** add **one field**, `FallbackPrefab`, to `HovlVfxCatalog.Row` and
`VFXCatalog.Entry`; at `VFXManager.Hovl.cs:210` try it instead of returning null, and log
which tier rendered. ~6 lines, zero new art, **zero committed bytes** — unlike WO-785 P1
(promote packs into `Resources/`), which has to stop and ask about binary size.

**It is not a substitution.** Memory `vfx-map-owner-tags-no-creative-pick` binds: nobody
overrides an owner's creative pick. The owner's pick renders whenever the pack is present,
which on her machine is always. The fallback exists only for the machine where the
alternative is *nothing*.

**Two free exposure reductions, available today:** repoint `Impact_ExplosionFire`
(`VFXCatalog.asset:46`) and `Impact_ExplosionAether` (`:52`) at the **tracked name-matching
mirrors already in `Assets/Resources/VFX/Projectiles/`** — `Explosion_Fire.prefab`
(`24d78e68...`) and `Explosion_Arcane.prefab` (`b447c205...`). Layer A exposure 6 -> 4.

---

## 6. Owned and NOT used — the highest-value section

| Pack | Prefabs | Wired | Tracked? |
|---|---|---|---|
| **Lana Studio** | 128 | ~20 distinct | **YES** (595 files) |
| Spells Pack | 466 | 6 | no (`.gitignore:214`) |
| Mirza Beig | 564 | **0** | no (`.gitignore:212`) |
| Hovl Studio | — | 60 picks | no (`.gitignore:218`) |
| UnityTech ParticlePack | ~55 | 54 picks | no (`.gitignore:399`) |

### 6a. Wired to real art, never played by gameplay — free wins

**26 of 79 enum values are wired to art and never asked for.** Connecting one is a call-site
edit, not an art task.

- **`Juice_CriticalHit`** -> `Lana/Burst/Flash_star.prefab` (`VFXCatalog.asset:238`). The
  PERFECT-hit mechanic is real (`PlayerAttackController.cs:440` arm, `:616` evaluate, `:657`
  multiplier) and its entire payoff is an **ASCII text stamp** (`:788`). *Best
  impact-to-effort ratio in the document.*
- **`Death_Skeleton` / `Death_Wolf` / `Death_Brute` / `Death_Tiefling`** (`:154-184`) — four
  distinct prefabs. `Enemy.Die()` (`:2547-2565`) routes override -> typeSet -> generic and
  never touches the species enum, so the pool/factory spawn path always lands on one grey poof.
- **`Projectile_EnemyCasterBolt`** (`:100`) — **zero tokens repo-wide.** The clearest
  "an enemy is shooting at you from off-screen" signal in the game, never once requested.
- `Juice_KillStreak`, `Impact_ShardsBurst`, `Impact_SmokeWisps`, `Cast_RangerDraw`,
  `Cast_NecromancerSummon`, `Cast_EnemyCaster`, `Aura_Necromancer/Healer/Flame/Ice/SmokeReaper/EnemyCaster`
  — all wired, all appear only in infra tables.
- Six with no code reference at all: `Projectile_EnemyCasterBolt`, `Aura_EmpowerTower`,
  `Env_LanternGlow`, `Env_GroundFog`, `Env_DungeonPortal`, `Env_TitleEmbers`.

### 6b. Tracked Lana categories at 0% usage

`Loot/` (4) · `Backlight_resources/` (8) · `Shields/` (5) · `States/` (8, only `Level_up`
used — **status effects have no visual representation at all**) · `Top_down_attack/` (19 —
the single best-matched family for a landscape phone camera) · `Regeneration/` (7 of 8).

### 6c. Dead VFX code — ~1,359 lines

`EliteVFXController` (141) · `WeatherManager` (685) · `EnvironmentVFX` (177) ·
`HeroChargeVFX` (171) · `PetAuraVFX` (109) · `CrystalVfx` (76).

A GUID sweep of every `.prefab` and `.unity` under `Assets` found **zero** VFX-module scripts
attached anywhere — every live VFX component in this project is code-attached. So for these
six, "never attached" means **no `AddComponent` call site exists** and they can never run.

`EliteVFXController` is dead **three ways**: its only consumer is a `GetComponent` at
`Enemy.cs:2538` that always returns null; its GUID appears in zero of 8,795 prefabs and 156
scenes; and its arming flags `EnemyData.isElite/.isBoss` (`Assets/Data/EnemyData.cs:22-23`)
have **zero readers**. Owner ruled 2026-08-04: **wire it.**

`WeatherManager` is also the project's last magenta hole (raw `Instantiate` bypassing the
shader proofer, `:363, 553, 561`). `_starIntervalMax = 0f` (`:105`) disables auto-stars
anyway. **Deleting it closes the magenta hole for free.**

---

## 7. Silent moments that cost the player information

| Moment | Today | Cost | Owned asset (tracked) |
|---|---|---|---|
| **Hero melee contact** | `PlayerAttackController.cs:682-688` deleted 2026-08-02; only receiver-side grey spark (`Enemy.cs:2146`) | The core verb reads the same whether it lands or whiffs | `Lana/Slash/` (12 arcs) — **owner picks which** |
| **PERFECT hit** | `:788` ASCII text stamp | A skill window with no visual reward stops teaching itself | `Juice_CriticalHit`, already wired |
| **Enemy blow connects** | `Enemy.cs:1541` silent | Damage arrives from nowhere | `Impact_Physical`, already wired |
| **Hero death** | `HeroHealth.cs:588-623` hit-stop + anim | The player's own death is unmarked | `Lana/Burst/Poof_generic` |
| **Defeat** | `BattleArena.cs:2211` burst is `if (won)`-gated | Wins celebrated, losses unacknowledged | `Lana/Fog/Fog_poison` |
| **Structure per-hit damage** | `Building.cs:220-231`, `HeartController.cs:322` silent; only a 0.3-0.5s poll reacts (`StructureDamageVisuals.cs:508`) | **The Heart of Elarion takes damage with zero visual response — the thing the game is named for does not flinch** | `Impact_Physical`; `Lana/Shields/Shield_gold` |
| **Lantern flame** | `Lantern.cs:162-166` bare `Light`, no particles | The signature object of the dungeon pillar is a value, not a thing | `Env_TorchFlame`, already wired |
| **Oil draining** | `Lantern.cs:303-304` radius lerp | The pillar's core tension has no perceptual channel | `Lana/Fog/` (3 of 6 unused) |
| **Wave start** | `WaveFeedbackDirector.cs:169` horn + HUD | Build->defend pivot is audio-only; a muted phone loses the beat | `Lana/Area_generic/*_outbreak` |
| **Raid start** | `ArenaMode.cs:143` silent | The most monetised loop opens on nothing | as above |
| **Building placed** | `BuildModeController.cs:1759` silent — **but upgrade complete gets fireworks** (`:2400`) | The first act of the game is unacknowledged | `Lana/Burst/Poof_generic` |
| **Resource collect tap** | `ResourceCollector.cs:364` silent | The most-repeated town action has no response | `Lana/Loot/` + `Backlight_resources/` (12, 0 used) |
| **Echo unlock** | `EchoUnlockFeedback.cs:271` audio only | A narrative awakening gets less than a stockpile filling (`CollectorStackView.cs:367`) | `Juice_LevelUp`, already wired |
| **Offline harvest claim** | no VFX | The retention hook has no payoff | `Lana/Backlight_resources/*_drop` |
| **Portal exit** | `PortalVFXController.cs:648` written, **zero callers** | Asymmetric transitions read as a bug | `Lana/Burst/Flash_circle` |
| **Elite/boss attack** | `EliteVFXController.cs:127` zero callers | An elite's swing looks like a walker's | Mirza Beig (owner-tagged) |

**The pattern:** with two exceptions (lantern flame, oil fog) *every* silent moment has a
tracked, owned, already-imported asset that fits — and in five cases the asset is **already
wired to an enum value gameplay never asks for.** This is not an art problem. It is a
connection problem — the same shape as the nine defects in `CANON_GROUND_TRUTH_2026-08-05.md:12-33`.

---

## 8. Killed — struck from the backlog, not deferred

- **WO-759 P5** (pack flames as env braziers) — `Env_TorchFlame` is already wired to a
  tracked Lana flame and reads fine. Swapping one continuous flame for another changes
  nothing a player can act on, and moves a **tracked** asset to a **gitignored** one: a net
  loss on the §5 axis.
- **WO-759 P8** (Goop / Water) — no gameplay hook exists or is planned.
- **`ShootingStar` / `WeatherManager`** — dead, and ambient weather carries no information in
  a game played in landscape on a phone. Delete.
- **`PetAuraVFX` + `Aura_PetLevel1/2/3`** — dead, and the concept was renamed to **Echoes**,
  which have their own presentation (`EchoSpiritPresentation.cs:105`).
- **`HeroChargeVFX`, `CrystalVfx`** — dead, no owner, no moment.

---

## 9. Phased plan

### Phase 1 — Plumbing. No art, no creative call. **IN FLIGHT 2026-08-05.**
1. Re-derive `IsLoop` for all 135 Hovl rows from emission structure; correct
   `VfxManualPicks.json` so a regenerate cannot undo it. Expect ~35-45 flips.
2. `[vfx-loop-flag]` regression — the oracle that stops the sticky toggle re-introducing it.
3. Fix the root cause in `VfxCasterWindow.cs` (derive from prefab; **remove the role-based
   force-set for Projectile/Aura** — that force-set manufactured most of the 95).
4. Build WO-785 P3 (`VFX_CATALOG_RESOLVE_OK`).
5. Add `FallbackPrefab` to both row types + the resolve path.
6. Add `MinQuality` to `HovlVfxCatalog.Row` + gate in `PlayKeyInternal`.
7. Repoint the two Spells Pack explosion rows at their tracked mirrors.
8. Delete the five dead VFX scripts (~1,218 lines) — closes the last magenta hole.

**Proving line:** one AutoPilot fleet run at 2670x1200 grepped for the loop-cap message.
**Its absence across a full wave is the proof.** Six of six current captures show it firing,
so this is a genuine before/after.

### Phase 2 — Free wins. Art already wired.
`Juice_CriticalHit` on PERFECT · per-species `Death_*` · `Impact_Physical` on enemy melee
connect · hero melee contact (**owner picks the `Slash/` prefab**) · wire `EliteVFXController`
(owner already ruled) · connect `PortalVFXController.OnHeroExit`.
**Verification: on-device Seeker capture. Headless cannot judge a particle.**

### Phase 3 — Towers and boss. **After Phase 1, not before.**
WO-870 (unblocked) · WO-759 §7 Syndrath breath (**SHIPPED 2026-08-05, `7f3971a3`**) ·
WO-875 · WO-876.

> **WO-875 landmine:** it un-gates `abilities.json` `vfx*` keys, and two live abilities
> (`ranger.q` Quick Shot at 0.45s cooldown, `knight.ranged-poke`) carry
> `vfxCast: "PP_MuzzleFlash"` — currently `IsLoop: 1`. Un-gating before Phase 1 lands would
> exhaust all 20 loop slots in **about nine seconds of shooting.** Phase 1 is a HARD
> prerequisite.

### Phase 4 — The dark and the bookends.
Lantern flame · oil fog · **dungeon projectile lights** · build placed · collect tap · Echo
unlock · defeat + hero death · wave start · raid start · Heart-takes-damage · per-threat-class
telegraphs.

> **Strongest purely-creative recommendation in this document.** All 12 tracked
> `Resources/VFX/Projectiles/*.prefab` ship a Point light with `m_Enabled: 0` —
> `Projectile_Fire_3` intensity 5 range 5, `Projectile_Arcane` intensity 1 range 10, etc.
> In a pitch-dark corridor a fireball that briefly lights the room is a **mechanic**: free
> reconnaissance the oil economy is otherwise charging for. Zero new art, zero new light
> objects, one boolean gated behind `VFXManager.ApplyDungeonMode` (`:364`) at MinQuality High.
> **Owner ruling needed — it changes how hard the dark is.**

---

## 10. Owner rulings required

| Ruling | Why it is not an agent call |
|---|---|
| **HDR on/off** (§2) | Frame cost vs. the entire glow language |
| **Global scale language** (§3) | Derive from gameplay quantity, or tag per-row |
| **The melee contact prefab** (§7) | The last one was deleted for being wrong art |
| **Dungeon projectile lights** (§9 P4) | Changes the difficulty of the dark |
| **Boss-breath scale** | WO-759's ~2.5x is a starting number, not a finding |
| **Any Mirza Beig / Hovl / ParticlePack pick** | Owner tags, CLI maps verbatim (memory `vfx-map-owner-tags-no-creative-pick`) |

Agents may execute unaided: all of Phase 1; any connection where the prefab is already
wired; the fallback mechanism; the `MinQuality` gate; choosing **tracked Lana fallbacks**
for owner-tagged keys (a survivability decision, not a creative one).

---

## 11. Open, with the check that settles each

| Uncertainty | Check |
|---|---|
| Does bloom truly no-op at threshold 1.1 in LDR? | Set threshold 0.8 with HDR still off, one Seeker capture. Glow appears -> threshold is the problem; no glow -> HDR is. |
| Is `_maxActiveLoops` 20 at runtime? | **SETTLED** — self-creates via `[RuntimeInitializeOnLoadMethod]` (`:81-87`) with no prefab, so the serialized default runs; six captures print `/20`. |
| Are all ~45 loop->burst flips safe? | Per-prefab, never in bulk: open each, read `rateOverTime`. |
| Does the `PP_FireBall` enemy projectile (`Enemy.cs:1686`, `IsLoop: 1`) leak like the towers? | Read the `LaunchProjectile` arrival closure ~`:1551`. The `:1680-1685` comment defends the *impact* key, not the *travel* key. |
| **What leaks the ONESHOT pool to 40/40?** (§1, second signature) | Own diagnosis. Do **not** assume the loop fix closes it. |

---

**One line:** the highest-value VFX work in this project is not new art — it is one boolean
per catalog row, one graphics flag, and connecting the twenty-six effects that are already
bought, already wired, and never asked for.
