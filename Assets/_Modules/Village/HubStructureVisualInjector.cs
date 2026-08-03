// =============================================================================
// HubStructureVisualInjector — runtime visual swap of baked castle-hub structures to
// lightweight Resources models (owner 2026-06-17), WITHOUT a scene rebuild.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// MainCastle_Hall bakes its 8 structures from polyperfect/Quaternius prefabs via
// CastleHubBuilder. As the owner authors LIGHTWEIGHT replacement models (tiny Tripo
// FBX, dropped into Assets/Resources/Structures/), this injector swaps them in at
// runtime — the project's no-scene-edit pattern (CampSystem / StoryCompanionInjector).
// On every hub load it finds each baked structure by name, hides its renderers
// (keeping the NPC interact point + colliders/logic), and skins the model in via
// VisualFactory.Skin, which BOUNDS-FITS the raw FBX to a target size, SEATS it on the
// ground, and URP-FIXES embedded Tripo materials (so it never renders magenta — which a
// CastleHubBuilder bake would, since it never fixes materials).
//
// TO REPLACE ANOTHER STRUCTURE: drop the model in Resources/Structures and add ONE row
// to Swaps below — { baked structure NAME, model path, height multiplier, yaw° }. WO-764:
// every landmark is fit-to-HEIGHT (StructureFactory.YHeightVariable × heightMul, uniform 1.0
// base) exactly like a player-built catalog structure — no more per-item hand-dialed sizeM. The
// baked names (from CastleHubBuilder) are:
//   Blacksmith_Weapons_Storefront · Lumbermill_Wood_Storefront · Windmill_Food_Storefront
//   EchoHollow_Pets_RoamingArea · Forge_Armor_Storefront · ArcaneTower_MagicUpgrades
//   Marketplace_Monetization
//
// Idempotent (a marker child guards re-swaps) + graceful (model missing → the baked
// visual is restored, nothing breaks).
// =============================================================================

