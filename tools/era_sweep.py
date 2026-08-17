#!/usr/bin/env python3
"""
era_sweep.py — the 2026-08-17 bulk era sweep of the stale READY work-order backlog.

WHY THIS EXISTS
---------------
474 work orders sat in READY (2026-06: 311, 2026-07: 103, 2026-08: 60). Two thirds of
them were two-plus months old, and a large block described scenes and systems canon has
since DELETED (`Village.unity`, `OuterWorld.unity` — neither is on disk or in `git ls-files`).
A READY bucket that large is an unsorted archive, not a queue: CLAUDE.md §11's "top up the
pipeline with the next READY ticket" cannot surface real work through it.

WHAT IT DOES — AND, MORE IMPORTANTLY, WHAT IT REFUSES TO DO
-----------------------------------------------------------
⛔ CLAUDE.md §15: "Frozen, never rewrite."  This script ADDS A BANNER at the top of a WO and
   rewrites EXACTLY ONE LINE — the `**Status:**` line. It never rewrites, summarises, trims,
   reflows or deletes a single line of any WO body. Those documents are the historical record
   of WHY decisions were made; a bulk edit that eats them is unrecoverable, and no amount of
   queue tidiness is worth that. Every banner it writes carries a one-paragraph REVIVE recipe,
   because a sweep a human cannot undo by reading one paragraph is a sweep that destroys
   information rather than sorting it.

ERA IS TAKEN FROM `git log --diff-filter=A`, NEVER FROM MTIME. This is not a style preference:
a prior pass used mtime and reported 165 "recent" WOs when only 22 were genuinely recent,
because the 2026-08-16 status-banner grooming sweep touched hundreds of files authored in
May–July. mtime measures when we last TIDIED a ticket; git-add measures when it was WRITTEN.

BUCKETS
-------
  OBSOLETE-DELETED-SYSTEM  the WO's SCOPE is a scene/system that no longer exists.
                           A mere MENTION is never enough — WO-336 says "Village.unity NOT
                           touched" and WO-916 exists to REPLACE the retired tagline; closing
                           either on a keyword hit would have destroyed live work.
  SUPERSEDED-BY-RULING     a later ruling/WO changed the design or the work already shipped.
  STALE-UNDATED-ASSERTION  §15: an UNDATED WO asserting current state (here: `**Branch:**
                           feat/tower-core-loop`, dead since the live branch moved to
                           wip/village2-and-f8-tickets; or an undated "#1 PRIORITY").
  AGED-UNVERIFIED          old, but its subject still exists and nothing contradicts it.
                           *** THE DEFAULT. STATUS IS LEFT ALONE — these stay READY. ***
  KEEP-READY               git-add >= 2026-08-01. Untouched entirely, no banner.

The asymmetry is deliberate and is the whole design: a wrongly-closed ticket loses real work
SILENTLY; a wrongly-kept one costs one line of noise. When in doubt this script keeps.

USAGE
-----
    python tools/era_sweep.py --dry-run   # writes WorkOrders/ERA_SWEEP_2026-08-17_REPORT.md
    python tools/era_sweep.py --apply     # stamps banners + rewrites Status lines
Idempotent: a file already carrying the sweep marker is skipped, so re-running is a no-op.
"""
import os, re, sys, glob, subprocess, collections

SWEEP_DATE = "2026-08-17"
MARKER = "<!-- era-sweep-%s -->" % SWEEP_DATE
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
WO_DIR = os.path.join(ROOT, "WorkOrders")
REPORT = os.path.join(WO_DIR, "ERA_SWEEP_%s_REPORT.md" % SWEEP_DATE)

