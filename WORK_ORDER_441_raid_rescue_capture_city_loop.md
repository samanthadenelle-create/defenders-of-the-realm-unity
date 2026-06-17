# WORK ORDER 441 — Raid → Rescue → Capture → Player Base (the loop that ties it together)

**Status: DESIGN SPEC (phased).** Feature. The "next natural step" (owner 2026-06-17). Ties the raid
pillar + companion pillar + outpost/harvest pillar into ONE motivated loop, taught via Yarn.

## The loop (owner design, 2026-06-17)
```
Raid an enemy outpost (cranked-up difficulty)
  → CLEAR it (kill the garrison)
    → RESCUE the 3rd hero (Grom, Knight) held there → he JOINS the party   [companion pillar]
    → Grom TEACHES "the secret of mining while offline"                     [idle/offline earnings]
    → the camp's SPECIAL NODES unlock as buildable                          [the denotation]
      → BUILD an outpost on a special node
        → it AUTO-HARVESTS passively WHILE the outpost DETERS the enemy      [player base]
        → and KEEPS harvesting WHILE YOU'RE OFFLINE → collect the accrued haul on return  [retention]
          → bank it if not overrun
Regular world nodes stay INSTANT-harvest (tap → amount). Special nodes (at camps) = auto-harvest-via-outpost,
ONLINE and OFFLINE.
```
A Yarn tutorial **teaches build-a-city**, and Grom's rescue beat **teaches offline mining** — narrative
wrappers so both the build loop and the offline-earnings mechanic feel earned, not bolted on.

## Companion recruitment mapped to the pillars (owner 2026-06-17)
Each companion is met through a different activity pillar, so recruiting them teaches that pillar:
| Member | Where met | Teaches |
|---|---|---|
| 1st | Castle hub — walk-up introducer NPC (exists) | town/dialogue |
| 2nd | *(open — TBD)* | — |
| 3rd (Grom, Knight) | **Rescued from an outpost** (this WO) | the raid → capture → build loop |
| 4th | **At the Arena** | the arena combat pillar |
This WO scopes the **3rd (outpost rescue)**; the 4th-at-arena + 2nd are noted for a companion-recruitment WO.

## Why this fills real gaps (from RCA, 2026-06-17)
- **3rd companion has no scripted meet** — join order is Sylas→Elara→Grom; only the 1st is recruited
  (walk-up introducer). The 2nd/3rd just fill the roster. The rescue gives Grom a real entrance.
- **Raid victory/return isn't built** — `RaidGarrisonSpawner.OnCleared` has ZERO subscribers → a cleared
  raid soft-locks (this is why `FeatureFlags.Raid=OFF`). Everything UP TO clear works (entry, spawn,
  combat all reuse proven systems; colliders fine).
- **Capture/player-base never scripted** — but every piece exists: `OnCleared` hooks, the `ClaimableCamp`
  clear→claim→build pattern, `OutpostFoundationGenerator` + harvestable `Outpost`, the memory's
  "claim vein → build+defend → auto-harvest → bank if not overrun" loop. It's assembly.

## Phases (sequenced — each gated + felt-tested)

**Phase A — Raid victory/return (the foundation, unblocks RAID).** Subscribe to
`RaidGarrisonSpawner.OnCleared` → a victory handler: reward + a victory screen + return-to-castle (mirror
the loss/retreat path that already exists). Add the missing `RaidScorer` if needed. Real hero (not the
capsule). Flip `FeatureFlags.Raid` ON once this lands. Files: `RaidGarrisonSpawner.cs:86/350`,
`RaidDeployController.cs`, `FeatureFlags.cs`.

**Phase B — Rescue the 3rd hero (Grom) + he teaches offline mining.** The first outpost holds Grom
(Knight). On clear, the rescue fires → Grom joins (`PartyMemberIds` += Knight), with a Yarn beat
("You found me — thought I'd rot in here… let me show you the trick to working these veins even when
you're not standing on them"). That beat is the **narrative UNLOCK for offline earnings** (Phase C's
offline accrual). Gives the raid its purpose. Reuse the companion-recruit path. Tie to `OnCleared`
(Phase A).

**Phase C — Special-node denotation + capture-to-outpost.** Denote special nodes via a **capability on
the node entry** (One Model §2b) — `NodeKind.Special`/`Outpostable`+`AutoHarvest`, **set by the camp/
outpost generator** when it places nodes at a camp (NO manual denotation — origin = the camp). Regular
world nodes default `InstantHarvest`. On a cleared camp, special nodes become buildable → build an
`Outpost` on one → it auto-harvests (reuse `OutpostFoundationGenerator`/`Outpost`) while it deters the
enemy. Persist ownership (PlayerPrefs `dotr-raid-owner-<id>` mirroring the cleared-state key; +SaveSchema
v24 `OwnedOutposts` with each outpost's node id + harvest rate + **`lastCollectedUtc`**).
**Offline accrual (unlocked by Grom, Phase B):** on load/return, compute accrued = rate × (now −
lastCollectedUtc), **capped** (a max-accrual ceiling so it's not unbounded — e.g. 8-12h), present the
"while you were away" haul, reset the timestamp. Server-authoritative time if available (else client
clock with the cap as the anti-cheat). Files: `NodeDiscoverySystem`, the camp generators,
`OutpostFoundationGenerator`, `Outpost`, `SaveSchema.cs`, + an `OfflineEarnings` calc.

**Phase D — Yarn: teach build-a-city.** A tutorial node (FTUE-style, gated once via PlayerPrefs) that
walks the first capture: clear → rescue → "this node will feed your hold — build here" → build → defend.
Reuse the dialogue/tutorial system; per the no-node canon, open any panel via C# / end nodes with
`<<stop>>` (memory `yarn-no-node-stop-after-panel-command`). Files: a new `Dialogue/...` node + bridge.

## Acceptance (per phase)
- A: cleared raid → victory + reward + return, no soft-lock; RAID flag can flip ON.
- B: clearing the first outpost rescues Grom → party of (hero + however many) gains the Knight.
- C: special nodes (camp) are buildable post-clear, auto-harvest while defended; regular nodes instant;
  ownership persists across save/load.
- D: a first-time player is taught the build-a-city loop in-flow.
- Each phase: compile gate + tests where logic warrants (§2c); owner felt-test.

## What NOT to touch / notes
- Reconcile onto the existing camp/outpost/node/companion systems — additive, don't greenfield.
- Phase A is the dependency for B/C/D. ATB is separate (WO-440). §0: CLI on Windows path.

*Cross-ref:* RCA reports (raid/capture, this session), `docs/RAID_PILLAR_VISION.md`, memory
`city-builder-empty-map-authoring` (the outpost-expansion loop), WO-440 (ATB), `FeatureFlags.cs`.
