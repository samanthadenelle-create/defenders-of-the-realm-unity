> ⚠ **STALE — predates the 2026-06-22 single-Knight pivot.** Treat its Blink-hero / party-of-4 / tower-defense-pillar framing as SUPERSEDED (hero = single Tripo "Grom", Blink rig junked, base-defense V2-gated); some architecture/monetization content may still hold. Live reality: `CANON_GROUND_TRUTH_2026-06-26.md` + `docs/COMBAT_PIVOT_NORTHSTAR.md`.

# Brand & Platform Canon — Legends of the Realm / Echoes of Elarion

**Status: CANON** (owner-directed, 2026-06-17). The single source of truth for the title hierarchy,
the platform roadmap, and the go-to-market thesis.

## Thesis — the flywheel (the whole idea, in one breath)
**One model → mobile hook (prove value) → capital → Steam (larger, polished, more mechanics) →
franchise.** The mobile build is the **HOOK** — a cheap, fast, prove-value slice of the endless loop.
If the hook lands, it **morphs into capital** (revenue + the proof that unlocks investment), which
**funds the Steam/Itch version**: bigger, more polished, more mechanics. Each rung **pays for the
next** — nothing is built on spec. The **bones carry through every rung unchanged**: the model (loop,
saga, systems, data) is the constant; mobile and Steam are *views*; capital is the bridge; more
mechanics and realistic renders are **additive to the same loop**, never a rewrite. The endless loop
is the spine the whole way up.

> ## THE LAW: the structure doesn't change — it simply extends.
> Every tier, chapter, platform, and render-quality is an **extension** of the same structure, never a
> change to it. This is what makes the flywheel safe: **capital buys extension, never rework — you never
> pay twice for the same bones.** Extension compounds; change burns capital on rebuilding. The day you'd
> have to *change* the structure to reach the next tier, the flywheel breaks. So: mobile→Steam,
> placeholder→pro art, Chapter One→franchise, portrait→landscape are all **extensions / views** of one
> unchanging model. Build the bones once; extend forever.

## Monetization — fun first; the Arena is the cash cow
Discipline: **fun first; the rest follows.** Monetization is **gated behind fun + investment**, never
front-loaded — the hook and early loop carry little/no spend pressure. **The Arena is the real cash cow:**
by the time a player reaches it they're invested (hooked, time sunk, they care), and **competition/PvP is
the highest willingness-to-pay** (status, winning, keeping up) — so spend pressure lands on people who
already love the game, not newcomers you'd scare off. Same shape as the flywheel: prove value, then monetize.

**The free layer — rewarded video (opt-in, additive, non-coercive):** free players can watch a rewarded
video for a temporary BOOST — e.g. **double resources for an hour** (a *session* driver: watch → play to
capitalize) or **extend an Echo's offline hold / longer offline collection** (a *return* driver: watch
before logging off → collect more on return). Together they hit both **session and return** — the two
retention vectors — from one ad unit each. **Synergy:** a longer offline hold = a fatter haul = a bigger
swarm coming for it (feeds the swarm counter — risk/reward, optional). Monetizes the free majority (ad rev)
+ deepens engagement.

**The full stack:** rewarded video (free majority) → IAP / store (`PackStore`) → **Arena** (competitive
cash cow). Each layer gated behind more fun + more investment; none steps on the fun.

**THE LINE: sell TIME, never POWER. No paywalls / stoppers — convenience only.**
Convenience = pay to reach the *same place* sooner (speed-ups, time-skips, temporary boosters); everyone
reaches the same ceiling. Pay-to-win = pay for a place free players can't reach.
- **OK:** farm/build faster, reach the next Echo-unlock sooner — but keep "faster" **temporary** (speed-ups/
  boosters), never a **permanent** stat multiplier (a permanent multiplier is power, not time).