# ── the Status-line vocabulary ────────────────────────────────────────────────
# The replacement strings below were checked against tools/board_build.py's bucket_of()
# BEFORE they were written, not guessed. That function keys on keywords in priority order:
#   SUPERSEDED|CLOSED|CANCELLED -> Closed ; DONE|IMPLEMENTED|COMPLETE -> Done ;
#   BLOCKED -> Blocked ; READY -> Ready ; DRAFT|SPEC|NOT STARTED|PROPOSAL -> Spec ;
#   otherwise -> Unlabeled.
# Note the trap: a bare "OBSOLETE — ..." or "STALE — ..." contains NO keyword, so it buckets
# as **Unlabeled** — which board_build --check treats as a DEFECT and which docs/BOARD.md
# defines as a broken status line. So each string leads with a canonical keyword (CLOSED /
# SUPERSEDED) and carries the sweep's own wording as the payload. Off the Ready board, and
# still a legal status.
STATUS_OBSOLETE = "CLOSED — OBSOLETE: {dead} no longer exists (era sweep %s)" % SWEEP_DATE
STATUS_SUPERSEDED = "SUPERSEDED by {by} (era sweep %s)" % SWEEP_DATE
STATUS_STALE = ("CLOSED — STALE: undated current-state assertion, needs re-dating "
                "(era sweep %s)" % SWEEP_DATE)

REVIVE = ("**TO REVIVE:** nothing was deleted and not one line of the body below was changed. "
          "If this work is still wanted, re-date the WO (add a `**Minted:** <today>` line), "
          "re-point it at the live scene/system, and set `**Status:** READY TO IMPLEMENT`.")

FROZEN = ("Only the `**Status:**` line was rewritten. The body below is UNTOUCHED — "
          "CLAUDE.md §15, *\"frozen, never rewrite\"*.")

# ── the decisions ─────────────────────────────────────────────────────────────
# Every entry is (filename, dead-thing-or-superseding-ruling, evidence). The evidence is a
# citation, never an impression: "looks old" is not evidence; "its target scene file is absent
# from disk AND from `git ls-files`" is.
#
# GROUND TRUTH ESTABLISHED THIS SWEEP (verified, not assumed):
#   `Assets/Scenes/Village.unity`    — ABSENT from disk and from `git ls-files`.
#   `Assets/Scenes/OuterWorld.unity` — ABSENT from disk and from `git ls-files`. The only
#                                      "OuterWorld" paths git tracks are four WorkOrders/*.md.
#   `Assets/Scenes/Main_Castle_Overworld.unity` — PRESENT; CLAUDE.md §7 names it the hub,
#                                      MergedWorld ON, ONE navmesh (so no scene-cut seam).
#   `Assets/Editor/VillageSceneBuilder.{Walls,NavMesh,Scene,Content,...}.cs` — PRESENT, i.e.
#                                      the partial-class split three WOs ask for HAS SHIPPED.
V = "`Assets/Scenes/Village.unity` is absent from disk and from `git ls-files`"
O = "`Assets/Scenes/OuterWorld.unity` is absent from disk and from `git ls-files`"

