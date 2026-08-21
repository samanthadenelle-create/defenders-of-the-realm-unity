# WORK ORDER 1026 — The base is never attacked: close the CoC consequence loop

**Status:** READY TO IMPLEMENT — ★ §3 RULED 2026-08-17: **(a) PvE siege**, built so (c) ghost-PvP drops in later

> Owner ruling 2026-08-17 (*"open ones follow your recommendations"*): **model (a)** — scripted/generated
> attackers assault the base on a cadence, reusing `WaveManager`, no backend.
>
> ⚠ THE STRUCTURAL CONDITION IS THE RULING, not a nice-to-have. (a) was chosen **specifically because it
> can become (c)**, so the attack REPORT / REPLAY ARTIFACT must be designed as DATA from the first line —
> a serialisable record of "who attacked, with what, where they broke through, what was lost". Build it
> that way and ghost-PvP later is a SOURCE SWAP (generated attacker -> snapshotted real layout). Build it
> as immediate UI state instead and (c) is a rebuild, which is exactly the cost this ruling exists to avoid.
> Do not hardcode "the attacker is generated" anywhere the report can see.
>
> ### ⛔ STILL OPEN — the stakes. I made no recommendation here and am not inventing one.
> §3's second question — **what does a loss actually cost the player?** — remains unruled. The CoC answer
> is stockpiled resources, but that collides with the storage-cap progression (memory
> `stockpiles-cap-capacity`) and the WO-947 basket ruling, and this WO explicitly forbids inventing an
> economy rule. Implementation may proceed on everything EXCEPT the loss consequence; that needs the owner.
> A safe interim: attacks resolve and REPORT, but take nothing, until stakes are ruled.
**Minted:** 2026-08-15 (UI seat) — provenance stack bumped 1026 → 1027 in the same edit
**Lane:** Raid / village defense. Design-led.
**Provenance:** owner ask 2026-08-15 — *"a full review from the lens of what makes COC fun and warcraft 3
fun, and determine where we need to strengthen the game"*. Full analysis:
`docs/DESIGN_REVIEW_COC_WC3_LENS_2026-08-15.md` §3 ⓵ (ranked **highest leverage**).

---

## 1. The gap, measured

Grep across `Assets/_Modules`, 2026-08-15:

| symbol class | hits |
|---|---|
| `RaidDefen*` / `IncomingRaid` / `WasRaided` / `RaidReport` / `OfflineRaid` | **0** |
| `DefenseReport` | **0** |
| `Revenge` | **0** |
| `Trophy` | **0** |

**Strategic placement is ALWAYS ON** (canon §7/§8 — movable functional storefronts, player-built town).
The player authors a layout. **Nothing ever shows that layout being tested.**

In Clash of Clans the loop is *design → watch it fail → redesign*. The watching is not a feature bolted
on the side; it **is** the game. Without it, every wall, every tower position, every storefront
placement is a decision the player makes blind and receives no feedback on. All the placement machinery
we have built is, from the player's seat, decorative.

## 2. Why this is cheap — the halves already exist

This WO connects shipped systems; it does not add a pillar.

- **Waves already attack the town.** `WaveManager` runs live assaults against the player's real layout.
- **The raid spine already resolves attacker-vs-base.** Raid V1 is built end-to-end (memory
  `raid-v1-spine-already-built`) — Teleport/Deploy, troops, structure damage.
- **Structures already implement the damage interfaces.** WO-853 dual-implemented
  `IDamageable` + `IDamageableStructure` on `WallSegment` / `Gate` / `DefenseTower` / `RaidSpire`, and
  widened the troop mask on both `TroopController` entry points (anchor 2026-08-09 §9).

What is missing is the **mirror and the record**: the player seeing their own base attacked, and a
consequence they can act on.

## 3. ⛔ OWNER RULING REQUIRED FIRST — do not implement before this is answered

**Where do attacks on the player's base come from?** The three answers produce very different games:

| model | what it needs | risk |
|---|---|---|
| **(a) PvE siege** — scripted/generated attackers assault the base on a cadence; player watches or defends live | Nothing new server-side. Reuses `WaveManager`. | Lowest risk, lowest social pull |
| **(b) Asynchronous PvP** — other players' towns are raided and yours is raided back | Real backend: base snapshots, matchmaking, loot rules, shields | Highest pull, highest cost. `api/` is **PREVIEW-only** today (anchor) |
| **(c) Ghost PvP** — real player layouts are snapshotted and replayed by AI, no live opponent | Snapshot storage + a replay of the sim | CoC's actual model. Middle cost |

**Recommendation: (a) first, structured so (c) drops in later.** It closes the feedback loop
immediately with zero backend, and if the *report/replay artifact* is designed as data from day one, (c)
becomes a source swap rather than a rebuild.

⚠ **Do NOT begin implementation until the owner rules.** The data model differs per branch, and picking
wrong means rebuilding it.

## 4. Scope once ruled (assuming (a))

**The deliverable is the FEEDBACK, not the combat.** The combat exists.

1. **A defense outcome record** — after any assault on the player's town: what attacked, where it
   entered, what it destroyed, what held, what was lost. Persisted as **data**, not just a toast, so
   (c) can later populate the same record from a snapshot.
2. **A surfaced report the player reads** — reachable from the town, showing that record legibly. This
   is the *"watch your base fail"* moment. Without it the loop stays open.
3. **A reason to redesign** — the report must make the failure point obvious (where the breach was), so
   the player forms an intent: *move that tower*.
4. **Stakes, sized by the owner** — what is actually lost. ⚠ Losing stockpiled resources is the CoC
   answer but it interacts with `stockpiles-cap-capacity` (memory) and the WO-947 cost-basket ruling.
   **Do not invent an economy rule here** — bring a proposal to the owner.

## 5. Explicitly OUT of scope

- Live PvP, matchmaking, clan wars
- Shields / revenge / trophies — these are *balancing* mechanics for model (b)/(c); they mean nothing
  under (a) and should not be built speculatively
- Any change to `WaveManager` composition or the smart-roster rules
- Any change to the raid attack flow (that is WO-774's lane)

## 6. Acceptance criteria (for model (a))

- [ ] An assault on the player's town produces a **persisted outcome record** — survives a session
- [ ] The player can **read that record in-game** and identify where their base failed
- [ ] The record is **data-shaped**, with the source (PvE vs snapshot) as a field, so model (c) is a
      source swap and not a rewrite
- [ ] A player who moves a structure and is attacked again sees a **different** outcome — the loop
      closes and the redesign has visible effect
- [ ] Zero changes to raid-attack behaviour (lane isolation from WO-774)
- [ ] `FlowTrace` instrumentation on record creation + surfacing, per §12 — this is a new subsystem and
      the trace is what makes its first bug cheap

## 7. Verify

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites`
2. Headless: assault a saved town, assert a record is written and reloads
3. **Screenshot the report screen** — memory `screenshots-are-primary-evidence-for-visual-defects`
4. Owner felt-verifies. ⚠ This one is *especially* a felt judgement: the question is not "does it
   work" but **"does losing feel like it was my fault, and do I know what to change?"** If the player
   cannot answer that, the loop is still open regardless of green gates.

> **AUDIT 2026-08-21 (agent fleet, read-only):** OPEN — STILL VALID. Evidence: `no RaidDefen*/IncomingRaid symbols` — nothing built; stakes unruled. Status left at READY deliberately: this work is real and unbuilt. Verified against HEAD 2f0b97bb5, not against the ticket's own claims.
