# CANON GROUND TRUTH — 2026-08-06

**This is the single live anchor (CLAUDE.md §15). It supersedes `CANON_GROUND_TRUTH_2026-08-05.md`.**
Every session and every agent checks docs against this file. Sourced from HEAD commit messages and the
working tree, never from assumption.

**Branch:** `wip/village2-and-f8-tickets` · **HEAD at write time:** `1534dffb`
**Local is 43 commits AHEAD of `origin/wip/village2-and-f8-tickets` (`2caca14a`) — NOT PUSHED.**
**Gates last emitted at HEAD:** `COMPILE_GATE_OK` + `REGRESSION_OK 120/120 suites`
(also seen tonight: `VFX_LOOPFLAG_OK` · `VFX_ART_MIRROR_OK` · `PARTICLE_PACK_VFX_BUILD_OK` ·
`BOSS_FIREBREATH_BUILD_OK`). **Save schema v36, unchanged.**
⚠ **Working tree is NOT clean — and it is a SHARED tree (CLAUDE.md §11).** At the time this anchor was
written it carried: `ProjectSettings/ProjectSettings.asset` with a newer APK stamp
(`2026.08.05.312459` / code `312459`, above the last committed `312348`); `WorkOrders/WORK_ORDER_885`
through `894` untracked; and **a concurrent implementation lane of ~32 modified `.cs` files plus the
dual-copy `structures-catalog.json` / `damage-states.json`** (VFX catalog generators, `VFXManager`,
`StructureFactory`, `ElarionUiKit`, the wallet trio, `EndStateView`, `DragonBoss`, `Enemy`, and the
`Village/Vfx/*` set) that landed **while this doc was being written** — consistent with WO-889–893 being
in flight. **One committer, staged by explicit path, never `git add -A`.**

> ⚠ **Never restate a suite count from a doc.** Read it off the marker. Tonight it moved 117 → 118 → 119
> → 120 inside eight hours, and the three entry points now emit DISTINCT markers
> (`REGRESSION_OK` / `CHECKIN_SUITE_OK` / `SESSION_GUARDS_OK`) precisely because one shared token let a
> 22-case suite's pass read as the full suite's.

---

## 0. THE PATTERN OF 2026-08-05/06 — read this before diagnosing anything

The 08-04 anchor named the dominant shape as **built and wired to nothing**. It held all night again —
but a **second, sharper shape** took over, and it is the one to carry forward:

> **A FLAG THAT IS AUTHORED BY HAND INSTEAD OF DERIVED FROM THE THING IT DESCRIBES.**

