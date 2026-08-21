# WORK_ORDER_496 — GAME-FEEL RESEARCH → actionable takeaways (Fallout/Clash/WC3/SC)

**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

**Status:** RESEARCH / SEQUENCED · Combat+Economy feel · research agent, 2026-06-23 (source-backed)
**Use:** sharpens WO-491 (animation/telegraph), WO-493 (feel), WO-494 (combat design). Mobile-first, URP, Tripo.

## THE one lesson (all four games)
**Close every interaction loop with multi-channel feedback (anim + audio + visual + camera), fired the
instant the INPUT lands — NOT when the action resolves.** That decoupling is the root of "crisp" (SC/WC3)
and hides latency on mobile. Everything below serves it.

## TIER 1 — cheap, highest feel-per-hour (fold into WO-493 first)
1. **Feedback on INPUT, not resolution** — tap → windup anim + button flash + SFX immediately, even if the
   swing connects 200ms later. Cheapest highest-impact change.
2. **Every hit = visible enemy reaction, scaled** — tiered flinch/stagger (light=flinch, heavy=full stagger
   that interrupts + locks ~0.3s). Tank resists most but STILL reacts. The #1 thing that kills combat feel
   if missing (sponge orcs).
3. **Hit-stop** — freeze both actors ~3-5 frames (50-80ms) on a connecting blow; 6-8 on a kill. Coroutine,
   no art, manufactures weight.
4. **Unified scalable `ImpactEvent(magnitude)`** — ONE event fires flinch + hit-stop + scaled camera shake +
   hit-flash tint + impact SFX + particle puff + haptic, all SAME frame. Synchronization fuses them into one
   "real" hit. The spine of WO-493 (one event, not scattered effects).
5. **Reserve heavy juice for best outcomes** — NO shake/slow-mo on normal hits; only crits/charged/kills get
   big shake + brief slow-mo. Contrast = the payoff's weight. (Mobile: constant shake nauseates.)
6. **Audio acknowledgment + barks** — Knight grunt on attack/cast (effort sells weight); orc death screech +
   cast-bark on telegraph. Cheapest aliveness. (Pairs AudioService.)
7. **Telegraph casts with a readable WINDUP** (→ WO-491) — distinct windup pose + color-coded ground
   telegraph that GROWS during windup + windup SFX rising in pitch. Teaches dodge timing = core kite readability.

## TIER 2 — medium effort, identity/clarity
8. **Silhouette + color = read the family at a glance** (→ WO-491/494) — tank big/wide, mage thin+staff,
   warrior mid+weapon; consistent role color accent (mage blue, warrior red, tank gray). Mobile-zoom legible.
9. **Death anim reflects HOW it died** — 2 death variants per orc (no identical mass-deaths); heavy kill
   ragdoll/knockback vs ranged crumple. Multi-channel death event (anim + screech + burst + kill hit-stop).
10. **Death-cam hold on the BATTLE-WINNING kill only** (→ WO-493) — brief push-in + slow-mo before the reward
    screen; NOT every kill. The arena→reward transition.
11. **Effects tight/fast, clear out FAST** (mobile-critical) — short particle lifetimes + aggressive pooling;
    lingering VFX hides the next telegraph AND tanks framerate. Readability = performance here.
12. **Wounded stance = legible HP** (→ WO-493) — orc <30% limps/hunches+slows; Knight low = hunched + heartbeat
    audio + screen-edge vignette. Read HP without the tiny bar.

## TIER 3 — economy + progression juice (isolated lane, §9 — no combat conflict)
13. **Every collect OVER-responds** — echo delivers wood/iron/grain → icon arcs to counter + count-up tween +
    DISTINCT per-resource SFX (wood thunk / iron clink / grain rustle). Same on life-force tick + tree growth.
    Turns passive harvest into a stream of micro-rewards (the whole appeal).
14. **Count-up numbers + slam-in reward beats** — arena reward screen ESCALATES: kill → loot arcs in →
    counters COUNT UP → rising stinger per tier. Count-up feels bigger than an instant pop. Pure dopamine.
15. **Always reward the return** — on app-open / return-from-arena, immediately show accumulated resources
    collect with the #13 juice. First interaction of every session = a reward, not a menu. Retention.
16. **Hero grows weak→strong, player OWNS it** (→ skill tree) — visible level-up flash + sound on the Knight +
    new-skill demo + stat-up popup. Every power gain = a celebrated beat, not a silent unlock. Spine of the
    single-hero pivot.
17. **Live progress meter** — arena "family 1/3 → 2/3 → 3/3" fill; the tree visibly growing toward the next
    life-force threshold. Seeing the bar move makes effort legible.

## DON'T (mobile + scope guards)
- Don't shake/slow-mo every hit (reserve it, #5). · Don't let effects linger (#11). · Don't bury hero growth/
  rewards in menus (#14/#16). · Don't build per-weapon gore (too costly — 2 variants + multi-channel death, #9).
- Don't make orcs sponges — every hit reacts, scaled (#2). The #1 combat-feel killer.

## Sequencing (agent's recommendation)
1. #1/#3/#4 → WO-493 (input-fired feedback, hit-stop, unified ImpactEvent) — cheapest + foundational.
2. #2/#5 → WO-493 tuning (scaled stagger + reserved juice).
3. #7/#8 → WO-491 (telegraph windup + silhouette/color clarity).
4. #13/#14/#16 → a small economy/reward-juice WO (high retention, isolated lane).
5. #9/#10/#12 → death/state-feedback polish once the impact spine lands.
Tier-1 is mostly coroutine/event/audio — little-to-no new art. Sources: PC Gamer/Krotos (Fallout),
Deconstructor of Fun / "Juice It or Lose It" (Clash), Hive/GDC death-anim talk (WC3), GDC Browder (SC).

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