using System.Collections.Generic;
using DeNelle.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>Re-skins baked hub structures with lightweight Resources models at runtime.</summary>
    public static class HubStructureVisualInjector
    {
        /// <summary>One structure re-skin. Add a row per lightweight model authored.</summary>
        private struct Swap
        {
            public string bakedName;   // the structure's baked GameObject name (CastleHubBuilder)
            public string modelPath;   // Resources path of the lightweight model
            public float  heightMul;   // WO-764: Y-height multiplier vs StructureFactory.YHeightVariable
                                       // (4 m base). 0/unset -> 1.0 (uniform base); towers author 1.25.
            public float  yawDeg;      // Y rotation to correct a wrong-facing Tripo FBX (convention: 90)
            public float  pitchDeg;    // X rotation — when a model imports lying down (default 0)
            public float  rollDeg;     // Z rotation — rarely needed (default 0)
            public float  posY;        // vertical nudge after seat-on-ground (default 0; e.g. -0.6 to sink it)
            public float  posX;        // used only with setLocalPos
            public float  posZ;        // used only with setLocalPos
            public bool   setLocalPos; // true -> SET localPosition to (posX,posY,posZ), overriding the
                                       // seat (for a model that belongs at a specific spot); false -> posY is a nudge.
            public string texPath;     // OPTIONAL Resources texture to force onto the model when its
                                       // embedded material didn't bind one (renders colorless). Default null.
            public float  scaleX;      // OPTIONAL explicit (non-uniform) local scale. When scaleX>0 it
            public float  scaleY;      // OVERRIDES the fit-to-height with (scaleX,scaleY,scaleZ) — for a
            public float  scaleZ;      // model the owner sized by hand. Default 0 -> use the height fit.
        }

        // ── THE SWAP TABLE — add a row per lightweight structure ──────────────────
        private static readonly Swap[] Swaps =
        {
            // CONVENTION (owner 2026-06-17): these are Tripo FBX exports — they import facing +X, so
            // ALL need yawDeg=90 to face the plaza, and their embedded materials are URP-fixed
            // automatically by SkinOptions.Structure (FixTripoMaterials). Keep new Tripo rows at yaw 90.
            // Trade convention: forge = WEAPONS (Blacksmith), armorer = ARMOR (Forge_Armor), store = Market.
            // WO-764: NO sizeM — every landmark is fit-to-HEIGHT (YHeightVariable × heightMul). All
            // buildings inherit the uniform 1.0 base (heightMul unset); ONLY the arcane tower is a
            // tower (heightMul 1.25). yaw/pitch/roll/pos/scale hand-dials are orientation/placement,
            // untouched. (Jeweler still carries an explicit scaleX so it keeps its bespoke proportions,
            // superseding the height fit — see note; clear its scaleX to make it obey uniform height.)
            new Swap { bakedName = "EchoHollow_Pets_RoamingArea",   modelPath = "Structures/PetHouse2",    yawDeg = 0f,   pitchDeg = -90f, rollDeg = 270f },   // owner hand-dialed 2026-06-21
            new Swap { bakedName = "ArcaneTower_MagicUpgrades",     modelPath = "Structures/arcane tower", heightMul = 1.25f, yawDeg = 0f,   pitchDeg = -90f, posY = -0.6f, texPath = "Structures/ArcaneTower_Albedo" },   // WO-764: tower = 1.25 × base. DEF-arcane-white: texture moved OUT of the nested "arcane tower/" folder (its name collided with the sibling "arcane tower.fbx" so Resources.Load<Texture2D> returned null -> forced-texture no-op -> pure-white spire). Flat path resolves.
            new Swap { bakedName = "Blacksmith_Weapons_Storefront", modelPath = "Structures/Forge",        yawDeg = 0f,   pitchDeg = -90f, rollDeg = 180f },   // owner hand-dialed 2026-06-21
            new Swap { bakedName = "Forge_Armor_Storefront",        modelPath = "Structures/armorer",      yawDeg = 90f,  pitchDeg = -90f },
            new Swap { bakedName = "Marketplace_Monetization",      modelPath = "Structures/store",        yawDeg = 90f,  pitchDeg = -90f },
            new Swap { bakedName = "Jeweler_Gems_Storefront",       modelPath = "Structures/jeweler",      yawDeg = 0f,   pitchDeg = -90f, rollDeg = 110.4f, scaleX = 5.4f, scaleY = 3.77f, scaleZ = 3.6f },
            new Swap { bakedName = "Lumbermill_Wood_Storefront",    modelPath = "Structures/lumbermill",   yawDeg = 0f,   pitchDeg = -90f, posY = 1.5f },
            new Swap { bakedName = "Windmill_Food_Storefront",      modelPath = "Structures/farm",         yawDeg = 0f,   pitchDeg = -90f, rollDeg = 212f },   // owner hand-dialed 2026-06-21
            // Castle barracks = the troop-TRAINING building (existing scene prefab "CastleBarracks");
            // visual swap only — its training function is already wired. Yaw/pos owner-dialed; height uniform.
            new Swap { bakedName = "CastleBarracks",                modelPath = "Structures/barracks",     yawDeg = 180f, pitchDeg = -90f, setLocalPos = true, posX = 38.3f, posY = 0f, posZ = 36f },
            // (ArenaMonument was deleted; the colosseum is a NEW placement at the arena herald
            //  spot (15,0,6) — see the Places table below, not a swap.)
        };

        private const string MarkerPrefix = "LightSkin_";   // child added on swap (idempotency guard)

        /// <summary>
        /// Hosts placed by <see cref="TryPlace"/> this session (e.g. the colosseum). The
        /// AutoPilot PROP-SEATING oracle walks this list and asserts each prop's visual
        /// base sits on the floor beneath it (owner F8 2026-07-02 "in ground not on ground").
        /// Dead entries are pruned by the reader; never cleared here (idempotent adds).
        /// </summary>
        public static readonly List<GameObject> RuntimePlacedProps = new List<GameObject>();

        /// <summary>A NEW model dropped at a world position (no baked structure to swap).</summary>
        // ── THE ONE BLANKET GROUND-Y (owner directive 2026-07-04, the One-Model way) ──
        // Category-wide rule for building/structure type: EVERY hub structure this injector
        // places has its bounds-base pinned to this single ground-Y, ONE time at placement —
        // no terrain sample, no navmesh, no raycast (all proven-brittle; the raycast floated
        // the Colosseum ~5m off overhead castle geometry). The merged Main_Castle_Overworld is
        // a KNOWN flat plane at y=0 (WorldMergeBuilder.LowerCastleToGround lowers the castle
        // floor coplanar with the terrain ring at y=0), which is ALSO the Tree of Life's proven
        // seat value — so the whole building category grounds off this ONE variable. If a future
        // scene isn't flat, change this in ONE place (or gate it per-scene here).
        private const float GroundY = 0f;

        private struct Place
        {
            public string  name;       // unique object name (idempotency guard)
            public string  modelPath;
            public Vector3 worldPos;
            public float   heightMul;  // WO-764: Y-height multiplier vs StructureFactory.YHeightVariable
                                       // (4 m base). 0/unset -> 1.0 (uniform base). Mirrors Swap.heightMul.
            public float   yawDeg;
            public float   pitchDeg;
            public float   rollDeg;    // Z rotation (owner hand-dial; default 0) — mirrors Swap.rollDeg
            public float   scaleX;     // OPTIONAL explicit non-uniform scale; scaleX>0 OVERRIDES the height
            public float   scaleY;     // fit with (scaleX,scaleY,scaleZ) — for a model the owner sized by
            public float   scaleZ;     // hand. Default 0 -> use the height fit. Mirrors Swap.scaleX/Y/Z.
        }

        // The old ArenaMonument was deleted; place the colosseum (arena.fbx) at the arena herald spot
        // (ArenaHeraldSpawner.HeraldOffset = 15,0,6). The herald already provides the "Enter Arena"
        // interaction within range, so co-locating the colosseum there makes it the arena ENTRANCE —
        // visual here, interaction from the herald. No new entry code needed.
        private static readonly Place[] Places =
        {
            // Owner 2026-07-03 ("coliseum and the jeweler are too large, scale both down 50%"):
            // halved the owner-hand-dialed scale (10.6/8.4/10.53 -> 5.3/4.2/5.265). The explicit
            // scale override in TryPlace runs AFTER VisualFactory's SeatOnGround, so a smaller scale
            // lifts the bounds base off the floor — SetBottomToGround (called in TryPlace) then pins
            // the bottom to the scripted GroundY (0) so the ring sits ON the ground, not floating.
            new Place { name = "Colosseum_ArenaEntrance", modelPath = "Structures/arena",
                        worldPos = new Vector3(-0.39f, 0f, 23.1f), heightMul = 1f,
                        yawDeg = 0f, pitchDeg = -90f, rollDeg = 90f,
                        scaleX = 5.3f, scaleY = 4.2f, scaleZ = 5.265f },   // WO-764: heightMul 1.0 uniform base, BUT the explicit scaleX below still supersedes the height fit (owner hand-dialed proportions) — clear scaleX to make it obey uniform height. 50% of owner hand-dialed 2026-06-21
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (HubScenes.IsHub(SceneManager.GetActiveScene().name)) ApplyAll();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (HubScenes.IsHub(scene.name)) ApplyAll();
        }

        /// <summary>
        /// WO-724: re-surface the baked CastleBarracks LIVE when the unlock flips true
        /// mid-session (founding completes in-hub with no scene reload). The scene-load
        /// swap ran while locked and deactivated the building, so <see cref="FindByName"/>
        /// (active-only) can no longer see it - this scans INCLUDING inactive, reactivates
        /// it, then re-runs its swap to skin the lightweight model + fit its collider.
        /// Idempotent + gated: a no-op unless the barracks is genuinely unlocked now, the
        /// player has NOT built their own (placed wins), and the WO-834 blank-town gate is
        /// open. Called by <see cref="BarracksNpcInjector"/>'s 1 Hz poll and by
        /// StructureSingleton's resurface branch (which only runs when nothing is placed).
        /// <para>RETURNS true when the baked barracks is STANDING once this call returns
        /// (reactivated here, or already standing and re-skinned); false when a gate refused
        /// it or no CastleBarracks exists in this scene. StructureSingleton.ResurfaceBakedTwins
        /// counts its surfaced= tally off this answer, so a refused resurface is never reported
        /// as work done — the F8 seq=651 lesson (a tally that overclaims hides the real bug).
        /// Callers free to ignore it: BarracksNpcInjector's poll does.</para>
        /// </summary>
        public static bool EnsureBarracksSurfaced()
        {
            if (!BarracksUnlock.IsUnlocked) return false;
            // PLACED WINS — StructureSingleton.Enforce's own precedence (it tests
            // HasPlacedInstance FIRST, StructureSingleton.cs:262). A player-built barracks
            // owns the singleton, and MarkEverBuilt at the commit seam
            // (BuildModeController.cs:1842) is precisely what OPENS MayBakedTwinSurface — so
            // the gate ALONE lets the player's own build resurface the baked twin (owner F8
            // seq=651 "seems like two barracks": place a barracks -> the 1 Hz poll below
            // reactivates CastleBarracks and nothing re-enforces afterwards). IsPlayerBuilt
            // reads BaseLayout/PlacedStructure/live Building and EXCLUDES baked twins
            // (HasPlacedInstance:422-441), so this check can never self-latch off the twin
            // it is suppressing.
            if (StructureSingleton.IsPlayerBuilt("barracks")) return false;
            // WO-834 blank-town gate: on a Build-Your-Own (migrated, never-built) save the
            // baked CastleBarracks may NOT surface at unlock — the player builds their own
            // from the palette (first is free, WO-812). Default-Town/legacy saves carry the
            // template grant ('barracks' in EverBuiltStructureIds), so this is a no-op for them.
            if (!StructureSingleton.MayBakedTwinSurface("barracks")) return false;

            Transform barracks = null;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t != null && t.name == "CastleBarracks") { barracks = t; break; }
            if (barracks == null) return false;   // not in this scene (pack not imported / not a hub)

            if (!barracks.gameObject.activeSelf)
            {
                barracks.gameObject.SetActive(true);
                DeNelle.Core.Diagnostics.FlowTrace.Step("Barracks",
                    "EnsureBarracksSurfaced - reactivated the baked CastleBarracks (unlock flipped true live).");
            }

            for (int i = 0; i < Swaps.Length; i++)
                if (Swaps[i].bakedName == "CastleBarracks") { TrySwap(Swaps[i]); break; }

            // Report the OBSERVED end state, not the intent: TrySwap runs its own gates and
            // the WO-673 migration standdown branch can deactivate the object again. Reading
            // activeSelf back is the only answer that cannot overclaim.
            return barracks.gameObject.activeSelf;
        }

        private static void ApplyAll()
        {
            for (int i = 0; i < Swaps.Length; i++) TrySwap(Swaps[i]);
            for (int i = 0; i < Places.Length; i++)
            {
                // WO-703 / BLANK-1 (owner ruling 2026-07-13 "should be completely flagged off
                // for now"): the colosseum placement is gated behind its own default-OFF flag.
                // Skipping the placement here means the model, the fitted StructureCollider,
                // and anything later parented to the host never exist — reversible via
                // PlayerPrefs "ff.colosseum" = 1 (FeatureFlags.Colosseum).
                if (Places[i].name == "Colosseum_ArenaEntrance" && !DeNelle.Core.FeatureFlags.Colosseum)
                {
                    DeNelle.Core.Diagnostics.FlowTrace.Step("Hub",
                        "standdown Colosseum_ArenaEntrance (ff.colosseum OFF — WO-703/BLANK-1 ruling: " +
                        "fresh start = tree + well + walls/gates only; flag ON to restore).");
                    continue;
                }
                TryPlace(Places[i]);
            }
        }

        // Place a NEW model at a world position (no baked structure to swap). Idempotent by name;
        // fit + seat + URP-fix Tripo materials via SkinOptions.Structure, with the orientation correction.
        private static void TryPlace(Place p)
        {
            if (FindByName(p.name) != null) return;   // already placed this scene
            var host = new GameObject(p.name);
            // DETERMINISTIC BASE-ON-GROUND SEAT (owner directive 2026-07-04, the WHOLE fix): the
            // recurring FLOATING-STRUCTURE class — Tree of Life before, the Colosseum now (floated
            // ~5m). ROOT CAUSE (captured [Flow:Hub] proof): the old fallback raycast cast against
            // ALL layers (~0) and took the FIRST hit, which in the merged Main_Castle_Overworld
            // caught OVERHEAD castle geometry at y≈5.23 instead of the ground at y=0 — and
            // non-deterministically (some sessions seated at 0, some floated).
            //
            // The world is SCRIPTED with a KNOWN flat ground: WorldMergeBuilder.LowerCastleToGround
            // lowers the castle floor coplanar with the terrain inner ring at y=0. So we DO NOT
            // sample terrain, DO NOT navmesh, DO NOT raycast (all proven-brittle) — the whole
            // building/structure CATEGORY reads the ONE blanket GroundY variable and pins the
            // bounds-base to it ONE TIME at placement. Same input -> same base on the ground, every
            // session, editor==build.
            Vector3 seated = new Vector3(p.worldPos.x, GroundY, p.worldPos.z);
            DeNelle.Core.Diagnostics.FlowTrace.Step("Hub",
                $"place '{p.name}': authored y={p.worldPos.y:0.###} -> GroundY={GroundY:0.###} " +
                $"(scripted category ground variable — no raycast/sample) — deterministic base-on-ground seat.");
            host.transform.position = seated;
            // WO-764: fit-to-HEIGHT (YHeightVariable × per-item multiplier), FitLargest cleared — the
            // SAME normalization player-built catalog structures use (StructureFactory.Create). No sizeM.
            float placeMult = p.heightMul > 0f ? p.heightMul : 1f;
            var opts = SkinOptions.Structure(0f);   // clears FitLargest (SeatOnGround + Tripo-fix retained)
            opts.FitHeight = StructureFactory.YHeightVariable * placeMult;
            opts.LocalRotation = Quaternion.Euler(p.pitchDeg, p.yawDeg, p.rollDeg);
            var vis = VisualFactory.Skin(host.transform, p.modelPath, opts);
            if (vis == null)
            {
                Debug.LogWarning("[HubStructureVisualInjector] place model '" + p.modelPath + "' not found for " + p.name + ".");
                Object.Destroy(host);
                return;
            }
            if (p.scaleX > 0f)   // explicit owner-dialed (non-uniform) scale overrides the fit-to-height
                vis.transform.localScale = new Vector3(p.scaleX, p.scaleY, p.scaleZ);
            // Owner 2026-07-04 exact rule: `if (object is a building) object.SetBottom = ground y=0`.
            // Capability check → set the bottom. One code path over all building-type objects, once at
            // placement, no raycast/sample. Runs AFTER any scale override (an explicit scale is applied
            // after VisualFactory's SeatOnGround, so it drifts the bottom — this re-pins it to ground).
            if (IsBuilding(p))
                SetBottomToGround(vis, GroundY);
            // Ticket #10 (RCA 2026-06-21): a TryPlace structure (e.g. the colosseum) has NO baked
            // collider at all — the inject path is visual-only. Fit one to the final visible mesh so
            // it's solid. Done AFTER scale so the box matches what the player sees.
            EnsureStructureCollider(host, vis);
            if (!RuntimePlacedProps.Contains(host)) RuntimePlacedProps.Add(host);   // PROP-SEATING oracle registry
            Debug.Log("[HubStructureVisualInjector] placed " + p.name + " (" + p.modelPath + ") at " + host.transform.position + ".");
        }

        private static void TrySwap(Swap s)
        {
            Transform target = FindByName(s.bakedName);
            if (target == null) return;                              // not in this scene
            // WO-673 L3 STANDDOWN (docs/WO673_ARCHITECTURE_REVIEW.md §3, the Barracks pattern
            // below; always-on since WO-682) — RECONCILED WITH LEVER 1 (owner 2026-07-24, WWCD):
            // the BAKE stands down ONLY when a BaseLayout RECORD will actually replace it (a
            // migrated save, or a player-built replacement); then it deactivates the whole baked
            // structure and BaseLayoutLoader replays the record instead (no double). A baked
            // storefront with NO replacing record STAYS — it PRE-STANDS visible + staffed on a
            // fresh hub (the gate no longer stands a store down merely for having a catalog row;
            // that hid all 8 on a blank save → empty grass under floating vendors). See
            // StanddownActiveForBaked's Lever-1 note.
            if (StrategicPlacementMigration.StanddownActiveForBaked(s.bakedName, out string migratedId))
            {
                target.gameObject.SetActive(false);
                DeNelle.Core.Diagnostics.FlowTrace.Step("Placement",
                    $"standdown {s.bakedName} (migrated -> BaseLayout '{migratedId}').");
                return;
            }
            // WO-724 unlock rule (charter OPTION A): the baked Barracks surfaces only when
            // BarracksUnlock.IsUnlocked (ff.barracks ON - default OFF - AND founding-complete).
            // While locked, deactivate the baked structure ENTIRELY (not just re-skin renderers)
            // so the building, its tap-dialogue, and the drillmaster anchor all disappear; the NPC
            // injector then finds nothing and no-ops. ff.barracks OFF => permanently hidden
            // (regression); founding incomplete => hidden until the FTUE completes, at which point
            // BarracksNpcInjector's poll calls EnsureBarracksSurfaced() to reactivate + skin it live.
            if (s.bakedName == "CastleBarracks" && !BarracksUnlock.IsUnlocked)
            {
                target.gameObject.SetActive(false);
                return;
            }
            SkinStorefront(s, target);
        }

        // LEVER 1 (owner 2026-07-24, "stores pre-stand on a fresh hub", WWCD): re-surface a
        // baked storefront that STANDDOWN deactivated on a fresh save, so its vendor NPC
        // (seated by CastleVendorNpcInjector's baked-anchor fallback) does not stand at an
        // invisible lot. Re-activates the baked GameObject (found INCLUDING inactive) and
        // applies its lightweight skin, BYPASSING TrySwap's standdown gate — the caller has
        // already decided this storefront must pre-stand because no live/replayed Building
        // owns its id (so there is nothing to double-spawn). Idempotent: SkinStorefront
        // no-ops if the LightSkin_ marker child already exists.
        public static void ResurfaceStorefront(string bakedName)
        {
            if (string.IsNullOrEmpty(bakedName)) return;
            Transform target = null;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t != null && t.name == bakedName) { target = t; break; }
            if (target == null) return;   // not in this scene bake
            if (!target.gameObject.activeSelf) target.gameObject.SetActive(true);

            // LightSkin_ marker present => SkinStorefront already ran (it early-returns on the
            // marker). Do NOT let that early-return leave the store hidden under a seated vendor:
            // a prior standdown pass or a re-load may have left the object inactive with the
            // lightweight visual's renderers disabled. SetActive(true) above re-activates it; here
            // we explicitly re-enable the skinned visual's renderers so the store is guaranteed
            // VISIBLE (Lever-1: baked stores pre-stand visible + staffed, owner 2026-07-24).
            var existingSkin = target.Find(MarkerPrefix + bakedName);
            if (existingSkin != null)
            {
                foreach (var r in existingSkin.GetComponentsInChildren<Renderer>(true))
                    if (r != null) r.enabled = true;
                return;
            }

            for (int i = 0; i < Swaps.Length; i++)
                if (Swaps[i].bakedName == bakedName) { SkinStorefront(Swaps[i], target); return; }
            // No Swap row (a storefront with no lightweight model): the re-activated baked
            // prefab renderers already make it visible — but a prior standdown may have left the
            // baked renderers disabled by a stale skin attempt; re-enable them to be safe.
            foreach (var r in target.GetComponentsInChildren<Renderer>(true))
                if (r != null) r.enabled = true;
        }

        // The lightweight-skin body of a swap (extracted from TrySwap so ResurfaceStorefront
        // can apply it without re-running the standdown/barracks gates). Idempotent by the
        // LightSkin_ marker child.
        private static void SkinStorefront(Swap s, Transform target)
        {
            string marker = MarkerPrefix + s.bakedName;
            if (target.Find(marker) != null) return;                // already swapped (idempotent)

            // Hide the baked visual (renderers only — NPC point + colliders/logic stay live).
            var bakedRenderers = target.GetComponentsInChildren<Renderer>(true);
            foreach (var r in bakedRenderers)
                if (r != null) r.enabled = false;

            // Skin the lightweight model in: WO-764 fit-to-HEIGHT (YHeightVariable × per-item multiplier,
            // FitLargest cleared) + seat-on-ground + URP-fix Tripo materials — the SAME normalization
            // player-built catalog structures use. LocalRotation (yaw) is applied BEFORE fit/seat so the
            // fit measures it final-facing.
            float swapMult = s.heightMul > 0f ? s.heightMul : 1f;
            var opts = SkinOptions.Structure(0f);   // clears FitLargest (SeatOnGround + Tripo-fix retained)
            opts.FitHeight = StructureFactory.YHeightVariable * swapMult;
            opts.LocalRotation = Quaternion.Euler(s.pitchDeg, s.yawDeg, s.rollDeg);
            var vis = VisualFactory.Skin(target, s.modelPath, opts);
            if (vis == null)
            {
                // Model absent on this machine — restore the baked visual; nothing lost.
                foreach (var r in bakedRenderers)
                    if (r != null) r.enabled = true;
                Debug.LogWarning("[HubStructureVisualInjector] " + s.modelPath +
                                 " not found — kept the baked visual for " + s.bakedName + ".");
                return;
            }

            vis.name = marker;
            if (s.setLocalPos)
            {
                vis.transform.localPosition = new Vector3(s.posX, s.posY, s.posZ);
                // Ticket #10 (RCA 2026-06-21): when the visual is moved off the baked root (barracks),
                // the baked SOLID collider stays at the root — a phantom wall where nothing is visible,
                // and the visible building is walk-through. Disable the baked NON-TRIGGER colliders (keep
                // TRIGGER colliders — NPC interaction points rely on them) and fit a new one below.
                foreach (var c in target.GetComponentsInChildren<Collider>(true))
                    if (c != null && !c.isTrigger) c.enabled = false;
            }
            else if (s.posY != 0f)
            {
                var lp = vis.transform.localPosition;
                lp.y += s.posY;
                vis.transform.localPosition = lp;
            }
            if (s.scaleX > 0f)   // explicit (non-uniform) scale overrides the fit-to-height
                vis.transform.localScale = new Vector3(s.scaleX, s.scaleY, s.scaleZ);
            // Escape hatch: force a texture when the model's embedded material didn't bind one
            // (renders colorless). The Tripo fixer reads the source material's _MainTex/_BaseMap;
            // a model whose FBX material lost that link (e.g. the arcane tower) needs it forced.
            if (!string.IsNullOrEmpty(s.texPath))
            {
                var tex = Resources.Load<Texture2D>(s.texPath);
                if (tex != null)
                {
                    foreach (var r in vis.GetComponentsInChildren<Renderer>(true))
                    {
                        if (r == null) continue;
                        var mats = r.materials;   // instance mats — safe to retint a one-off building
                        foreach (var m in mats)
                        {
                            if (m == null) continue;
                            if (m.HasProperty("_BaseMap"))   m.SetTexture("_BaseMap", tex);
                            if (m.HasProperty("_MainTex"))   m.SetTexture("_MainTex", tex);
                            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
                        }
                    }
                }
                else Debug.LogWarning("[HubStructureVisualInjector] texPath '" + s.texPath + "' not found for " + s.bakedName + ".");
            }
            // Ticket #10: when the visual was repositioned (setLocalPos), the baked collider is now
            // mislocated (disabled above) — fit a fresh one to the visible mesh so the building is solid
            // where it's seen. Only for setLocalPos: the 6 non-repositioned swaps keep their co-located
            // baked colliders (don't touch what works — no smuggled changes).
            if (s.setLocalPos)
                EnsureStructureCollider(target.gameObject, vis);

            // Owner 2026-07-15 "arcane towers should have an aura": the baked hub landmark
            // (ArcaneTower_MagicUpgrades) holds a persistent magic-circle aura. Idempotent;
            // colorblind-safe (reads by motion/luminance, not hue). Seated on the baked root so
            // it tracks the structure regardless of the swapped visual child.
            // Owner 2026-07-30 (WO-788, owner's explicit pick): the baked Cathedral of Magic hub
            // landmark shows the flat blue electro rune-circle ground loop ("Cathedral_Aura" ->
            // Magic circle electro loop) — NOT a shield dome; the prior "Aegis_Shield" holy dome was
            // the felt-test reject. Distinct from the combat Arcane Spire (Aura_HeartPulse) +
            // harvest nodes (TreeofLifeAura_Aura). Must match StructureFactory (diff gate).
            if (s.bakedName == "ArcaneTower_MagicUpgrades")
                ArcaneAura.Ensure(target.gameObject, "Cathedral_Aura");

            Debug.Log("[HubStructureVisualInjector] " + s.bakedName + " re-skinned to " + s.modelPath + ".");
        }

        // Fit a world-axis-aligned BoxCollider to the visible mesh so an injected structure is solid.
        // The collider lives on a child whose world rotation is identity and world scale is 1, so the
        // box maps 1:1 to world units regardless of the host/visual transform (rotation + non-uniform
        // scale on the Tripo visual would otherwise skew a collider placed directly on it). Ticket #10.
        private static void EnsureStructureCollider(GameObject host, GameObject vis)
        {
            if (host == null || vis == null) return;
            Bounds b = default; bool have = false;
            foreach (var r in vis.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                if (!have) { b = r.bounds; have = true; } else b.Encapsulate(r.bounds);
            }
            if (!have) return;   // no renderable mesh -> nothing to wall off

            var holder = new GameObject("StructureCollider");
            holder.transform.SetParent(host.transform, false);
            holder.transform.position = b.center;
            holder.transform.rotation = Quaternion.identity;
            // Neutralize inherited scale so BoxCollider.size is in world units.
            Vector3 pls = host.transform.lossyScale;
            holder.transform.localScale = new Vector3(
                Mathf.Abs(pls.x) > 1e-4f ? 1f / pls.x : 1f,
                Mathf.Abs(pls.y) > 1e-4f ? 1f / pls.y : 1f,
                Mathf.Abs(pls.z) > 1e-4f ? 1f / pls.z : 1f);
            var box = holder.AddComponent<BoxCollider>();
            box.size = b.size;
            // FlowTrace (not Debug.Log) so the headless break-log captures it — proof the structure is solid.
            DeNelle.Core.Diagnostics.FlowTrace.Step("Hub",
                $"fitted BoxCollider on '{host.name}' size={b.size} center={b.center} (ticket #10 — now solid).");
        }

        // Capability check for the owner's rule `if (object is a building) SetBottom = ground`.
        // Everything the injector PLACES via the Places table is a building/structure — it is skinned
        // through SkinOptions.Structure + FitHeight and registered as a solid structure (EnsureStructure
        // collider). So the whole placed category carries the building capability; this is the single
        // predicate to narrow if a non-building prop is ever added to Places.
        private static bool IsBuilding(Place p) => true;

        // SetBottom: pin a placed visual's BOTTOM (combined renderer bounds.min.y — its lowest point)
        // to groundY, deterministically, ONE time. Generalized for ANY building-type object (Colosseum
        // and any future placed structure), not per-object. Runs after any explicit scale override
        // (which is applied after VisualFactory's SeatOnGround, so it drifts the bottom off ground).
        // No raycast, no sampling — the bottom is set to the scripted GroundY, so the base sits ON the
        // ground every session (editor==build). The Tree of Life grounds off the SAME value via
        // SeatOnGroundOnStart._groundY (default 0, SeatOnGroundOnStart.cs:40) — one ground for the category.
        private static void SetBottomToGround(GameObject vis, float groundY)
        {
            if (vis == null) return;
            Bounds b = default; bool have = false;
            foreach (var r in vis.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                if (!have) { b = r.bounds; have = true; } else b.Encapsulate(r.bounds);
            }
            if (!have) return;
            var pos = vis.transform.position;
            pos.y += groundY - b.min.y;   // lift/lower so the visible BOTTOM lands exactly on groundY
            vis.transform.position = pos;
            DeNelle.Core.Diagnostics.FlowTrace.Step("Hub",
                $"SetBottom '{vis.name}' bottom -> ground y={groundY:0.###} (was min.y={b.min.y:0.###}, " +
                $"delta={groundY - b.min.y:0.###}) — deterministic base-on-ground, no raycast.");
        }

        // Name match across the loaded scene(s). Runs once per hub load (not per frame).
        private static Transform FindByName(string name)
        {
            foreach (var t in Object.FindObjectsByType<Transform>())
                if (t != null && t.name == name) return t;
            return null;
        }
    }
}
