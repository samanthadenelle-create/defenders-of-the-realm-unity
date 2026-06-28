# WORK ORDER 566 — Knight Talent Effect Interpreter (V1 behavioural handlers)

**Status:** IMPLEMENTED (headless-verify + felt-verify pending)
**Date:** 2026-06-28
**Lane:** Combat/AI (code only — §9). No scene files touched.
**Branch base:** `wip/village2-and-f8-tickets` @ 9797ff3b

---

## Problem (data-grounded)

The Knight talent tree's effect INTERPRETER was incomplete. `HeroTalentModifiers.cs`
aggregated only ~6 pure-stat/heal/unlockAbility effect types; the SELF/DEFENSIVE/COMBAT
*behavioural* types had **no handler** and were explicitly inert (its own header listed
`reflect / dot / invuln / laststand / … not yet built; nodes takeable but inert`). The
panel sold full power-text on dead nodes → a player spends Wisdom for zero effect.

## RCA — effect schema + apply pipeline (file:line, verified from code)

- **Schema (effect payload):** `HeroTalentEffectDef` —
  `Assets/_Modules/Village/Talents/HeroTalentCatalog.cs:31-49` (fields:
  `type/value/stat/ability/targets/radius/duration/cooldown/chance/threshold/reflect/condition/ally/allyValue/note`).
- **Data:** `Assets/StreamingAssets/Data/Canonical/hero-talents.json` (+ dual
  `Assets/Resources/Data/Canonical/hero-talents.json`). Knight = 20 nodes; 8 shared.
- **Interpreter:** `HeroTalentModifiers.cs` — pure-stat aggregators
  (`DamageMultiplier`, `CooldownMultiplier`, `MaxHpMultiplier`, `IncomingDamageReduction`,
  `BlockChance/RollBlock`, `HealAmountMultiplier`, generic `StatSum`).
- **Defensive consumer:** `HeroHealth.TakeDamage` — `Assets/_Modules/Village/Hero/HeroHealth.cs`
  (block/DR already wired at ~:281-289; MaxHp at ~:104-114).
- **Offensive consumer:** `HeroAbilities.ResolveEffect`
  (`Assets/_Modules/Village/Hero/HeroAbilities.cs:433,456` — damage/cooldown/heal scalars);
  basic-melee path `PlayerAttackController.ResolveAttack`
  (`Assets/_Modules/Village/Enemies/PlayerAttackController.cs:503-566`).
- **Taunt already functions:** `HeroAbilities.ResolveTaunt`
  (`HeroAbilities.cs:651-668`, WO-494) — the Defender's Call / Suppressing Volley
  taunt ABILITIES work; only the talent NODE's numeric overrides are unread.

### Knight + shared effect types that had NO handler (the dead list, pre-WO-566)
`taunt` (param-tuning), `aura`, `proc`, `onEvent`, `reflect`, `invuln`, `laststand`,
`summon` (ranger), `shieldStrength` (absorb), `stun` (Charge Impact), `revive` (shared),
`stealth/dodge/range/critChance/…` (mage/ranger — out of V1 scope).

---

## Node-type → handler map (LIVE vs DEFERRED)

| Effect type | Node(s) | V1 status | Handler (file) | FlowTrace tag |
|---|---|---|---|---|
| `reflect` | knight.t3n4 Retaliation Surge (0.30) | **LIVE** | `HeroHealth.ApplyReflect` | `[Flow:HeroTalents] reflect` |
| `laststand` | knight.t4n3 Last Stand (DR 0.60 + reflect 0.50, <20% HP, 5s/120s) | **LIVE** | `HeroHealth.UpdateEmergencyTalents` + TakeDamage DR + ApplyReflect | `[Flow:HeroTalents] Last Stand TRIGGERED` |
| `invuln` | knight.t4n1 Eternal Aegis (8s / 90s) | **LIVE (auto-emergency)** | `HeroHealth.UpdateEmergencyTalents` → `ActivateInvuln` | `[Flow:HeroTalents] Eternal Aegis TRIGGERED` |
| `proc` (self on-hit DoT) | knight.t2n4 Emberbrand Strike (8 dps / 3s) | **LIVE** | `PlayerAttackController.ResolveAttack` → `Enemy.ApplyDamageOverTime` | `[Flow:HeroTalents] proc-<nodeId>` |
| `revive` (shared self) | shared.n6 Legendary Resolve (40% once/run) | **LIVE** | `HeroHealth.TakeDamage` cheat-death | `[Flow:HeroTalents] Legendary Resolve REVIVE` |
| `taunt` | knight.t1n5 Battle Call, knight.t3n1 Suppressing Bastion | **PARTIAL** — ability taunts via `ResolveTaunt`; node param overrides unread | (existing) | — |
| `aura` (ally) | knight.t2n3/t2n5/t3n5/t4n5 | **DEFERRED** (ally — V2) | — | — |
| `onEvent` (ally) | knight.t3n2 Oathweld, knight.t4n2 Knight Eternal ally-portion | **DEFERRED** (ally — V2); SELF portion of Knight Eternal +45% def already applies via `defense` StatSum | — | — |
| `defense` (self) | knight.t3n3 Legendary Vanguard, knight.t4n2 Knight Eternal self | already LIVE (StatSum "defense"; stationary gate flat in V1) | `IncomingDamageReduction` | — |
| `summon` | ranger.t3n4 Beast Companion | **DEFERRED** (ally/summon — V2) | — | — |
| stun on ability | knight.t2n2 Charge Impact | **DEFERRED** (modifyAbility stun — V-later) | — | — |
| `shieldStrength` | knight.t2n1 Aegis Reinforcement | **DEFERRED** (no absorb system) | — | — |
| mage/ranger effects | all | **STORED-not-wired** (V1 = solo Knight) | — | — |

