# Installed Third-Party Packs — Index

One line per pack → its notes doc → the hook. Authored 2026-06-05 to "understand the
product before implementing." Each notes doc mirrors `YARNSPINNER_DIALOGUE_NOTES.md`:
key paths + how-to-use-from-code + gotchas + sources.

## Documented packs (have a `_NOTES.md`)

- **KayKit** → [`KAYKIT_NOTES.md`](KAYKIT_NOTES.md) — low-poly heroes/enemies/buildings/dungeons; shared `Rig_Medium`/`Rig_Large` rig, one controller drives the cast; URP-fix via `Tools ▸ DeNelle ▸ Fix KayKit Materials`. Full pick-list in `kaykit-asset-catalog.md`.
- **Spells Pack (Zakhanfx)** → [`SPELLS_PACK_NOTES.md`](SPELLS_PACK_NOTES.md) — 7-element spell/projectile/shield/aura particle prefabs; `Instantiate` + destroy; needs the bundled URP `.unitypackage` or it's magenta.
- **Mirza Beig VFX** → [`MIRZABEIG_VFX_NOTES.md`](MIRZABEIG_VFX_NOTES.md) — 300+ general VFX prefabs + runtime force-field/plexus components (namespace `MirzaBeig`); **Particle Scaler tool is THE way to resize any particle pack**.
- **Lana Studio Casual RPG VFX** → [`LANA_RPG_VFX_NOTES.md`](LANA_RPG_VFX_NOTES.md) — ~128 cute RPG VFX prefabs (slash/orbs/heals/loot); run `Upgrade for URP/` or magenta; demos authored for Gamma.
- **polyperfect Low Poly Ultimate Pack** → [`POLYPERFECT_NOTES.md`](POLYPERFECT_NOTES.md) — single-atlas low-poly village art; use `_M/Prefabs_M/`; **gitignored (re-import on clone)**; URP-fix via `Defenders ▸ Art ▸ Fix Polyperfect URP Materials`. Pick-list in `polyperfect-asset-catalog.md`.
- **Lean Touch / CW** → [`LEANTOUCH_NOTES.md`](LEANTOUCH_NOTES.md) — cross-platform touch/mouse/pinch (namespace `Lean.Touch`); ref asmdefs `LeanTouch`+`LeanCommon`+`CW.Common`; only `*Driver` classes touch it.
- **Quaternius MegaKit** → [`QUATERNIUS_NOTES.md`](QUATERNIUS_NOTES.md) — CC0 modular medieval kit, **already URP (ShaderGraph, no magenta-fix)**; the source art for the Village2 factory; per-piece pivot variance.
- **UniTask (Cysharp)** → [`UNITASK_NOTES.md`](UNITASK_NOTES.md) — zero-alloc async (`Cysharp.Threading.Tasks`, v2.5.10); `.Forget()` not `async void`; no thread-pool on WebGL.

## Art-only packs (no API — meshes/materials only)

- **Black Dragon** — `Assets/Black Dragon/` — baked-animation dragon FBX (`Dragon_Baked_Actions_fbx_7.4_binary.fbx`) + bump/normal materials. The boss dragon mesh (note: narrative is dropping the dragon per the rebrand memo).
- **Medieval Village** — `Assets/Medieval Village/FBX/` — the unzipped raw Quaternius MegaKit source FBX set (same `Balcony_*`, etc.); prefer the prefabs under `Assets/Quaternius/...` instead.
- **CastleGate** — `Assets/Models/CastleGate/castle+ballast+Tower.fbx` (+ `.fbm` textures) — a single castle/gate/tower mesh.
- **Cathedral** — `Assets/Models/Cathedral/` — **EMPTY on this checkout** (the cathedral mesh is gitignored/absent; was a heavy Tripo mesh now superseded by polyperfect).