OBSOLETE = [
    # --- scope is the deleted Village.unity scene (rebake/scene-content work) ---
    ("WORK_ORDER_126_scene_material_bugs.md", "Village.unity",
     V + "; the WO's four bugs are playtest defects *in* `Village.unity`, each fixed by a "
         "`VillageSceneBuilder` edit + a rebake of that scene."),
    ("WORK_ORDER_137_castle_rampart_rebake.md", "Village.unity",
     V + "; the WO IS a rebake order — its sole acceptance criterion is "
         "\"`Village.unity` rebuilt via batchmode\"."),
    ("WORK_ORDER_150_skip_deleted_generators.md", "Village.unity",
     V + "; scope is the building roster of the baked village + magenta ghosts in it."),
    ("WORK_ORDER_157_strip_crystal_veins.md", "Village.unity",
     V + "; scope is stripping crystal-vein decoration from the Village bake."),
    ("WORK_ORDER_168_navmesh_gate_openings.md", "Village.unity",
     V + "; the report is \"in scene Village all gates too small\" and the fix is a "
         "curtain-wall navmesh rebake of that scene."),
    ("WORK_ORDER_192_ground_coplanar_zfight_fix.md", "Village.unity + OuterWorld.unity",
     V + " and " + O + "; the bug IS the coplanarity between the two now-merged scenes' floors."),
    ("WORK_ORDER_212_gate_alignment_z_fighting.md", "Village.unity",
     V + "; the WO names `Assets/Scenes/Village.unity` as the file to verify."),
    ("WORK_ORDER_247_village_scene_cleanup.md", "Village.unity + OuterWorld.unity",
     V + " and " + O + "; both halves (DEF-96 tree in the Village bake, DEF-126 "
         "Village↔OuterWorld terrain seam) target deleted scenes."),
    ("WORK_ORDER_256_double_wall_ring.md", "Village.unity",
     V + "; acceptance is a `VillageSceneBuilder` wall-ring change landed by rebaking that scene."),
    ("WORK_ORDER_263_upside_down_tree.md", "Village.unity",
     V + "; acceptance is \"absent from Village scene hierarchy after rebake\"."),
    ("WORK_ORDER_311_tree_of_life_canonical_placement.md", "Village.unity",
     V + "; scope is explicitly \"via VillageSceneBuilder only — never hand-edit Village.unity\". "
         "The RULE it enforces is not lost: CLAUDE.md §7 already canonises the Heart of Elarion "
         "at scene centre (0,0,0)."),
    ("WORK_ORDER_321_missing_side_gate_pet_house.md", "Village.unity",
     V + "; scope is a missing gatehouse in the Village.unity wall build."),
    # --- scope is the deleted OuterWorld.unity scene ---
    ("WORK_ORDER_142_outer_world_regions.md", "OuterWorld.unity",
     O + "; the WO is a new `OuterWorldBuilder` pass and states \"no new scenes — all in "
         "`Village.unity`'s exterior\", so BOTH of its target scenes are gone."),
    ("WORK_ORDER_173_world_void_terrain_missing.md", "the Village.unity/OuterWorld.unity scene split",
     "the WO's root cause is literally \"the two-scene split (Village.unity + OuterWorld.unity) "
     "created a terrain orphan\". " + V + " and " + O + " — the split it repairs no longer exists "
     "(CLAUDE.md §7: hub = Main_Castle_Overworld, MergedWorld ON, one navmesh). Its ⛔ P0 banner "
     "is a P0 against a scene arrangement that was dissolved."),
    ("WORK_ORDER_245_world_terrain_foundation.md", "OuterWorld.unity",
     O + "; the WO's own lane line is \"OuterWorld files only, not Village.unity\" and DEF-61 is "
         "\"create a Unity Terrain object in OuterWorld scene\"."),
    ("WORK_ORDER_255_terrain_seam_height_mismatch.md", "the Village.unity/OuterWorld.unity seam",
     V + " and " + O + "; the entire WO is the height ledge at that boundary. (This file is also "
     "one of two co-claimants of WO-255 — closing it does NOT touch "
     "`WORK_ORDER_255_hero_backwards_walk.md`, which stays READY.)"),
    ("WORK_ORDER_279_am_build_bake_chain.md", "OuterWorld.unity",
     O + "; the chain's steps 2–4 are `OuterWorldBuilder.BuildOuterWorld` / "
         "`ExteriorTerrainBuilder.BuildExterior` / `BakeWorldNavMesh` against that scene, and its "
         "acceptance is \"hero can exit a gate to OuterWorld\". (Co-claimant of WO-279 — "
         "`WORK_ORDER_279_village2_generator_fixes.md` targets Village2, which is LIVE, and stays READY.)"),
    ("WORK_ORDER_448_hub_outerworld_seam_natural_transition.md", "OuterWorld.unity",
     O + "; both root causes are cross-scene (the hub→OuterWorld auto-cross seam, and the castle "
         "plaza z-fighting against `OuterWorld.unity`'s terrain). CLAUDE.md §7: one merged scene, "
         "one navmesh — there is no seam left to cross."),
    ("WORK_ORDER_450_runtime_injector_fixes.md", "OuterWorld.unity",
     O + "; 450a is the hub floor z-fight against OuterWorld terrain and 450b injects an "
         "`OuterWorldBoundaryInjector` on OuterWorld scene load."),
    ("WORK_ORDER_453_outerworld_gated_regions.md", "OuterWorld.unity",
     O + "; the vision is \"make OuterWorld much larger\" by extending that scene's terrain."),
    ("WORK_ORDER_468_castle_to_outerworld_redesign.md", "OuterWorld.unity",
     O + "; the flow is castle exit → OuterWorld (≥4× larger) → portal, i.e. a redesign of a "
         "scene-to-scene crossing that the merged world removed."),
    ("WORK_ORDER_509_four_gate_seam_expansion.md", "OuterWorld.unity",
     O + "; scope is extending the castle↔OuterWorld scene seam to all four OuterWorld edges."),
]

