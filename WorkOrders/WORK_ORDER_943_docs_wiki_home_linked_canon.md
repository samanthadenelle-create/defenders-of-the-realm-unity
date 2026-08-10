# WORK ORDER 943 — The docs get a HOME: wiki-style linked navigation over the doc lake

**Status:** READY TO IMPLEMENT (overnight lane, owner-directed 2026-08-09)
**Minted:** 2026-08-09 (CLI session, WO-1010 pass) — number from the `CLI_LANES_WO_NUMBERS.md` header, bumped 943 -> 944 in the SAME edit.
**Lane:** Docs/tooling. No gameplay code. Pairs with the overnight aged-vs-new due-diligence sweep.
**Provenance (owner, verbatim fragments, 2026-08-09 late session):** *"almost like a Wiki where there's a link that we start at from home, whether it's board or whatever we call it... it can point to these are the rules. These are the architectural canons. This is a north star... the different components that are actually part of it."* — *"I'd like to be able to look through the catalog of VFX and see how they're organized... I'd like to see how the sounds are organized."* — *"I know it's supposed to be just a doc, but it's become more of a lake over the months... that's the technical debt that builds up."* — *"the next CLI seat doesn't have to dig. They're able to reference... a static content page and... click a link and take it to that piece."*

---

## 1. The problem (owner's framing, mapped to the tree)

~1090 markdown files with no navigable entry point. The load-bearing docs exist and are good
(`PROJECT_INDEX.md`, `docs/README.md`, `docs/ARCHITECTURE.md` as the architecture hub,
`docs/MASTER_CATALOG.md` + per-area files, `SESSION_CANON_LOADER.md`, `docs/HANDOVER.md`,
`CANON_GROUND_TRUTH_<date>.md`, `BOARD.html`) — but they are a LAKE, not a wiki: entry requires
already knowing which file to open. The owner herself cannot browse "how is the VFX organized"
or "how are the sounds organized" without a seat digging for her.

## 2. The deliverable

**ONE generated static home page** — working name `HOME.html` at repo root (or a nav rail folded
into `BOARD.html`; implementer's choice, owner said "board or whatever we call it") — that is the
START link for every seat and for the owner. From it, one click reaches:

1. **The rules** — `RULES.md` when WO-938 lands (it is READY); until then `CLAUDE.md` +
   `PREFLIGHT_GATE.md` + `SESSION_CANON_LOADER.md`.
2. **Architectural canon** — `docs/ARCHITECTURE.md` (the ruled single hub) and
   `docs/ARCHITECTURE_PRINCIPLES.md`; the per-area `*_ARCHITECTURE.md` deep-dives reached THROUGH
   the hub, not listed flat.
3. **North star** — `docs/COMBAT_PIVOT_NORTHSTAR.md` + the current `CANON_GROUND_TRUTH_<date>.md`
   (resolve NEWEST by date at generation time — never a hardcoded filename, the stale-copy lesson).
4. **The board** — `BOARD.html` (regenerated in the same run).
5. **Component catalogs** — `docs/MASTER_CATALOG.md` and its per-area pages.
6. **Asset organization views the owner asked for by name:**
   - **VFX**: a generated registry page of how the VFX are organized — source it from the actual
     tree (`Resources/VFX/_Shared`, the VFX key->hook maps, WO-935's inventory when it lands), per
     the `audit-outputs-as-known-dictionaries` memory: source-cited facts, not prose.
   - **Sounds**: same treatment for audio — `SfxId` / `MusicTrack` enums, `SfxClipLibrary`
     contents, where clips live on disk.
7. **The operator docs** — `docs/HANDOVER.md`, `docs/TICKET_PIPELINE.md`, `docs/BOARD.md`,
   `docs/UI_PLAYBOOK.md`, `docs/INSTRUMENTATION_STANDARD.md`.

## 3. Binding constraints

- **GENERATED, NEVER HAND-MAINTAINED.** Extend `tools/board_build.py` or add a sibling
  `tools/home_build.py` run in the same breath. A hand-edited link page is a new stale-copy
  surface — the exact failure §15 / the numbering-banner history warns about. The repo is the
  source of truth; the home page is a derived view.
- **LINK, NEVER DUPLICATE.** The home page carries titles + one-line descriptions + links only.
  Any copied content goes stale the day it is written (CLAUDE.md §2/§5 lessons, restated in the
  banner itself).
- **Dead-link check at generation:** the generator FAILS (nonzero exit) on a link target that does
  not exist, so a renamed doc breaks the build of the page instead of silently 404ing the owner.
- **Newest-by-date resolution** for dated families (`CANON_GROUND_TRUTH_*`, dated ledgers) — the
  generator picks, a human never re-points.
- **Markdown targets stay markdown.** Do not convert the corpus; the home page may link to raw
  `.md` (opens in editor/GitHub) — rendering-to-HTML of individual docs is OPTIONAL polish, not
  the deliverable.
- **Compose, don't collide:** WO-938 (RULES.md), WO-1011 (board acclimation), WO-940 (date tags),
  WO-937 (status hygiene) all touch adjacent surfaces — the home page CONSUMES their outputs.
  Check their status before implementing overlapping pieces.

## 4. The due-diligence rider (owner, same directive)

While structuring: date-first triage of what gets linked. *"...making sure everything is correct
and that we've done our due diligence with things that have aged versus things that are new and
not spinning our wheels too long on something that's completely outdated."* Concretely: a doc
found stale while wiring the home page gets a `⚠ SUPERSEDED <date>` banner or a `STALE:` flag
(§15 mechanics) in the same pass — but do NOT boil the lake; flag-and-move-on, the home page links
to the load-bearing set, not to all 1090 files.

## 5. Acceptance

- [ ] `HOME.html` (or the BOARD nav rail) generates from the repo in one command alongside the board.
- [ ] Every §2 destination reachable in one click; generator fails on dead links.
- [ ] VFX + sounds organization pages exist and are source-cited (file paths, not prose claims).
- [ ] Ground truth / dated docs resolved newest-by-date at generation time.
- [ ] Zero content duplicated from linked docs (titles + descriptions only).
- [ ] A fresh seat (or the owner) can go home -> rules -> architecture -> a specific catalog entry
      without grepping.
