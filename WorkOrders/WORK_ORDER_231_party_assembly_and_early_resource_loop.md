**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 231 — Party Assembly Redesign + Early Resource Loop Fix

**Status: READY TO IMPLEMENT**
**Author:** UI (creative lane)
**WO Number:** 231
**Date:** 2026-06-02
**Triggered by:** Owner — progression lock: resources in world, can't leave without party/towers, can't upgrade towers without resources.

---

## The Problem (two interlocked locks)

1. **Party lock:** The town arc asks you to survive waves and build up before you can go out — but your solo start is under-powered and the party-of-four only assembled when you were ready to *leave*. There is no reason to stay in town once you can leave, so the party never properly forms.

2. **Resource lock:** Resources (Iron, Wood, Food, Crystals) come from world nodes. Tower upgrades cost resources. You cannot upgrade towers before going out. You cannot safely go out without upgraded towers. The loop is closed against the player.

**Fix philosophy:** break both locks in the town arc — give the player a companion on day one, and give them *starter* resources inside the walls — so they can actually progress through the early waves without being stuck.

---

## Part 1 — Party Assembly Redesign

### Core rule

**You never start alone.** Every player begins with Sylas already in their party — day one, no tutorial gate. The exception is if you picked Sylas as your hero, in which case Thrain fills that slot (he's always at the spire; see below).

### Beat table

| Beat | Trigger | Who joins | Party size |
|---|---|---|---|
| 0 — Game start | Hero select complete | **Sylas** (or Thrain if player is Sylas) | 2 |
| 1 — Wave 3 cleared | Third wave defeated | **Elara** (or Grom if player is Elara) | 3 |
| 2 — First world return | Player exits and re-enters town for the first time | **Grom** (or Thrain if player is Grom) | 4 |

### "Player IS Sylas" — creative ruling

Sylas can't be in two places. **Thrain fills the starting slot.** He's always at the spire — it's his chord to hold — so no explanation needed; he's just there. When you arrive as Sylas and report what you saw on the Outer Paths, Thrain enlists you immediately. Same 2-person start, caster-plus-ranger flavour instead of double-ranger.

### Summary table by hero choice

| You play as | Start partner | Joins after wave 3 | Joins on world return |
|---|---|---|---|
| Thrain (Wizard) | Sylas | Elara | Grom |
| Grom (Knight) | Sylas | Elara | Thrain |
| Sylas (Ranger) | Thrain | Elara | Grom |
| Elara (Healer) | Sylas | Grom | Thrain |

---

## Part 2 — Early Resource Loop Fix

### The lock

Town nodes (Iron, Wood, Food) are world-side. You need them to upgrade towers. You can't reach them until you have a party. The loop is closed.

### Fix: two parallel seams, both small

**Seam A — In-town starter nodes (environmental, always present)**

Place 2–3 *low-yield* resource nodes inside the village walls from the start:

| Node | Location | Yield | Replenish |
|---|---|---|---|
| Woodpile (Wood) | Near the workshop | Small (enough for 1 tower upgrade) | Slow — every 2 waves |
| Stone cache (Iron proxy) | Near the wall base | Small | Every 3 waves |
| Grain sack (Food) | Near the inn/healer | Small | Every 2 waves |

These are not infinite. They are deliberately thin — just enough to afford one upgrade and one tower build across the first 3 waves. They communicate that resources exist and how to spend them before the world opens up.

Pet auto-harvest (WO-229, now shipped) already handles the harvesting loop — pets will find and bank these nodes automatically once placed.

**Seam B — Wave kill drops (immediate, exciting)**

Hollow Ones drop small resource shards on death — not a lot, but:
- Enough to feel the reward of a cleared wave immediately
- Enough to nudge the player toward their first tower upgrade
- Varies by wave enemy type (Frost-Voice drops Iron shards, Ember-Voice drops Wood embers, Half-Voice drops Crystal dust)

This also makes waves feel more meaningful beyond pure survival — you are *taking something back from the Choir*, which fits the tone perfectly.

### Together these mean

After wave 1: player has a handful of resources from drops + can harvest the in-town woodpile.
After wave 2: one tower upgrade is within reach.
After wave 3 (Elara joins): player is stable enough to consider leaving town for the first time.
First world trip: full world nodes come online via pet harvest. Resource economy opens properly.

---

## Files to touch

| File | Change |
|---|---|
| `VillageSceneBuilder.cs` | Add 2–3 starter `MineNode` placements inside the wall perimeter (bake required) |
| `Enemy.cs` / `EnemyBrain.cs` | Add `OnDeath` resource-drop seam (small yield, type by enemy variant) |
| `PARTY_OF_FOUR_STORYLINE.md` | Update beat table to match this WO |
| New: `StoryCompanionController.cs` | Companion join logic — wave count listener → trigger companion join at wave 3, first-world-exit listener → trigger Beat 3 join |

---

## What NOT to touch

- `Village.unity` — do not hand-edit. Any node placement goes through VillageSceneBuilder + rebake.
- `WaveManager.cs` — wave logic is stable. Add a thin event (`OnWaveCleared(int waveNumber)`) only if one does not already exist.
- Store/monetization stack — unrelated.

---

## Acceptance criteria

- [ ] Nessa is present as AI companion from game start (or Thrain if player is Sylas)
- [ ] Elara joins automatically after wave 3 cleared (or Grom if player is Elara)
- [ ] Grom joins on first return from the world (or Thrain if player is Grom)
- [ ] 2–3 in-town resource nodes are placed and harvestable by pets
- [ ] Hollow Ones drop small resource shards on death (type varies by enemy class)
- [ ] Player can afford at least one tower upgrade before leaving town for the first time
- [ ] No hand-edits to Village.unity — all scene changes via VillageSceneBuilder + rebake

---

## Open questions for owner (creative calls)

1. **Is Bram's Beat 3 join fixed to "first return from the world" or can it be earlier?** Could make it "wave 5 cleared" if the world trip feels too gated.
2. **Do wave drops feel too gamey for the tone?** Alternative: Hollow Ones leave behind corrupted materials that the Folk salvage (more in-fiction). Same mechanic, different framing.
3. **Starter node count — 2 or 3?** More nodes = faster ramp but risks making the world feel less necessary. Suggest 2 to start; add the third if playtest says it's too slow.
4. **Does the Keeper have a companion of her own when she's an NPC?** (The Twilight Sprite is bonded to her canonically.) If the Sprite follows the Keeper-NPC around, it adds a lot of visual richness at zero code cost.

---

*Creative/design doc (UI lane). No code written here. CLI implements against this spec.*
*Reconciles with: PARTY_OF_FOUR_STORYLINE.md, STORYLINE.md, narrative-bible.md, WO-229 (pet harvest), WO-227 (companion system).*

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
