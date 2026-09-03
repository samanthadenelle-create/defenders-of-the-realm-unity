# WO-1305 RESULT — PART A ONLY (`Spell_Fire_9` as a marquee spell)

**Date:** 2026-09-02 · **Silo:** VFX / spell wiring · **Scope worked:** **PART A ONLY**
**Part A status:** FIXED (seam wired + owner-tagged key declared) — **two owner rulings still open**
**Part B status:** **UNTOUCHED, deliberately** (see §6)

---

## 1. What was wired, and why it is one seam and not a second stack

The defect the marquee ruling names is a **double projectile**: a `_Cast` key is normally a ROLE
COMPONENT (wind-up flash only) and `HeroAbilities.LaunchProjectile` then flies the engine's own body
to the real target. `Spell_Fire_9` is not a role component — it winds up *into its own* flying,
bouncing fireballs. Tagged as a plain `_Cast`, both would fly.

The fix is a **declaration plus one suppression**, riding the existing seams:

| File | Change |
|---|---|
| `Assets/_Modules/Village/Vfx/MarqueeSpellVfx.cs` **(NEW)** | The one registry of SELF-CONTAINED keys. Holds strings and answers `IsMarquee(key)`. **It instantiates nothing, pools nothing, spawns nothing** — `VFXManager.PlayKey` remains the single spawn owner, so the VfxPool-vs-VFXManager scar is not widened. Declares exactly one key: **`firespell_Cast`**. |
| `Assets/_Modules/Village/Vfx/VFXManager.Hovl.cs` | Added `VFXManager.CanPlayKey(string)` — a **read-only** probe of the same three conditions `PlayKeyInternal` checks (catalog loaded / row present / row.Prefab non-null). Resolves, warms and spawns nothing. |
| `Assets/_Modules/Village/Hero/HeroAbilities.cs` | `_currentCastIsMarquee` set beside `_currentCastKeyword` in `CastAbility` (same single writer, same per-cast lifetime); `ResolveCastIsMarquee` + `ConfirmMarqueePlayable`; the suppression branch in `LaunchProjectile`; the cast-beat trace in `FireRegistryCastVfx` now names the resolved spawn transform. |

**Behaviour for every other ability is byte-identical** — `_currentCastIsMarquee` is false unless a
declared marquee key resolves, and nothing else in the project declares one.

**Damage timing adds no new model.** On suppression, `onArrive` fires on the immediate beat — the
same beat the Knight's keyless thrown path already uses (`LaunchProjectile`, knight branch). No
second flight timer, no second spawner.

### The §12 guard that matters most here
Suppression is a decision taken *before* the effect draws. If the marquee key could not resolve, the
ability would have **no visible body at all** — a spell that silently does nothing, indistinguishable
from a broken projectile. So `ConfirmMarqueePlayable` refuses to suppress when `CanPlayKey` is false,
`FlowTrace.Warn`s the reason, and falls back to the normal engine projectile. A throttled miss-log
inside `PlayKey` cannot undo a decision already taken, which is why the probe exists.

### Instrumentation (requirement 5)
- `[Flow:Vfx] marquee VFX '<key>' recognised for cast '<id>' …` — `Once` per key.
- `[Flow:Vfx] owner bundle vfx '<key>' fired … at <worldPos> yaw=<deg> [MARQUEE — prefab owns cast+flight+impact]` — the **resolved spawn transform**.
- `[Flow:HeroAbility] marquee cast (keyword '<kw>'): engine projectile SUPPRESSED toward <target> …` — **every cast, not Once**: a suppression with no line in the log reads exactly like a projectile that failed to spawn.
- `[Flow:HeroAbility] marquee vfx '<key>' … cannot play it … NOT suppressing …` — the fallback warn.
- Spawn/release pairing is the existing `VFXManager` `hovl-play:<key>` / `hovl-at:<key>` pair (spawn + resolved lifetime), which the new line sits alongside on the same key. **No FlowTrace was stripped.**