SUPERSEDED = [
    ("WORK_ORDER_181_villagescenebuilder_partial_split.md", "work that has already shipped",
     "the split is DONE in tree: `Assets/Editor/VillageSceneBuilder.{Scene,Walls,NavMesh,Content,"
     "Dressing,Fortify,Helpers,Materials,Portals,Systems,Wiring,Characters,CityManifest,"
     "Village2Inject,Village3Recipe}.cs` all exist alongside `VillageSceneBuilder.cs`."),
    ("WORK_ORDER_207_villagescenebuilder_partial_split.md", "work that has already shipped",
     "duplicate of the same request; the partial-class files listed above are in tree."),
    ("WORK_ORDER_253_split_village_scene_builder.md", "work that has already shipped",
     "third copy of the same request; the partial-class files listed above are in tree. "
     "(Co-claimant of WO-253 — `WORK_ORDER_253_tutorial_speech_bubble_overlay.md` is untouched.)"),
    ("WORK_ORDER_467_region_gate_system.md",
     "WO-608 (world merge) + the shipped RuntimeRegionGate",
     "the WO's stated premise is \"scenes baked at the same origin cannot share one navmesh, so a "
     "crossing is ALWAYS a masked transition\" — CLAUDE.md §7 records the opposite as current: "
     "`Main_Castle_Overworld`, MergedWorld ON, ONE navmesh. Its first recipe row is "
     "`castle_to_outerworld` (from `MainCastle_Hall` to `OuterWorld`), and " + O + ". The primitive "
     "itself already exists as `RuntimeRegionGate` + `Assets/Resources/Data/region-gates.json` "
     "(cited as working in WO-509)."),
    ("WORK_ORDER_593_wide_gates_cliffs_wayfinding.md", "WO-608 (world merge to one scene)",
     "the WO is a Castle→OuterWorld crossing-wayfinding MVP whose whole problem statement is "
     "\"new players can't tell they must cross at a seam\". " + O + " and CLAUDE.md §7 puts the "
     "castle and the overworld in ONE scene on ONE navmesh — there is no seam to sign-post."),
    ("WORK_ORDER_607_seam_4side_traversal.md", "WO-608 (world merge to one scene)",
     "scope is 4-side walk-traversability ACROSS the castle/OuterWorld scene cut, and its work "
     "products are re-bakes of `MainCastle_Hall.unity` + `OuterWorld.unity`. " + O + "."),
]

