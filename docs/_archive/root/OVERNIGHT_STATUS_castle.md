# Overnight Status — Castle WO-104 + Bug Fixes (2026-05-30 → morning)

Branch `feat/tower-core-loop`, all green-gated + **pushed to origin**. 3 commits:

| Commit | What |
|--------|------|
| `5dbcc1e` | Wall fit + gate diagnostic + 5 P1 gameplay bug fixes |
| `dac38d0` | WO-104 Stage 2: moat (204 tiles) + 3 drawbridges + gate stone tint |
| `e3802b3` | 2 P2 fixes (VFX lifetime, deprecated API) |

---

## ✅ Done + verified (compile-green, baked, built)

**Castle walls** — the "right shape" you confirmed. Root cause of all the wall
drama: `Wall_Stone_3x3_A` is natively a proper **3×3×0.35 m** panel (bounds-logged) —
never tiny. The "pinpricks" were the distant *outer* ring seen from center; my ×4.5
"fix" then over-corrected to 13 m. Final: measure world bounds, fit run-axis to the
3 m pitch (factor 1.0, no distortion), raise height to 5 m. KayKit wall mesh hidden so
it doesn't double the poly curtain.

**Gate "magenta"** — was **not a bug**. GATE-DIAG logged the material: valid grey
URP/Lit (`M_21_Grey_Light_LPUP`, baseColor 0.65). The purple is the **dusk ambient**
(`LastChanceLightingPreset`) glowing on the brightest flat face. Tinted the gate's
stone slots to warm-dim masonry (0.46) so it reads as stone. *If you still want it
gone, the lever is the scene **ambient light**, not the material.*

**5 P1 bug fixes** (from a read-only audit → `docs/bug-triage.md`):
- CrystalMine: F-to-upgrade now needs a **2nd confirm press** (was silently spending
  200/400 coins on a single walk-by F — real economy bug)
- CrystalMine: idempotent wave subscription (was leaking listeners / double crystals)
- WaveManager: prune stuck-tracking dicts on normal death (key leak)
- VFXManager: track loop objects → correct return-counter (drift was muting VFX mid-run)
- +2 P2: HeroAbilities VFX lifetime, CrystalMine deprecated API

---

## 👀 Needs your eyes (built + launched, but I can't judge visually)

1. **Moat** — 204 `Terrain_Plane_Lake` tiles in a 6 m ring around the wall (y = −0.4,
   below grade). Should fill that black void at the wall base. Check: does the water
   read right, sit at a good depth, line up with the wall?
2. **Drawbridges** — 3 `Drawbridge_Medieval` laid flat at the S/E/W gates
   (`NormalizeProp` to 7 m). **Most likely to need tuning** — orientation/scale of an
   unfamiliar prefab. Tell me how they sit and I'll fix in one pass.
3. **Gate tint** — does the gate read as stone now, or still too purple? (→ ambient).

## ⏭ Next (left for your visual loop)
- **Rampart stairs** (WO-104 §7) — held deliberately; trivial to add *with* your
  feedback so I don't stack blind visual changes.
- Interactive drawbridge (`DrawbridgeController` exists; needs WO-105 `Player` tag).
- Moat corner-fills + valley transition tiles (WO-104 §6) once the ring looks right.

## 📋 Parked analysis docs (from the parallel read-only lanes)
`docs/build-mode-architecture.md` (WO-108 CREATE verb — "a base is data, not a scene"),
`docs/world-construction-plan.md` (outward-in build order), `docs/bug-triage.md`.

**Nothing is red. Everything is pushed. Drive the visual loop when you're up.** 🏰