### Requirement 2 — the loop clamp prerequisite: ALREADY SATISFIED, verified at source
`Assets/Resources/VFX/HovlVfxCatalog.asset:1052` — `Key: firespell_Cast`, **`IsLoop: 0`**. So
`VFXManager.EnforceOneshotEmission` clears `main.loop` on the 4 looping emitters before play. Nothing
to change; do not flip that row.

---

## 2. Provenance — every key wired is OWNER-TAGGED, none is a CLI pick

| Key | Owner tag | Prefab |
|---|---|---|
| `firespell_Cast` | **YES** — `Assets/Editor/VfxManualPicks.json:921-925`, `"manual": true`, `"isLoop": false`; mirrored in `Assets/Editor/VfxCasterLibraryIndex.json:2957` (`catalogued:true`) | `Assets/Spells Pack/Particles/Prefabs/Spells/Spell_Fire_9.prefab` |

That is the **only** key added anywhere by this work. No effect was chosen, substituted or "improved"
by the CLI, and no other catalog row, pick or motion-castings row was edited.

---

## 3. ⚠ LEFT UNWIRED — AWAITING THE OWNER'S TAG (the one thing that still blocks her seeing it)

**`firespell_Cast` has no consumer.** Repo-wide, the string appears in exactly three places, all of
them stores: the picks JSON, the caster index, and the baked catalog. **No ability plays it.**

The prefab→key half is her tag. The **key→ability** half is a second creative pick, and it is not
recorded anywhere, so it was **not made**. The marquee mechanism is dormant until she binds it.

**The exact one-row edit, for when she rules:** in her Motion Caster
(`Assets/Resources/Data/Canonical/motion-castings.json`, target **`knight`**, keyword **`cast`** —
today `vfxKey: "Fireball_Cast"`, `vfxProjectile: "Fireball_Projectile"`, `vfxImpact: "Fireball_Impact"`):

- set `vfxKey` → `"firespell_Cast"`
- clear `vfxProjectile` and `vfxImpact` (the marquee prefab owns both; the suppression already skips
  the travel key, but leaving an authored `vfxImpact` would add a second landing burst — an authored
  impact is an explicit owner pick and is deliberately **not** auto-suppressed)

⛔ **It must be authored on the `knight` target, not `mage`.** `HeroAbilities.RegistryTarget` is
hardcoded `"knight"` (untouched, per the WO), and `motion-castings.json` `"mage"` still has
`inherits: humanoid` with **zero rows** — a mage row would never be read.

---

## 4. Owner concern 1 — town bouncing. FINDING (source-verified). **HER CALL, NOT MADE.**

Read from `Assets/Spells Pack/Particles/Prefabs/Spells/Spell_Fire_9.prefab`, `Fireballs` system,
`CollisionModule`:

| Field | Value | What it means |
|---|---|---|
| `enabled` / `type` | `1` / `1` | **World collision, on** |
| `quality` | `0` | **High** — collides against real colliders, not the cheap planes path |
| `collidesWith.m_Bits` | `4294967295` | **Everything — all 32 layers.** Buildings, walls, props, the hero's own capsule |
| `m_Bounce` scalar | `1` | **Perfectly elastic** — a fireball loses no speed on a hit |
| `m_Dampen` scalar | `0` | no damping |
| `minKillSpeed` / `maxKillSpeed` | `0` / `10000` | **nothing is ever killed by a bounce** |
| `collidesWithDynamic` | `1` | also bounces off moving colliders |
| `radiusScale` | `0.05` | tiny collision radius |
| `maxCollisionShapes` | `256` | |

**Her concern is real and mechanically confirmed:** in the walled town these fireballs will ricochet
off every building and wall with **no energy loss and no kill speed**, so they keep bouncing for the
full emitter lifetime rather than dying on the first surface. In open ground they mostly hit only
terrain. `Sparks` also carries `type: 1` but is `enabled: 0`, so it is not part of this.