# §15: an UNDATED WO asserting current state is stale BY DEFINITION. The assertion here is
# concrete and checkable, not a vibe: each of these carries `**Branch:** feat/tower-core-loop`
# (the live branch is `wip/village2-and-f8-tickets`) or an undated "#1 PRIORITY" claim, and none
# carries a date ANYWHERE in the file — no `**Minted:**`, no YYYY-MM-DD at all.
# They are NOT judged obsolete: the quest/pet/crafting designs in the 290–305 block may well
# still be wanted. Re-dating one is a two-minute job and puts it straight back on the board.
STALE = [
    ("WORK_ORDER_208_webgl_rebuild_current_tree.md",
     "undated, and asserts a state that has expired twice over: `**Branch:** feat/tower-core-loop` "
     "(live branch is `wip/village2-and-f8-tickets`) plus \"the CURRENT green tree\" — a build "
     "order against a tree from June."),
    ("WORK_ORDER_278_village_rebuild_modular.md",
     "undated, and claims \"THIS IS THE #1 PRIORITY. Nothing else ships until the village looks "
     "right.\" Its complete spec is delegated to \"DEF-242 in Linear\" — and Linear is RETIRED "
     "(CLAUDE.md §2/§13), so the spec it points at is unreachable."),
    ("WORK_ORDER_282_BuildPreviewModal_Premium_Rotation.md",
     "undated; asserts `**Branch:** feat/tower-core-loop` (live branch is `wip/village2-and-f8-tickets`)."),
]
STALE += [(f, "undated; asserts `**Branch:** feat/tower-core-loop` (live branch is "
              "`wip/village2-and-f8-tickets`). Part of the single WO-290→305 authoring burst.")
          for f in [
    "WORK_ORDER_290_quest_service_and_tracker.md",
    "WORK_ORDER_291_vendor_yarn_pack_and_quest_verbs.md",
    "WORK_ORDER_292_keystone_spire_finale_wiring.md",
    "WORK_ORDER_293_crafting_tiers_and_legendary_recipes.md",
    "WORK_ORDER_294_forgemasters_saga_yarn_and_scenes.md",
    "WORK_ORDER_295_legendary_aegis_set_and_ward.md",
    "WORK_ORDER_296_reforge_choice_finale_ending.md",
    "WORK_ORDER_297_pet_acquisition_and_slots.md",
    "WORK_ORDER_298_pet_skill_catalog_content.md",
    "WORK_ORDER_299_pet_bond_questlines.md",
    "WORK_ORDER_300_weaponsmithing_lore_integration.md",
    "WORK_ORDER_301_party_persistence_wallet_keyed.md",
    "WORK_ORDER_302_floating_healthbar_oversize_fix.md",
    "WORK_ORDER_303_combat_party_hud_wire_to_data.md",
    "WORK_ORDER_304_brom_rumor_board.md",
    "WORK_ORDER_305_relic_recovery_quests.md",
]]

# ── repo reading ──────────────────────────────────────────────────────────────
_STATUS_RE = re.compile(r"^\*\*Status:?\*?\*?:?\s*(.+)$", re.MULTILINE)

def git_added_dates():
    """basename -> YYYY-MM-DD of the commit that first ADDED it. ONE git call.
    --no-renames so a file moved into WorkOrders/ still registers an Add there."""
    dates = {}
    out = subprocess.run(
        ["git", "log", "--reverse", "--no-renames", "--diff-filter=A", "--date=short",
         "--format=%x01%ad", "--name-only", "--", "WorkOrders/"],
        cwd=ROOT, capture_output=True, text=True, timeout=180)
    cur = None
    for line in out.stdout.splitlines():
        if line.startswith("\x01"):
            cur = line[1:].strip()
        elif line.strip() and cur:
            dates.setdefault(os.path.basename(line.strip()), cur)
    return dates

def bucket_of(status_text, has_result):
    """Mirror of tools/board_build.py bucket_of() for work orders."""
    s = (status_text or "").upper()
    if "SUPERSEDED" in s or "CLOSED" in s or "CANCELLED" in s: return "Closed"
    if has_result or "DONE" in s or "IMPLEMENTED" in s or "COMPLETE" in s: return "Done"
    if "BLOCKED" in s: return "Blocked"
    if "READY" in s: return "Ready"
    if "DRAFT" in s or "SPEC" in s or "NOT STARTED" in s or "PROPOSAL" in s: return "Spec"
    return "Unlabeled"

