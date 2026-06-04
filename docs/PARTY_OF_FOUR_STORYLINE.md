# The Party of Four — assembling your band before you leave Elarion (story + system weld)

> Owner ask (2026-05-30): *"write into the storyline — we need a party of 4 by the time we leave the
> town, to work within the enemy AI."* Today the canon is a **lone Keeper + assignable spirit-pets**
> (creative review confirmed: no party exists). This doc writes the party INTO the existing mourning-
> story so that **by the time you leave Elarion you travel as four** — which is what makes the
> party-vs-party targeting / role AI (BATTLE_2D_PARTY_DESIGN) actually matter. Grounded in
> `narrative-bible.md` + `dungeons-storyline.md` + `regions-narrative-and-npcs.md`. Creative/design only.

---

## Why a party (the design reason, in-fiction)

The targeting/role AI (focus healers → ranged → tanks) is meaningless with one unit. A **party of four**
is the context where it becomes the gameplay — and it must feel *earned*, not handed over. So the party
assembles **across the town arc**, one bond at a time, reaching four exactly as you're ready to leave the
walls. This doubles as the **tutorial for party tactics**: you start alone (learn the basics), and each
companion teaches one role before the open world demands you coordinate all four.

> Ties to the owner's earlier "start as one, then it opens to more and more" — the party IS that opening.

---

## The four (built from EXISTING canon — no new cast invented)

The review found the companions already in canon; the party uses them, giving the two thin pets (Sprite,
Flame Pup) the arcs the bible promised but never wrote. **Roles map to the targeting system** (tank/
healer/ranged/caster) so composition is real.

| # | Member | Canon source | Battle role | Joins when |
|---|---|---|---|---|
| 1 | **The Keeper** (you — Wizard/Knight/Ranger class) | bible §4, STORYLINE §4 | the player's class (caster/melee/ranged) | start — alone |
| 2 | **A spirit companion** (Aether Sprite *or* Flame Pup *or* Ice Wolf — your bonded starter) | bible §5 | support/healer or DPS by element | early — the first bond (defending the Heart) |
| 3 | **A Folk ally — Sir Bram of the Last Banner** (the Knight; "the master's contemporary") | STORYLINE §4, dungeons-storyline §8 | **tank** — the shield that holds the line | mid town-arc — he answers the muster |
| 4 | **A second ally — Nessa of the Outer Paths** (the Ranger) | STORYLINE §4 | **ranged/DPS** — the eyes beyond the wall | as you secure the last gate — she returns from scouting |

> If the player IS Bram or Nessa (class choice), that slot is filled by the Wizard/Keeper instead — the
> party is always **the player + 3 of {Keeper-mage, Bram, Nessa, a spirit}**, so all four roles (tank/
> healer/ranged/caster) are covered regardless of class. The spirit-pet is always one of the four.

This also **resolves a review gap**: it turns the alternate-class heroes (Bram/Nessa) from "pick one,
play solo" into **a recruited party** — and finally gives the Flame Pup / Sprite a reason to be characters,
not just placeable units.

---

## The storyline beats — how the party assembles across the town arc

Woven into the existing "defend the Heart" opening. Each beat = one companion + one role taught.

**Beat 0 — Alone at the Heart (start).** The master is gone; the watch fell to you before you were ready
(bible §4). You defend the Heart solo — learn to fight, place a tower, no party yet. *Emotional: the
weight of standing alone.*

**Beat 1 — The first bond (your spirit answers).** A chip of Heart-crystal wakes — your **starter spirit**
(Sprite/Pup/Wolf) bonds to you (bible §5 origin). Now you fight as **two**. *Teaches: a second unit, the
support/DPS role, the first "who do I protect / who do I send" choice.* — the seed of targeting.