**Not decided, and not silently changed** — per requirement 3 this is a felt judgement. The prefab
was **not edited**. The two options to put to her, with the screenshot, are: (a) constrain the
collision (drop `bounce` toward 0 and/or raise `minKillSpeed` so a fireball dies where it lands, or
narrow `collidesWith` off the structure layers), or (b) gate the spell to open-ground encounters.
**A screenshot of it cast inside the walls is still owed** — that needs a Unity/play capture and this
was an edit-only pass with the gate withheld, so it is handed to the lead/PO.

## 5. Owner concern 2 — the Point Light. FINDING (source-verified). **HER CALL, NOT MADE.**

Her instinct was right, and the cost is **larger than one light**:

- The `Point Light` child carries a `Light` with `m_Type: 2` (Point), `m_Intensity: 5`, `m_Range: 5`,
  `m_Shadows.m_Type: 0` (**no shadows** — the one saving grace), and the component itself is
  `m_Enabled: 0`.
- **That disabled component is a PROTOTYPE, not an off switch.** Two ParticleSystems drive it through
  `LightsModule`: `Fireballs` (`enabled: 1`, `ratio: 1`, `maxLights: 20`) and the sub-emitter
  `Explosion ` (`enabled: 1`, `ratio: 1`, `maxLights: 5`). `ratio: 1` = *every* particle gets a light
  instance, and Unity enables the instantiated copies.
- **Worst case ≈ 25 concurrent real-time point lights per cast**, on top of URP's per-object
  additional-light budget — and the fireballs are the ones that bounce around the town.

**Stated, not acted on.** Removing or capping it is her ruling (WO requirement 4). If it does end up
frequently cast rather than genuinely marquee, the cheap dial is `maxLights` / `ratio` on those two
modules, not deleting the child. The prefab was **not edited**.

---

## 6. ⛔ PART B — NOT TOUCHED

Nothing in part B was worked. **No Addressables file was edited, and no address was read, re-pointed,
renamed or "tidied".** `Assets/AddressableAssetsData/AssetGroups/Structure_Art.asset` is untouched, as
is everything under `Assets/StructureContent/Synty/`.

Reason (owner ruling 2026-09-02, canon): **the Synty re-wrap duplicates are DELIBERATE and must never
be bulk-purged.** The archer tower was the one sanctioned revert (commit `9dbba0450`); any further
change happens **one id at a time and only on her explicit word**.

Part A and part B are **not entangled**: part A touches three `.cs` files in the VFX/spell lane and
adds one string to a code-side registry. It changes no Addressables group, no bundle input and no
content hash, so **part A does not trigger the §16 content-build / `tools\r2-ship.ps1` gate.** That
gate remains attached to part B alone.

---

## 7. Files changed + gate evidence

| File | Braces | NUL |
|---|---|---|
| `Assets/_Modules/Village/Vfx/MarqueeSpellVfx.cs` *(new)* | BALANCED | clean |
| `Assets/_Modules/Village/Vfx/VFXManager.Hovl.cs` | BALANCED | clean |
| `Assets/_Modules/Village/Hero/HeroAbilities.cs` | BALANCED | clean |
| `WorkOrders/WORK_ORDER_1305_…md` (Status line) | — | — |

Checked with the CLAUDE.md §1 command; all three written with the Write/Edit tools on the Windows
path (no bash redirect).

**Not run, by instruction (edit-only pass):** no Unity compile gate, no regression, no capture, no
`git add`, no commit. `MarqueeSpellVfx.cs` has **no `.meta`** — Unity generates it on the lead's first
import; it must be staged with the file.

### Not touched
`HeroAbilities.RegistryTarget` (still `"knight"`), `EnforceOneshotEmission`, `CAST_BEAT_MAX_SECONDS`,
the `Spell_Fire_9` prefab, `VfxManualPicks.json`, `HovlVfxCatalog.asset`, `motion-castings.json`, and
every wave/battle-lock, locomotion, tutorial, audio, inventory, talent, enemy-asset and modal file.

### Suggested follow-up (not done)
A regression asserting every `MarqueeSpellVfx.DeclaredKeys` entry exists in `VfxManualPicks.json` with
`manual: true` — it would make "a CLI added a key here" fail the gate. Left out of this pass because
it adds gate surface that could not be run here.