def scan():
    """Every READY work order, with its git-add era and assigned bucket."""
    added = git_added_dates()
    results = {os.path.basename(p).replace(".RESULT.md", ".md")
               for p in glob.glob(os.path.join(WO_DIR, "*.RESULT.md"))}
    dec = {}
    for f, dead, ev in OBSOLETE: dec[f] = ("OBSOLETE-DELETED-SYSTEM", dead, ev)
    for f, by, ev in SUPERSEDED: dec[f] = ("SUPERSEDED-BY-RULING", by, ev)
    for f, ev in STALE:          dec[f] = ("STALE-UNDATED-ASSERTION", "", ev)
    rows = []
    for path in sorted(glob.glob(os.path.join(WO_DIR, "*.md"))):
        base = os.path.basename(path)
        if base.endswith(".RESULT.md") or not base.upper().startswith("WORK_ORDER_"):
            continue
        text = open(path, encoding="utf-8", errors="replace").read()
        m = _STATUS_RE.search(text)
        status = re.sub(r"[*`]", "", m.group(1)).strip() if m else ""
        if bucket_of(status, base in results) != "Ready":
            continue
        git = added.get(base, "")
        title_m = re.search(r"^#\s+(.+)$", text, re.MULTILINE)
        title = re.sub(r"[*`#]", "", title_m.group(1)).strip() if title_m else base
        if base in dec:
            b, key, ev = dec[base]
        elif not git:
            # NO git-add date = the file is UNTRACKED = it was written moments ago and has never
            # been committed. Empty-string sorts BEFORE every real date, so a naive `git < cutoff`
            # test would age-stamp the NEWEST files in the repo as stale — the exact inversion this
            # sweep exists to undo. (It caught WORK_ORDER_1026_IMPLEMENTATION_PLAN.md, written the
            # same day by another seat.) Untracked is current, always.
            b, key, ev = ("KEEP-READY", "",
                          "untracked (no git first-add) — written this session; current era.")
        elif git >= "2026-08-01":
            b, key, ev = "KEEP-READY", "", "git first-add %s — current era; no evidence against it." % git
        else:
            b, key, ev = ("AGED-UNVERIFIED", "",
                          "git first-add %s; subject still exists and nothing in canon "
                          "contradicts it. Kept READY." % git)
        rows.append(dict(file=base, path=path, title=title, git=git, status=status,
                         bucket=b, key=key, evidence=ev, text=text,
                         swept=(MARKER in text)))
    return rows

# ── banners ───────────────────────────────────────────────────────────────────
def banner(r):
    if r["bucket"] == "OBSOLETE-DELETED-SYSTEM":
        return ("%s\n"
                "> ### ⛔ ERA SWEEP %s — CLOSED as OBSOLETE (deleted system)\n"
                "> **Dead thing:** %s. **Git first-add:** %s.\n"
                "> **Evidence:** %s\n"
                "> %s\n"
                "> %s\n\n") % (MARKER, SWEEP_DATE, r["key"], r["git"], r["evidence"], FROZEN, REVIVE)
    if r["bucket"] == "SUPERSEDED-BY-RULING":
        return ("%s\n"
                "> ### ⛔ ERA SWEEP %s — SUPERSEDED\n"
                "> **Superseded by:** %s. **Git first-add:** %s.\n"
                "> **Evidence:** %s\n"
                "> %s\n"
                "> %s\n\n") % (MARKER, SWEEP_DATE, r["key"], r["git"], r["evidence"], FROZEN, REVIVE)
    if r["bucket"] == "STALE-UNDATED-ASSERTION":
        return ("%s\n"
                "> ### ⛔ ERA SWEEP %s — STALE (undated current-state assertion, CLAUDE.md §15)\n"
                "> **Git first-add:** %s (the WO itself carries no date at all).\n"
                "> **Evidence:** %s\n"
                "> %s This is a DATING problem, not a verdict on the design — the content may well "
                "still be wanted.\n"
                "> %s\n\n") % (MARKER, SWEEP_DATE, r["git"], r["evidence"], FROZEN, REVIVE)
    if r["bucket"] == "AGED-UNVERIFIED":
        return ("%s\n"
                "> ### ⚠ AGED %s — still READY, but unverified since %s\n"
                "> The %s era sweep found **no evidence** that this WO's subject was deleted or "
                "superseded, so its **Status stays READY** and nothing else was changed. It is "
                "simply OLD (git first-add %s) and has not been re-verified against current canon "
                "(`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**\n\n"
                ) % (MARKER, SWEEP_DATE, r["git"], SWEEP_DATE, r["git"])
    return ""

NEW_STATUS = {
    "OBSOLETE-DELETED-SYSTEM": lambda r: STATUS_OBSOLETE.format(dead=r["key"]),
    "SUPERSEDED-BY-RULING":    lambda r: STATUS_SUPERSEDED.format(by=r["key"]),
    "STALE-UNDATED-ASSERTION": lambda r: STATUS_STALE,
}