| Instance | What the hand-authored value claimed | What the artefact actually was |
|---|---|---|
| `IsLoop` in the VFX catalog | 95 of 135 rows "are loops" (a sticky UI checkbox force-set true for role Projectile/Aura) | **53 of 122 picks were wrong** — `PP_MuzzleFlash` and every `PP_*Impacts` are single bursts at t=0 |
| The tracked VFX prefab copy | "self-contained, this is what ships" (WO-759's own commit said so) | `CopyAsset` copies the PREFAB ONLY — **27 of 28 prefabs, 183 references** pointed back into gitignored art |
| `HeroTalentNodeDef.Hidden` | its own comment said the View skips hidden nodes | **ZERO runtime readers** — writing `hidden:true` would have greened the gate and left every node clickable |
| `TalentStrategyRegression.HiddenTrees` | guard G3 green on 41 nodes | hardcoded `{ranger, mage}` — **40 player-reachable nodes had NEVER been audited** |
| The UI capture harness resolution | the resolution in the PNG filename | a **label, not a layout** — only `canvas.scaleFactor` was rewritten, never `Screen.*` |
| `CatalogBootstrap.RegisterFallback` | mirrors `structures-catalog.json` | **all three rows had drifted** |

**The law this session encodes:** *if a value describes an asset, DERIVE it from the asset and pin the
exceptions with their reason — never let a human type it twice.* Every derivation added tonight
(`IsLoop`, height fit, fallback parity, the art mirror) is enforced by a regression that goes red the day
someone re-authors by hand.

**Corollary, learned the hard way in `bd532d5b`:** the prefab is the authority on what the art **DOES**,
not on what the game **SHOULD DO**. Deriving truthfully promoted the upgrade fireworks to a loop — which
the owner had already ruled one-shot. **Standing owner rulings are PINNED in a table with their reason and
outrank the derivation**, and every consumer resolves through ONE method so a pin cannot be honoured in one
place and forgotten in another.

---

## 1. ⚠ THE VFX LOOP-CAP P0 — fixed, with six captured proving sessions

**This is the headline of the night and it explains a felt complaint the owner had already reported.**

- `IsLoop` was a **sticky manual checkbox**. `VfxCasterWindow` **force-set it true** for any row tagged
  `Projectile` or `Aura`; nothing ever read the prefab's emission.
- **A loop row never returns its slot.** The oneshot branch registers a deadline and gets swept; the loop
  branch does a bare `++` and hands back a handle, and the only loop reclaim frees **DESTROYED** hosts —
  and pooled objects are never destroyed. **The global cap is 20.**
- So a loop played **fire-and-forget permanently consumes one of the 20 slots for the rest of the session.**
  The archer and ballista — the most common towers — fire `PP_MuzzleFlash` and **discard the handle**.
  After roughly twenty shots a tower renders **no projectile at all**, and in the same breath starves the
  Tree of Life aura and every POI marker.
- **PROVING LINES:** `ArcherTower_Projectile`, `ARcaneTower_Projectile`, `ArcaneTower-Baselevel_Projectile`,
  `Poi_NodeAura`, `Poi_Landmark` all appear in `break-log` as **`SKIPPED - active loops 20/20`** across
  **six F8 sessions on two dates**. All five were themselves mis-flagged loops — **they were filling the
  cap that then starved them.**
- **THE FIX (`bd532d5b`):** both catalog generators now **DERIVE** the flag. Rule stated once, in one
  place: `main.loop` AND a positive rate over time or distance, with emission enabled. **The authority is
  the root system UNLESS the root cannot emit**, in which case it falls through to the first system that
  can — Lana's `Fire_medium.prefab` is a root with emission DISABLED over a child emitting 15/sec, and
  strict root-reading would have cut the burning-structure, torch and fog auras off mid-burn.
- The `VfxCasterWindow` checkbox is now **read-only and derived**; the role-based force-set is deleted.
  `VFXManager` gained a guard: a loop declaring a finite lifetime is a timed effect and routes through the
  leak-proof oneshot path (no row declares one today — it exists so the next fire-and-forget loop cannot
  quietly re-open this). Marker: **`VFX_LOOPFLAG_OK`**.
- ⚠ **NOT YET PROVEN — the ABSENCE of the cap message across a full wave.** Six of six captures show it
  firing; that is a real before, and the after **needs a fleet run before anyone claims it.**
- ⚠ **A SECOND, SEPARATE SIGNATURE, deliberately NOT bundled:** the **oneshot pool saturates at 40/40** in
  three other captures. Different pool, different reclaim path. **The loop fix must not be assumed to close
  it.** OPEN.

---

## 2. ⚠ THE TRACKED VFX PREFABS WERE NOT SELF-CONTAINED — 183 pack deps to 0

`948080f5` retracts a claim made earlier the same night: **WO-759 said the tracked prefab copy is what
ships. `CopyAsset` duplicates the PREFAB ONLY** — never its materials, textures, shaders, meshes or
animations.

- **Measured: 27 of 28 prefabs, 183 references, 73 distinct assets** pointing into gitignored art. On any
  machine without the packs — a fresh clone, the laptop, CI — all of it renders **missing**: magenta,
  untextured, or invisible depending on platform. Latent only because this machine has the packs.
- **Now 0**, verified twice: the mirror's own report **and** an independent recursive GUID walk that does
  not reuse the builder's code. **~23.85 MB mirrored, deduped**, into `Assets/Resources/VFX/_Shared/`
  (`Glow.mat` was referenced twelve times, `Trail` nine — one copy each). The packs were never bulk-copied:
  only what the shipped prefabs actually reach.
- Exposure was wider than materials: a mesh (`FireFly.fbx`), a nested pack prefab pulled in through the
  ParticleSystem **LIGHTS** module, two `.anim`, a `.controller`, and **two C# MonoBehaviours**.
- ⚠ **THE TWO SCRIPTS COULD NOT BE MIRRORED AND WERE STRIPPED — felt-visible: `Casting_Fire` no longer
  spawns a projectile.** Copying a `.cs` would put two identical types in `Assembly-CSharp` and take the
  compile gate down for every lane. Removal is right on its own merits anyway: inside a pooled
  manager-driven prefab those demo scripts read a Rigidbody that is not there, `Destroy()` a pooled
  instance on collision, and `InvokeRepeating` a fireball once a second forever.
- **The mirror only converged on a FIRST run** until `29f9ac2b`. It seeded its walk from the prefabs, so on
  any later run the prefab already pointed at the MIRRORED material, the walk saw a target outside the pack,
  skipped it, and never re-entered that material — leaving the pack texture the material itself referenced
  undiscovered. **Six prefabs read as self-contained while their art was one hop away.** It now re-seeds
  from everything already mirrored: **a fixed point has to be fixed ACROSS runs.**
- Two collisions surfaced with it: `ParticlesLight` and `ramp01`/`Ramp01` exist in two pack folders — the
  second pair differing **ONLY IN CASE**, which is one file on Windows and two on CI.
- **Lana Studio is NOT gitignored**, contrary to standing assumption — only its URP upgrade subfolder is.
- The regression is the durable half: it walks every `Resources/VFX` prefab and fails on ANY dependency in a
  gitignored root. It deliberately does NOT require zero deps outside the VFX tree. Marker:
  **`VFX_ART_MIRROR_OK`**.
- **KNOWN, NOT FIXED:** `SpellsPackVfxMirror.cs`'s header repeats the same false fresh-clone claim;
  `_Shared/Textures` is **16.9 MB of `.tif`** sitting outside the texture-optimizer sweeps' root list; and
  the base tower's Hovl muzzle key still points straight into the gitignored pack (left alone because it
  carries the tier scale — the tracked type now plays alongside it).

---

## 3. VFX — what else landed, and what was REFUSED with measurements

