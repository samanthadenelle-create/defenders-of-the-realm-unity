<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-30
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-30) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-785 — VFX runtime-art survivability: 117 of 121 owner-tagged keys live in gitignored packs

**Status:** READY TO IMPLEMENT
**Minted:** 2026-07-30 (CLI, from the check-in sweep + the Web/Audio/VFX SME dossier)
**Lane:** VFX art pipeline + `tools/art/`. File-disjoint from gameplay lanes.
**Scope correction:** this was first framed as "the ParticlePack has no fallback". Measured, it is
**four packs and nearly the whole catalog** — see Why. The narrower framing is superseded.

---

## Why (evidence, measured on disk 2026-07-30)

The owner tags VFX creatively in the VFX Caster; each tag persists to
`Assets/Editor/VfxManualPicks.json` as `key -> prefabPath`, and `HovlVfxCatalogGenerator.Generate`
bakes those into `Assets/Resources/VFX/HovlVfxCatalog.asset` (134 rows, 0 null refs, **tracked**).

Counting the `prefabPath` roots in `VfxManualPicks.json` (121 rows total):

| Root | Rows | Tracked in git? |
|---|---|---|
| `Assets/Hovl Studio/` | **59** | **GITIGNORED** (`.gitignore:216-219`, ~236 MB) |
| `Assets/UnityTechnologies/` | **54** | **GITIGNORED** (`.gitignore:399`, 191 MB / 886 files — ignored 2026-07-30, commit `4780e3cc`) |
| `Assets/Mirza Beig/` | **3** | **GITIGNORED** (`.gitignore:212`) |
| `Assets/Spells Pack/` | **1** | **GITIGNORED** (`.gitignore:214`) |
| `Assets/Lana Studio/` | 3 | tracked |
| `Assets/Resources/` | 1 | tracked |

**117 of 121 owner-tagged rows (96.7%) point into gitignored trees.** The catalog `.asset` is tracked,
but it binds those prefabs **by GUID**, and the GUIDs live in the ignored packs' `.meta` files. On any
machine that only did `git pull` — the laptop (`Kayden-Laptop`), a fresh clone, CI — every one of
those GUIDs dangles and `VFXManager.PlayKey` finds a null prefab.

This is the **exact failure class** that PAIN_POINTS §1.2 already ruled on for character art:
*"Runtime-playable silhouettes MUST exist in tracked paths under `Assets/Resources/` so a fresh clone
still runs."* VFX never got that treatment. Unlike the character packs — which degrade to a tracked
`Resources/Enemies` fallback — **VFX has no fallback at all.** A missing pack is silent: the miss is
throttled to a `FlowTrace.Throttle("VFXManager","hovl-nokey:<key>")` / `hovl-noprefab:<key>` line
(`VFXManager.Hovl.cs:205-214`) and the effect simply never plays.

**Known live consumers that break:** `Enemy.cs:1551` (the travelling fireball body),
`PoiCalloutSystem.cs:44` (`TreeofLifeAura_Aura` = the ParticlePack FireFlies on the tree crown),
plus the tower/weapon/ability chains through `DefenseTower`, `ArcaneTower`, `TowerCombat`,
`HeroAbilities`, `WeaponVfxMap`, `StructureBurn`, `HealingFountain`, `ArcaneAura`.

**Two-machine drift is not theoretical here** — it has already caused two shipped bugs
(`KEY_FACTS.md:167-171`): the magenta terrain material that existed only on the laptop, and the
22:00 exe cut 45 minutes before the art copy that shipped 4 of 41 models as placeholders while
reporting SUCCESS.

## The standing constraint (BINDING — do not violate)

**The owner alone picks VFX creatively.** Memory `vfx-map-owner-tags-no-creative-pick`, owner ruling
2026-07-25, verbatim: *"you do not get creative control you take the direct reference i told you to
map."* This WO **promotes assets she already tagged**; it must NEVER substitute, re-pick, re-key or
"improve" an effect. If a key's prefab is missing, HOLD and report — do not choose a replacement.
Key names encode their hook (`<OwnerLabel>_<Cast|Impact|Projectile|Aura|Slash>`); preserve them byte-exact.