def apply_one(r):
    """Banner + (for the three closing buckets) the ONE status-line rewrite. Returns
    (changed, before, after)."""
    text = r["text"]
    b = banner(r)
    # Two no-op cases, and neither may touch the file: already stamped (idempotency), and
    # KEEP-READY (banner() returns "" — a current-era WO is left EXACTLY as its author wrote it).
    if r["swept"] or not b:
        return False, r["status"], r["status"]
    new = b + text
    before, after = r["status"], r["status"]
    if r["bucket"] in NEW_STATUS:
        after = NEW_STATUS[r["bucket"]](r)
        # Replace ONLY the first **Status:** line — the same one board_build.py reads.
        new, n = _STATUS_RE.subn(lambda m: "**Status:** " + after, new, count=1)
        if n == 0:
            print("  !! no **Status:** line in %s — banner only, status NOT rewritten" % r["file"])
            after = before
    with open(r["path"], "w", encoding="utf-8", newline="") as fh:
        fh.write(new)
    return True, before, after

# ── report ────────────────────────────────────────────────────────────────────
def write_report(rows):
    by = collections.defaultdict(list)
    for r in rows: by[r["bucket"]].append(r)
    era = collections.Counter(r["git"][:7] for r in rows)
    order = ["OBSOLETE-DELETED-SYSTEM", "SUPERSEDED-BY-RULING", "STALE-UNDATED-ASSERTION",
             "KEEP-READY", "AGED-UNVERIFIED"]
    L = []
    A = L.append
    A("# Era sweep %s — dry-run classification of the READY backlog\n" % SWEEP_DATE)
    A("**Status:** REPORT (not a work order — no work is requested by this file)\n")
    A("Generated by `python tools/era_sweep.py --dry-run`. Read that script's header for the "
      "method and for why the body of every WO is left frozen.\n")
    A("## Why the era column is git-add, not mtime\n")
    A("A prior pass used file mtime and reported 165 \"recent\" work orders when only 22 were "
      "genuinely recent: the 2026-08-16 status-banner grooming sweep refreshed mtimes across "
      "hundreds of files authored in May–July. Every date below comes from "
      "`git log --diff-filter=A` — when the file was WRITTEN, not when we last tidied it. Where "
      "a WO's own dated header disagrees with git-add, git-add wins and the disagreement is noted.\n")
    A("## READY backlog by era (git first-add)\n")
    A("| Era | READY WOs |\n|---|---|")
    for k in sorted(era): A("| %s | %d |" % (k, era[k]))
    A("| **total** | **%d** |\n" % len(rows))
    A("## Bucket counts\n")
    A("| Bucket | Count | Status line touched? |\n|---|---|---|")
    touched = {"OBSOLETE-DELETED-SYSTEM": "yes → `CLOSED — OBSOLETE: …`",
               "SUPERSEDED-BY-RULING": "yes → `SUPERSEDED by …`",
               "STALE-UNDATED-ASSERTION": "yes → `CLOSED — STALE: …`",
               "AGED-UNVERIFIED": "**no — stays READY**, banner note only",
               "KEEP-READY": "**no — untouched entirely**"}
    for b in order: A("| %s | %d | %s |" % (b, len(by[b]), touched[b]))
    A("| **total** | **%d** | |\n" % len(rows))
    A("### The asymmetry this sweep is built on\n")
    A("A wrongly-closed ticket loses real work **silently**; a wrongly-kept one costs one line "
      "of noise. So `AGED-UNVERIFIED` is the DEFAULT for anything not positively placed, and it "
      "**keeps its READY status** — %d of %d READY work orders are left on the board untouched "
      "or note-only.\n" % (len(by["AGED-UNVERIFIED"]) + len(by["KEEP-READY"]), len(rows)))
    A("### Keyword hits that were deliberately NOT closed\n")
    A("A mention of a dead system is not proof; the SCOPE has to depend on it. Worked examples "
      "from this pass:\n")
    A("- **`WORK_ORDER_336_atb_village_wall_environment.md`** names `Village.unity` three times — "
      "every one of them saying *\"Village.unity NOT touched\"*. Its scope is `ATBBattle.unity`, "
      "which is live. Kept.")
    A("- **WO-916 (marketing site tagline)** hits the retired \"Hold the last light\" three "
      "times because it is the WO that REPLACES it. Closing it would have cancelled the fix for "
      "the very staleness being swept. Kept.")
    A("- **WO-182 (Avalon → Elarion canon purge)** hits the retired village name ten times; the "
      "purge is still performable and still wanted. Kept.")
    A("- **All nine PatriciaLight / Defend-the-Tower hits** turned out to be background mentions "
      "in audio, boss and RCA work orders — none is scoped to the removed pillar. Zero closures "
      "on that signal.")
    A("- **`VillageSceneBuilder` was rejected as a dead-system signal entirely** (111 hits). The "
      "builder is very much alive — it now has `Village2Inject` and `Village3Recipe` partials. "
      "Only its DELETED OUTPUT scene counts.\n")
    for b in order:
        A("\n## %s — %d\n" % (b, len(by[b])))
        if b == "AGED-UNVERIFIED":
            A("Old but unrefuted. **These stay READY.** They receive a dated `⚠ AGED` banner only, "
              "so a puller knows to re-verify before starting.\n")
        if b == "KEEP-READY":
            A("git first-add 2026-08-01 or later — current era. **Untouched entirely**, no banner.\n")
        A("| WO file | Title | git-add | Evidence |")
        A("|---|---|---|---|")
        for r in sorted(by[b], key=lambda r: r["file"]):
            A("| `%s` | %s | %s | %s |" % (
                r["file"], r["title"][:80].replace("|", "\\|"), r["git"],
                r["evidence"].replace("|", "\\|").replace("\n", " ")))
    with open(REPORT, "w", encoding="utf-8", newline="") as fh:
        fh.write("\n".join(L) + "\n")
    print("report written: %s" % REPORT)