- **DO NOT sell Echoes directly.** An extra Echo = a permanent extra base/stream = **compounding power**
  that feeds the **Arena** → poisons competitive fairness (the cash cow becomes "wallet wins"). It also
  **guts the narrative** (Echoes are souls FREED through heroism; a soul as a cash SKU kills "people, not
  collectibles"). **Instead: sell the SPEED to the next Echo, never the Echo** — pay to reach the unlock
  sooner; the Echo is always earned/freed. Same roster for all, payer just earlier. Convenience + Arena
  fairness + meaning all preserved.

**Guard (§5):** (1) the road to the Arena stays genuinely fun and FAIR — NOT a grind-wall engineered to
sell shortcuts; the Arena earns cash-cow status because *competition* drives the spend, not *desperation*
manufactured on the way up. (2) Rewarded boosts are **ADDITIVE (a bonus on top), never penalty-removal**
("watch or LOSE your stuff" = the dark pattern to avoid). (3) Watch **cooldowns/caps** + boosts tuned to
**accelerate, not trivialize** (no ad-stacking that flattens the curve); decide active-play vs passive for
the hour-double. Paywalling the path breaks the whole thesis. Cross-ref: `ArenaMode`, `PackStore`,
web3/wallet (WO-443/445).

## Title hierarchy (universe → game)
- **Defenders of the Realm** — the **larger idea / franchise / universe.** The overarching vision where
  multiple games and chapters can live. This is the big-picture project name — the repo named the
  *franchise*, not the game. **NOT retired.** Sits in the background as the world *Echoes of Elarion*
  belongs to; an optional small tagline, never a competing title card at launch.
- **Echoes of Elarion** — **THIS game** (Chapter One): the web-forward, prove-value launch set within
  the Defenders of the Realm universe. The title players install and name. **Lead with this everywhere.**
  Already the canon narrative name (`docs/ECHOES_OF_ELARION_NARRATIVE.md`).
- **Future chapters** — additional games/chapters under *Defenders of the Realm*. The universe is the
  content + monetization spine, not a one-shot. Earned only once Chapter One validates.
- **"Legends of the Realm"** — DROP. A working banner from early opener renders. Don't run three
  "…of the Realm" names: the **universe is Defenders of the Realm**, the **game is Echoes of Elarion**.

**The opener should resolve on ONE title — *Echoes of Elarion*** (the game). The glory tree → it burns →
it lands on *Echoes of Elarion*. The give→take is written into the branding: glory burns down to an
*Echo*. *Defenders of the Realm* may appear only as a small franchise tag, never a second title card.

## Genre & positioning — "Clash of Clans, with more town halls"
A proven base-defense + raid + offline-economy builder (CoC-class: known, monetizable, retentive), with
**one differentiator that IS the lore**: you run **multiple bases (town halls) in PARALLEL**, not one. Each
parallel town hall is a **freed Echo tending a hold** — so the differentiator (multi-base) has a *reason*
(more town halls = more souls of Elarion brought back to work the land). Reconciles onto the existing
outpost capture/flip (WO-441) = the multi-town-hall system; `Dungeons/DungeonController` = the endgame path.

**Dungeon PLACEMENT — post-arena horizon, NOT a launch feature.** The Dungeons (the true evil; opened by the
4th Echo / high priest) come only **AFTER the player is satisfied at the arena.** Why: (1) opening earlier
would cannibalize the Arena (the cash cow) for time/attention; (2) it catches the most-invested players right
when they'd otherwise churn ("maxed the arena — now what?" → the true evil awaits) — retention insurance.
So there is **no slot for dungeons in the current flow yet, by design** — they sit beyond the arena, which is
itself the deep/late layer. The cold-open "true evil" LORE sets up the destination; the dungeons themselves
are **Tier-2 / future, not built for the mobile prove-value launch.** (Gate TBD later: the 4th-Echo unlock =
an arena milestone — rank/wins/league — designed when arena content is built, not now.)

**Dungeon DESIGN (Tier-2 vision — capture, don't build for launch):** a **roguelike push-your-luck crawl**:
- **Finite health** (the run's life total, no mid-run regen) · **limited light** (a dwindling resource / fog
  / visible clock) · **unknown depth** (push deeper for better loot, or **extract**). The "push or bank?"
  gamble is the engine; the extract-vs-all-or-nothing rule sets the whole tension (pin later).
- **Combat = random 2D Final-Fantasy-style ATB battles** — REUSES the existing `BattleATB`/`ATBCombatManager`
  in a 2D dungeon skin, NOT a new engine.
- **Yields → crafting** (existing `VillageCrafting`) → **items/spells for RAIDS** (feeds WO-441's raid power
  loop). So the dungeon **ties the three pillars together** (ATB combat + crafting economy + raid expansion)
  and makes you **stronger at the CORE game** — a connected endgame, not a side-show. Mostly ASSEMBLY of built
  systems + the roguelike wrapper.
- **§5 rail:** do NOT monetize light into pay-to-win ("buy light to go deeper" = a depth-paywall, breaks
  *sell time not power*). Light refills, if sold at all, stay convenience/rewarded-video — never required.
- Pin later (post-arena): extraction vs all-or-nothing; the light mechanic; HP persistence; the 2D-battle
  tonal shift vs the 3D base.

## Why "Echoes of Elarion" — the title IS the core mechanic
The **Echoes** are the **preserved spirits of Elarion's people**, sheltered inside the Heart-Tree when the
Withering came (see WO-446 cold open: the people give themselves to the Tree; it spends its strength to
shield them and falls dormant, holding them, waiting for a hero). **The Echoes are the PETS.**

- As the player drives back the dark and meets **progression tiers** (e.g. unlocking the first buildable),
  the Tree **releases an Echo** — a freed spirit — as a companion/pet.
- **The cycle:** an Echo emerges → the player progresses → it is **summoned back to the Tree** → **another
  Echo emerges.** Every tier met frees another of Elarion's lost souls.
- **This is why the loop has NO end:** there is always another soul in the Tree. The endless defend-loop's
  true purpose = **bring them all back, one Echo at a time.** As they wake, the Tree's light returns and the
  town grows — **freed, not built.**
- The pets are **the people you are saving, in spirit form** — not generic creatures. The title names the
  mechanic: title = lore = system.

### Echoes gate parallel bases (the scaling / idle engine)
**Each Echo = one base = one stream.** A freed Echo doesn't just follow you — it returns to **tend a hold**
(run + harvest + defend a base while you're away). So **base capacity == Echo count:**
- **Start small: 1 base** (first Echo). → **Unlock first buildable / raids** frees another Echo → **2 pets
  = 2 bases** (two parallel streams). → **then 3** → and up.
- **Multiple bases = multiple parallel offline-harvest STREAMS** = the idle/F2P income engine. Echo count
  caps simultaneous bases; **progression** (buildables, raids, tiers) caps Echo count.
- **Per-base yield (Warcraft "2 goldmines = twice the gold" model):** each base produces **GOLD** (resource)
  **+ passive EXP** (generated by its **defense structures**). N bases = **N× gold + N× passive EXP** —
  linear parallel streams. Defense structures are **dual-purpose**: defend the base AND mint the EXP, so
  every defensive upgrade pays back twice (survival + progression) → drives the CoC upgrade economy.
- **The counterweight — escalation counter (keeps idle from being passive-only):** each base accrues a
  **counter that staggers roaming-mob attacks: weak → middle → large swarms** over time. More/longer-running
  bases = bigger incoming attacks. More bases = more yield BUT more escalating swarm-clocks to defend — the
  risk/reward of scaling; this is what makes the build-loop (towers/walls) matter.
  *Open:* gold buys what / EXP levels what (hero? base tier? Echo?); counter **per-base vs global**,
  **time- vs yield-driven** (does a fat un-collected haul attract the bigger swarm?), reset-on-clear?;
  **linear vs tempered** scaling so base #4 doesn't trivialize it.
- **The in-game flywheel mirrors the business one:** start with one stream; each success funds/unlocks the
  next parallel one (1→2→3 bases inside == mobile→capital→Steam outside — same shape top to bottom).
- **The mechanic IS the theme:** restoring the realm = freeing its people = each freed soul working a hold.
  Not "building bases" — *bringing Elarion back to life, one Echo at a time.*

**Open (design):** (a) accumulate (growing roster) vs rotate (one returns as next emerges)? (b) strict 1:1
Echo:base cap? (c) the Echo-unlock ladder (which milestone frees #2, #3…) + pacing; (d) per-stream offline
harvest rate (the number that makes "more streams" matter). **Reconcile** the existing pet system
(`IntroPetCatalog` Aether/Flame/Ice, `PetDeployer`, Pet House) — re-source pets to the **Heart-Tree** (Pet
House = where Echoes settle, not where pets are "chosen"). Additive reframe. Cross-ref: WO-446, WO-441
(raid→capture→outpost→auto-harvest = the bases the Echoes tend), `docs/TOWN_LOOP_CANON.md`.

## Platform roadmap (staged — prove value, then expand)
**Tier 1 — Mobile-first, web-delivered. GOAL: PROVE VALUE.**
- Target: **phone browser (WebGL), portrait 2:3 (9:16-class).** Design every screen for a thumb.
- Scope discipline: ship *Echoes of Elarion* and validate the core loop **cheaply**. No landscape/
  desktop cut, no gold-plating, until mobile-web earns it. (Same lens as the rest of the project:
  *build for what you'll DO, not what you MIGHT.*)
- Portrait assets (`opener.mp4`, `Intro2.mp4` @ 448×672) are on-target for Tier 1.
- **Chapter One IS the playable endless loop — NO scripted ending, by design.** The loop (defend →
  waves → build/manage → raid → rescue → capture → flip → harvest → offline → repeat) is a cycle, not
  a story with a finish line. We validate **retention** (do players come back), not **completion**.
  The endlessness is **thematically earned**: the Withering never stops, so holding the song is
  forever — *"the song is yours now"* is a forever job, not a quest. **The absence of an ending is the
  story, not missing content.** Tier 2 / funded chapters add arcs & scenes AROUND the loop, never an
  "end" bolted onto it. (See `docs/TOWN_LOOP_CANON.md`, WO-441 expansion loop.)

**Tier 2 — Steam / Itch, robust desktop. Built ONLY after Tier 1 validates.**
- Target: **landscape 16:9, HD**, mouse/keyboard/**gamepad**, the fuller cut.
- "More robust" = more chapters under the saga, deeper systems, store integration
  (achievements/cloud), and **HD landscape re-renders** of the front-door assets (the 448px portrait
  videos are Tier-1-only — a monitor needs HD landscape cuts).

## The architectural rule that makes this ONE game, not two
**Platform is a VIEW, not a fork** — the MVVM seam applied to platform. The **game model** (loop,
saga, systems, data, the model everything pulls from) is **platform-agnostic.** Mobile-web (portrait)
and Steam (landscape) are **two presentation views of the same model.** Build the model once; the
mobile view now; the desktop view *adds on*. Design layouts **orientation-aware** so Tier 2 is
additive (new assets + input), never a rebuild.

## Art strategy (bootstrap — placeholder now, pro renders when funded)
**The bones are the asset; the art is a swappable skin.** The game has enough structure (loop, saga,
front-door beats) to prove value with **placeholder AI/Grok renders**. IF it takes off, reinvest
revenue into **professional realistic scenes + better renders** — a *funded upgrade*, not a precondition.

This is safe because **art is a VIEW** (same model/view rule): renders live in **named slots**
(`opener.mp4`, `intro-1..6`, loaded by role/id). Upgrading placeholder → pro art is an **asset swap in
the same slot**, never a rebuild — the lore, timing, structure, and wiring are untouched. Build the
slots clean now; drop better files in later.

**The docs ARE the creative brief.** The storyboard, WO-446, and this canon specify exact beats,
durations, aspect (portrait 2:3 Tier 1 / landscape HD Tier 2), palette, and the give→take→tell. A
future artist renders straight against that spec into pre-wired slots. No re-spec, no rework.

## Front door (both tiers, same structure)
The `give → enter → dust → lore → you` sequence (**WO-446**) is the canonical front-door **structure**
and does not change between tiers. Tier 1 = portrait mobile-web (current build). Tier 2 = a landscape,
HD "director's cut" of the *same* structure with desktop input. See `WORK_ORDER_446_front_door_cold_open.md`.

*Cross-ref:* `docs/ECHOES_OF_ELARION_NARRATIVE.md`, `docs/BRAND_BIBLE.md`, `docs/STORYLINE.md`,
the MVVM seam canon (`ARCHITECTURE_PRINCIPLES.md` / memory `ui-mvvm-binding-seam`), WO-446 (front door).