**Landed:** WO-759 boss fire breath (`7f3971a3`, three-layer recipe on `dragon_Snout_bone`, URP
`m_RequireDepthTexture` 0 → 1 for soft particles, HDR deliberately left off as a mobile call) · 16 new
`VFXType` values appended after `Boss_FireBreath` (`0011b8ba`) · **14 of those 16 built into tracked
prefabs + catalog rows** (`a12c6d22`) · four bought-and-never-played effects finally fired (`a186c282`) ·
**WO-886 death ladder** (`29f9ac2b`) · **WO-887 empowered-tower element routing** (`4ef2d532`) ·
**WO-888 low-health tell** (`1534dffb`, §7).

**The connection ledger that makes this a wiring problem, not an art problem (`3db877d2`):**
**26 of 79 enum values are wired to real art with ZERO gameplay callers.** Six whole tracked Lana
categories sit at **0% usage**. A GUID sweep of **8,795 prefabs and 156 scenes found ZERO VFX scripts
attached anywhere** — which is what makes `EliteVFXController` dead three separate ways.

⚠ **THE ENUM IS SERIALISED BY ORDINAL, NOT BY NAME.** An insert anywhere above an existing value silently
re-points every row below it at the wrong art. **Appends only.** (Verified: `Boss_FireBreath` still reads
`Type: 79` after the append.)

⚠ **`Build()` does `entries.arraySize = rows.Count`** — a row written only by a builder is **silently
dropped by the next `Generate VFX Catalog`**, and the effect falls back to something that still *looks*
like it works. Map entries must land in `VFXCatalogGenerator` alongside the rows. That failure is invisible.

**Refusals, each recorded with its measurement so nobody re-attempts the copy:**

| Refused | Ground |
|---|---|
| `Death_Skeleton` / `Death_Wolf` → `SparksEffect` | MEASURES CONTINUOUS — 80/sec on loop at the root; its only burst is a 0.2 s child that is not the derivation authority. Cataloguing it hands a rate-emitting loop to a fire-and-forget death = the loop-cap P0 straight back |
| The five WO-887 **surface** impact rows | (1) **DEMO GEOMETRY** — all five carry, on the prefab ROOT, a MeshFilter with a built-in primitive, a MeshRenderer with a pack material and a **SPHERE COLLIDER**; copying one renders a lit primitive and ADDS A PHYSICS COLLIDER at every hit. (2) All five emit **5/sec on loop** at the derivation authority. (3) **No enum home** — there is no `Impact_Flesh/Metal/Stone/Wood/Dirt` |
| `Cast_Heal` / `Impact_Heal` as auras | Fire-and-forget one-shots whose ratified recipes measure CONTINUOUS (3/sec and 5/sec on loop) — repointing leaks a loop slot per cast |
| The arcane gear aura as a loop | Rate-0 with a single burst: held as a loop it pops once and then occupies a slot showing nothing |
| `Enemy_Spawn` / `Despawn_Dissolve` | **SCRIPTED** recipes — each carries a pack MonoBehaviour plus a demo mesh to dissolve. They need a runtime component driving the TARGET's material cutoff. Authoring work, not a copy |
| `GoopSpray`, ever | `DamageElement` is `{None, Aether, Flame, Ice}` — **this game has no nature element** |

⚠ **THE SURFACE SIGNAL DOES NOT EXIST — verified, not assumed.** No `SurfaceType` field, no
physic-material read, no per-material tag; wood palisades, stone walls and steel gates all share one
`Structure` layer, and both footstep implementations play a single clip with no surface query. The nearest
real signal is `WallTier` on player walls — a progression index. **Defining a surface taxonomy is design
work and belongs to the owner.**

**Other findings the work orders could not have known:**
- **Boss deaths were detonating TWICE.** Every explosion in this pack ships `looping:1` + `prewarm:1` with
  its whole payload in a burst at t=0, while the pool reclaims at duration + max lifetime (~4.3 s on
  BigExplosion) — so the burst re-fired at t=2. Cleared per-row, because it is felt-visible.
- **The 0.7 boss death shake in WO-886's own acceptance criteria HAS NEVER FIRED.** `EliteVFXController`
  is attached to nothing; every kill, boss included, got the flat 0.18.
- **`TowerCombat.OnProjectileImpact` computed the projectile's element EIGHT LINES BELOW the impact pick
  and never used it** — so **every empowered tower burst as `Impact_ExplosionAether`**: a fire tower's bolt
  detonating in violet arcane light with the arcane bang over it. Routing by element also routes the paired
  `SfxId`, which fixed the sound as a side effect.
- **`FireAt` was playing `Projectile_TowerArcane` — a PROJECTILE-BODY row with `IsLoop` TRUE — as a muzzle
  flash.** Another fire-and-forget loop, on the busiest call in the game.
- **`Death_Boss` sat on the 3f fallback case while `Boss_Death` sat on 4f** — live alias drift. Merged onto
  one case sharing ONE prefab. `Boss_Death` had pointed into the gitignored Spells Pack.
- **`Elite_Death` had no catalog row at all**, and elites died as plain Hollow trash because the species
  check tested family before role.
- **MagentaGuard was painting vendor particle TRAIL slots white** (`449b16bb`). On a
  `ParticleSystemRenderer` slot 0 is the particle material and slot 1 the trail; the Arcane Tower aura is
  **trail-only by vendor design**, so the empty slot is legitimate. The guard's recovery assignment sat
  OUTSIDE the dedupe guard — unconditional, and **stuck in built players**, rendering the aura as a white
  opaque blob. **28 of 261 Hovl prefabs carry this pattern.**

---

