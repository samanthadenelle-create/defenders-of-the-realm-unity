# WORK ORDER 193 — Overworld Random Encounters

**Status:** DESIGN → READY once the resolution decision is made (see §Decision)
**Lane:** World / Combat — `OuterWorld` + encounter system + (ATB or skirmish). Code; no village bake.
**Source:** owner 2026-06-01 — "add overworld random encounters on the map."
**Ties:** WO-155 (region spawning + roaming mobs — DONE), WO-164 (ThreatLevel — DONE), WO-169 (ATB party battle — DONE), WO-112 (ward-tether expedition — DONE), `DESIGN_CORE_LOOP_AND_STRUCTURE.md` §5d (expedition).

## Intent
As the player moves through the overworld, **random encounters trigger** — giving exploration stakes and a
steady combat cadence beyond the village wave loop. Frequency + difficulty scale with the **region's
ThreatLevel** (deeper/farther = more frequent, tougher), using the region enemy rosters from WO-155.

## ⚠ KEY DECISION (owner) — how does an encounter RESOLVE?
- **Option A — drop into the ATB party battle (recommended).** Encounter → loads the ATB battle (WO-169) with
  the region's enemy roster, scaled to ThreatLevel. **Clean reuse of the finished ATB engine, and it gives ATB
  its world content + reason to exist** (matches "ATB = separate PvE mode"). Classic JRPG random-battle feel.
- **Option B — real-time skirmish on the map.** Fight where you stand (the WO-155 roaming mobs are already
  real-time). No scene load, but no party/ATB depth.
- **Hybrid:** roaming mobs (WO-155) stay real-time ambushes; *random* encounters trigger the ATB battle.
**→ Recommend A (or the hybrid).** Confirm before build.

## Scope (assuming A)
- **Trigger:** a per-step / per-time roll while moving in a danger zone; chance scales with ThreatLevel
  (safe/home band ≈ 0, deep regions high). Respect the expedition gating — encounters only in the danger maps,
  never the calm home band (DESIGN §5d).
- **Encounter table:** data-driven per region (reuse WO-155 roster + ThreatLevel scaling). Vary group size/composition.
- **Resolution:** load ATB (WO-169) with the rolled enemy group; win → rewards (resources/XP/loot feeding the loop),
  lose → expedition consequence (haul-loss per §5d).
- **Avoidance/flee:** the player can attempt to **avoid** (don't walk into the telegraph) or **flee** the battle
  (ATB already wires Flee) — ties the risk/provisioning loop.
- **Telegraph:** a brief warning (threat skull / screen cue from WO-155) before the encounter fires — not a pure ambush.

## Acceptance
- Moving through a danger region triggers random encounters at a ThreatLevel-scaled rate; **zero encounters in the safe/home band.**
- Each encounter resolves via the chosen path (A: ATB battle with the region roster, scaled); win/lose has real consequence.
- Player can avoid/flee; encounter frequency is tunable (not punishing-spammy).
- No encounters during village defense / inside the walls.

## Open knobs
- Resolution model (A/B/hybrid — decide first).
- Frequency curve + flee odds (tuning).
- Do encounters drop loot/resources directly, or just XP? (economy tie.)
