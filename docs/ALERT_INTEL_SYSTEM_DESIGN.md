# Alert / Intel System Design — warnings you EARN with lookouts (town safe, world exposed)

> Owner question (2026-05-30): *"keep or lose the alerts when waves attack? Maybe only at town because
> there's a lookout."* — and that framing is the answer. An alert is **information**, and information
> should have a **source.** A lookout justifies a warning; no lookout = no warning. So alerts become a
> thing you **earn by building**, strongest at town, absent in the raw wild. Turns a UI toggle into a
> gameplay system. Design only; reconciles to defenses/settlements/encounters.

---

## The principle — alerts are diegetic intel, not free UI omniscience

| Place | Lookout? | Alert | Feel |
|---|---|---|---|
| **Town** (walls, watchtower) | yes | **full warning** — incoming wave, direction, size | *safety* — someone's on the wall |
| **Settlement with a lookout** (built) | yes | warned of the raid (quality = what you built) | a defended claim sees it coming |
| **Settlement WITHOUT a lookout** | no | **ambushed — hit cold** | you didn't invest, you pay |
| **Raw open world** | none | **no alert — surprised** | *exposure* — nobody's watching for you |

**The asymmetry is the point:** the town *feels* safe because the lookout earns you warning; the world
*feels* dangerous because you're blind out there. That's not a limitation — it's what makes leaving the
walls feel like leaving safety. Pre-warning in the wild would be *unearned omniscience*; getting
ambushed is *correct* — no one was watching.

## Earned + scaling (owner: better lookout = better intel)

The lookout is a **build investment that buys information** — so a watchtower is meaningful beyond damage:

| Lookout tier | Intel granted |
|---|---|
| none | nothing — ambushed |
| basic lookout | "**something's coming**" + rough timing |
| watchtower | + **direction** (which gate/side) + earlier warning |
| upgraded watchtower | + **size** (how big) + **composition** (casters? a brute? air?) + earliest warning |

So **intel scales with what you build** → "buying foresight" is a real defensive choice, and the
watchtower earns its place as a *reason to build* (not just a tower that shoots). Ties straight into the
defensive-depth + crafting tree (a higher lookout = a tier unlock).

## How it threads into systems we already designed
- **Watchtower = a build with a non-combat payoff** (intel radius/quality) — feeds DEFENSE_DEPTH_ANALYSIS
  (more archetypes, support buildings) + the Forge/tech tree (lookout tiers unlock better intel).
- **Node settlements (WO-159):** a settlement's survival depends on defense — now *also* on whether you
  built a lookout there. Lookout = you get the raid warning + can respond; no lookout = the 3-day-lockout
  loss can hit you cold. Another meaningful thing to build at a claim.
- **Encounters / tribe scouts (WO-160, ENCOUNTER_SYSTEM):** in the world the "warning" is **diegetic, not
  a HUD popup** — you *see the tribe scouts* before the raid (the encounter beat) instead of an alert.
  Spotting scouts = the world's version of intel, earned by being alert, not by UI. Consistent rule:
  **information comes from a source** (a lookout, or your own eyes), never from omniscient UI.
- **The red-skull (ZONE doc):** tells you a *visible* enemy's danger — that's *assessment*, not
  *forewarning*. You still don't know what's around the bend in the wild. The two are complementary:
  red-skull = "this one I can see is deadly"; lookout = "something I can't see yet is coming."

## Build shape (reconcile)
- An **`IntelService`/lookout component** on watchtower/lookout structures: defines a warning **range +
  quality tier**; when a wave/raid spawns within range, it emits an alert with detail scaled to tier.
- Town alerts (existing wave system) become **driven by the town's lookout** rather than always-on — i.e.
  the current alert is "the town has a watchtower." (If the town has no watchtower yet, it still warns —
  the town is the tutorial-safe baseline; or building the watchtower is an early milestone that *grants*
  the alert. Owner's call — see open Q.)
- Settlements query their own lookout structure for whether/what to warn.
- World = no `IntelService` in range = no alert; the encounter system's scout beats are the diegetic
  substitute.
- Reuses: the existing wave/alert HUD (now gated on intel), WaveManager (spawn = the event), the
  build/defense system (the lookout is a buildable), the encounter system (scout tells).

## Open questions for owner
- **Town baseline:** does the town *start* with a lookout (always warned, the safe home) or must you
  **build the watchtower to unlock alerts** (an early milestone, "earn your first intel")? Recommend:
  town starts with a basic lookout (safe baseline) → upgrade for better intel; settlements start blind →
  must build one. (Keeps town = onboarding-safe, world/settlements = earned.)
- **Intel detail ceiling:** how much does the top tier reveal — size + composition + exact timing, or stop
  short of full omniscience to keep some tension even at town? Recommend: never *fully* certain (a
  margin), so even a maxed watchtower has a sliver of surprise.
- **World total-blind, or rare faint cues?** Pure ambush in the wild, or occasional environmental tells
  (birds scatter, distant horn)? Recommend pure-blind early; ambient tells as later polish.

🤖 Design doc (UI lane). Reconciles to the wave/alert HUD, watchtower/defenses (DEFENSE_DEPTH_ANALYSIS),
node settlements (WO-159), tribes/encounters (WO-160, ENCOUNTER_SYSTEM), red-skull (ZONE doc). No code/bake.