## 4. Hero classes — Ranger and Mage are UNLOCKED, and their trees are EMPTY

- **`ff.knightonly` now defaults OFF** (`9a0ff548`). Flag-OFF roster = **Knight / Ranger / Mage**, resolved
  through the single registry `DeNelle.Core.State.PlayableHeroes` (WO-861 Phase 0 built it precisely so one
  flag would widen the select screen, `ChooseHero`'s coercion and the vendor shelf at once).
  **CLERIC STAYS OUT deliberately** — no authored kit. `ff.knightonly=1` restores the solo-Knight V1 pivot.
- ⚠ **WO-910 — THE PRODUCT FINDING, and it is READY FOR OWNER RULING.** `TalentStrategyRegression`
  hardcoded `HiddenTrees = {ranger, mage}`, so guard **G3 (no dead talent nodes) had NEVER audited them**.
  Emptying that set surfaced **31 real, pre-existing dead nodes across 40 player-reachable talents** —
  *dead* meaning **no runtime consumer**: the node is visible and clickable and does nothing. In the
  commit's own words, **"Ranger collapses to ONE usable talent out of 20 and Mage to five, and BOTH lose
  their entire tier-4 capstone row."** Knight's 32 and the 9 shared are fully green. **Two of the three playable classes ship
  with no talent progression and nothing to build toward.**
- **Hiding was CONSIDERED AND REJECTED.** `HeroTalentNodeDef.Hidden` had **zero runtime readers** while its
  own comment claimed the View skips hidden nodes — so `hidden:true` would have turned the gate green and
  left all 31 nodes fully clickable. Hiding also strands three whole tiers and orphans three nodes.
  `hero-talents.json` is **UNTOUCHED (md5 unchanged)**; the 31 are a dated, WO-910-numbered, **ratcheted**
  baseline — new dead debt fails, **and a baseline id that stops reporting dead ALSO fails**, so a baseline
  entry can never outlive its debt. `Hidden` is now genuinely wired into both `Rebuild` loops, so the
  owner's ruling will work when she makes it. No node sets it today.
- ⚠ **A LATENT P0, FIXED (`d0c7b8fd`): the INVISIBLE HERO.** Ranger and Mage have **no FBX at all**; both
  fell through to a **Blink base body**, and **`Assets/Blink` is GITIGNORED**. On a fresh clone the terminal
  fallback logged a failure and **returned without instantiating anything**, after `Start` had already
  destroyed the placeholder. Not a Knight-degrade — **nothing at all.** Both bail-outs now build a tracked
  **KayKit** body (`git ls-files`-verified as the only humanoid bodies actually in the repo), so a missing
  pack can look wrong but can never look like nothing.
- **Identity:** the nameplate printed the CLASS word and the inventory medallion hardcoded Grom's face, so
  **all four heroes wore the Knight's portrait**. Both now read canon identity.
- ⚠ **STILL OPEN:** **Grom and Elara** have Thrain's exact portrait-import defect (imported as a plain
  texture, so `Load<Sprite>` returns null and they fall to the blurrier RawImage path). **Grom is the
  default hero and is on the blurry path.** Thrain was fixed; these two were flagged, not fixed.
- ✖ **REFUTED — `Ranger.fbx.tripo-extracted` is NOT a parked mesh.** It is a **125-byte plain-text
  sentinel** written by `TripoAssetPostprocessor`. There is nothing to un-park. Knight's sentinel sits
  beside a live `Knight.fbx`, which proves the marker never blocked an import. **WO-909's premise was
  wrong.** Nobody should spend another cycle on it.

---

## 5. Structure height — one cadence, and the walls deliberately left alone

Owner ruling: *"take whatever the y height that we use across the board... all of the other structures stay
within that cadence... relatively the same size... all scaled to the same point."*

The cadence, anchored on the archer tower she already ruled at **1.2** (`0ac59581`, `d42e2817`):

| `heightMul` | Class | Note |
|---|---|---|
| **1.25** | landmark | the cathedral, tallest in town by design |
| **1.2** | towers | 4.8 m tall, 2.778 m across = **49.9% of a house**, exactly "half as wide". The wizard tower and arcane spire had been floating 4% above it |
| **1.0** | building base | 4.0 m — the width reference |
| **0.75** | siege engines | machines, not architecture, so a wall-mounted ballista cannot out-top a tower |
| **0.35** | decoration | `deco_torch` inherited the building default and was fitting a **WALL TORCH to 4 m** — as tall as a house |

**The ruling now lives IN THE DATA as a `_heightCadence` key**, so the authority travels with the file
rather than only in a commit message. Catalog version bumped **6 → 7 → 8** across the two commits (7 = the archer, `0ac59581`; 8 = the cadence, `d42e2817`) — **verified at HEAD as `"version": 8`**; dual copies byte-identical.

⚠ **`heightMul` fits BOUNDS, so equal multipliers do NOT mean equal apparent size.** `collector_farm` reads
as the worst outlier in the file at **1.4** — taller than every tower and the cathedral — and is exactly the
row a flatten-everything pass hits first. **It is not an outlier. It is a compensation:** its windmill
blades inflate the Y bounds, so at 1.0 its BODY fitted far smaller than a boxy forge at the same 4 m, and
the owner felt-reported that as the shrunk farm (`31b41d19`). **Left alone, with a row note so nobody
"fixes" it again.**

