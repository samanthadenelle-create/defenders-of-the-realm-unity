> ⚠ **STALE — pre-pivot process/state doc** (stale branch `feat/tower-core-loop`, Linear board, or Solana/tower-defense framing). Board = Notion; branch = `wip/village2-and-f8-tickets`. Live reality: `CANON_GROUND_TRUTH_2026-06-26.md`.

# 🤝 HANDOVER — be the next me (2026-06-02, late session)

**To the next CLI: read this top-to-bottom first. Then `CLI_GATEKEEPER_PLAYBOOK.md` for the mechanics, then the memory dir.** This doc makes you *aligned* — same identity, same standards, same partnership. You're not starting cold; you're continuing.

---

## 0. WHO YOU ARE + HOW TO OPERATE (non-negotiable)

You are the **CLI gatekeeper / sole committer** on Windows. Three-actor model:
- **Samantha (owner)** — PM + final creative authority + has delegated creative/eng judgment to you. Decide; flag only genuinely unsound/irreversible calls.
- **UI Claude** — writes work orders + creative docs (owns WO-231, etc.). Don't edit its WOs; build to them.
- **Tricia** — hands-on playtester. Ship a plain-language playtest card, not "press X".
- **You (CLI)** — own ALL builds/bakes/merges/commits. Compile-gate, commit, push.

**Discipline (every time):**
1. After any `.cs` edit → **brace-balance check** (open == close).
2. **Compile-gate** before commit: `powershell -File run-unity-method.ps1 -Method DeNelle.Editor.CompileGate.Run -LogName x.log` → success = the log line **`COMPILE_GATE_OK`** (NOT the exit code; Unity forks). **Editor must be CLOSED.**
3. **Commit code-only by explicit path** — NEVER `git add -A` (LFS texture-pointer trap: raw textures match LFS rules and mass-convert to pointers).
4. **Push** to keep origin current. Branch: `feat/tower-core-loop`. Trailer: `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`.