def main():
    dry = "--apply" not in sys.argv
    rows = scan()
    by = collections.Counter(r["bucket"] for r in rows)
    print("READY work orders scanned: %d" % len(rows))
    for k, v in by.most_common(): print("   %-26s %d" % (k, v))
    missing = ({f for f, _, _ in OBSOLETE} | {f for f, _, _ in SUPERSEDED} |
               {f for f, _ in STALE}) - {r["file"] for r in rows}
    # Only meaningful BEFORE the sweep: after --apply every closed decision has left the Ready
    # scan by design, so the same list post-apply is a success signal, not drift.
    if missing and dry:
        # Loud, never silent: a decision that matched no READY file means the table drifted
        # from the repo (renamed/already-swept file) and the sweep is no longer describing reality.
        print("WARNING %d decision(s) matched no READY work order (already swept, renamed, "
              "or mistyped):" % len(missing))
        for f in sorted(missing): print("    %s" % f)
    if dry:
        write_report(rows)
        print("DRY RUN — no work-order file was modified. Re-run with --apply to stamp.")
        return 0
    changed = skipped = 0
    for r in rows:
        did, before, after = apply_one(r)
        if not did:
            skipped += 1
            continue
        changed += 1
        if before != after:
            print("[%s] %s\n    status BEFORE: %s\n    status AFTER : %s"
                  % (r["bucket"], r["file"], before, after))
        else:
            print("[%s] %s\n    status UNCHANGED: %s" % (r["bucket"], r["file"], before))
    print("\nAPPLIED: %d file(s) stamped, %d already carried the %s marker (skipped)."
          % (changed, skipped, MARKER))
    print("Status lines rewritten: %d. Status lines deliberately left alone: %d."
          % (by["OBSOLETE-DELETED-SYSTEM"] + by["SUPERSEDED-BY-RULING"]
             + by["STALE-UNDATED-ASSERTION"],
             by["AGED-UNVERIFIED"] + by["KEEP-READY"]))
    print("No WO body was rewritten, trimmed or deleted (CLAUDE.md §15).")
    return 0

if __name__ == "__main__":
    sys.exit(main())