⚠ **WALLS ARE DELIBERATELY UNCHANGED — this is the one to read.** At 1.0 they are 4 m, nearly tower height,
so they look like the obvious next correction. But the fit is **uniform**: lowering a wall **NARROWS** it by
the same factor, and every wall in an already-saved town sits on the cell pitch of its old claim. A narrower
segment does not re-pitch its neighbours — **it opens PATHABLE GAPS in existing wall runs and shrinks the
navmesh obstacle with them.** That is a worse save break than an overlap, and it is invisible to the usual
shrink-is-safe reasoning. **It needs a measured audit (`StructureHeightAudit` prints `measuredY` per
prefab) plus a migration decision.** Noted on all three wall rows.

Every applied change is a **shrink**, and a shrink can only reduce a cell claim — it frees a cell, never
collides, so saved placements are safe. Tiers 2 and 3 inherit with no per-tier values (the reskin re-fits
through the same `EffectiveVisualHeight`; `RepoProps` has no per-level height field).

**Height and footprint are NOT independently authored.** `BuildModeController` measures the XZ bounds of the
model **after** it is fitted to `EffectiveVisualHeight`, so one uniform number moves both — 1.25 → 1.2 shrank
the archer's diameter by the same 4%. The authored `PlacementRules.footprint` is only the **fallback** for a
prefab that fails to skin, and its archer value had drifted to 2.5 against the catalog's 1.75. Corrected.
**`repo.visualHeight` is DEAD for runtime placement** — deprecated by WO-764, authored zero times, and
no longer read by `StructureFactory.EffectiveVisualHeight`. *(Corrected 2026-08-06: one legacy EDITOR
reader survives in `Assets/Editor/WallTools/RaidBaseGenerator.cs`, so "nothing reads it" is too strong —
nothing at RUNTIME does.)*

⚠ **Why 1.2 and not the literally-stated 1.5:** the ruling was formed against a **stale number**. Canon said
the tower was 7 m, but WO-764 had already cut it to `heightMul 1.25` (5.0 m) — so 1.5x reads as a cut from 7
and is a **20% increase** over the live 5.0. At 1.5 the base reaches 3.472 m, **125% of the half-house
target**, and crosses the 3 m grid cell — flipping the tower from a 1x1 to a 2x2 claim, i.e. the same town
real estate as a house, the exact opposite of the ruling's second half. Owner was shown both and chose 1.2.

---

## 6. UI — what changed tonight, and the two theories that were killed

