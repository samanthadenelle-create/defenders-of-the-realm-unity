# Session — 2026-06-20 · The Yarn Pivot (one hell of a day)

The day a stuck loop broke. The owner made the executive call to **rip out YarnSpinner** and
build dialogue ourselves — and that *decision* (refuse the patch, fix the seam) became the method
for everything after. Every win below is the same move: instrument → capture → **name the real
cause** → fix it at the root → make the next person inherit it.

## The method (now standing practice)
- **Instrument, don't guess (§12).** When something's wrong, add a trace, run a capture, read the
  data. We did NOT fix on theory once today — and it paid off twice where the obvious guess was wrong.
- **The Raid** = the headless autopilot fleet (`run-autopilot-fleet.ps1`) running the dev build with
  `AutoPilotProbes` + `MagentaGuard`. It *names* what it finds (the stray Capsule, the lavender floor's
  exact material/shader). Owner's name; it earned it.
- **Validate agent RCAs against the tree.** Two agent recommendations were wrong on inspection
  (`GridLayoutGroup.childScaleWidth` doesn't exist; "force the Tripo fixer" contradicts the code's own
  comments). Caught both before shipping.

## Shipped + pushed (origin `2200c071` and earlier)
| Ticket | What | Commit |
|---|---|---|
| T-pose | **ROOT fix** — Blink armor was imported Generic; flipped to Humanoid + base avatar (proven animates) | `7fbcd700` |
| Sync | modular pieces **share the base skeleton** (one animator) — natural motion, weapon placed | `55f45127` |
| Arms/Legs | pieces overlay keeps the bare body (never a missing limb) | `f3127110` |
| TKT-1 pink | **MagentaGuard** — runtime, kills magenta anywhere + **names** the culprit (the stray `Capsule`) | `d350696f` |
| TKT-2 underwear | **rig-level Dressable capability** (`BlinkWardrobe` @ `VisualFactory.Skin`) — every humanoid starts clothed | `2200c071` |
| WO-455 | dialogue **Phase 1 engine + Phase 2 routing seam** (Yarn's exit ramp) | `f9c3511d` / `fc8dce2a` |
| TKT-4 / TKT-13 | quest-tracker pin → uGUI; diamond buttons → bottom-center; Raid F8-archive guard | `bf0f90d5` / `baa55066` |

## Local commits (held for owner verify / next push)
| Ticket | What | Commit |
|---|---|---|
| TKT-15 | vendor button **reskinned to BUY/SELL** (gold coin "$"); upgrade/quest toggle untouched | `6dd75deb` |
| TKT-3 | quest board → real **ScrollRect** (was anchor-math overflow) | `6c6625bb` |
| TKT-12 | build **ghost = placed scale** (WYSIWYG; ghost mirrors StructureFactory's fit) | `6c6625bb` |
| TKT-8a | emergency hero seats at the scene's **HeroStartPoint marker** (Village2 off-map) | `6c6625bb` |
| TKT-1 | lavender floor → **URP/Lit + texture/wood color** (Raid proved it's `MI_WoodTrim`/`M_BaseMaterial`, untextured white tinted by light — NOT a stripped shader); diagnostic spam → one quiet log | `aebf3324` |
| TKT-2 | wardrobe keeps the **whole bare body** under the outfit (Starter is incomplete → legs were vanishing) | `aebf3324` |
| Dev | **hide dev tools from testers** — invisible **5-rapid-tap** bottom-left gesture (mobile has no F-keys); F10 for desktop QA | `ef57a8f9` / `ec45077b` |

## New canon written (so the next session inherits it)
- **`docs/WARDROBE_ARCHITECTURE.md`** — dressing is a capability at the rig level; data-driven wardrobe
  collection that feeds the cosmetic store (foundation shipped; data layer = **WO-456**).
- **`docs/drop-yarnspinner-custom-dialogue`** memory + WO-455 — the dialogue engine + phased migration.

## Queued for next pass (fully RCA'd, file:line in hand — not rushed tonight)
- **Enemy families** — `WaveManager._smartComposition=true` overrides authored `waves.json` families
  (Hollow→Orc→Troll). Owner chose: **expand the smart-comp pools** per wave (keep tactical positioning).
- **TKT-11 build palette** — it's a UIDocument with null PanelSettings (the canon trap) → **uGUI rewrite**
  (catalog confirmed non-empty: 18 buildables). Full plan in the RCA.
- **TKT-5 equip book** — responsive `GridLayoutGroup` cell-size (the agent's `childScaleWidth` one-liner
  was invalid — that property doesn't exist on GridLayoutGroup).
- **TKT-8 Village2** — HUD/Heart bridges in `Village2RaidController`; pink enemies need the real Village2
  capture (missing-art fallback vs material).
- **TKT-1 floor (root)** — retarget `MI_WoodTrim` → URP/Lit at import (Polyperfect-fixer pattern) so the
  wood texture ships; the runtime MagentaGuard fix is the safety net.
- **World seams** — only the **south** castle↔OuterWorld bridge exists (`AddCastleBridgeSeam`), and it's
  `BridgeLinkWalkable=false` (warp-only). N/E/W have approach pads, no NavMeshLink. Generalize to the
  recipe-driven **RegionGate** (canon: `region-gate-crossing-primitive`).
- **TKT-6 companion** — verified covered by the armor fixes; just needs a felt-pass on a build.

## Builds
- Windows: `Builds/Windows/DefendersOfTheRealm.exe` (floor + legs + dev-hide).
- WebGL: `Builds/WebGL/` — the end-of-day artifact (run `serve-webgl.ps1`).

— Refuse the easy patch, find the real seam, build it so it stays fixed. That's the standard the
Yarn call set, and it held for eight hours.