**Beat 2 — The muster (Sir Bram answers the call).** As the waves mount, **Sir Bram of the Last Banner**
— old, of the master's generation — comes to the wall. He knew your master; he stays because *"someone
has to hold the line while the young one learns."* Now **three**. *Teaches: the tank role — put Bram in
front, he holds, you and the spirit work behind.* The targeting logic starts to bite (the enemy can
choose Bram, you, or the spirit).

**Beat 3 — The return (Nessa comes back from the dark).** **Nessa of the Outer Paths**, a scout who went
looking for the master and is the first to come *back*, returns as the last gate is secured — she carries
the first hint of what's beyond the valley (hooks Act I exploration). Now **four**. *Teaches: ranged/DPS +
the full four-role party — focus-fire, protect the squishy, the complete tactical picture.*

**Beat 4 — Leaving Elarion (the threshold).** The town is secured; the four stand at the gate. The Keeper
relights the first ward-stone (the ward-tether, regions §0) — and the band walks out **as four.** From
here, every battle is party-vs-enemies, and the targeting/role AI is the game. *Emotional: you came to the
watch alone; you leave it with a band. The mourning story gains companions to carry it.*

---

## Why this is the right weld (story ⊥ system)
- **The party is the targeting system's reason to exist** — four roles (tank/healer/ranged/caster) = the
  focus-fire decisions are real (BATTLE_2D_PARTY_DESIGN). Leaving town with four = the moment the enemy AI
  and your tactics both turn on.
- **It's earned, not given** — assembling the party IS the town arc + the tactics tutorial. Start alone →
  leave as four. Matches the owner's "start as one, opens to more."
- **It uses only existing canon** — Bram, Nessa, the spirits are all named in the docs; this gives the
  thin ones their arcs (review gap #4 partly closed).
- **It hooks the open world** — Nessa carries the first thread beyond the walls (Act I exploration), and
  the ward-tether gates the leaving (regions §0).

## Open questions for owner (creative calls)
- **Is the party FIXED (these four) or RECRUITABLE/SWAPPABLE** (more companions found in the world, you
  pick your four)? Recommend: the town arc gives a **fixed starter four** (teaches all roles), then the
  world offers **more recruits to swap in** (encounter NPCs, more pets) — the cozy/collector loop feeds
  the party. Confirm.
- **Do the spirits count as a party slot, or are they separate** (party of 3 humans + a spirit on top)?
  Recommend the spirit IS slot 2 (keeps it 4 total, clean for the battle screen).
- **Can party members fall / be revived** (FF-style KO in battle) — and is losing one permanent or just
  per-battle? Recommend per-battle KO + revive; permanent loss is too harsh for the cozy tone.

---

## ⚠ SEPARATE — creative review flagged CANON RECONCILIATION needed (owner-level, not this WO)
The review found real coherence gaps the party arc sits on top of — flag for an owner reconciliation pass
(these don't block the party storyline but should be settled before final polish):
1. **Premise fork:** living Heart-Tree (bible/Echoes) vs burned-Tree-+-Spire (STORYLINE "supersedes"). Pick one.
2. **Three apex antagonists** un-reconciled: **Alduin** (necromancer), **Syndrath** (dragon, STORYLINE),
   **Alerion** (the game's own subtitle). Decide if they're one being or distinct.
3. **"Avalon" contamination** — retired name still live in ~9 docs (incl. dungeons-storyline, port-specs). Purge.
4. **Pet name drift** — Aether Sprite vs Twilight Sprite. Lock one.
5. **Name collision** — "Old Bram" (Ashwood NPC) vs "Sir Bram" (Knight). Deduplicate (rename one).
> These are owner creative decisions — I can write a reconciliation WO that proposes a resolution for each
> if you want, but they need your ruling, not mine.

🤖 Creative/design doc (UI lane). Reconciled to narrative-bible.md, dungeons-storyline.md, STORYLINE.md,
regions-narrative-and-npcs.md, BATTLE_2D_PARTY_DESIGN.md. No code/scene/bake.
