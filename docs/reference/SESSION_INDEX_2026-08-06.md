# SESSION INDEX — 2026-08-05 evening / 2026-08-06 overnight (the VFX night)

> **Known dictionary** (SUNDAY_HOUSEKEEPING §2). Every row carries its **proving line** or
> **measurement** so any single fact is re-verifiable at a glance rather than re-derived.
> Built from the commit corpus `8fdb29a5..1534dffb` read in full, not from memory. Where a claim
> was **refuted**, the refutation is recorded next to it — a wrong belief that cost time is itself
> a durable fact, and the refuted list is the half that stops the next session re-deriving a wrong
> answer.
>
> **Companion ledger:** `DEFECT_INDEX_2026-08-05.md` covers the EARLIER half of the same day
> (`fe44ddc7` → `8fdb29a5`: the dungeon P0, the wallet dossier, the catalog fallback drift). This file
> picks up where that one stops. Do not duplicate between them.
>
> Session shape: the owner felt-tested on a **Seeker (Android, native 2670x1200)**; the CLI then ran a
> VFX program overnight. **25 commits after `fe44ddc7`, 14 of them in this file's window.**
> HEAD `1534dffb`, **43 commits ahead of origin, NOT pushed.**

---

## 0. HOW TO READ THIS

| Column | Meaning |
|---|---|
| **PROVEN** | A captured line, a measurement, or a source read establishes it. Cite included. |
| **REFUTED** | Believed during the session (sometimes written into a commit or a WO), then disproven. The refuting evidence is named. |
| **REFUSED** | Asked for by a work order and deliberately NOT built, with the measurement that disqualifies it. |
| **OPEN** | Recorded, not yet resolved. |
| **RULING** | An owner decision. Implement it; do not re-litigate it. |

---

## 1. THE DEFECT CLASS OF THE NIGHT (reusable — this is the transferable finding)

> ### A FLAG AUTHORED BY HAND INSTEAD OF DERIVED FROM THE THING IT DESCRIBES.

Six independent defects, one shape:

| Hand-authored value | What it claimed | What the artefact actually was |
|---|---|---|
| `IsLoop` in the VFX catalog | 95 of 135 rows "are loops" | **53 of 122 picks wrong** — `PP_MuzzleFlash` and every `PP_*Impacts` are single bursts at t=0 |
| The tracked VFX prefab copy | "self-contained — this is what ships" (WO-759's own commit said so) | `CopyAsset` copies the **prefab only**: 27 of 28 prefabs, **183 references** into gitignored art |
| `HeroTalentNodeDef.Hidden` | its own comment said the View skips hidden nodes | **zero runtime readers** |
| `TalentStrategyRegression.HiddenTrees` | guard G3 green | hardcoded `{ranger, mage}` — **40 player-reachable nodes never audited** |
| The capture harness resolution | the number in the PNG filename | **a label, not a layout** |
| `CatalogBootstrap.RegisterFallback` | mirrors `structures-catalog.json` | **all three rows drifted** |

**THE LAW:** *if a value describes an asset, DERIVE it from the asset, and PIN the exceptions with their
reason.* Every derivation added tonight (`IsLoop`, height fit, fallback parity, the art mirror) is enforced
by a regression that goes red the day someone re-authors by hand.

**THE COROLLARY, learned inside the same commit that added the derivation (`bd532d5b`):** the prefab is the
authority on **what the art DOES**, not on **what the game SHOULD DO**. Deriving truthfully promoted the
upgrade fireworks to a loop — a flag that is *correct* and *still leaks*, because it is played
fire-and-forget, and the owner had already reported "perma-fireworks" and ruled it one-shot. **So standing
owner rulings are pinned in a table with their reason and outrank the derivation, and every consumer
resolves through ONE method** — because a pin honoured in one place and forgotten in another is the
original bug's exact shape.

### 1.2 Sub-class: the second copy of a rule
`a12c6d22` routes `IsLoop` through the **shared** `VfxLoopFlagRegression` resolver rather than a second
local derivation, explicitly because "writing a second copy of that rule is exactly the divergence that
caused the loop-cap P0." Both catalog generators were corrected together for the same reason: their `Map`
literals carried the same defect one layer up (**15 Hovl entries and 3 `VFXType` entries contradicted their
own art**), and since `Build()` rebuilds the array wholesale, a corrected catalog would have been **silently
undone by the next regenerate**.

### 1.3 Sub-class: a builder-only row is invisible debt
⚠ **`Build()` does `entries.arraySize = rows.Count`.** A row written only by a builder is **silently
dropped** the next time anyone runs `Generate VFX Catalog`, and the effect falls back to something that
**still looks like it works**. That is why map entries land in `VFXCatalogGenerator` alongside the rows.
**The failure is invisible** — named in `7f3971a3` as a trap worth stating out loud.

### 1.4 Sub-class: ordinal serialisation
⚠ **The VFX catalog serialises `VFXType` by ORDINAL, not by name.** An insert anywhere above an existing
value silently re-points **every row below it** at the wrong art. **Appends only.** Verified rather than
assumed: `Boss_FireBreath` still reads `Type: 79` in `VFXCatalog.asset` after the 16-value append
(`0011b8ba`).

### 1.5 Sub-class: comments lie (continuing the 08-05 registry)

| Claim in source | Reality | Where |
|---|---|---|
| `HeroTalentNodeDef.Hidden` — "the View skips hidden nodes" | `HeroSkillTreeVM.Rebuild` **never consulted it**; zero runtime readers | `04d375c3` |
| `VFXCatalogGenerator` header — "`DeNelle.Editor.asmdef` does not reference `DeNelle.Village`" | **It does, and has for some time** | `7f3971a3` |
| `WO-759` commit — "the tracked prefab copy is what ships" | `CopyAsset` copies the prefab only; **183 pack references remained** | `948080f5` retracting `7f3971a3` |
| `SpellsPackVfxMirror.cs` header | Makes the **same** false fresh-clone claim. **NOT fixed** | `948080f5`, OPEN |
| MagentaGuard — "the recovery line was demoted to Warn" | A companion `ProbeFail` **two lines above** still fired at error level for the same offender | `449b16bb` |
| Victory panel — its own comment documented a `0.03..0.97` span | The arithmetic gave `0.06..1.00` — flush with the top screen edge. **The comment had been false since it was written** | `afa50e44` |
| `Poi_NodeAura` / `Poi_Landmark` — files literally **named** `...loop.prefab` | They emit **one burst and stop**. "Which is how the mistake looked reasonable for so long" | `bd532d5b` |

---

## 2. P0 #1 — THE VFX LOOP CAP WAS LEAKING DRY (`3db877d2` diagnosis, `bd532d5b` fix)

**PROVEN, with six captured sessions across two dates.** This is very likely the real content of the
owner's felt report *"the fire from the towers is horrible."*

**Mechanism, in four facts:**
1. `IsLoop` was a **sticky manual UI checkbox**. `VfxCasterWindow` **force-set it true** for any row tagged
   `Projectile` or `Aura`; **nothing ever read the prefab's emission.** 95 of 135 Hovl rows carried
   `IsLoop:1`, including every `PP_*Impacts` and `PP_MuzzleFlash` — all burst prefabs.
2. **A loop row never returns its slot.** The oneshot branch registers a deadline and gets swept; the loop
   branch does a bare `++` and hands back a handle — and **the only loop reclaim frees DESTROYED hosts,
   while pooled objects are never destroyed.**
3. **The global cap is 20.**
4. **The archer and ballista — the most common towers — fire `PP_MuzzleFlash` and DISCARD the handle.**

**Consequence:** after roughly twenty shots a tower renders **no projectile at all**, and in the same breath
starves the Tree of Life aura and every POI marker.

**PROVING LINES (`break-log`, six F8 sessions, two dates):**
```
SKIPPED - active loops 20/20     ArcherTower_Projectile
SKIPPED - active loops 20/20     ARcaneTower_Projectile
SKIPPED - active loops 20/20     ArcaneTower-Baselevel_Projectile
SKIPPED - active loops 20/20     Poi_NodeAura
SKIPPED - active loops 20/20     Poi_Landmark
```
**The victims turn out to BE the culprits** — all five just flipped from loop to burst. *They were filling
the cap that then starved them.*

**THE DERIVATION RULE, stated once in one place:** `main.loop` AND a positive rate over time or distance,
with emission enabled. **The authority is the root system UNLESS the root cannot emit**, in which case it
falls through to the first system that can. ⚠ **That exception is not tidiness:** Lana's
`Fire_medium.prefab` is a root with its emission module **DISABLED** over a child emitting 15/sec, so
strict root-reading would have called the burning-structure, torch and fog auras one-shots and **cut them
off mid-burn**.

**Blast radius the diagnosis caught before it shipped:** WO-870 would have made it worse (more
fire-and-forget plays into the same leaking bucket). **WO-875 is worse still — two live abilities carry
`PP_MuzzleFlash` as a cast key, and Quick Shot has a 0.45 s cooldown, so un-gating it would exhaust all
twenty slots in about nine seconds of shooting.** Their diagnoses were right; only the sequence was wrong.

**Durable guard:** `VFXManager` now routes a loop with a **declared finite lifetime** through the leak-proof
oneshot path. No row declares one today — it exists so the next fire-and-forget loop cannot quietly re-open
this. `VfxCasterWindow`'s checkbox is **read-only and derived**; the role-based force-set is deleted.
Marker `VFX_LOOPFLAG_OK`.

> ### ⚠ OPEN — TWO THINGS THIS FIX DOES **NOT** CLOSE
> 1. **The ABSENCE of the cap message across a full wave is NOT proven.** Six of six captures show it
>    firing — that is a real *before*. **The *after* needs a fleet run before anyone claims it.**
> 2. **The ONESHOT pool saturates at 40/40 in three OTHER captures.** Different pool, different reclaim
>    path. **Recorded and deliberately NOT bundled. The loop fix must not be assumed to close it.**

---

## 3. P0 #2 — THE TRACKED VFX PREFABS WERE NEVER SELF-CONTAINED (`948080f5`)

**PROVEN by measurement, and it retracts a claim this same session had already committed.**

`CopyAsset` duplicates the **PREFAB ONLY** — never its materials, textures, shaders, meshes or animations.
So every prefab duplicated into `Resources/VFX` was a **tracked file pointing straight back into gitignored
art.**

| Measurement | Value |
|---|---|
| Prefabs exposed | **27 of 28** |
| References | **183** |
| Distinct assets | **73** |
| After the mirror | **0**, verified twice — the mirror's own report **and** an independent recursive GUID walk that does not reuse the builder's code |
| Mirrored size | **~23.85 MB**, deduped (`Glow.mat` referenced 12x, `Trail` 9x — one copy each) |

On any machine without the packs — a fresh clone, the laptop, CI — all of it renders **missing**: magenta,
untextured, or invisible depending on platform. **Latent only because this machine happens to have the
packs.** The exposure was wider than materials: a mesh (`FireFly.fbx`), a nested pack prefab pulled in
through the ParticleSystem **LIGHTS** module, two `.anim`, a `.controller`, and **two C# MonoBehaviours**.

⚠ **THE ONE JUDGEMENT CALL, AND IT IS FELT-VISIBLE: the two scripts were STRIPPED, so `Casting_Fire` no
longer spawns a projectile.** Copying a `.cs` would put two identical types in `Assembly-CSharp` and take
the compile gate down for **every** lane. Removal is right on its own merits anyway — inside a pooled,
manager-driven prefab those demo scripts read a Rigidbody that is not there, `Destroy()` a pooled instance
on collision, and `InvokeRepeating` a fireball once a second **forever**. Severing them also dropped the
last path to six more pack prefabs.

### 3.1 The mirror only converged on a FIRST run — PROVEN, fixed in `29f9ac2b`
It seeded its dependency walk **from the prefabs**. On any later run the prefab already pointed at the
**mirrored** material, the walk saw a target outside the pack, skipped it, and **never re-entered that
material** — leaving the pack texture the material itself referenced undiscovered. **Six prefabs read as
self-contained while their art was one hop away.** It now re-seeds from everything already mirrored:
**a fixed point has to be fixed ACROSS runs.**

Two collisions surfaced with it: `ParticlesLight` and `ramp01`/`Ramp01` exist in two pack folders — **the
second pair differing ONLY IN CASE, which is one file on Windows and two on CI.** Destinations are now
derived from the source path and qualified only for the later claimant, so **no committed GUID moves.**
The manifest had also carried that collision to disk, where `File.Exists` happily confirmed the **wrong**
texture; shared-mirror entries are now dropped on load so they re-resolve.

### 3.2 Settled facts
- **Lana Studio is NOT gitignored** — only its URP upgrade subfolder is. `Flash_generic` sources all seven
  of its dependencies there and measures **zero** exposure. *(This corrects a standing assumption.)*
- The mirrored FireFly shader is renamed under a `VFXMirror` namespace so it cannot collide with the pack
  copy still present on this machine; **materials bind by GUID, so it is free.**
- The regression deliberately does **NOT** require zero deps outside the VFX tree — that would force
  mirroring tracked art like Lana Studio and the URP package shaders for no benefit — so it reports that
  total separately. Marker `VFX_ART_MIRROR_OK`.

### 3.3 OPEN, stated not hidden
- `SpellsPackVfxMirror.cs`'s header makes the same false fresh-clone claim.
- `_Shared/Textures` is **16.9 MB of `.tif`** sitting outside the texture-optimizer sweeps' root list.
- The base tower's Hovl muzzle key **points straight into the gitignored pack** and renders nothing on a
  fresh clone. Left alone (it carries the tier scale); the tracked type now plays alongside it.

---

## 4. VFX — THE CONNECTION LEDGER (`3db877d2`)

**PROVEN by a GUID sweep. This is a wiring problem, not an art problem.**

| Measurement | Value |
|---|---|
| Enum values wired to real art with **ZERO gameplay callers** | **26 of 79** — including the PERFECT-hit flash, four per-species death bursts, the enemy caster's bolt |
| Tracked Lana categories at **0% usage** | **Six whole categories** |
| Prefabs + scenes swept for VFX scripts | **8,795 prefabs / 156 scenes → ZERO VFX scripts attached anywhere** |

That last row is what makes **`EliteVFXController` dead three separate ways**. Its GUID appears in zero
prefabs and zero scenes, so its `GetComponent` has **always** returned null and `OnEliteDeath` **has never
run in the shipped game**. ⚠ **Consequence: the 0.7 boss death shake written into WO-886's own acceptance
criteria has never fired. Every kill, boss included, got the flat 0.18.** The tier rule was lifted into
statics that both the Enemy death path and the controller call, rather than auto-attaching the component —
attaching it would also switch on an aura light pulse and a dramatic spawn routine, **three unrequested
felt changes under a death-VFX ticket.**

**The creative frame, in one sentence (owner-facing):** *in a game where you control one unit among many
autonomous ones, VFX is the only channel through which the autonomous half reports to you.* So
**readability, not spectacle** — and the pack that is tracked in git turns out to be the pack whose
vocabulary best fits a landscape phone.

---

## 5. DEFECTS FOUND WHILE FIXING SOMETHING ELSE (each PROVEN)

| # | Defect | Proving line / measurement | Commit |
|---|---|---|---|
| 5.1 | **Boss deaths detonated TWICE** | Every explosion in this pack ships `looping:1` + `prewarm:1` with its whole payload in a burst at t=0, while the pool reclaims at duration + max lifetime — **~4.3 s on BigExplosion — so the burst re-fired at t=2** | `29f9ac2b` |
| 5.2 | **Every empowered tower detonated in arcane violet** | `TowerCombat.OnProjectileImpact` computed the projectile's element **EIGHT LINES BELOW the impact pick and never used it** — so a fire tower's bolt burst as `Impact_ExplosionAether` with the arcane bang over it. Routing by element also routes the paired `SfxId`, so an ice tower had been playing an arcane bang | `4ef2d532` |
| 5.3 | **`FireAt` used a LOOP as a muzzle flash** | It played `Projectile_TowerArcane` — a **projectile-BODY row with `IsLoop` TRUE** — on the busiest call in the game. Another instance of the §2 P0 | `4ef2d532` |
| 5.4 | **`Death_Boss` / `Boss_Death` alias drift, already live** | `Death_Boss` sat on the 3f fallback case, `Boss_Death` on 4f — the exact drift WO-886 warned about. Merged onto one case sharing ONE prefab. `Boss_Death` had pointed into the **gitignored Spells Pack** and rendered nothing on a clone | `29f9ac2b` |
| 5.5 | **Elites died as trash** | `Elite_Death` had **no catalog row at all**, and the species check tested **family before role**, so elites resolved as plain Hollow | `29f9ac2b` |
| 5.6 | **The death ladder ran out of order** | Dungeon deaths were bigger than elite ones | `29f9ac2b` |
| 5.7 | **Enemy deaths all landed on one grey procedural blob** | `Die()` routed override → typeSet → generic and **never consulted the species**, so the pool/factory spawn path (which sets neither) could never reach the four authored death bursts | `a186c282` |
| 5.8 | **The PERFECT hit's entire visual payoff was an ASCII text stamp** | A real timing mechanic rewarding precision, with no flash — "or it stops teaching itself" | `a186c282` |
| 5.9 | **The enemy's blow connecting was silent** | The pre-swing telegraph fired but the hit did not, so the player learned they were struck from a health bar | `a186c282` |
| 5.10 | **The hero's own death was the least-marked event in the game** | Hit-stop and an animation, nothing else | `a186c282` |
| 5.11 | **MagentaGuard was painting vendor particle TRAIL slots white** | `MagentaProbe FAIL` on the Arcane Tower Hovl aura (`ElectricyCenter`, slot 0, material NULL). On a `ParticleSystemRenderer` slot 0 is the particle material and slot 1 the trail; that system is **trail-only by vendor design**. The recovery assignment sat **OUTSIDE the dedupe guard** → unconditional, stuck in built players, aura rendered as a **white opaque blob**. **28 of 261 Hovl prefabs carry this pattern**, all sharing one material instance | `449b16bb` |
| 5.12 | **The potion tap was dead because the button disabled itself at zero** | **433,897 `Player.log` lines with ZERO `command potion fired` entries**, while `attack` and `assignableCast` fired hundreds of times through the same `ActionBar` mount | `164ade0a` |
| 5.13 | **THERE IS NO APOTHECARY, and the empty state pointed at it** | Recipes, panel, VM and service all exist — but `PanelId.ConsumableCrafting` has exactly two openers, both requiring an `ApothecaryWorkbench`; the station injector returns early (its standdown check is unconditional); and **`structures-catalog.json` has no apothecary row at all**, so the palette can never build one and no saved layout can replay one. **A message pointing at a place the player cannot reach by any path is worse than no message** | `164ade0a` |
| 5.14 | **The mana icon fallback chain terminated in `crystal`** | Literally the wrong sprite the owner reported | `164ade0a` |
| 5.15 | **The potion count badge clipped its own digit** | Fraction-anchored, resolving to ~**41x30 px on a ~94 px medallion — SHORTER THAN THE 32 px GLYPH**. Now a fixed 52x40 plate, parented **after** the round-medallion styling (that call can add a gold rim child, and last-parented draws on top) | `164ade0a` |
| 5.16 | **The victory reward icon was ONE LETTER** | Label `Crystals`, data key `crystal`. The lookup missed and fell through to a generic CHEST. **The correct art was committed and imported correctly the entire time.** Fixed in the DATA (five plural aliases, both copies byte-identical) | `afa50e44` |
| 5.17 | **The victory subtitle assumed 36 characters per line** | It now measures **real glyph advances** from the shipped TMP asset. That constant was tuned for the portrait panel, and **in portrait it was UNDER-reserving by 45%** — the hero-death message was spilling. Column width is now derived from aspect, so one number cannot be right on one orientation and wrong on the other again | `afa50e44` |
| 5.18 | **A 92 px void between a reward icon and its label** | A fraction cannot hold a constant gap across a 2x width change — it was **105 px in landscape and 20 px in portrait**. Now fixed pixels | `afa50e44` |
| 5.19 | **The side menu's "second gear" was the drawer HANDLE** | `BuildSlideTab` pins the tab and the panel to the **SAME edge at the same origin**, so the handle sat on the panel's rim — and being centred on the mount's midpoint, landed **dead on the Music row**, whose band spans 0.42-0.58. **Every resolution, not a Seeker quirk.** The handle was authored at **84 px — under the kit's own touch floor** | `fce950ae` |
| 5.20 | **The side menu's row icons could never be uniform** | `concept-icons.json` maps only `settings` of the five row concepts; there is no art at all for chat, music, leaderboard or pause. **Four rows silently resolved null.** Removed rather than restyled | `fce950ae` |
| 5.21 | **The "wrong colour" gear was ONE sprite, twice** | `icon_settings.png` is dark art: on the handle's gilt plate it reads gold, bare on the Gray obsidian face it reads as a smudge | `fce950ae` |
| 5.22 | **The tutorial stand-down would have tripped its own watchdogs** | `GuardedKickoff` had armed `RetryTillActive` + `StallWatchdog`, and **neither can tell a deliberate stand-down from a stall**: three refused `BeginLoop` re-fires, a `FlowTrace` failure, then `StallWatchdog`'s `LogError` at nine seconds. **Under the F8 daemon contract that is a captured error mid-tutorial — a FALSE P0 on the owner's first-ever run, manufactured by the fix for the real one** | `c95ff5f4` |
| 5.23 | **Both FTUE predicates FAIL OPEN on the same null** | `WaveManager.Start` auto-arms on `!IsFirstRun()`, and `IsFirstRun` fails open when `GameStateService` has not bootstrapped. **A fresh save that wins the race against Core bootstrap does fire the kickoff**, and async `BeginLoop` reaches `EnterCountdown` a few frames later — *after the gate has closed behind it.* That is why the per-tick re-check was right and swapping the door predicate alone would not have closed it | `c95ff5f4` |
| 5.24 | **A pooled instance can silently inherit a modulation FOREVER** | Driving an HP pulse means mutating a **pooled** instance, and the pool resets only what it changed itself — so the next effect to use that slot inherits the modulation with **no way to trace it back**. A modulator now keeps the pristine baseline on the instance and restores from **both** ends: the handle's stop **and** the pool's return | `1534dffb` |
| 5.25 | **The archer fallback row would have shipped a wrong-sized plot** | `CatalogBootstrap.RegisterFallback` authored `footprint 2.5` against the catalog's **1.75** | `0ac59581` |
| 5.26 | **`deco_torch` was fitting a WALL TORCH to 4 m** | It inherited the building default — as tall as a house | `d42e2817` |
| 5.27 | **The wizard tower and arcane spire were floating 4% above the tower tier** | Surfaced by the cadence pass | `d42e2817` |
| 5.28 | **All 14 Particle Pack sources ship `playOnAwake` enabled** | The builder clears it on every system, **or a prewarmed pool instance emits at the world origin** | `a12c6d22` |
| 5.29 | **`RegistryMarkerRegression` treats a marker token inside a string as an EMISSION** | Naming the harness marker in a failure string made that file look like a **second emitter** and failed the collision guard on the first run — caught by the very guard that exists because three entry points once shared one marker | `7e05e6d3` |

---

## 6. REFUTED — the beliefs that were killed, and what killed them

> **This section matters as much as §5.** Each of these was believed, several were written into a commit
> message or a work order, and re-deriving any of them costs a session.

### 6.1 REFUTED — "the victory diamonds are a font / TMP glyph problem"
**Believed, and COMMITTED, in `c374bd44`:** *"The gold diamonds are NOT a missing sprite — they are a
deliberate placeholder (TMP star glyphs tofu'd on the build font...)"*.
**KILLED by reading the code (`afa50e44`):** the old code built a **sprite-less `Image`** — which draws a
**solid white quad** — and rotated it **45 degrees**, with the literal comment `// diamond`.
**There was never a star to fix; there was a square pretending to be one.** No star sprite exists anywhere
in `Resources`, so one is now **generated** the same way the kit already generates its rounded, circle and
ring sprites, behind a three-rung degrade ladder (generated star → kit pip → the legacy square) so the row
can never vanish.
**Why it mattered:** the font theory pointed at the build font and the TMP asset. Neither was involved.

### 6.2 REFUTED — "`ClampMinTouch` centre-grow is the cause" (three separate sites)
This guard is a real, five-times-shipped defect class (`DEFECT_INDEX_2026-08-05.md` §1.2), which is exactly
why it kept being reached for first. **It was CHECKED, not assumed, at three sites tonight and RULED OUT at
all three:**

| Site | Measured band | Floor | Actual mechanism |
|---|---|---|---|
| Echo card, buttons (`ee2a2855`) | **116.7 – 130.6** units | 112 | Content anchored to `chrome.content` (panel fractions) while the black plate **is** `chrome.layout.body`, whose floor the kit **RAISES at runtime** |
| Merged Echo card, CTAs (`6737f983`) | **117 px** | 112 | **MIXING fraction-sized buttons with the kit's fixed-size `Close` in one row** — two heights, two widths, and until recently two corner styles |
| Side-menu rows (`fce950ae`) | **exactly 112.0 px** | 112 | **Two rects sharing one anchor origin** (`BuildSlideTab` pins tab and panel to the same edge) |

And a fourth, different again — **the victory crush is ARITHMETIC, NOT LAYOUT** (`afa50e44`): the
panel-height law solves so the well equals the required body **exactly**, so by construction there is **zero
slack anywhere** and the only breathing room on the screen is an 8 px gap. **Landscape has 965 reference px
of height; an arena win with five spoils asks for a 1027 px panel.** The content was taller than the screen,
so every band got squashed 11%.

⚠ **The counter-instance, so the class is not dismissed either:** FOUND YOUR TOWN (`13c0e728`) **genuinely
was** the centre-grow — 0.18-of-body bands against a body shrunk to ~313 units by a reserved-but-hidden
`Close` box, resolving to **~56 px**, then grown ~28 px symmetrically about each centre.
**RULE: measure the band before naming the guard.** And note `fce950ae`'s razor-thin margin — the side-menu
rows sit at exactly 112.0, so **any shrink of the 700 px panel drops them under the floor**. Left as a
load-bearing comment.

### 6.3 REFUTED — "`Ranger.fbx.tripo-extracted` is a parked mesh" (WO-909's stated premise)
**KILLED by opening the file (`d0c7b8fd`):** it is a **125-byte PLAIN TEXT SENTINEL** written by
`TripoAssetPostprocessor`. **There is nothing to un-park.** And the disproof is structural, not just
textual: **Knight's sentinel sits beside a live `Knight.fbx`**, which proves the marker never blocked an
import. The comments repeating the premise were fixed in the same commit.
**Standing instruction from that commit: "Nobody should spend another cycle on it."**

### 6.4 REFUTED (as the explanation) — "a stale `ff.knightonly` PlayerPref is why only Knight shows"
**The theory is on disk**, as WO-909 §4 *"Known gotcha — stale PlayerPref"*
(`WorkOrders/WORK_ORDER_909_activate_mage_ranger_character_select.md:93-101`), and its **mechanism is
real**: `FeatureFlags.Get` reads `PlayerPrefs "ff.<name>"` FIRST and a stored `1` **wins over the new
default** (`Assets/_Modules/Core/FeatureFlags.cs:689-695`; `:68`
`KnightOnly => Get("knightonly", defaultOn:false)`).

**But it is not what explains the observed behaviour.** The proven reason a returning save never shows
Ranger or Mage is that **the hero-select screen SELF-SKIPS**:
```
HeroSelectController.cs:123-131   OnEnable(): if (_skipWhenIntroComplete && IsIntroComplete())
                                             { SceneRouter.GoCastle(); return; }
HeroSelectController.cs:156-161   IsIntroComplete() => svc.State.HeroClass != HeroClassOpt.None
HeroSelectController.cs:85-89     [SerializeField] private bool _skipWhenIntroComplete = true;  // default ON
```
Corroborated from both entry points: `TitleController.cs:385-395` **clears the persisted class on
Play Intro precisely because of this self-skip**, and `TitleController.cs:411-431` routes **Continue
straight to the castle with no hero-select at all**.

> ### ⚠ THE OPERATIONAL FACT: **testing Ranger or Mage requires New Game / Play Intro. Continue will
> never show the carousel, no matter what the flag says.**

**STALE / OPEN:** no stale-pref **migration** was ever implemented in `FeatureFlags.cs`, and no WO-909
`.RESULT.md` exists on disk. So the pref hazard remains **real and untested** on an install that carries a
V1-era `ff.knightonly=1` — it is simply not the thing that was observed. **Verify on a cleared-prefs
profile before ruling the gotcha closed.**

### 6.5 STALE — "the companion was colliding with itself / shoving the hero" — NO ON-DISK TRACE
> **STALE FLAG (2026-08-06):** this suspicion was raised in session, but **a full sweep of `.md`, `.cs`,
> `WorkOrders/`, `docs/qa/`, `Builds/*.log` and `git log --all` found NO record of it being raised or
> refuted.** There is no `CompanionController` or `PetFollower` type in the repo, and no
> `Physics.IgnoreCollision` call anywhere. **Recorded here as unverified rather than written up as fact.**

What the code **does** say — and it argues the opposite, i.e. the companion is deliberately built not to
push anyone:
- `Assets/_Modules/Village/NPCs/StoryCompanionInjector.cs:633-648` — a slim non-trigger `CapsuleCollider`
  (r 0.35, h 1.9), with the note *"Kept slim + the NavMeshAgent yields (avoidancePriority 60) so it never
  shoves the hero."*
- `StoryCompanionInjector.cs:609` — `Object.Destroy(col); // no collider -> never shoves anyone`
- `StoryCompanion.cs:355-356` — `_agent.radius = Mathf.Min(_agent.radius, 0.35f)` / `avoidancePriority = 60`
- `Assets/_Modules/Pets/PetDeployer.cs:830` — `StripPetColliders`; pets carry **no collider at all**.

**The two "hero cannot move / hero is in the wrong place" symptoms a companion theory would explain both
have proven, unrelated causes** (both in `DEFECT_INDEX_2026-08-05.md`): the `HeroLocomotion.Update` ±50
clamp writing `transform.position` **every frame** in every unbaked dungeon (`ArenaCentre (5000,0,5000)`
clamps to exactly `(50,0,50)`), and the hub spawn resolving to world origin **inside the tree's canopy**
(`bfe9f0c3`). Check those before re-opening a collision theory.

### 6.6 REFUTED — "the tracked prefab copy is what ships"
Committed as fact in `7f3971a3` (WO-759). **Retracted with a measurement eight commits later**
(`948080f5`): 27 of 28 prefabs, 183 references, 73 assets. See §3.

### 6.7 REFUTED — "`EliteVFXController` is running"
Its GUID appears in **zero prefabs and zero scenes**. `OnEliteDeath` has never run in the shipped game.
See §4.

### 6.8 REFUTED — "`collector_farm` at 1.4 is the file's worst height outlier"
**It is a COMPENSATION, not an outlier** (`d42e2817`). `heightMul` fits **bounds**, and the farm's windmill
blades inflate the Y bounds — so at 1.0 its **body** fitted far smaller than a boxy forge at the same 4 m,
and **the owner felt-reported exactly that as "the shrunk farm"** (`31b41d19`). It is precisely the row a
flatten-everything pass hits first. **Left alone, with a row note so nobody "fixes" it again.**

### 6.9 REFUTED — "walls are the obvious next height correction"
At 1.0 they are 4 m, nearly tower height. But the fit is **uniform**: lowering a wall **NARROWS** it by the
same factor, and every wall in an already-saved town sits on the cell pitch of its old claim. A narrower
segment does not re-pitch its neighbours — **it opens PATHABLE GAPS in existing wall runs and shrinks the
navmesh obstacle with them.** *"That is a worse save break than an overlap, and it is invisible to the
usual shrink-is-safe reasoning."* Needs a measured audit (`StructureHeightAudit` prints `measuredY` per
prefab) **plus a migration decision.**

### 6.10 REFUTED — "the ruling was 1.5x, so apply 1.5x"
The ruling was **formed against a stale number**. Canon and the briefing said the tower was 7 m, but WO-764
had already cut it to `heightMul 1.25` (5.0 m). **So 1.5x reads as a cut from 7 and is a 20% INCREASE over
the live 5.0.** At 1.5 the base reaches 3.472 m — **125% of the half-house target** — and crosses the 3 m
grid cell, flipping the tower from a **1x1 to a 2x2 claim**: the same town real estate as a house, the exact
opposite of the ruling's second half. **The two halves of the ruling are only simultaneously satisfiable
near 1.2.** Owner was shown both and chose 1.2.

### 6.11 REFUTED — "`FleshImpacts` is a hybrid, so a burst flag is fine"
WO-887's own prose called it hybrid. **Measured, the hybrid layer IS the derivation authority**, which makes
it **disqualifying rather than a footnote**. Forcing a burst flag would leave a five-second trickle, not a
hit.

### 6.12 REFUTED — "there is a surface signal to read"
**Verified rather than assumed (`4ef2d532`):** no `SurfaceType` field, no physic-material read, no
per-material tag. **Wood palisades, stone walls and steel gates all share one `Structure` layer**, and
**both** footstep implementations play a single clip with no surface query. The nearest real signal is
`WallTier` on player walls — a **progression index**, not a material. **Defining a surface taxonomy is
design work and belongs to the owner.**

### 6.13 REFUTED — "the Arcane Tower aura is missing art" (MagentaProbe FAIL)
**FALSE POSITIVE.** On a `ParticleSystemRenderer` slot 0 is the particle material and slot 1 the trail; that
system is **trail-only by vendor design**, so the empty slot is legitimate. The guard's own recovery was the
defect. See §5.11.

### 6.14 REFUTED — "`DeNelle.Editor.asmdef` does not reference `DeNelle.Village`"
It **does**, and has for some time (`7f3971a3`). The false premise was removed *"before it talks another
agent into contorting around a dependency that already exists."*

### 6.15 Carried from the earlier half of the day (see `DEFECT_INDEX_2026-08-05.md`)
"The fade mask never lifts" · "off-mesh landing = immobility" · "the dungeon repositions her, so suppress
the warp" · "the user declined the wallet request" · "devnet is the blocker" · "the orientation conflict
killed the handshake". **All refuted with evidence there. Do not re-derive them here.**

---

## 7. REFUSED — asked for, deliberately not built, with the disqualifying measurement

| Asked | Refused because |
|---|---|
| `Death_Skeleton` / `Death_Wolf` → `SparksEffect` | **MEASURES CONTINUOUS — 80/sec on loop at the root**; its only burst is a 0.2 s child that is **not** the derivation authority. Cataloguing it either hands a rate-emitting loop to a fire-and-forget death (**the §2 P0 straight back — one of 20 slots per kill**) or forces a burst flag onto a live emitter and reclaims it mid-emit. They keep their tracked Lana rows, so nothing regresses |
| The five WO-887 **surface** impact rows | Three independent grounds, any one disqualifying: **(1) DEMO GEOMETRY** — all five carry, on the prefab ROOT, a MeshFilter with a built-in primitive, a MeshRenderer with a pack material and a **SPHERE COLLIDER**; they are the demo *target*, with the particle tree hanging off one child. Copying one renders a lit primitive and **ADDS A PHYSICS COLLIDER at every hit**. (`MuzzleFlash` has none of the three — which is exactly why it was safe.) **(2)** All five emit **5/sec on loop** at the authority. **(3) NO ENUM HOME** — there is no `Impact_Flesh/Metal/Stone/Wood/Dirt`, and the enum is a single-owner append. Bytes with no consumer |
| Re-pointing `Impact_Flame` / `_Ice` / `_ExplosionAether` / `_Physical` at pack recipes | They already point at **deliberate tracked picks**, including the Lana slash arc the owner ruled for on 2026-08-02. Swapping would be a downgrade |
| `GoopSpray`, ever | `DamageElement` is `{None, Aether, Flame, Ice}` — **this game has no nature element** |
| `Cast_Heal` / `Impact_Heal` as held auras | Fire-and-forget one-shots whose ratified recipes **measure CONTINUOUS (3/sec and 5/sec on loop)** — repointing leaks a loop slot per cast |
| The arcane gear aura as a loop | **Rate-0 with a single burst** — held as a loop it pops once and then occupies a slot showing nothing |
| `Enemy_Spawn` / `Despawn_Dissolve` prefabs | **SCRIPTED recipes** — each carries a pack MonoBehaviour plus a demo mesh for it to dissolve. Copying ships a prefab that renders a demo mesh wherever it plays and carries a **missing-script reference on any clone**. They need a runtime component driving the **TARGET's** material cutoff: authoring work, not a copy |
| The death "lingering-loop" column | The recipes measure correctly as loops, but **no `VFXType` exists for a death linger** and the enum is a single-owner append. Bytes with no consumer |
| `Death_Wolf` / `Death_Tiefling` mappings | The roster has exactly **three** families — hollow, orc, troll. Ice Wolf exists only as a **pet**. Routing them at an orc or a troll is **a creative pick, and the owner's call** |
| An `SfxId` mapping for the new release flash | The tower already plays its own fire sound, so a mapping would **double every shot and put a tower sound on a bow** |
| A star sprite chosen from the icon library for **Wisdom** | Nothing in the icon set reads as wisdom, and the 500-icon spell library was catalogued **by silhouette inspection specifically because the owner is colourblind** — picking one blind is the substitution that is not allowed. An override lever is staged so she can repoint it in one JSON line |
| Widening `IsVillageScene` for the dungeon | It also gates `Watch()` and the emergency-hero path, so a dungeon clause would **fabricate an emergency pill** during the window where `FindLoco` is legitimately null |
| Auto-attaching `EliteVFXController` | It would also switch on an aura light pulse and a dramatic spawn routine — **three unrequested felt changes under a death-VFX ticket** |
| Registering the right-rail flyout with `PanelManager` | It would flip `AnyOpen` — suppressing interaction prompts — and subject the rail to the WO-437 battle lock. **A behaviour change smuggled into presentation work, which the architecture rules forbid** |
| Writing `hidden:true` on the 31 dead talent nodes | `HeroTalentNodeDef.Hidden` had **zero runtime readers**, so it would have greened the gate and left every node **fully clickable**. See §8 |

---

## 8. WO-910 — THE PRODUCT FINDING (READY FOR OWNER RULING)

**PROVEN (`04d375c3`).** `TalentStrategyRegression` hardcoded `HiddenTrees = {ranger, mage}`, so **guard G3
(no dead talent nodes) had NEVER audited them.** Unlocking both classes made **40 player-reachable nodes**
live while the gate still reported green on 41. Emptying that set surfaced **31 real, pre-existing failures.**

**"Dead" here means NO RUNTIME CONSUMER** — the node is visible, clickable and spendable, and does nothing.
It is not a visibility state. That distinction is exactly why §8's fix could not be "hide them."

| Class | Usable talents | Tier-4 capstone row |
|---|---|---|
| **Ranger** | **1 of 20** *(usable = has a runtime consumer)* | **dead** |
| **Mage** | **5 of 20** | **dead** |
| Knight | 32 of 32 | green |
| Shared | 9 of 9 | green |

**Two of the three playable classes ship with no talent progression and nothing to build toward.** That is
the direct answer to "does picking Ranger work end to end": **the class is selectable and playable, and its
tree is empty.**

**Hiding was ordered, then found to be wrong, and was NOT done.** `HeroTalentNodeDef.Hidden` had **zero
runtime readers** — `HeroSkillTreeVM.Rebuild` never consulted it — **while its own comment claimed the View
skips hidden nodes.** Writing `hidden:true` would have turned G3 green and left all 31 nodes fully clickable
in the player's tree: *"suppression spelled in JSON instead of a baseline list. A field whose comment lies
is worse than no field, because it makes the next person confident about something false."* Hiding also
**strands three whole tiers and orphans three nodes** whose only prerequisite would vanish.

**So:** `hero-talents.json` is **UNTOUCHED, md5 unchanged.** The 31 are one dated, WO-910-numbered baseline
entry, and **the ratchet cuts both ways** — a dead node **not** in the baseline still fails (debt cannot
grow), **and a baseline id that stops reporting dead ALSO fails**, naming the line to delete, so a baseline
entry can never outlive its debt. **There is no way to make this gate green by editing the set.**
`Hidden` is now genuinely wired into both `Rebuild` loops — a no-op today (no node sets it), but it means
the owner's ruling will work when she makes it. It deliberately does **not** re-point prerequisites: a child
of a hidden node stays visible and reads that it requires it, because auto-repointing would silently reshape
the tree, **which is her call.**

---

## 9. OWNER RULINGS IN FORCE (from this session)

| # | Ruling | Verbatim / note |
|---|---|---|
| R10 | **Unlock Mage and Ranger** | `ff.knightonly` defaults OFF; roster Knight/Ranger/Mage via `PlayableHeroes`. **Cleric stays out deliberately** — no authored kit |
| R11 | **Mage is the magic/VFX showcase** | *"Mage should obviously live heavily in that realm"* |
| R12 | **The Echo unlock is ONE screen** | *"I don't need one screen that tells me that I have an echo, and the next screen that shows me about the echo, it should just simply be one screen"* |
| R13 | **Two buttons, not three** | *"it does not need three buttons. Two buttons is fine."* The retired one is the shared **Close** — a duplicate dismiss that the review found sitting **between** the two positive actions. Retired **locally, not in the kit** (that Close is canon for ~19 other panels) |
| R14 | **One right-rail style** | *"The echoes, the builders, and the resources should all be styled similarly. So they're all the same until you click and open and expand them."* |
| R15 | **One height cadence across every structure** | *"take whatever the y height that we use across the board... all of the other structures stay within that cadence... relatively the same size... all scaled to the same point."* Landed as 1.25 / **1.2** / 1.0 / 0.75 / 0.35, recorded in the data as `_heightCadence` |
| R16 | **Archer tower = `heightMul` 1.2** *(carried from 08-05 R1)* | Chosen over the literally-stated 1.5 once shown that 1.5 would make it **bigger** and flip it to a 2x2 grid claim |
| R1..R9 | *(carried — see `DEFECT_INDEX_2026-08-05.md` §6)* | Incl. **R9: every ticket carries its screenshot**, standing |

**Standing rulings now PINNED in code so a derivation cannot overwrite them:**
- **The upgrade fireworks are ONE-SHOT** (owner reported "perma-fireworks"). The truthful derivation says
  `loop`; the pin wins. Caught on the first run by the existing `vfx-aura-diff` oracle.
- **The bar Queues button is RETIRED** (2026-08-01) — the right-column **Builders chip** is the one Queues
  entry; tapping the open chip fires the full Work Queue. Oracle stays green.
- **Echo affinity is a MATCH BONUS, NEVER A LOCK** (WO-830 / WO-907 grammar) — unchanged.
- **The dungeon is authored pitch-dark BY DESIGN** (the lantern/torch mechanic) — the arena's lighting leak
  was scoped without touching the darkness.
- **VFX keys are mapped from an owner-tagged key VERBATIM, never picked.** This is why the item heal aura
  ships inert and why Wisdom keeps its campfire stand-in.

---

## 10. OPEN — awaiting an owner decision or a run

| # | Open item | Why it is blocked |
|---|---|---|
| O1 | **WO-910 — Ranger and Mage talent trees** | Wire the 31, hide them, or re-author. **A design call, not an implementation ticket** |
| O2 | **The surface taxonomy** | No surface signal exists anywhere in the game (§6.12). Defining one is design work |
| O3 | **`Death_Wolf` / `Death_Tiefling` / `Death_Skeleton`** | Routing them at one of the three real families is a creative pick; or rule that `SparksEffect` may be **re-authored as a one-shot** |
| O4 | **Which accessory carries the heal aura** | The item aura is **inert until an accessory is tagged**. **Only the flameblade carries element data today**, so the fire smoulder is the one gear aura with live data |
| O5 | **`Cast_Heal` is a green glow** | The heal **CAST** beat still reads partly by hue even though the HP state no longer does. A second accessibility pass |
| O6 | **Wisdom's icon** | Staged behind a one-line JSON override; picking one blind is not allowed |
| O7 | **The market has THREE player-facing names** | `Store` (build palette) · `Market Stalls` (shop header) · `Marketplace` (NPC) — **no single authority**. The toast uses the shop header because that is what the player reads while buying, but the owner's own word was "store" |
| O8 | **Potion crafting has NO reachable entry point** | Recipes, panel, VM, service — and no apothecary catalog row. The only potion supply in the game is the market shelf and loot drops |
| O9 | **The Echo level-up header reads "Echo Leveled Up to N!"** | Loot-popup phrasing on a narrative card. **An oracle asserts that exact string as owner-ruled**, so changing it needs her word *plus* a matching oracle edit |
| O10 | **The wave stand-down restarts the countdown from FULL** | Rather than letting the wave arrive on schedule with aggression held. Defensible, but a product call. ⚠ **No suite covers the stand-down, and this system has now broken three times** |
| O11 | **The absence of the loop-cap message across a full wave** | Owed a **fleet run**. Six of six captures show the *before* |
| O12 | **The ONESHOT pool 40/40 saturation** | Separate pool, separate reclaim path. Not addressed |
| O13 | **`assetlinks` + `api/` promotion to prod** | ⚠ **`assetlinks` must be live BEFORE an APK carrying the new wallet identity ships, or it reproduces the exact bug.** Owner's call |
| O14 | **Grom and Elara portraits are on the blurry RawImage path** | The identical import defect Thrain had. **Grom is the default hero.** Flagged, not fixed |
| O15 | **Device geometry verification** | Several 2026-08-05 UI commits are explicitly **not** geometry-verified — the harness had declared `UI_CAPTURE_FIDELITY_DEGRADED` (the editor would not move `Screen.*`), so those PNGs are scale-accurate only and **cannot prove rail or panel geometry** |
| O16 | **The Medium VFX quality tier is unreachable from `DragonBoss`** | `VFXHandle` exposes no accessor for the pooled GameObject, by design. Needs a second catalog prefab or child-disabling inside `VFXManager.Acquire`. **Today Medium and High both get the full three-layer stack** |
| O17 | **`BuildSlideTab` defaults tab and panel to the same edge** | The caller-side fix is complete (one caller exists), but **any future caller inherits the defect**. The kit deserves its own ruling |
| O18 | **`_isTeleporting` does NOT cover the arena return** | `WarpTo` sets and clears it within one synchronous call, so `Update` never observes it true. **The comment claiming "clamp/movement skips this frame" is wrong for that path.** Left as-is and flagged rather than widened speculatively |
| O19 | **The `_deathVFXOverride` branch defaults `playSound` true and double-fires `EnemyDeath`** | One word, out of scope of the change that found it |
| O20 | **`SpellsPackVfxMirror.cs` header + `_Shared/Textures` (16.9 MB of `.tif`)** | Known, not fixed (§3.3) |
| O21 | **`StructureHeightAudit` has not been run against the new cadence** | It prints `measuredY` per prefab and would state the two towers' exact cell claims instead of relying on the shrink argument. **It is the prerequisite for ever touching the walls** |
| O22 | **Push** | **43 commits are local-only** |

---

## 11. COMMITS (this window, `8fdb29a5` → HEAD `1534dffb`)

| commit | lane |
|---|---|
| `7f3971a3` | WO-759 boss fire breath — the Particle Pack's first sanctioned import |
| `34d3fd5d` | tools: `run-unity-method` can force the active build target |
| `3db877d2` | **VFX direction registry + the loop-cap P0 diagnosis (six proving captures)** |
| `cbe0495a` | right-rail collapsed chip style + one shared gutter |
| `bd532d5b` | **P0 FIX — derive `IsLoop` from the art** |
| `fce950ae` | side menu: the second gear was the drawer handle |
| `c95ff5f4` | cancel the retry + stall watchdogs on a deliberate wave stand-down |
| `0011b8ba` | append 16 `VFXType` values (ordinal-safe) + picks registry + handbook |
| `a186c282` | fire the four combat effects that were bought, wired and never played |
| `a67f1e77` | docs: correct the height guidance the 1.2 archer ruling left stale |
| `164ade0a` | potion tap — the button disabled itself at zero |
| `a12c6d22` | build 14 Particle Pack recipes into tracked prefabs + catalog rows |
| `948080f5` | **P0 FIX — 183 pack dependencies to 0; the VFX art mirror** |
| `d0c7b8fd` | Ranger/Mage identity + **the invisible-hero P0** |
| `afa50e44` | victory screen — real stars, right icons, the crush gone |
| `04d375c3` | **WO-910 — audit Ranger and Mage trees honestly, 31 dead nodes** |
| `6737f983` | the Echo unlock is ONE screen with two buttons |
| `d42e2817` | one height cadence across every structure — walls left alone |
| `29f9ac2b` | WO-886 death ladder + three bugs the WO could not have known |
| `4ef2d532` | WO-887 — empowered towers were all detonating in arcane violet |
| `1534dffb` | WO-888 — low health you can actually see |

**Gates across the window:** `COMPILE_GATE_OK` throughout; `REGRESSION_OK` moved **117 → 118 → 119 → 120
suites**; plus `BOSS_FIREBREATH_BUILD_OK`, `VFX_LOOPFLAG_OK`, `PARTICLE_PACK_VFX_BUILD_OK`,
`VFX_ART_MIRROR_OK`. **Four commits carry `COMPILE_GATE_OK` only** (`4ef2d532`, `afa50e44`, `d0c7b8fd`,
`cbe0495a` — the last at 117/118 with one in-flight red that was the registry guard working correctly).

---

## 12. METHOD NOTES THAT COST OR SAVED TIME

1. **An APK build leaves the project's active target on Android.** The next desktop build dies with
   *"Native extension for Android target not found"* plus an SBP/Addressables failure — and because the
   wrapper judges success from **log text rather than a marker**, that reads as a generic failure rather
   than a target mismatch. **It burned an hour.** `-BuildTarget Win64` after any Android build.
2. **Measure the band before naming `ClampMinTouch`.** Three sites tonight, three different mechanisms,
   zero centre-grows (§6.2). The guard's reputation was doing the diagnosing.
3. **A "green marker" is only as good as what the harness can see.** The UI capture harness was
   geometry-blind (`7e05e6d3`), a dungeon settle guard is a **source lint** that proves the bridge exists
   and never that a fight can reach it, and `TalentStrategyRegression` reported green on a hardcoded
   exclusion set. **Ask what the guard is structurally incapable of observing.**
4. **Refuse with a measurement, not with a shrug.** Every refusal in §7 carries the number that
   disqualifies it, specifically so nobody re-attempts the copy. That is the difference between a refusal
   and a deferral.
5. **A retraction in the same session is cheaper than a latent P0.** `948080f5` exists because `7f3971a3`
   committed a claim that was false. Both are in the log; the second one names the first.
6. **The commit message is the primary source.** Several findings in this file — the 40 never-audited
   talent nodes, the 8,795-prefab GUID sweep, the 183 references, the 433,897-line log read — exist
   **nowhere else** in the repo.

---

*Built from the commit corpus 2026-08-06. Frozen ledger — banner, never rewrite (CLAUDE.md §15).*
