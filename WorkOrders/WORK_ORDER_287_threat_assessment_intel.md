# WORK ORDER 287 — Threat Assessment / Defensibility Intel ("can my base hold?")

**Status: SPEC — captured, NOT yet ready to implement** (depends on the roaming-enemy +
outpost-defender systems maturing; build once the raid loop is solid).
**Lane:** Combat/AI + UI/HUD (read-only intel layer; touches no scene files).
**Origin:** Owner design idea (2026-06-06) — "intel on whether roaming troops / strong
enemies / large swarms attacking the castle or an outpost can be defended" — i.e. surface
the **probability of defensibility vs risk**, the CoC-style "can my base hold?" read. A
recalled best-part-of-games-I-played mechanic.

---

## Concept
A **passive intel layer** (NOT a building/structure — confirmed with owner) that, per
defended location, weighs the **incoming threat** against the **local defense** and surfaces
a **risk read** so the player can decide to reinforce, send troops, or abandon.

This is the OPEN-WORLD analog of `AlertIntelSystem` (which already does scripted-wave
"raid incoming" banners). It is explicitly **not** about `WaveManager` waves — it is about
roaming warbands / strong enemies / swarms converging on the **castle (Heart)** or an
**Outpost**.

---

## The model
For each defended point L (the Heart/castle + every live `OutpostHub`):

**Threat(L)** — from the live enemy registry:
- `TargetManager.Instance.CollectInRange(L.position, approachRadius, buf)` → converging enemies
- `Threat = Σ (enemy.MaxHp + enemy.Damage * k)` — so a large **swarm** of weak units OR a few
  **strong** units both raise it. (Use whatever threat/HP/damage `Enemy` exposes.)

**Defense(L)** — scan what guards L:
- Towers near L: `TowerCombat` DPS ≈ `CurrentDamage / cooldown`, weighted by whether L is in range
- `OutpostHub.Defenders` (Tank/DPS/Healer) — Σ defender `Damage / AttackCooldown`
- The structure's own HP buffer (`Outpost` / `HeartController` current HP)

**Defensibility = Defense ÷ max(Threat, 1)** → risk band:
| Ratio | Read |
|---|---|
| > 1.5 | 🟢 Secure |
| 0.8–1.5 | 🟡 Contested |
| 0.4–0.8 | 🟠 At risk |
| < 0.4 | 🔴 Will fall — reinforce! |

---

## Files (new — read-only intel; no scene edits, no new structure)
- `Assets/_Modules/Village/World/ThreatAssessment.cs` — self-bootstrapping
  (`RuntimeInitializeOnLoadMethod`, DDOL) scanner service. Mirrors `AlertIntelSystem`'s
  self-attach pattern. Throttled recompute (~1s). Exposes per-location `{ location, threat,
  defense, ratio, RiskBand }` and a `UnityEvent`/`Action` on band change.
- HUD readout (extend an existing HUD or a small chip list): one **risk chip per location**
  ("🏰 Castle: Secure", "⛺ N. Outpost: WILL FALL"). Reuse the code-built uGUI pattern; no UXML.
- Optional: minimap ping + reuse the **attack-warning audio** already wired in
  `StructureAttackAlert` / `GameSfx` (owner linked it 2026-06-06).

## Data sources to hook (all already exist — verified 2026-06-06)
- `TargetManager` (DeNelle.Village.Enemies): `Instance`, `Count`, `CollectInRange(pos, r, list)`,
  `GetClosestTarget(pos, r)` — the live enemy registry.
- `OutpostHub` (`IDamageableStructure`): `Defenders` (`IReadOnlyList<OutpostDefender>`), position.
- `OutpostDefender`: `Damage`, `AttackCooldown`, `IsAlive`, `Role`.
- `Tower` / `TowerCombat`: `CurrentDamage` / `CurrentRange` (DPS contribution).
- `HeartController` / `Outpost`: current HP (the buffer being defended).

## Acceptance criteria
- [ ] No new structure/building; no `.unity` scene edits; self-bootstraps (no scene wiring).
- [ ] Scans on a throttle (≥0.5s), `OverlapSphere`-free (uses `TargetManager`), `sqrMagnitude`.
- [ ] Per-location risk band recomputes from live Threat vs Defense and updates the HUD chip.
- [ ] Band-change raises an event + (optional) plays the existing attack-alert audio once.
- [ ] Silent no-op when `TargetManager`/no defended locations are present.
- [ ] Compile-gate green (`COMPILE_GATE_OK`).

## What NOT to touch
- `WaveManager` / `AlertIntelSystem` (that's the scripted-wave path — leave it alone).
- No new building catalog entry; this is intel, not a structure.
- Don't fork enemy/targeting — read `TargetManager` only.

## Intel fidelity (upgradeable) — "the better the intel, the better you know if your defense is enough"
Intel **quality scales with investment** — make accuracy a progression hook, not a constant.
A higher scout/watchtower tier (or a tech) reveals more of the *same* underlying assessment:
| Tier | What the player sees |
|---|---|
| 0 — none | Vague: "⚠ Danger near the north outpost" (direction only, no numbers) |
| 1 — basic | Risk **band**: 🟠 "At risk" |
| 2 — scouted | **Composition + ETA**: "12 raiders + 1 brute, ~20s out" |
| 3 — full intel | **Defensibility %**: "~30% hold chance — reinforce" + suggested action |

- Reuses the upgrade pattern we just built (e.g. an `ApplyUpgrade`-style fidelity bump, or a
  tech-tree node). Investing in intel literally lets you *know whether your defense is enough*
  before committing — that's the decision the mechanic exists to serve.
- The full Threat/Defense math is computed regardless; **tier only gates how much is shown.**

## Presentation (open — owner: "banner, ribbon, a screen, anything really… probably even through Yarn")
The assessment is computed regardless; how it's SHOWN is a free design choice (mix freely):
- **Risk chip per location** (persistent small HUD readout) — at-a-glance, always-on.
- **Banner / ribbon** — transient "⚠ North outpost: WILL FALL" on threat onset (pairs with the
  lookout horn already wired in StructureAttackAlert/WaveManager).
- **Dedicated intel screen** — a pull-up war-table view (all locations + recommendations).
- **Yarn dialogue (fits the dialogue-as-interaction-layer)** — a **lookout NPC reports** it:
  "My lord — a warband nears the north outpost, twelve strong. The garrison won't hold."
  Especially good for Tier-3 full intel; routes through the same NPC command/Yarn path as the
  shops (NPCCommandBridge). Intel fidelity (above) gates how much the NPC actually tells you.
- **Audio:** reuse the lookout horn (`GameSfx.PlayLookoutHorn`) as the onset cue.

## Notes / open design
- Tuning the threat/defense weights + risk thresholds is an eyes-on pass (do live).
- "Probability" can stay a simple ratio→band v1; a real win-chance sim is out of scope for v1.
- Future: a `PeekNextWave()` on `WaveManager` would let the same chip cover wave threat too,
  unifying open-world + wave intel under one readout (separate, optional follow-up).