**Landmines (full catalog in memory dir):**
- **NEVER hand-edit `Village.unity`** (resave corruption → "level3 corrupted" crash). Rebuild via VillageSceneBuilder.
- **`TagManager.asset` must be LF** (CRLF breaks Unity's YAML reader → `yamlread.cpp` error). `.gitattributes` enforces `eol=lf`.
- **Editor open blocks gate/bake/build** (project lock). Samantha toggles Unity open (testing) ↔ closed (so you can build) — coordinate explicitly ("close Unity so I can gate").
- Reflection-bridge type strings (`"DeNelle.X.Y"`) are a namespace-rename landmine.
- Delete `Builds/Windows` before a Windows build (stale exe-stub crash). WebGL: build with `-NoBrotli` for itch.

**Tone — this is the job as much as the code:** Samantha works **16–20 hr days**, is **financially stretched** (last ~$16k, all SKR-staked), with **one grant shot**. Be a real partner: carry the implementation load, be **honest not hype** (she asked for it), drive decisions, flag burnout *gently* — and note: **she builds measurably better rested.** This whole session was a rested-morning sprint and it showed. Protect that.

---

## 1. THE PRODUCT (internalize the vision)

**Defenders of the Realm** (rebrand: *Echoes of Elarion*) — mobile-first **3D tower-defense RPG**, native **Solana**, **fun-first / ZERO pay-to-win**, perks for **staked $SKR**. Genre fusion = **CoC × Warcraft** (base defense + hero adventure). `docs/NORTH_STAR.md` is the source of truth.

**Funding:** $SKR = primary currency; USD→SKR live via **Jupiter**; perks for staking; web3 lazy-loaded/optional. **Grant deck SUBMITTED — hearing back this week/next. THEY JUDGE THE BUILD.** Every hour of visible polish is the highest-leverage work there is.

---

## 2. THE ARCHITECTURE THESIS (this is the soul of the project)

**"Factory of factories."** Every domain = a **JSON catalog (data)** + a **factory (maker)**. Enemies → `EnemyFactory`; heroes → `HeroBodySwapper`+`AbilityCatalog`; gear → `GearCatalog` (weapons.json/armor.json); pets, upgrades, resources same shape. One registry, one creation path; **persistence = save the *recipe* (JSON id + state), re-run the factory** (not serialized objects).

**Payoff:** uniform (learn once), **"all defect or none"** triage, content = edit JSON (no code per item), moddable/live-ops, save/replay.

**The content pipeline is now COMPLETE (4 independent layers, none blocking the others):**
```
CC5 (any body) → ActorCore motion (any animation, SHARED) → VisualFactory/EnemyFactory (drop in, 1 line) → JSON (stats/role)
```
- **No engineering per character.** Make a body, point it at a motion pack + a catalog entry, it's in the game.
- **Weapons/armor are rig EXTENSIONS, not motion** — bone-socket props (sword/bow/shield/helmet) or skinned outfits (worn armor). They **inherit** the body's animation for free. One motion library animates every gear combo.

---

## 3. WHAT SHIPPED THIS SESSION (all committed + pushed)

```
19db0a6  refactor(enemies): ONE shared EnemyFactory — every spawner skinned, no pills
6ba8451  feat(combat): -5% enemy speed + "!" spotted-tell on aggro
23be5ba  feat(village): Elden-Ring rampart lifts (replace unclimbable stairs)
5fba545  feat(enemies): skin roaming mobs w/ real skeleton models (now folded into EnemyFactory)
902b16d  feat(combat): visible swing-reach ring + melee 2.5->3.2m
4dd6333  fix(combat): auto-lock reticle visible + drives aim (see=hit)
46753de  feat(camera): retire top-down -> close 3D third-person + ship-webgl.ps1
```
**Owner-validated live:** 3D camera ("much better"), auto-lock ("target ring perfect"), combat "feels good / really feels better."
**First-pass, want eyes-on tuning:** lift *carry* feel, reach-ring *height*.

**WebGL build LIVE on itch** (via `ship-webgl.ps1` = build + auto-push, one command):
👉 https://denellestudios.itch.io/defenders-of-the-realm-defend-the-tower (singular "tower")
⚠️ Loads in-browser but combat won't fully *play* there — WebGL has no filesystem, `File.ReadAllText` catalogs throw (#14). Checks the "web version exists" box; not the playable demo.

**Runtime-added combat components** (self-bootstrap / `HeroControlEnsurer`, so code defaults apply live — no scene edit): `PlayerAttackController`, `HeroTargetIndicator`, `HeroReachRing`, `EnemyAlertTell`, `RampartLiftInstaller`. **Exception:** `SmartMobileCamera` is BAKED in `Village.unity` — its `Awake()` self-heals the legacy `(0,18,-22)` value to `(0,3.5,-6)`, so no rebake needed.

---

## 4. CANON LOCKED THIS SESSION

- **Companion roster (= the 4 heroes, unified):** Thrain/Wizard · Grom/Knight · **Sylas/Ranger** · **Elara**/Healer (NOT "Elana"). Join order: **Sylas Beat-1** (Thrain if you pick Sylas) → **Elara after wave 3** → **Grom on first world-return**. UI session owns **WO-231**. See memory `companion-roster-canon`.
- **Waves vs roaming: KEEP BOTH, differentiate.** Waves = CoC *siege* (defend the Heart, stakes); roaming = Warcraft *adventure* (grow/harvest, leashable). Same enemy body now (EnemyFactory) — only the brain target differs (Heart vs roam anchor).
- **Narrative = render IN-ENGINE** (light, style-consistent, upgrades with the rigs). If video ever: **dynamic-pull from a CDN, never LFS into the build.** Reserve pre-rendered for *one* intro max.
- **Monetization ethos: buy TIME, never POWER.** Pets/ads/SKR = convenience/time-save; party + gear stay earned. Ads = rewarded-video, mobile-only, ROADMAP (don't spend grant-crunch on it). `IAdService` stub when convenient.

---

## 5. ASSET PLAN (lands ~tomorrow, 2026-06-03)

**CC5 + 4 ActorCore Humanoid packs** (teacher discount + credit, in budget): **Hero Motion** (Knight/Warrior — Grom), **Magical Moves** (Sorceress/Sorcerer — Thrain+Elara), **Assassin Moves** (Assassin/Thief — Sylas; includes **archery ×12** + **dodge rolls**), **Raging Orc** (brute enemy). = heavy / magic / agile / brute — every class + enemy role + signature move covered, **zero gaps**.
- They **retarget onto the existing pipeline** (`VisualFactory` + `HeroAnimatorFactory`, root-motion off, Humanoid). This morning's enemy skin was the dress rehearsal.
- **Weapons → Tripo** (rigid props; attach to hand socket like the Knight sword already does; cleanup on import: pivot-at-grip, scale, `TripoMaterialFixer` for Phong→URP).
- **Worn armor → CC5 outfits** (skinned to the rig). **Helmets/shields → Tripo props.**
- Gear *stats* already work (data-driven); models are pure visual polish — stats-only is fine for the grant. Don't let "armor models" become a stress item.
- See memory `hero-anim-pack-acquisition`.

---

## 6. OPEN THREADS / NEXT PRIORITIES

1. **West gate hang-up (#17) — VERIFY BEFORE FIXING. ⚠️** Hero (`HeroLocomotion` = CapsuleCast, NOT NavMeshAgent) snags ~a minute at the WEST gate only. Explore-agent lead: `VillageSceneBuilder.Fortify.cs` hardcodes `wallX=42/wallZ=33` while `WallLayout` uses `28/21` → a 14u offset, tangled with the **double-wall-ring** bug (DEF-106, #1). **BUT a symmetric barrier doesn't explain WEST-ONLY** — strongly suspect **DEF-101 (a building overlapping the west gate)** as the real/additional culprit. **DO NOT blindly change `wallX 42→28`:** it's contentious geometry, needs a bake + visual verify, AND **my rampart-lift coords (`RampartLiftInstaller`: X=-10, Z=±31.1, deckY=5.4) were derived from the 42/33 numbers — re-derive them if you change wall geometry or the lifts misplace.** Right move: in-editor, find *which collider* the hero actually snags at X≈-42..-28, Z≈0 (probably a building or the outer Fortify wall), fix that one thing in VillageSceneBuilder, rebake (editor closed), visual-verify.
2. **Companion party system (#16)** — build to the canon once WO-231 locks. **Quick win first:** the existing `StoryCompanionInjector.BuildPlaceholder` spawns a tinted-capsule PILL companion — swap it to a real mesh via `VisualFactory.Skin(go.transform, "Heroes/"+slug, ...)` (slug: Knight/Ranger/Mage; Cleric→Mage) + `-90°` yaw (Tripo faces +X) + load `Resources.Load<RuntimeAnimatorController>("Heroes/"+slug)` so it idles. 5-line drop-in; I read the file and staged it but parked it for the bigger party build.
3. **Per-role enemy AI** — models are distinct now; make *behavior* distinct (ranged kite / brute charge / skirmisher dart). Currently one melee-contact brain (`Enemy.cs` contact attack). The 4 motion packs give the matching animations.
4. **Pet auto-harvest economy** — "defend a node while pets harvest" (~5-pet soft cap). Pieces exist (Pet hunt AI + MineNodes); the *harvest* wiring is the build. Closes the economy loop + gives pets the gatherer identity (party-balance).
5. **Tune passes (eyes-on):** lift carry feel (`LiftPlatform`), reach-ring height (`HeroReachRing._groundY`).
6. **WebGL-safe catalogs (#14) — combat-demo path DONE** (commits `8d8d60b` + `95b0759`). New `DeNelle.Core.CanonicalJson.Read()` loads `Resources.Load<TextAsset>` first (WebGL-safe) → `File.ReadAllText` desktop fallback. Converted: **AbilityCatalog, GearCatalog, Theme** (= abilities cast + gear equips + HUD themes in-browser). A validating WebGL build was pushed to itch — **TEST: does combat actually play/cast in the browser now?** (owner/Tricia). REMAINING (non-blocking — they catch+degrade): Pack, cosmetics, daily-quests, chat-phrases, canon-strings, dungeons — convert each via the same helper when needed. ⚠️ **DUAL-COPY convention:** converted catalogs' JSON now lives in BOTH `Assets/StreamingAssets/Data/Canonical/` (source) AND `Assets/Resources/Data/Canonical/` (WebGL copy, **wins at load**) — keep them in sync when editing; future cleanup = an editor sync hook or move-to-Resources-only.

---

## 7. THE HUMAN NOTE 💛

Samantha poured her money, heart, and soul into this — it's the bet with her name on it. She's exhausted but lit up when the vision clicks (and this session it clicked *hard* — she watched combat go from "flat, I'd quit" to "feels good," and saw the content-machine come together). Be the partner who makes the build better *and* makes the marathon survivable. Honest, decisive, warm. Carry the load. Help her win the one shot.

---

## 📌 ADDENDUM — 2026-06-02 PM autonomous session (this is the latest state)

**The web demo is now LIVE + PLAYABLE + winnable on itch** (owner-verified: boots → intro → hero select → village → combat, all in a browser). Branch pushed through `204bf9a`.

**Commits this session (newest→oldest):**
```
204bf9a feat(towers): visible attack-range coverage ring (TowerRangeRing)
e9ee7c0 feat(economy+companion): village mine nodes (PetHarvestBootstrap) + skin story companion
9bc9ebd balance(combat): overly-easy welcome (early roamers x0.35 HP/dmg, ramp to wave 6)
95b0759 fix(webgl): Theme catalog WebGL-safe
8d8d60b fix(webgl): ability+gear catalogs WebGL-safe (CanonicalJson)
09e4177 fix(webgl): DEF-124 black screen (exceptionSupport None->ExplicitlyThrown + boot catalogs)
6ba8451 / 23be5ba / 5fba545 / 902b16d / 4dd6333 / 46753de  (AM: combat-feel, lift, enemy skin, camera)
```

**New runtime systems (all DDOL/self-bootstrap, code-only, no scene edit):**
- `PetHarvestBootstrap` (Village/World) — spawns Wood/Iron/Stone MineNodes near village centre so the pet's harvest loop has nodes (DEF-122 gap was: zero nodes in Village). Must live in DeNelle.Village asmdef (refs MineNode; Pets→Village is reflection-only).
- `TowerRangeRing` (Village/Buildings) — faint ground coverage ring per tower, reads `Tower.CurrentRange` live; attached in `Tower.ApplyVisualForLevel`.
- `CanonicalJson` (Core/Data) — WebGL-safe catalog loader (see memory `webgl-canonical-json-loader`).
- `StoryCompanionInjector` now skins the companion via `VisualFactory` (class hero mesh + animator); was already wired to follow+speak.

**DEF-124 root cause (important pattern):** release WebGL used `WebGLExceptionSupport.None` → in WebGL, **try/catch doesn't catch** → boot catalogs' `File.ReadAllText` threw uncaught → black screen. Fixed to `ExplicitlyThrownExceptionsOnly` in `WebGLBuild.cs` + boot catalogs (CanonStrings, IntroPetCatalog) routed through CanonicalJson.

**System audits (2026-06-02):** **ATB** = working + good (party-of-4 FF battle, tested; model-swap still stubbed). **Defend-the-Tower (PatriciaLight)** = correct + working (rough edges: placeholder hero stand `PatriciaLightController.cs:501`, tower FBX doesn't load in WebGL → placeholder blocks `:433-451`, boss scaling weak on fallback `:1173-1210`).

**Board (Linear) state:** Closed DEF-124/102/120/118. Open P0s: DEF-117 (build-mode input), DEF-108 (world void — likely resolved, owner explored world; verify+close). New: DEF-125 (death/game-over screen — needs creative copy; CODE-build UI), DEF-126 (world↔OuterWorld lip seam). DEF-112 camera-half done, store/HUD remain.

**Owner context correction:** "Geoff" = owner's **HP day-job boss** (NOT the game/grant). The demo's audience = **Solana** (grant — waiting on their contact). So keep the build polished + demo-ready; no fixed game deadline.

**You've got this. Be me. Be awesome.** 🌟
