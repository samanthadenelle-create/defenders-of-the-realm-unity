# Data Architecture Decision — Hybrid Local/Remote + DB-ready (2026-06-27)

**Status: RATIFIED (owner, 2026-06-27).** Context: grants not awarded; Pi2Day 2026 deadline
passed → project is now **self-paced**. With no deadline pressure, we build the **correct**
structure, not the fast one. This is a structural/holistic-leverage workstream (HP B2B law:
NOT to be smuggled into player-facing work — own lane, own WOs).

## The model — "how our data lives" (three tiers)

The recurring "move it to a database" idea actually splits into THREE separate levers. Keeping
them distinct is the whole point — they use different tools and ship in different phases.

| Tier | What | Lives where | Lever it pulls |
|---|---|---|---|
| **T0 — Definitions** (static catalog) | ActorDef, Weapon/Armor/AccessoryDef, talent nodes, abilities, recipes — *numbers + pointers* | Local JSON **today** → behind a source-agnostic `ICatalogSource` seam → remote JSON / DB **later** | Add content (armor sets, talents) **without a code change / rebuild** |
| **T1 — Binary assets** (sprites, models, anims) | the heavy MB | **CDN via Addressables-remote**, keyed by the pointer in T0. **NEVER in the DB.** | **Build size + download-on-demand** (the 138 MB Heroes problem) |
| **T2 — Player state** (save, progression, owned inventory) | per-player mutable data | Local `SaveSchema` **today** → behind an `ISaveProvider` seam → cloud DB → **Solana** for owned items | Cross-device, anti-cheat, NFT ownership |

**Critical correction we must not lose:** "reduces build size" comes from **T1 Addressables-remote**,
NOT from the database. You never put a 25 MB FBX in SQLite/Supabase — the DB stores the *pointer*;
the binary streams from a CDN. A DB that tried to hold binaries would be the wrong structure.

## Build order (staged — game stays runnable; Knight-V1 north star protected)

1. **Seams first (structural, zero behavior change, regression-guarded).** Introduce `ICatalogSource`
   (T0) and `ISaveProvider` (T2) wrapping today's local loaders (`CanonicalJson`, `SaveSchema`).
   No functional change — this IS "the correct structure" made real, still 100% offline. Foundation
   for everything else. Reuse the existing remote-config appetite (WO-445 remote flags, WO-443 web
   remote trace) rather than greenfielding.
2. **T1 Addressables-remote (WO-545).** Heroes/enemies out of `Resources/` → per-hero Addressable
   groups, remote on WebGL. V1 ships hub + Knight; mage/ranger stream when V2 unlocks them.
   Reuses the proven `Gear` Addressable-group pattern (prefabPath → `Addressables.LoadAssetAsync`).
3. **T0 remote source.** Point `ICatalogSource` at hosted JSON → live content updates without a build.
4. **T2 cloud save / DB progression.** Swap `ISaveProvider` to a backend (Supabase/Firebase).
   **This is where the game stops being purely offline** — deliberate, online dependency + server
   cost + security surface begin here.
5. **T2 Solana inventory.** Owned/NFT items become the authoritative source for the "owned" subset,
   layered on T2. Last.

**Parallelism:** the Knight-only talent build authors against the T0 catalog seam, so it is NOT
blocked by this workstream — they proceed together.

## Why this is right for us now
- Self-paced → correctness over speed; the seams are cheap insurance paid once.
- Catalog is already flat records + pointers → DB-ready by design; the swap is one seam, not a rewrite.
- Future-proofs for remote live-ops + Solana **without** paying the backend/online/security cost until
  a real reason (NFT story, multiplayer) pulls it in.

## Uniform methodology (owner refinement, 2026-06-27) — consistency over case-by-case

The rule is **NOT** "is this asset light enough to keep." That's a per-asset judgment call and it's
inconsistent. The rule is **uniform across ALL content** and classified by **role, not size**:

1. **Everything is a DEFINITION** = a JSON record + pointers, interpreted by a thin generic runtime.
   Heroes, enemies, weapons, armor, accessories, sprites, **and future recipes/formulas/crafting** —
   all the same shape. Nothing is special-cased.
2. **Common vs Explicit split (the consistent policy that replaces "is it light enough"):**
   - **COMMON / shared** (universal, used by everyone, small): the 8 shared talents, the shared rig,
     common icons → **stay in the base bundle.**
   - **EXPLICIT / specific** (per-hero, per-user, per-variant): → **externalized uniformly** — out of
     `Resources/`, into per-entity Addressable groups, **source in LFS**, delivered on-demand via CDN.
   So the dragon, the orc roster, per-hero kits, per-user cosmetics all follow ONE rule, not 50 size checks.
3. **Add/remove = data + a script, NEVER a code change.** New content is authored as JSON + assets and
   registered by a script/recipe. No recompile to add a sword, an armor set, an enemy, or a beer recipe.
4. **Generic engines, built ONCE (the engineering truth behind "no code change"):** the "pure data"
   promise only holds if the generic INTERPRETER for that *shape* already exists. So we build each abstract
   engine once — **Definition catalog, Recipe (`inputs → process → outputs`), Loadout, Drop-table** — and
   after that every concrete instance is free data. **Crafting, brewing ("reveling/crafting of beer"),
   upgrades, drops are all the SAME `Recipe` shape** → one engine, infinite JSON instances. The investment
   is in the small set of generic shapes; the content is then weightless to add.

This is the data-structures north star applied universally: a thin runtime over lookup tables, not
hardcoded branches. The seams above (`ICatalogSource`, `ISaveProvider`, Addressables) are the substrate;
this section is the **policy** layered on them.

## Guardrails
- Seams + Addressables are a structural lane — keep them out of player-facing tickets (HP B2B).
- Do NOT take an online dependency (T2 step 4+) by default — only when a concrete need arrives.
- Binaries never enter the DB. The DB holds pointers; Addressables resolves the asset.

Indexed by: `docs/ARCHITECTURE.md` (hub). Related WOs: WO-545 (Addressables-remote), WO-445/443
(remote config seam to extend). Supersedes the implicit "everything in Resources/, local-only save"
assumption.
</content>
