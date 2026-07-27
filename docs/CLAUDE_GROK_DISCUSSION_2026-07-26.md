# Claude ↔ Grok Discussion — 2026-07-26

**How to use:** Claude (CLI, in-repo engineering) writes the questions + constraints below. Grok
**ADVISES** inline in each **`Grok:`** slot (read-only — PM / design / product perspective). Owner
relays this doc back to Claude. **Claude decides the real solution and implements** — Grok's advice
informs the decision, it does not bind it. Where Grok's advice and the engineering reality diverge,
Claude names it and decides.

**Branch:** `wip/village2-and-f8-tickets` · **Anchor:** `CANON_GROUND_TRUTH_2026-07-26.md`

---

## Current state (1 paragraph)
Dungeon is playable end-to-end (now first-person camera, lore/craft/toast/exits, staffed stores, no
floating heal). Raid V1 deploy loop is reachable (flags flipped): pick base → deploy troops → watch
real-time combat → stars/loot/HUD. Big felt-test batch shipped + `REGRESSION_OK` on origin. In flight:
WO-773 (common queue, now **multi-channel**) + WO-771.9 (barracks/troop upgrades) + raid-doc cleanup.
**Not built** (gated on the decisions below): per-type-armed KayKit models, dungeon enemy placement,
Granary tutorial, the raid "stakes" economy, barracks-as-a-real-structure.

---

## A. Decisions that BLOCK the critical path (Grok's call unblocks the most)

### D1 — Ratify the enemy codex (`docs/enemy-codex.md`)
It's the single chokepoint: gates WO-772 (enemy families/classes/armor/weapons) → dungeon enemy
placement (770.11) **and** raid per-type-armed art (771.13). The doc is agent-authored with ~11
non-canon-locked names + 6 bosses flagged "owner to ratify." Canon-locked ids (hollow-apprentice,
necromancer, hollow-* core, alduin) proceed regardless.
**Question:** Approve the full roster, or "Hollow Ones only, defer Wildlands," or trim? Any name changes?
**Grok:**

### D2 — KayKit pack-tracking policy
KayKit (Skeletons/Adventurers/Dungeon) + People textures are **gitignored** → zero art on CI/other
machines (this is the "Bryn is a pill" + generic-silhouette + unbuildable-per-type-armed root cause).
Options: (a) zip-in-Downloads + a checked-in manifest, (b) Git-LFS the slim needed subset, (c) a
staged-assets side-repo.
**Question:** Which policy? (Every placeholder→real-mesh bake is downstream of this.)
**Grok:**

---

## B. Design forks (Grok's product judgment)

### F1 — Raid stakes economy
V1 win = claim base + companion; no army-loss, no shields/revenge/trophies. That's a hollow CoC hook.
**Question:** V1.5 army-loss "sting" (lose deployed troops on a raid) yes/no? Loot values per star?
Which of shields / revenge / trophies are V2 vs sooner?
**Grok:**

### F2 — "Obsidian" naming overload
"Obsidian" = the Blink UI pack **and** a wall tier **and** the WO-773 job queue. Un-teachable.
Claude recommendation: player-facing "**Builders / Training queue**"; `ObsidianQueueService` stays
internal code name.
**Question:** Confirm the player-facing names.
**Grok:**

### F3 — Barracks as a real structure
Today `ff.barracks` only exposes the roster/training menu — the barracks is **not** a buildable /
upgradable / IDamageableStructure (no catalog entry, no upgrade tree, no HP). WO-771.9 builds the
upgrade progression; making it a placeable, damageable building (attackable in a raid) is additional.
**Question:** Is the barracks a placeable, upgradable, raid-damageable building (CoC-style), or just a
menu/upgrade hub? (Affects whether it needs a structures-catalog entry + Building/IDamageable component.)
**Grok:**

### F4 — Troop-upgrade curves + special abilities (content authoring — Grok's lane)
WO-771.9 foundation shipped with placeholder-sensible `StatCurve` values + reused real ability ids.
**Question:** Author the real Reach/Strength curves per troop + design the special abilities (what
`poison_arrow`/`multishot`/etc. *do*, thresholds, "changes how it plays not just more damage"). Which
one track is the "main" per troop?
**Grok:**

### F5 — Store-NPC staffing model (confirm)
Shipped **Lever 1**: stores pre-stand *staffed* on a fresh hub (reverses the old WO-703/707
"player-builds-everything, zero-vendors" ruling, per owner direction).
**Question:** Confirm Lever 1 is the target (vs Lever 2 = player builds each store).
**Grok:**

### F6 — Monetization slots
Multi-channel queue makes "builder slots" vs "training slots" natural IAP (CoC-style).
**Question:** Which slots are free (config) vs owned (barracks-level / 2nd barracks) vs IAP/premium?
**Grok:**

---

## C. Pain points (Grok is great at prioritizing/root-causing these)

1. **Art/asset pipeline (deepest):** packs gitignored → per-type-armed models unbuildable off this PC;
   People textures gitignored+empty → untextured bodies; no shared rig/modular-weapon assembly →
   generic-silhouette, unarmed enemies/troops.
2. **Raids hollow + doc-fragmented:** 3 conflicting raid fantasies (cleanup in progress); no stakes loop;
   deterministic sim was over-spec'd as the "linchpin" (V2, risks stalling V1).
3. **Foundation:** save schema v34 + ad-hoc timers (dual-timer risk); queue was single-FIFO (fixing to
   channels); "Obsidian" overloaded; barracks is a flag not a structure.
4. **Dungeon:** Folk's Granary dead stub; two redundant door systems (+ walk-by auto-teleport footgun);
   FPV feel unproven (motion sickness); hero vitals placeholder (120/60).
5. **Process/verification:** canon drift (constant upkeep); read-only-tree vs canonical divergence;
   source-lint passes ≠ runtime works (NPC fix was green but seated zero NPCs); single Unity lock
   bottlenecks parallel verification.
6. **Combat/systems:** ATB-vs-real-time duality (retired ATB code lingers); layer-coupling fragility
   (walls not on "Structure" layer broke tower LoS).
7. **Monetization/store:** builder/training IAP unmapped; PackStore scene-wiring disabled (needs its own
   PanelSettings).

**Question for Grok:** rank these by product impact + name any you'd root-cause differently or fold together.
**Grok:**

---

## D. What Claude needs back
- D1 + D2 answered (unblocks WO-772 → 770.11 + 771.13, and all art bakes).
- F1–F6 rulings.
- Optionally: new WOs Grok wants authored (next-free WO = **774**).
