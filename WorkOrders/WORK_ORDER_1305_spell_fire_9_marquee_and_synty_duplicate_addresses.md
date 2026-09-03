# WORK ORDER 1305 — Spell_Fire_9 as a marquee spell + the 27 duplicate Synty addresses

**Status:** FIXED (part A — marquee seam wired 2026-09-02; two owner rulings still open, see the RESULT)
/ **NOT STARTED — DELIBERATELY UNTOUCHED (part B)**

> ⛔ **PART B WAS NOT WORKED AND MUST NOT BE BULK-WORKED.** Owner ruling 2026-09-02: the Synty
> re-wrap duplicate addresses are **DELIBERATE**. The archer tower was the ONE sanctioned revert
> (commit `9dbba0450`); any further change happens **one id at a time and only on her explicit
> word**. No address in `Structure_Art.asset` was read, edited, re-pointed or "tidied" by the
> part-A pass — and because nothing under Addressables changed, part A does **not** trigger the
> §16 content-build / `r2-ship.ps1` gate below. That gate belongs to part B alone.
**Silo:** VFX / Content addressing
**Minted:** 2026-09-02 (CLI) from an owner browsing session in the VFX Caster.

---

# PART A — `Spell_Fire_9` becomes a marquee spell

## Owner ruling 2026-09-02 (verbatim context)

She browsed the VFX Caster and reported of `Spell_Fire_9`: *"the way it displays is there is a wind up
directly into projectiles flying and bouncing"*. Shown that this makes it a self-contained sequence
rather than a role component, she chose **"Make it a marquee spell"** — the prefab owns the whole
show and the engine's projectile driver is suppressed for that ability.

She ALSO flagged BOTH watch-outs as real concerns, so these are requirements, not notes:
1. **"Bouncing in town may look wrong"** — the collision is world-space.
2. **"The Point Light is a mobile cost"**.

## What the asset actually is (verified at source, do not re-derive)

`Assets/Spells Pack/Particles/Prefabs/Spells/Spell_Fire_9.prefab`
- Children: `Fire`, `Fireballs`, `Force Field`, `Glow`, `Lava`, `Point Light`, `Sparks`
- **5 VelocityModules enabled** — the fireballs fly under their own authored velocity
- **1 CollisionModule enabled, `type: 1` (world)** — the bouncing she watched
- 1 SubEmitter enabled, 1 Trail enabled, 2 Noise enabled
- 7 emitters carry a looping flag; **4 of the 7 loop**; `lengthInSec` 4.5

⛔ **THIS IS WHY IT CANNOT BE TAGGED Cast / Projectile / Impact.** Tagged as Cast, the ability system
would ALSO fire its own orb (`FireSpellOrb`), producing two projectiles: the prefab's fireballs
bouncing along their authored vector, and the engine's orb travelling to the real target.

## Requirements

1. Route this through a **self-contained / marquee** presentation path where the prefab owns cast,
   flight and impact, and the engine's projectile spawn is SUPPRESSED for that ability. The project
   already sanctions ordered multi-part prefabs for marquee moments — **reuse that seam, do not add a
   second spawner or a second pool** (ARCHITECTURE_PRINCIPLES 2b/2b.1; `Assets/_Modules/Village/Vfx/`
   is already scar tissue from exactly that mistake).
2. **The loop clamp is a hard prerequisite.** `VFXManager.EnforceOneshotEmission` (added 2026-09-02)
   makes the catalog `IsLoop` flag authoritative and clears `main.loop` before play. The catalog row
   for this prefab MUST declare `IsLoop: 0`. Without it, 4 emitters burn forever — the exact defect
   the owner reported as *"casts at me and stays at me"*.
3. **Owner concern 1 — town bouncing.** Determine what the world-collision does inside the walled
   town versus an open field, WITH A SCREENSHOT. If fireballs ricochet off buildings in a way that
   reads as a bug, either constrain the collision or gate this spell to open-ground encounters. This
   is a felt judgement: capture it and put it to her, do not decide it silently.
4. **Owner concern 2 — the Point Light.** Measure it. If this ends up frequently cast rather than
   marquee, the real-time light should go. State the finding; let her rule on removal.
5. Instrument per CLAUDE.md sec.12: log the resolved spawn transform, whether the engine projectile
   was suppressed, and spawn/release pairing for the sequence.

## What NOT to touch

- ⛔ Do NOT re-point `HeroAbilities.RegistryTarget` (still hardcoded `"knight"`). `motion-castings.json`
  has `"mage": { inherits: humanoid }` with ZERO rows, so repointing takes the mage from wrong-VFX to
  NO VFX. The mage needs authored rows first — owner's tagging pass.
- ⛔ Do NOT pick a replacement effect. Creative picks are hers; she chose this one.
- ⛔ Do NOT weaken `EnforceOneshotEmission` or the `CAST_BEAT_MAX_SECONDS` ceiling.

## Note for whoever takes this

The loop clamp did not just repair the fire spell — it **unlocked a 76-prefab library**. Every prefab
under `Spells Pack/Particles/Prefabs/Spells/` has at least one looping emitter; **zero are fully
oneshot**. Before the clamp, none of them were safely usable.

---

# PART B — the 27 duplicate Addressables addresses

## The defect

`Assets/AddressableAssetsData/AssetGroups/Structure_Art.asset` holds **71 entries but only 44 distinct
addresses — 27 addresses are claimed by two assets each.** Cause: the re-wrap prefabs under
`Assets/StructureContent/Synty/` reuse the ORIGINAL asset filenames verbatim, so e.g.
`Structures/farm` is claimed by both `farm.fbx` and `Synty/farm.prefab`.

**This is not theoretical.** It is the mechanism by which `Structures/Tower_Wooden_Watchtower`
resolved to `SM_Bld_Castle_Wall_Tower_S_01` — a stone castle tower wearing the owner's Tripo
filename — which she reported as *"one thing i hate is the changes to the archer towers"*. With a
duplicated address, Addressables resolves to whichever location the built catalog lists first, and no
catalog JSON edit can steer it.

## The fix, and the boundary on it

Give each Synty re-wrap a DISTINCT address. The correct pattern is already in the tree from the tower
repair: `Structures/Synty_Tower_Castle_Wall_S` / `_M` / `_L`. Apply that shape to the other 27.

⛔ **OWNER RULING 2026-09-02, verbatim: "the other synty were on purpose."** She was offered a full
purge and an audit and took NEITHER; scope was the archer tower only. So:
- **RE-ADDRESS, never delete.** Both assets stay; both intents are preserved.
- This does NOT revert any art swap. If it would change which art a structure displays, STOP and
  report — that is a separate owner decision.
- Do not "fix" the art choices. Only the ambiguity.

## Ship gate

⛔ Any Addressables change re-hashes bundles. Content build + `tools\r2-ship.ps1` is MANDATORY, and a
prior push can never cover it (CLAUDE.md sec.16 — this has burned the project four times). Judge by
`R2_PUSH_OK` + `R2_PARITY_OK` on a FRESH log, never the exit code.

## Reference

`docs/reference/SYNTY_PACK_REGISTRY.md` — the durable audit: category counts, usage method, ranked
opportunities, and three open questions that need the Unity lock to settle.
