# Manage Redesign — the CLI seat's driving plan

**Owner instruction 2026-09-06:** *"import them at the right size and wire the heart then build a plan to implement
this and drive it."*

This is the execution plan for `WorkOrders/ManageRedesign/`. It does not replace the delivered
`IMPLEMENTATION_SEQUENCE.md` — it refines it with what this seat measured on 2026-09-06 and adds the four rulings the
owner gave after delivery. Where the two disagree, the reason is stated.

---

## 0. What changed since the program was written

Four rulings arrived after delivery and are recorded in `OWNER_RULINGS_LOCKED.md` §21-23:

| # | Ruling | Where it lands |
|---|---|---|
| 21 | **The two barracks levels MERGE** — the building tier gates troop unlocks | WO-2011, WO-2008, WO-2009 |
| 22 | **The Cathedral ladder is priced in STONE**; correct the data to match the charge | WO-2005, WO-2007 |
| 23 | **One of each storage type**; capacity grows by LEVEL, never count | WO-2005 |
| — | Art delivered: the Manage UI asset sheet + individual transparent assets incl. **the Heart** | WO-2015, WO-2017 |

And four defects were fixed the same day whose seams this program now builds on: the village-tier scale conflation
(WO-1423), the caravan's death path (WO-1424), cap-aware refusals (WO-1425) and the wave-clear panel (WO-1426).

## 1. The through-line, so nobody loses it

Every defect this program answers is the same species: **two things that should be one, or a thing built with no door.**
Village tier vs building tier. Barracks level vs barracks tier. Data authoring vs the charged lane. A panel with no
caller. A heal field owned by a movement component. Drops granted but never shown.

The systems are not broken. **The seams were assumed rather than written down.** That is why 394 suites passed while
seven of nine troops were unreachable — every oracle asks "does this system do its job", none asks "do these two agree"
or "can a player get here at all".

So this program has a second deliverable beside the UI: **a family of SEAM oracles.** They are cheap and they are the
only thing that stops the next iteration re-discovering the same class.

1. `ProgressionReachabilityRegression` — **shipped 2026-09-06.** No authored gate may demand a level above its ladder's
   max. Proven RED by restoring the old scale.
2. **Every authored effect string is honoured by code.** The barracks tier rows advertise troop unlocks that nothing
   reads; that is how ruling 21's defect hid in plain sight.
3. **Every panel has a door.** A `MonoBehaviour` panel with no caller, no scene reference and no prefab reference is a
   dead system. `BarracksPanel` was exactly this, proven five ways.
4. **Every singleton's death routes through `Destructible`.** The caravan's did not, so its record survived, its cell
   stayed occupied and — because singleton presence reads the RECORD — it could never be rebuilt.
5. **Every gate is satisfiable from a fresh save.** The generalisation of (1): walk the prerequisite graph from a new
   game and assert every rung is reachable.

**Author (2) and (3) in Wave 0.** They are the two that would have caught the barracks defect, and they are pure source
analysis — no UI, no Unity runtime.

## 2. Sequence — refined

### WAVE 0 — model, data and seams. No UI.
Nothing here is visual, so it can all run in parallel on disjoint files, and it de-risks everything after.

| Order | Item | Why first |
|---|---|---|
| 0.1 | **WO-2011** unified action-state model | Everything binds to it. Ownership / upgrade-track / action-state must be separable or the UI lies again. |
| 0.2 | **Ruling 21** — barracks merge | Model work, and it is the single highest-value fix on the board: it turns 7 unreachable troops into reachable ones. Do it INSIDE 2011 so the state model is authored against the merged truth, not retrofitted. |
| 0.3 | **WO-2005** BUILD inventory reconciliation + filters | ⚠ Must read the **CHARGED** lane, not the authored key (ruling 22) — `BuildingUpgradeService.TierCost` picks by tier index, so every tier-2 row is charged Stone whatever its JSON says. A reconciliation that reads the authored key is wrong above tier 1. |
| 0.4 | **Ruling 23** — storage singleton | Data-only, 3 keys, two canonical copies. Byte-mode edit, prove the LF count. |
| 0.5 | **WO-2003 / WO-2004** Heart spine + unlock data | ⚠ Must move together with `ProgressionReachabilityRegression`: it fails the build if a gate exceeds `VillageTierService.MaxTier`, so raising the Heart's ceiling moves that pin WITH the ruling. |
| 0.6 | **Seam oracles (2) and (3)** | Cheap, source-only, and they close the class that produced this program. |

**Wave 0 exit:** compile green, regression green, and a fresh-save walk proving every troop and every building rung is
reachable. **Do not start UI until that walk passes** — the redesign must not be built against guessed states.

### WAVE 1 — the common shell
WO-2001 (information architecture) → WO-2002 (the dumb-UI contract) → WO-2012 (global Queue).
The Queue is P0 because the shared context strip depends on it.
⚠ WO-2002's "views may not" list is the load-bearing part. Enforce it with a source oracle, not with review.

### WAVE 2 — the three tabs
WO-2006 BUILD grid → WO-2007 build detail → WO-2008 ARMY 3x3 → WO-2009 troop detail → WO-2010 research schools.
Each tab is file-disjoint from the others ONLY if they share the common renderer from Wave 1. If they do not, they
serialise on one file and must run as one lane — that is what happened on WO-1422 and it is why Wave 1 comes first.

### WAVE 3 — progression navigation
WO-2017 Heart Manage surface → WO-2013 direct prerequisite navigation.
⚠ WO-2013 is P0, not polish. **A lock without a route is the defect this whole program exists to kill.** Every locked
tile's CTA must open something that genuinely opens — the barracks case proves a CTA can point at a phantom.

### WAVE 4 — copy and art cohesion
WO-2014 copy density → WO-2015 art cohesion, binding the delivered asset pack.
⚠ Any text band under ~24 px renders **BLANK**, not small (TMP culls a line whose `fontSizeMin` cannot seat). That cost
three separate defects on 2026-09-06. Every authored band states its px height in a comment.

### WAVE 5 — regression closure
WO-2016 capture rewrite + scroll auditor fix.
⚠ Real, not cosmetic: the flow-map capture reported `geometry=5 touch=5` purely because rows sat outside a
deliberately-scrolled viewport. The auditor was written for unscrolled panels. Fix it; do not waive it.

## 3. How this seat drives it

- **Lanes are file-disjoint and dispatched per wave**, never across waves. Wave N+1 starts when Wave N's gate is green.
- **The CLI seat holds the Unity lock, the gates and the single commit.** Lanes edit and hand back; they never gate,
  never commit, never run Unity.
- **Every new oracle gets a one-line REVERT RECIPE** and the CLI proves it RED then GREEN before the commit lands. A
  suite nobody has seen fail is not evidence.
- **Frames are OPENED, not just captured.** A green capture marker proved nothing on 2026-09-06 — the Defense frame
  passed 12/12 while painting an empty state, because the fixture spoke a job-key grammar the game never produces.
- **Device verification closes each wave**, because the editor gates are blind to whole classes: both capture paths
  force `CanvasGroup.alpha = 1` before photographing, so a reveal that never completes is invisible to every gate we
  have and visible only to the player.

## 4. Definition of done
The delivered `IMPLEMENTATION_SEQUENCE.md` states it; this seat adds three:

- **Every troop is reachable from a fresh save**, and an oracle proves it.
- **No panel in the tree lacks a door**, and an oracle proves it.
- **Every authored effect string is honoured by code**, and an oracle proves it.