## Scope

**P1 — Measure precisely, then promote only what is WIRED.**
The audit found ~48 of the tagged keys are actually referenced by a code hook; the rest are tagged
but not yet wired (23 non-`PP_` + 50 `PP_*`). Re-verify that split at implement time — keys can also
be composed at runtime (`CastKeyFor(element)`, `ImpactKeyFor(element)`, `def.VfxCast`, `row.vfxKey`),
so a literal-reference grep UNDER-counts. Resolve the runtime-composed key space before deciding what
is dead.
Promote the WIRED set's prefabs (plus their dependency closure: materials, textures, shaders,
sub-emitters) into a **tracked** `Assets/Resources/VFX/<pack>/` path. Re-point `VfxManualPicks.json`
`prefabPath` at the tracked copies and re-bake `HovlVfxCatalog.asset` via
`HovlVfxCatalogGenerator.Generate` so the GUIDs resolve from tracked assets.
**Weigh the size cost before bulk-copying** — the point is a runtime-playable set, not re-committing
236 MB. If the closure is too large, report the measured size and stop for an owner call rather than
committing it.

**P2 — Make a missing pack LOUD, never silent.**
Extend `tools/art/verify-runtime-art.ps1` to fail (`-Strict`) when any `HovlVfxCatalog` row's prefab
GUID does not resolve on disk, naming the key and the pack. This is the machine half of the §1.2
ruling and the same shape as the existing critical-Resources check.

**P3 — A headless oracle so it can never rot.**
New `[vfx-catalog-resolve]` suite in `Assets/Editor/Regression/`, registered in
`DataRegression.RunAll`, marker `VFX_CATALOG_RESOLVE_OK`: every row in `HovlVfxCatalog.asset` must
resolve to a real prefab, AND every wired key must live under a **tracked** path. On this machine it
passes today (the packs are present) — its value is that it goes RED on the laptop / a fresh clone /
CI, which is precisely where the bug lives. State this asymmetry in the suite header so nobody
"fixes" it by deleting it.

**P4 — Travel manifest (partially done).**
`tools/art/REQUIRED_PACKS.md` already gained a ParticlePack row in commit `4780e3cc`. Add the other
three (Hovl Studio, Mirza Beig, Spells Pack) with the same honesty note: **no runtime fallback**.

## Acceptance (data-verified)

1. `VFX_CATALOG_RESOLVE_OK` green on this machine; **prove it goes RED** by temporarily pointing one
   row at a non-existent path (positive control) — a suite that cannot fail proves nothing.
2. `verify-runtime-art.ps1 -Strict` exits non-zero with a pack absent (simulate by renaming a folder
   locally; do not commit the rename).
3. **Screenshot proof** (owner standing rule): a headless/device capture showing at least one
   promoted effect rendering — the tree-crown fireflies (`TreeofLifeAura_Aura`) are the cheapest
   visible case. Compile-green does not prove a particle system renders.
4. Every promoted key keeps its **exact original name** and maps to the **same prefab the owner
   tagged** — diff `VfxManualPicks.json` and assert only `prefabPath` changed, never `key`.

## Do NOT touch

- **Do not re-pick, substitute or rename any VFX key or prefab.** Owner-only creative territory.
- Do not commit the full source packs (owner policy: git never holds the multi-GB packs).
- Do not "clean up" the ~73 tagged-but-unwired keys — they are the owner's staged intent for hooks
  not yet built. Leave them; only report which are unwired.
- Do not touch `VfxPool` / `VFXManager` pooling internals — one owner per concern (the two-VFX-stack
  scar, `ARCHITECTURE_PRINCIPLES` §2b.1). This WO changes WHERE prefabs live, not how they pool.

## References

`.gitignore:212-219,399` · `tools/art/REQUIRED_PACKS.md` · `docs/PAIN_POINTS_2026-07-26.md` §1.2 ·
memory `vfx-map-owner-tags-no-creative-pick`, `asset-pipeline` · `KEY_FACTS.md:162-171` (two-machine
drift) · SME dossier: Web/Audio/VFX/Art, 2026-07-30 fan-out · commit `4780e3cc`.
