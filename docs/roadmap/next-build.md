# Next Build — Goals & Notes

Tracked goals for the build cycle **after** the current v2 Unity foundation
(i.e. post-Week-8 / v2.1+). Captured as the owner raises them — this is a
living list, not a commitment of the current 8-week scope.

---

## Art & content

### Unique models + animation per boss
**Goal:** every boss gets its **own unique model with bespoke animation** — not
a reskinned shared-rig character.

**Current state (v2 foundation):** the 8 bosses in `docs/enemy-codex.md` reuse
KayKit "Mystery Monthly" characters on the shared humanoid rig
(`Rig_Medium` / `Rig_Large`), animated by retargeted clips from the KayKit
Character Animations pack. Only **Syndrath the Devourer** (the dragon) is a
distinct, non-shared model. This is efficient for the foundation, but the bosses
do not yet read as distinct, memorable creatures — several share a silhouette
and an animation set.

**Next build:** commission or source a **unique model per boss level**, each
with its own animation set — idle, move, signature attack(s), hit-react, death.
A boss encounter should feel one-of-a-kind. The per-boss roster this applies to,
and the animation-set breakdown, are already in `docs/enemy-codex.md`
(§ Animation Strategy + the gap list).

**Why deferred:** unique boss models are an asset-production effort (commissioned
art, or curated dedicated packs) beyond the v2 foundation's KayKit-only,
shared-rig scope. It is a content/budget line item for the next build.

**Related:** the enemy-codex's flagged quadruped-wolf rig gap (the only
non-humanoid in the current KayKit set) is an early case of exactly this need.

## Combat & progression

### Weapon selection + upgrades
**Goal:** the village battle mechanics tie combat to weapons — the player
selects a weapon and upgrades it; animations are weapon-specific.

**Current build (owner-decided scope):** combat runs on **basic animations**
(the `AnimatorSetup` Attack / Hit / Death states) with no weapon variety —
this is the intended v2-foundation default.

**Next build:** a weapon-selection screen, per-weapon animation sets, and a
weapon-upgrade progression. Asset base: the **KayKit Fantasy Weapons Bits**
pack (imported, currently untapped per `docs/kaykit-asset-catalog.md`).
