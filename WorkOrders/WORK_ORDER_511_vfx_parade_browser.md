# WORK_ORDER_511 — VFX Parade (owner-curated effect browser)

**Status:** READY TO IMPLEMENT (owner-requested 2026-06-24) · Tooling/VFX lane · editor-only
**Origin:** owner — "run a routine where we implement all of them 10 sec apart and I capture the numbers of
the best to use... with so many... I'm not sure you can visualize what they are and when to apply." CORRECT:
the AI can't see pixels, so it wires VFX blind by name. This tool makes the OWNER the visual judge and emits
her picks as data the AI wires.

## 1. The problem it solves
~466 Spells Pack effects (and ~1000 total) are too many to wire by name-guessing. The owner CAN see them; the
AI can't. So: parade the effects past her with IDs labeled, she judges + tags the combat MOMENT each is for,
and the tool writes a picks file the AI reads and wires. Division of labor: owner = eyes+taste, AI = wiring.

## 2. Build (editor window, mirror the Offset Forge PreviewRenderUtility pattern in
`Assets/OffsetForge/Editor/OffsetForgeWindow.cs` — proven in this repo)
`Assets/VfxParade/Editor/VfxParadeWindow.cs` (Tools > VFX Parade):
- **Source folder** field (default `Assets/Spells Pack`) + **category filter** (substring: All / Casting /
  Projectile / Explosion / Aura / Buff / Shield / Slash / Hit / Death). Scan via `AssetDatabase.FindAssets
  ("t:Prefab", {folder})`, filter by path substring -> ordered prefab list.
- **Viewport** (PreviewRenderUtility): instantiate the current prefab at origin; **step its ParticleSystems
  each repaint** via `ParticleSystem.Simulate(accumTime, true, false)` (+ children) so particles actually
  animate in the editor window (the standard VFX-preview technique). Loop/restart on advance. Orbit/zoom like
  the Forge. Light + neutral bg so nothing is black.
- **Transport:** Play/Pause auto-advance (interval field, DEFAULT 10s), Next, Prev. Show `[i / total]  <path>`
  as a large label.
- **Bookmark panel:** a **moment** selector (cast / hit / death / buff / projectile / aura / other) + a
  "Bookmark this" button. Appends `{ "path": "<asset path>", "name": "<prefab name>", "moment": "<tag>" }` to
  `Assets/VfxParade/vfx-picks.json` (create if absent; APPEND/UPSERT, never overwrite the list). Show a running
  count + a small list of current picks with a remove button. Self-contained JsonUtility.
- **Robustness:** a prefab with no ParticleSystem still shows (static mesh fx); a broken/missing prefab is
  skipped with an ASCII LogWarning (never hard-crash the parade). Null-safe throughout.

## 3. Output contract (what the AI consumes)
`Assets/VfxParade/vfx-picks.json` = `{ "picks": [ { "path", "name", "moment" } ] }`. The AI reads this and
wires each pick's prefab to its moment via the existing `VFXCatalogGenerator.Map` / `VFXType` (committed-pack
note: Spells Pack is gitignored/local — same local-reference pattern WO-504 already uses; VFX fall back to
procedural on a clean clone. Acceptable per the local-art policy; flag in the wire commit).

## 4. Acceptance
- Tools > VFX Parade opens, lists the folder's prefabs (category-filtered), parades them with particles
  animating + an index/path label, auto-advances at the set interval, and Bookmark writes tagged picks to
  vfx-picks.json. Editor-only; gate-clean (compiles). The owner runs it, picks, hands off the JSON.
- BONES vs FINESSE: the browser + sim + bookmark file are CLI gate-provable; which effects are "best" is the
  owner's visual call (the whole point).

## 5. Do NOT
Reference Mirza Beig in any WIRING that ships (gitignored, breaks clone) — the parade may PREVIEW any local
folder, but only wire what the owner picks + accept the local-reference caveat. No runtime changes (editor
tool only). Don't auto-wire from the picks file — the AI reviews + wires deliberately.