---

## Implementation

### `HeroTalentModifiers.cs` — data-driven query API (new)
`ReflectFraction`, `ProcSpec` + `ForEachOnHitProc`, `TryGetLastStand`, `TryGetInvuln`,
`TryGetRevive`, `FirstUnlockedEffect`. All read params off the node's `effect` payload
(NOT hardcoded per id) so other heroes' trees reuse them verbatim. Identity (0/false)
until a matching node is learned → combat unchanged at baseline.

### `HeroHealth.cs` — defensive consumers
- `ApplyReflect(damageTaken, attackerCount)` — bounces `ReflectFraction` (+Last Stand
  reflect) of damage actually taken across the contact attackers via `Enemy.TakeDamageFrom`.
  Wired into the contact-damage tick (captures the struck `Enemy[]` in `_attackerBuf`).
- `UpdateEmergencyTalents(heroClass, incoming)` — arms Last Stand (DR + reflect window) and
  Eternal Aegis (auto-invuln) when projected HP drops below threshold, each on its cooldown.
- Last Stand DR folded into TakeDamage's DR step (clamped 0.95).
- Legendary Resolve cheat-death in TakeDamage (restore to 40%, brief grace, once/run).
- `ActivateInvuln(seconds)` (public) reuses the existing `_invulnUntil` grace.
- `ResetTalentRunState()` re-arms revive / clears Last Stand on Respawn + RestoreToFull.

### `PlayerAttackController.cs` — offensive consumer
- On-hit proc loop in `ResolveAttack`: rolls each owned proc's chance and applies a DoT to
  the struck enemy.

### `Enemy.cs`
- `ApplyDamageOverTime(dps, duration, sourceWorldPos)` — public reusable 1s-tick DoT
  coroutine routed through `TakeDamageFrom` (number + flinch per tick).

### Data (canon, §15)
- Updated `note` on the 5 wired nodes in BOTH `hero-talents.json` copies to
  `… — wired WO-566`.

---

## ★ OWNER-DECISION FLAGS

1. **Eternal Aegis activation model (invuln).** Authored as a PLAYER-ACTIVATED "active".
   V1 has no free hotkey / HUD button for a non-slot capstone (keyboard 1-4 removed,
   mobile-first), so it is wired as an **AUTO emergency** (fires below 25% projected HP,
   8s invuln, 90s cd). If you want player-activation, drive `HeroHealth.ActivateInvuln`
   from a HUD button / bound input and drop the auto branch. — *open V1-solo phasing Q.*
2. **Ally/summon effects DEFERRED** (the V1-solo-vs-ally phasing question): all `aura`
   nodes (Honored Warden, Shield Wall, Bulwark Command, Elarion's Champion), ally `onEvent`
   (Oathweld Armor, Knight Eternal ally-portion), and `summon` (ranger Beast Companion).
   These need an ally/companion combat context that does not exist in the solo BattleArena.
3. **Taunt node param-tuning.** The taunt ABILITIES already function (ResolveTaunt). The
   talent nodes' numeric overrides (targets/radius/cooldown) are not read by ability
   resolution — small follow-up if you want the nodes to tune the ability.
4. **shieldStrength / stun-on-ability** deferred pending an absorb system / ability-status
   hook (Aegis Reinforcement, Charge Impact).

---

## Validation
- Brace balance: HeroTalentModifiers 30/30 · HeroHealth 82/82 · PlayerAttackController
  57/57 · Enemy 181/181 — all OK. No NUL bytes.
- JSON: both `hero-talents.json` copies parse; dual copies byte-identical.
- Did not touch the 6 working stat/heal/skill types or the Wisdom spend/respec flow.

## Files modified / new
- M `Assets/_Modules/Village/Talents/HeroTalentModifiers.cs`
- M `Assets/_Modules/Village/Hero/HeroHealth.cs`
- M `Assets/_Modules/Village/Enemies/PlayerAttackController.cs`
- M `Assets/_Modules/Village/Enemies/Enemy.cs`
- M `Assets/StreamingAssets/Data/Canonical/hero-talents.json`
- M `Assets/Resources/Data/Canonical/hero-talents.json`
- A `WorkOrders/WORK_ORDER_566_talent_effect_interpreter.md`
