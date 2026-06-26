> ⚠ **STALE — predates the 2026-06-22 single-Knight pivot.** Treat its Blink-hero / party-of-4 / tower-defense-pillar framing as SUPERSEDED (hero = single Tripo "Grom", Blink rig junked, base-defense V2-gated); some architecture/monetization content may still hold. Live reality: `CANON_GROUND_TRUTH_2026-06-26.md` + `docs/COMBAT_PIVOT_NORTHSTAR.md`.

# Design — Pet System (acquisition · skills · unlock questlines)

**Status:** DESIGN (creative call). **Reconciles with existing code — do NOT greenfield:**
`PetUnlockTracker` (per-species Level 1–20 + Xp + unlocked skill set, PlayerPrefs blob),
`PetSkillTreeCatalog` (skills + prereqs + canUnlock), `PetSkillTreePanel` (HUD), `PetDeployer`
(spawn/deploy, `SetHeartPosition`, `DeployStarterPets`), `PetHarvester` (auto-harvest), `PetAuraVFX`,
pet anti-ranged (WO-128), pet aura (WO-58), `PetSelectController` (onboarding).
**Feeds new WOs:** 297 (acquisition), 298 (skill content), 299 (unlock questlines).

---

## 1. Fantasy + roles

Pets are **bonded companions of Elarion** — beasts that fled when the Heart dimmed and can be won back.
Every pet fills one of four **roles** (a pet can specialize via its tree):

- **Harvester** — auto-gathers a resource node (`PetHarvester`), banks via EconomyService. The economy pet.
- **Guardian** — garrisons an outpost/wall, taunts + body-blocks, anti-ranged screen (WO-128). The defense pet.
- **Striker** — fights beside the party, focuses what the hero targets. The combat pet.
- **Warden (support)** — auras: heal-over-time, yield bonus, move-speed (WO-58 `PetAuraVFX`). The buff pet.

> **Combat always wins:** a Harvester drops gathering to defend if threatened (existing `PetHarvester` rule).

## 2. Species roster (region-themed, ~8)

| Species | Region | Natural role | Signature |
|---|---|---|---|
| **Sproutling** | Verdant (E) | Harvester | Wood/Food yield; "Photosynth" offline bonus |
| **Craghound** | Stone Mtns (W) | Guardian | Stone hide taunt; reduces wall damage nearby |
| **Frostkit** | Frost Peaks (N) | Striker | Chill-on-hit (slows enemies) |
| **Emberpup** | Ashen Wastes (S) | Striker | Burn DoT; thrives in danger zones |
| **Mirewing** | Swamp/Mire | Warden | Poison-cleanse + HoT aura |
| **Glimmermoth** | Crystal nodes | Harvester | Crystal yield; "sniffs" rare timed spawns |
| **Stoneback Calf** | Stone Mtns | Guardian | Mobile cover; carries extra resources |
| **Aether Fox** (rare) | post-Dimming | Warden | Aether aura: small ability-cost reduction (late unlock) |

Starter pet from onboarding (`PetSelectController`) is one of Sproutling/Craghound/Frostkit (player choice).

## 3. How you get more pets (acquisition) — every method ties to a quest or system

1. **Taming (primary).** Track a wild pet in its region, weaken-but-don't-kill, then bond (a short
   timing/approach minigame). Gated behind **Fenn's "Wild Hearts"** umbrella questline (vendor doc) +
   that region being reachable. Each species = its own short **bond quest**.
2. **Eggs / hatching.** Rare drops from region camps/raids. Hatch by **caring** — feed Food over time
   (works offline, ties to accrual). Hatch yields a random pet of that region's species + a chance at a
   rare variant.
3. **Rescue.** Some enemy camps hold a **caged beast**; clearing + freeing it bonds it instantly (a warm
   beat, and a reason to clear camps). Ties to `ClaimableCamp`.
4. **Breeding/lineage (stretch).** Two high-level pets at the Stables (Fenn) → an egg inheriting one
   skill. Post-launch.
5. **Cosmetic skins** via Glimmer/store — **appearance only, never power** (monetization guardrail).

Active **slots**: start with **1** deployed pet; unlock **2nd** via Fenn's questline, **3rd** via village
tier (Warden Alric). Un-deployed pets rest at the Stables (SW Pet district) and still gain a trickle of XP.

## 4. Skills & leveling (extends PetUnlockTracker + PetSkillTreeCatalog)

- **XP & Level:** per-species, Level 1→20 (existing cap). XP from the pet's *role work* — Harvesters gain
  XP per bank, Strikers per assist/kill, Guardians per damage soaked, Wardens per aura uptime. Each level
  grants **1 skill point**; starter skill auto-granted on first use (existing behavior).
- **Skill tree — 4 branches** (catalog content for WO-298):
  - **Harvest:** Yield +, Gather Speed +, Auto-range +, Offline Cap +, Dual-node.
  - **Combat:** Attack, Anti-ranged screen (WO-128), Taunt/Guard, Pack Tactics (bonus when near hero).
  - **Utility:** Carry Cap, Move Speed, Rare-node Scent (Glimmermoth), Revive-assist (drag downed hero to safety).
  - **Aura (Warden):** Heal-over-time, Yield aura, Speed aura, Aether aura (cost reduction — gated late).
  - Each species also has **one signature node** (table above) at the tree's apex (needs Level ~15 + a quest item).
- **Respec:** at the Stables for Food/Glimmer (so builds aren't permanent traps).

## 5. Unlock questlines (the "how do I get this pet" content — WO-299)

Umbrella: **Fenn Wildmane's "Wild Hearts"** (vendor doc). Under it, each species is a short bond quest,
gated so pets arrive at a sane pace:

1. **Bond: Sproutling** (early) — cleanse a Verdant harvest site, leave an offering → it follows you home.
2. **Bond: Craghound** (early-mid) — survive a Stone Mtns raid *protecting* a wounded hound → loyalty.
3. **Bond: Frostkit / Emberpup** (mid) — region-clear + a taming approach in Frost/Ashen.
4. **Hatch: any** (mid) — recover an egg from a camp, care for it N days (offline-aware).
5. **Rescue: Stoneback Calf** (mid) — free it from a Stonebelly camp cage.
6. **Bond: Aether Fox** (late, rare) — only appears after the Heart is partway restored (ties to the
   Forgemasters/Spire arc) — the "you've healed enough of the world that wonder returns" payoff.
- Signature-skill unlocks each have a tiny capstone errand (e.g., feed Glimmermoth a flawless crystal from
  Sable the Jeweler) — cross-wiring pets into the vendor web.

## 6. Persistence + implementation hooks

- **Persistence:** `PetUnlockTracker` currently saves a **PlayerPrefs** blob. Recommend folding pet
  roster + per-species level/skills + bonded species into the **wallet-keyed GameState save** (persistence
  lane) so pets survive logins with everything else. Migrate the prefs blob on first load.
- **New systems (reconcile, don't fork):** `PetSpeciesCatalog` (species defs: region, role, signature,
  model), `PetAcquisitionService` (tame/egg/rescue flows + slot management), egg/hatch timer (offline-aware).
- **Gating:** species + slot unlocks via `QuestService` (the new quest backbone) + region-clear flags.
- **Deploy:** reuse `PetDeployer` (assign role: harvest node / guard outpost / follow party).
- **UI:** extend `PetSkillTreePanel` (already exists) with a roster/collection tab + slot assignment.
- **Suggested WOs:** 297 acquisition + slots, 298 skill catalog content + balance, 299 bond questlines.
