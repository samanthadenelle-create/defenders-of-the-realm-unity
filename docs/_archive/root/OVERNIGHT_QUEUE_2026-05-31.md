# OVERNIGHT QUEUE — 2026-05-31 (late eve, refreshed)

Decision-free, parallel-safe work to run unattended. Owner-gated items quarantined at the bottom.
Pull by lane; lanes don't collide. **One hard rule: do NOT bake `Village.unity` unless the village lane is yours + owner-authorized.**

## 🟡 IN FLIGHT NOW (do NOT re-queue)
- **Freeze-2 village rework bake** (CLI) — stone bridges + ~12m moat + flat water + fixed stairs/ramparts + sole-driver camera. **Baking now** (BuildVillage ✓ → navmesh → build).
- **WO-169 ATB party-of-4 FF screen** (CLI) — core P0+P1 landed (party + ControlMode + dynamic HUD + FF layout + Skills/Item/targeting); **gating now** (golden RNG test → commit).

## ✅ Shipped today (don't re-open)
World/terrain 173 · biomes 142 · gates 158/168 · stairs-v1 166 · refactor 181 · hero anim 174 · death DEF-102 · HUD compass+timer DEF-104 · pet 187/184 · **dragon hittable + lose 125/132** · city populate 189 + **district redo** · canon 182 · zone 164 · backend 80 · **wallet unify 131** · **worker auto-collect 117** · **backend save-auth 120 (server `e572391` + client signing)** · **camera deoccluder→sole-driver 156** · **WebGL size-opt Phase 0 191** · freeze-2 village (bridges/water/stairs/camera, baking).

## 🟢 OVERNIGHT QUEUE — ready, no owner decision (run by lane, in parallel)

**Combat / AI**
- **WO-170** — 2D retro battle VFX/anim (pairs with the just-built 169 FF screen).
- WO-145 / 146 / 147 — advanced enemy tactics + formation + perception (squad/raid foundation).
- WO-135 — P1 bug cluster (verify what's actually left after today).

**Economy / Build**
- ⭐ **WO-108 — player build-mode keystone — top priority, now fully unblocked** (131 done; freeze-2 lane releasing). The core CoC base-building loop. Touches the village system → run only when the village lane is free + authorized.
- WO-115 offline harvest accrual (builds on 117's `ActiveAssignments()` seam) · WO-172 build timers + ad speedup.

**World / Expedition** (code only — may bake `OuterWorld`, must NOT bake `Village`)
- WO-153 world crystal mine · WO-155 region enemy spawning · WO-159 settlements · WO-160 wandering tribes.
- WO-112 expedition foundation — gated danger maps + ward-tether (`DESIGN_CORE_LOOP_AND_STRUCTURE.md` §5d).

**Content / Social**
- WO-116 NPC dialogue/barks (Avalon purged). Also **remove/decide the gate "wards/force-field" bark** (see owner-decision below).
- WO-129 leaderboard/profile/social endpoints (v2 §3.3, per WO-120 LATER set).

**Polish / UI / Audio**
- WO-163 console-error cleanup · WO-185 hero→pet-select screen · WO-175 store polish · WO-178 health-bar styling.
- WO-162 music selection · WO-171 battle/overworld themes.

**WebGL / Deploy**
- WO-191 Phase 1 — mesh decimation (incl. `pet-aether-twilight.fbx` 91MB) + Cathedral.png dead-path (now UI's freeze lane) + Addressables streaming toward ~20MB initial; re-measure.
- Fresh WebGL build on the green tree → itch.io (`DEPLOY_WEBGL_ITCH_GUIDE.md`).

**Characters**
- **WO-190 CharacterFactory harness** — process the owner's roster (`Downloads/Models`: 4 Human heroes + 3 Orc enemies) once decimated: import → URP material + basecolor → Generic animator from own walk → register hero/enemy. (Owner decimates in Blender; CLI wires.) *Renumber off the 190 collision with `webgl_rebuild`.*

## 🔴 HELD — needs OWNER decision before building (NOT overnight)
- **NEW — Crystal currency (WO-131 follow-up):** wave rewards land in `AetherCrystals`, build-spend in `Resources.Crystals` — two stores. Pick: (a) waves → `Resources.Crystals` [waves fund building], (b) merge the two fields [big blast radius], (c) keep two, clearly labeled. *Rec: (a) if waves are meant to pay for towers.*
- **NEW — Gate "wards"/force-field visual:** keep the purple ward over each gate (the "wards hold steady" bark) or drop it for clean open archways?
- **NEW — Backend go-live (owner action):** `npm i tweetnacl bs58` → run `api/schema.sql` in Neon → deploy → **rotate the exposed Neon credential**. Then flip `BackendAuthConfig.Enforced` when a real MWA signer lands.
- **Expand vs tight village walls** (±28/±21 vs ±32/±24) — `DESIGN_VILLAGE_DISTRICTS.md`. Gates Commerce T4 quarter + final district coords.
- **Commerce upgrade-tier values** (what unlocks at each seat tier).
- **Expedition map specifics** (which regions are gated maps, haul-loss tuning).
- **Freeze-2 playtest findings** → freeze 3.

## ⚠️ Notes for CLI
- **WO-190 number collision:** two files share it (`webgl_rebuild` + `orc_necromancer_enemy`/CharacterFactory). Renumber one to a free WO.
- World-content WOs may bake `OuterWorld` — fine — but never `Village.unity` unless the lane is yours + authorized.
- Mount-sync is active: **validate any UI-authored file (manifest, builder `.cs`) on Windows** (NUL/truncation/brace) before baking.
- Leftover `DrawbridgeController.cs` is orphaned (0 refs) — safe to delete in a cleanup pass.