- ✖ **REFUTED — "the victory diamonds are a font/glyph problem."** They were **never a font problem**. The
  old code built a **sprite-less `Image`** (which draws a solid white quad) and rotated it 45 degrees, with
  the literal comment `// diamond` — a deliberate workaround from when the TMP star glyph tofu'd. **There
  was never a star to fix; there was a square pretending to be one.** No star sprite exists anywhere in
  `Resources`, so one is now generated the same way the kit already generates its rounded, circle and ring
  sprites. *(This corrects `c374bd44`, which had recorded the tofu'd-glyph theory as the cause.)*
- ✖ **REFUTED — "the crush / the mismatched buttons are `ClampMinTouch` centre-grow."** Checked, not
  assumed, at three separate sites: the Echo card's button bands resolve **116.7–130.6** units, the merged
  card's fraction bands resolve **117 px**, and the side-menu rows resolve **exactly 112.0 px** — all at or
  above the 112 floor, **so that guard never fires.** Same family, different mechanisms:
  - Echo card: content anchored to `chrome.content` (panel fractions) while the black plate **is**
    `chrome.layout.body`, whose floor the kit **raises at runtime**.
  - Victory screen: **arithmetic, not layout.** The panel-height law solves so the well equals the required
    body **exactly** — by construction there is zero slack. Landscape has **965 reference px**; an arena win
    with five spoils asks for a **1027 px** panel, so every band got squashed 11%.
  - Side menu: **two rects sharing one anchor origin** — `BuildSlideTab` pins the tab and the panel to the
    SAME edge at the same origin.
  - FOUND YOUR TOWN: this one **was** the centre-grow (0.18-of-body bands resolving to ~56 px).
- ⚠ **WO-894's own spec made the victory crush WORSE** — raising the star band 48 → 72 pushed arena-win from
  0.893 to 0.859. **The work order written to fix the crush tightened it.** The fix instead takes the spoils
  list **TWO COLUMN in landscape** — a deliberate, documented deviation from the WO's wireframe, overridden
  because the wireframe was drawn without knowing the content does not fit the surface. Arena win with five
  spoils: **0.859 → 1.000**, unclamped. Raid win 0.950 → 1.000. Portrait stays single column.
  **One case still compresses:** arena + FLAWLESS + five spoils at 0.992 — currently **UNREACHABLE**
  (nothing ever sets `perfect` true). Latent, stated rather than claimed away.
- **The broken reward icon was ONE LETTER.** The label says `Crystals`, the data key is `crystal`; the
  lookup missed and fell through to a generic CHEST. **The correct art was committed and imported correctly
  the entire time.** Fixed in the DATA (five plural aliases, both copies byte-identical).
- **The Echo unlock is now ONE screen with TWO buttons** (owner ruling), not two screens with four. The
  announcement screen's content is **folded in, not dropped** — headline, arrival line verbatim, artwork and
  fade-in all move onto the one card. The retired button is the shared **Close**: a duplicate dismiss sitting
  **between** the two positive actions. Retired **locally, not in the kit** — that Close is canon for ~19
  other panels. The merge is what pays for the layout: the plate grows **326 → 533 px**.
- **The right rail is one collapsed chip style** (owner ruling: *"The echoes, the builders, and the
  resources should all be styled similarly"*). They had been three different things — Builders a large
  expanded gold-bordered panel, Echoes a small dark chip, Resources **both**, with the panel's top edge
  overlapping the chip. Every element on the rail now derives its inset from **one shared constant**.
  The **Builders chip remains the ONE Queues entry point** per the 2026-08-01 bar-button retirement.
- ⚠ **THE NUMERAL `1` RENDERS AS A BARE VERTICAL STROKE** in the chip font — so `Builders 1/2 | Train 1`
  rendered as three identical vertical marks carrying three different meanings, with the pipe sitting
  between the digits it was being confused with. The Train count moved to a second line. **The glyph itself
  is a wider problem** — the same capture shows the Work Queue panel rendering a properly flagged `1` in a
  different font — and is ticketed as an owner look call, not papered over.
- **The side menu's "duplicate gear" was not a duplicate.** The gilt glyph is the drawer **HANDLE**
  (authored at 84 px, under the kit's touch floor, and centred on the mount's midpoint so it landed dead on
  the Music row); the bare one is a per-row icon painted over the "S" in "Settings". **One sprite, two
  treatments** — `icon_settings.png` is dark art: gilt-plated it reads gold, bare on obsidian it reads as a
  smudge. Row icons are **removed rather than restyled**: `concept-icons.json` maps only `settings` of the
  five row concepts, and four rows silently resolved null.
- **The potion tap was dead because the button disabled itself at zero.** Proven from the owner's own
  `Player.log`: **433,897 lines with ZERO `command potion fired` entries**, while `attack` and
  `assignableCast` fired hundreds of times through the same `ActionBar` mount — which kills both obvious
  theories at once (**the handler WAS registered** and **the raycast WAS reaching that surface**).
  ⚠ **And the destination was wrong: THERE IS NO APOTHECARY.** The recipes, panel, VM and service all
  exist, but `PanelId.ConsumableCrafting` has exactly two openers, both requiring an `ApothecaryWorkbench`;
  the station injector returns early; and **`structures-catalog.json` has no apothecary row at all**, so the
  palette can never build one and no saved layout can replay one. The empty state pointed at a place the
  player cannot reach by any path — **worse than no message.** The craft clause is now conditional on a
  bench actually standing in the world.

---

## 7. Accessibility — the low-health tell is no longer a colour

**The owner is red/green colourblind, and the signal warning her she was about to die was a RED EDGE
VIGNETTE — the one signal she could not reliably read. That was the ticket** (`1534dffb`, WO-888).

Severity now drives **three greyscale-legible channels at once**:
- **PULSE RATE** — 0.85 Hz rising to **3.2 Hz**
- **GUTTERING DEPTH** — the trough falls to a **tenth** of authored density, so near death the effect nearly
  goes **out** between beats and snaps back
- **SIMULATION SPEED** — so recovery reads as a snap rather than a drift

**Below a quarter health the RECIPE SWAPS**, smoke wisps to a candle gutter: **a shape change, not a hue
change.** Healing is the opposite vocabulary — a calm, steady rise. **The vignette STAYS as a redundant
cue rather than being deleted: redundancy is good accessibility; colour-ONLY was the bug.**

- **Mutual exclusion is STRUCTURAL, not behavioural** — exactly ONE handle field, so two HP auras running at
  once is *unrepresentable* rather than merely unlikely. Priority: near-death > low-health > healing, so a
  danger read is never masked by a comfort read.
- Every loop has a proven stop on every exit (state change, healed above the cutoff, death — including an
  explicit stop on the lethal-hit line so it dies on the same frame as the death burst — `OnDisable`,
  `OnDestroy`, scene unload) **plus a WATCHDOG**, because a held loop whose driver is disabled would strand
  a slot forever. A refused start does not latch. **Worst case this adds 3 of the 20 loop slots.**
- ⚠ **AN UNGUARDED HOLE FOUND AND CLOSED:** driving a pulse means mutating a **POOLED** instance, and the
  pool resets only what it changed itself — so the next effect to use that slot would silently inherit the
  modulation, **forever**, with no way to trace it back. A modulator now keeps the pristine baseline on the
  instance and restores from **both** ends: the handle's stop AND the pool's return.
- Low-health and near-death auras are authored **MinQuality 0** against the ambient default of 1,
  deliberately — a survival read that vanishes on a low-end device reintroduces the bug it exists to fix.
- ⚠ **TWO THINGS STILL NEED THE OWNER:** `Cast_Heal`'s committed row is a **green glow**, so the heal CAST
  beat still reads partly by hue even though the HP state no longer does (a second accessibility pass). And
  the **item heal aura is INERT until she tags an accessory** — picking which relic glows is a creative
  call, and the standing rule is to map an owner-tagged key verbatim, never to pick one. **Only the
  flameblade carries element data today.**

Related, from the 08-05 Seeker review and still open: **the build placement ghost signals valid/invalid by
COLOUR ALONE on the red/green axis**, in the one mode where the player commits resources; and the **hero
health bar is colour-only and unlabelled at 4% health.**

---

## 8. Waves / tutorial — the stand-down, and the watchdogs it would have tripped

- **The wave clock now stands down while the tutorial is live** (`56f1139c`). The peace window had been
  checked **at the door but never while the clock ran** — `WaveManager` consulted the gate in `BeginLoop`
  and `GuardedKickoff` only; once `EnterCountdown` armed the phase, the clock ran to zero and spawned
  regardless. It was **the only consumer that asked once** (`RegionMobSpawner`, `OverworldEncounterSpawner`
  and `HeroHealth` all re-check every tick). Captured: the countdown ticked **cd29.9 down to cd6.8** with
  the tutorial live.
- **One overloaded predicate was split into two** so the owner's two rulings stop fighting over a single
  boolean: `WaveLoopSuppressedForTutorial` (FTUE-scoped, zone-independent, drives the wave clock) and
  `HostilesSuppressedForTutorial`, **left byte-identical** so the 2026-07-24 ruling (leaving town resumes
  ambient spawns without cancelling the tutorial) cannot regress.
- ⚠ **The fix would have manufactured a FALSE P0 on the owner's first-ever run** (`c95ff5f4`).
  `GuardedKickoff` had already armed `RetryTillActive` and `StallWatchdog`, and **neither can tell a
  deliberate stand-down from a stall** — three refused `BeginLoop` re-fires, a `FlowTrace` failure, then
  `StallWatchdog`'s `LogError` at nine seconds. **Under the F8 daemon contract that is a captured error
  mid-tutorial.** Both are now cancelled on stand-down.
- **Why the countdown was armed on a fresh save at all:** `WaveManager.Start` auto-arms on `!IsFirstRun()`,
  and **`IsFirstRun` FAILS OPEN when `GameStateService` has not bootstrapped.** Both FTUE predicates fail
  open on the same null. A fresh save that wins the race against Core bootstrap does fire the kickoff, and
  async `BeginLoop` reaches `EnterCountdown` a few frames later — **after the gate has closed behind it.**
  That is why the per-tick re-check was the right fix and swapping the door predicate alone would not have
  closed it.
- ⚠ **`pausePressure` remains untouched — it is a dead flag and the documented trap on this exact bug.**
- **FOR THE PO:** the shipped design stands the **whole** countdown down and restarts it from full after the
  tutorial, rather than letting the wave arrive on schedule with aggression held. Defensible, but a product
  call worth confirming. **No suite covers the stand-down; this system has now broken three times.**

---

## 9. Dungeon / world / wallet — carried forward from earlier the same session

These landed 2026-08-05 afternoon and are fully written up in
`docs/reference/DEFECT_INDEX_2026-08-05.md` (frozen). In one breath:

- **THE DUNGEON WAS UNPLAYABLE FROM THE FIRST ENCOUNTER** and had been for every prior report — the hero
  staged into `BattleArena` as a **partial hero** (no `PlayerAttackController`, no `HeroHealth`). Mutual
  null-target deadlock, so `battleLock` never released. Fixed (`219924ca`); **the owner then won the first
  dungeon victory in the project's history**, which immediately surfaced three more latent defects
  (`912b500f`): the `±50` playable-box clamp writing `transform.position` every frame in every unbaked
  dungeon (`ArenaCentre (5000,0,5000)` clamped is **exactly `(50,0,50)`**), the arena's stage prefab leaking
  a **scene-wide directional light** plus a global ambient overwrite, and `WarpHero` raw-assigning position
  onto a live `CharacterController`.
- **Hero no longer spawns inside the tree at the hub** (`bfe9f0c3`) — measured: the spawn resolved to world
  origin, the Heart anchor sits at `(0,0,12)` and the tree's XZ footprint reaches at least `z = -4`.
  Four independent resolvers had no shared authority; the worst computed `heart.position + heart.forward*4`,
  landing **deeper inside** than the normal spawn.
- **Wallet: three layers, each hiding the next.** The NRE (`c457150d`) only revealed that we ship the SDK's
  **default identity** `https://solana.unity-sdk.gg/`, whose `/.well-known/assetlinks.json` returns
  **HTTP 404** — so MWA's `ERROR_AUTHORIZATION_FAILED` is **structurally unpassable as shipped**. Real
  identity + `api/assetlinks.js` + a `vercel.json` **rewrite** (DAL does not follow redirects) are in
  (`8090bd79`), and the `<queries>` block that **never reached the APK** is fixed.
  ⚠ **NOT DEPLOYED — `assetlinks` must be live BEFORE an APK carrying the new identity ships, or it
  reproduces the exact bug. That promotion is the owner's.**
- **The UI capture harness was geometry-blind** (`7e05e6d3`) — `RenderCanvasToPng` rewrote only
  `canvas.scaleFactor` and never `Screen.*`, so **every PNG shared one layout and the resolution in the
  filename was a LABEL, NOT A LAYOUT**. Two panels shipped broken behind a green marker.
  **2670x1200 — the Seeker's REAL surface — is now in the matrix; nothing in this repo had ever rendered at
  it.** ⚠ A run that cannot move `Screen.*` now degrades **loudly** as
  `UI_CAPTURE_FIDELITY_DEGRADED` / scale-only. Several of tonight's UI commits are **explicitly not
  geometry-verified** for that reason and need a device check.
- **AutoPilot gained a dungeon-loop probe** (`a09424ee`) because **`adb input tap` drives uGUI but
  `adb input swipe` does NOT drive the virtual d-pad** — locomotion is not automatable from the device,
  which is why the owner had to walk her own hero into a dungeon to prove a fix. Run with
  `-Phases DungeonLoop` and **WITHOUT `-Graphics`** (it asserts state, not pixels, and a graphics run wipes
  the real UI captures).

---

## 10. Work orders

**WO numbering: read the `CLI_LANES_WO_NUMBERS.md` banner — it is the SOLE authority. Never copy a number
into a doc.** Two disjoint blocks: **main line (CLI)** and **860–899 reserved (UI seat)**; each seat bumps
its own row in the SAME edit as the mint.

| WO | State at HEAD |
|---|---|
| **884** VFX facade + Particle Pack deliverables | READY TO IMPLEMENT (spec on disk, untracked) |
| **885** VFX registry wiring, all domains | READY (after 884 Phase 0 + P1) |
| **886** death ladder | **LANDED** `29f9ac2b` |
| **887** on-hit surfaces | **LANDED `4ef2d532` — element half fixed, the five SURFACE rows REFUSED with measurements** |
| **888** heal / HP / item auras | **LANDED** `1534dffb` |
| **889** combat auras nearest-N · **890** harvest economy · **891** structures/healer · **892** building damage state · **893** portals/spawn/dissolve | **IN FLIGHT — specs on disk, untracked, NOT implemented** |
| **894** victory screen stars/spin | **LANDED `afa50e44`, with a documented deviation** (two-column spoils in landscape; the WO's own star-band spec had made the crush worse) |
| **908** side-menu gear icons | **LANDED** `fce950ae` |
| **909** activate Mage/Ranger | **LANDED `9a0ff548` + `d0c7b8fd` — but its premise (a parked `.tripo-extracted` FBX) was REFUTED** |
| **910** Ranger/Mage talent consumers | **READY FOR OWNER RULING — this is a DESIGN call, not an implementation ticket** |

---

## 11. Open items awaiting the owner

1. **WO-910 — Ranger and Mage have no talent progression.** Wire the 31, hide them, or re-author the trees.
2. **The surface taxonomy** (WO-887's refused half) — no surface signal exists anywhere in the game.
3. **`Death_Wolf` / `Death_Tiefling` / `Death_Skeleton`** — the roster has exactly three families (hollow,
   orc, troll). Routing a wolf or tiefling at one of them is a creative pick, not a mapping.
4. **Which accessory carries the heal aura** — the item aura is inert until an accessory is tagged.
   Only the flameblade carries element data today.
5. **`Cast_Heal` is a green glow** — a second colourblind pass on the CAST beat.
6. **Wisdom's icon** stays on the campfire stand-in; nothing in the icon set reads as wisdom, and the
   500-icon library was catalogued by silhouette **specifically because the owner is colourblind**, so
   picking one blind is the substitution that is not allowed. An override lever is staged (one JSON line).
7. **The market has THREE player-facing names** — `Store` in the build palette, `Market Stalls` in the shop
   header, `Marketplace` on the NPC — and **no single authority.**
8. **Potion crafting ships with recipes, a panel, a VM and a service but NO reachable entry point.** The
   only potion supply in the game is the market shelf and loot drops.
9. **The Echo level-up header still reads "Echo Leveled Up to N!"** — loot-popup phrasing against a
   narrative card. An oracle asserts that exact string as owner-ruled, so changing it needs her word plus a
   matching oracle edit.
10. **The wave stand-down restarts the countdown from full** after the tutorial rather than letting the wave
    arrive on schedule. Product call.
11. **Promote `api/` (and `assetlinks`) to prod** — still the owner's call, still blocking the wallet rail.
12. **Push.** 43 commits are local-only.

---

## 12. Carried forward UNCHANGED from the 2026-08-05 anchor

Nothing tonight touched these; read `CANON_GROUND_TRUTH_2026-08-05.md` for the detail:
- **Save schema v36**; dual-copy canonical JSON law (`Resources/Data/Canonical/` WINS);
  `/Assets/Resources/Structures/` is gitignored.
- **The economy rebalance (WO-855)** and the still-open **reward economy** findings — active play pays
  LESS than idling, rewards do not scale with enemy level, the apex dragon pays nothing, endless mode is an
  unbounded inflation exploit.
- **Two parallel tower systems still exist**; System B (`Tower`/`TowerCombat`) is **DEAD legacy** by owner
  ruling. *(Note: WO-887's element fix touched `TowerCombat.OnProjectileImpact` — that path is still live
  on the impact seam. Worth re-confirming the ruling's scope.)*
- **`tower_catapult` is UNREACHABLE** (the build menu lists only the cheapest four of five tower rows);
  intended future content as a deployed siege unit, WO-906.
- **Six review screens have no design template** — Hero Loadout, Game Guide, Echo Workforce, Raid Selection,
  Raid Deploy, Troop Training. **The template they need ALREADY EXISTS in the build:** the Echoes roster is
  the one screen that fills its frame, uses a real grid, keeps text inside its plate, and distinguishes
  states by lightness rather than hue.
- **WO-837 step 1 never shipped** — `lumberyard` is still in `BuildModeController.FoundingKit`.

---

*Live anchor. Supersede by date; do not rewrite in place once superseded (CLAUDE.md §15).*
