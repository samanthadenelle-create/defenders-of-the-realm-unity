# WORK ORDER 953 — Harvest drip feedback (+N pops via the damage-number spawner) + gated-faucet honesty

**Status:** DONE (implemented + gated 2026-08-10; RESULT filed; the §3 instrumented-run citation is still owed before any rate is retuned)
**Minted:** 2026-08-10 (CLI seat, main line — banner bumped 953 → 954 in the same edit)
**Silo:** Village/Harvest feedback + picker honesty — coordinate with the landed WO-811 lane (uncommitted)
**Origin:** owner 2026-08-10, verbatim: *"Should be some simple + value that pops everytime they bring
some back"* + *"i only ask as I have the pet set to iron and dont see any new iron and wasnt sure if
it was me"* + ruling: *"we can use the same item that spawns the damage points."*
**Design affirmation (owner, same session):** *"that lays weight to building your store houses, i love
it"* — the gate + NEEDS cue is CONFIRMED direction, not a workaround: earning a resource requires its
works to exist, which makes the storehouse/collector build path matter. Composes with WO-837
(lumberyard/foundry/silo capacity caps) and the WO-1012 2c-bis nudge chain.

---

## 1. The felt defect (RCA'd from her live session)

She assigned her pet/Echo to IRON and saw zero income. Proving line (her own log):
`[Flow:Harvest] existence gate CLOSED for 'forge' (liveCollector=no, everBuilt=[<empty>]) - NEVER
BUILT, so it earns nothing (phantom-income gate)` — `ResourceBuildingHarvester.cs:129-143`. The gate
is CORRECT by design (no phantom income), but the experience is silent three ways:
1. The assignment picker allows a resource whose faucet is gated shut, with no cue.
2. Nothing on-screen ever explains why the resource is not arriving.
3. When resources DO arrive (any harvest path), there is no felt moment — no "+N" pop.

## 2. Deliverables

1. **"+N <resource>" pops on every delivery — OWNER RULING: reuse the damage-number spawner** (the
   same pooled floating-text system that spawns damage points; ONE pool, one owner — do not build a
   second floating-text stack). Fire on: Echo silo dump/claim, pet harvest extract
   (`HarvestSite`/`PetHarvester` — a floating "+X Resource" hook already exists there per
   `PetHarvestBootstrap.cs:121` comments; route it through the damage-text pool if it is currently a
   separate mechanism), and resource-building ticks that land in the wallet. Word+shape (resource
   name/icon + number), ASCII, pooled, throttled so a burst cannot spam.
2. **Picker honesty:** in the Echo/pet resource picker, a resource whose existence gate is CLOSED
   shows a words cue (e.g. `NEEDS: Foundry` — resolve the right building display name via
   canon-strings, mind QR-5.7 name inversions) — assignment stays ALLOWED (her choice persists and
   starts paying when the building lands) but the state is honest. Status line mirrors it
   ("Gathering iron - waiting on a Foundry").
3. **RCA gate before tuning (§12):** ONE instrumented run pinning her exact iron path (Echo silo vs
   pet node vs building tick) — cite the line in the RESULT before touching any rate.
4. **Rate pass (owner-tunable data):** promote the hardcoded pet-node demo numbers
   (`PetHarvestBootstrap.cs:172` YieldPerExtract=5, `:189` BaseYield=5) into owner-tunable data
   (dual-copy, versioned) — values unchanged by default; the tuning itself is HER pass, not this WO.

## 3. What NOT to touch

The phantom-income gate itself (correct by design) · WO-811's repair lane files (coordinate — its
honest-status pattern is the model) · the damage-number system's combat behavior · no new UI stacks.
