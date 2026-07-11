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
// to Swaps below — { baked structure NAME, model path, target size (m), yaw° }. The
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
            public float  sizeM;       // fit the model's largest dimension to this many metres
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
            public float  scaleY;      // OVERRIDES the uniform sizeM fit with (scaleX,scaleY,scaleZ) —
            public float  scaleZ;      // for a model the owner sized by hand. Default 0 -> use sizeM fit.
        }

        // ── THE SWAP TABLE — add a row per lightweight structure ──────────────────
        private static readonly Swap[] Swaps =
        {
            // CONVENTION (owner 2026-06-17): these are Tripo FBX exports — they import facing +X, so
            // ALL need yawDeg=90 to face the plaza, and their embedded materials are URP-fixed
            // automatically by SkinOptions.Structure (FixTripoMaterials). Keep new Tripo rows at yaw 90.
            // Trade convention: forge = WEAPONS (Blacksmith), armorer = ARMOR (Forge_Armor), store = Market.
            new Swap { bakedName = "EchoHollow_Pets_RoamingArea",   modelPath = "Structures/PetHouse2",    sizeM = 7f,  yawDeg = 0f,   pitchDeg = -90f, rollDeg = 270f },   // owner hand-dialed 2026-06-21
            new Swap { bakedName = "ArcaneTower_MagicUpgrades",     modelPath = "Structures/arcane tower", sizeM = 12f, yawDeg = 0f,   pitchDeg = -90f, posY = -0.6f, texPath = "Structures/arcane tower/arcane tower" },
            new Swap { bakedName = "Blacksmith_Weapons_Storefront", modelPath = "Structures/Forge",        sizeM = 7f,  yawDeg = 0f,   pitchDeg = -90f, rollDeg = 180f },   // owner hand-dialed 2026-06-21
            new Swap { bakedName = "Forge_Armor_Storefront",        modelPath = "Structures/armorer",      sizeM = 7f,  yawDeg = 90f,  pitchDeg = -90f },
            new Swap { bakedName = "Marketplace_Monetization",      modelPath = "Structures/store",        sizeM = 8f,  yawDeg = 90f,  pitchDeg = -90f },
            new Swap { bakedName = "Jeweler_Gems_Storefront",       modelPath = "Structures/jeweler",      sizeM = 7f,  yawDeg = 0f,   pitchDeg = -90f, rollDeg = 110.4f, scaleX = 5.4f, scaleY = 3.77f, scaleZ = 3.6f },
            new Swap { bakedName = "Lumbermill_Wood_Storefront",    modelPath = "Structures/lumbermill",   sizeM = 7f,  yawDeg = 0f,   pitchDeg = -90f, posY = 1.5f },
            new Swap { bakedName = "Windmill_Food_Storefront",      modelPath = "Structures/farm",         sizeM = 8f,  yawDeg = 0f,   pitchDeg = -90f, rollDeg = 212f },   // owner hand-dialed 2026-06-21
            // Castle barracks = the troop-TRAINING building (existing scene prefab "CastleBarracks");
            // visual swap only — its training function is already wired. Size/yaw owner-dialed.
            new Swap { bakedName = "CastleBarracks",                modelPath = "Structures/barracks",     sizeM = 8f,  yawDeg = 180f, pitchDeg = -90f, setLocalPos = true, posX = 38.3f, posY = 0f, posZ = 36f },
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
            public float   sizeM;
            public float   yawDeg;
            public float   pitchDeg;
            public float   rollDeg;    // Z rotation (owner hand-dial; default 0) — mirrors Swap.rollDeg
            public float   scaleX;     // OPTIONAL explicit non-uniform scale; scaleX>0 OVERRIDES the sizeM
            public float   scaleY;     // fit with (scaleX,scaleY,scaleZ) — for a model the owner sized by
            public float   scaleZ;     // hand. Default 0 -> use sizeM fit. Mirrors Swap.scaleX/Y/Z.
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
                        worldPos = new Vector3(-0.39f, 0f, 23.1f), sizeM = 16f,
                        yawDeg = 0f, pitchDeg = -90f, rollDeg = 90f,
                        scaleX = 5.3f, scaleY = 4.2f, scaleZ = 5.265f },   // 50% of owner hand-dialed 2026-06-21
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

        private static void ApplyAll()
        {
            for (int i = 0; i < Swaps.Length; i++) TrySwap(Swaps[i]);
            for (int i = 0; i < Places.Length; i++) TryPlace(Places[i]);
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
            var opts = SkinOptions.Structure(p.sizeM);
            opts.LocalRotation = Quaternion.Euler(p.pitchDeg, p.yawDeg, p.rollDeg);
            var vis = VisualFactory.Skin(host.transform, p.modelPath, opts);
            if (vis == null)
            {
                Debug.LogWarning("[HubStructureVisualInjector] place model '" + p.modelPath + "' not found for " + p.name + ".");
                Object.Destroy(host);
                return;
            }
            if (p.scaleX > 0f)   // explicit owner-dialed (non-uniform) scale overrides the uniform sizeM fit
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
            // WO-673 L3 STANDDOWN (docs/WO673_ARCHITECTURE_REVIEW.md §3, the Barracks pattern below):
            // once the one-shot migration has written this storefront's BaseLayout record
            // (ff.strategicplacement ON + persisted marker + not the migration load itself),
            // the BAKE stands down — deactivate the whole baked structure (building, NPC
            // interact markers, tap-dialogue anchors all disappear; the vendor injector then
            // finds no markers and no-ops by construction) and let BaseLayoutLoader replay the
            // record instead. Per-structure: a storefront with NO record (missing catalog row,
            // skipped by the writer) keeps its bake — nothing is ever lost. Flag OFF → false.
            if (StrategicPlacementMigration.StanddownActiveForBaked(s.bakedName, out string migratedId))
            {
                target.gameObject.SetActive(false);
                DeNelle.Core.Diagnostics.FlowTrace.Step("Placement",
                    $"standdown {s.bakedName} (migrated -> BaseLayout '{migratedId}').");
                return;
            }
            // Owner 2026-07-10: the whole Barracks is hidden for V1 (ff.barracks OFF) — deactivate the
            // baked structure entirely (not just re-skin renderers) so the building, its tap-dialogue,
            // and the drillmaster anchor all disappear; the NPC injector then finds nothing and no-ops.
            if (s.bakedName == "CastleBarracks" && !DeNelle.Core.FeatureFlags.Barracks)
            {
                target.gameObject.SetActive(false);
                return;
            }
            string marker = MarkerPrefix + s.bakedName;
            if (target.Find(marker) != null) return;                // already swapped (idempotent)

            // Hide the baked visual (renderers only — NPC point + colliders/logic stay live).
            var bakedRenderers = target.GetComponentsInChildren<Renderer>(true);
            foreach (var r in bakedRenderers)
                if (r != null) r.enabled = false;

            // Skin the lightweight model in: bounds-fit + seat-on-ground + URP-fix Tripo materials.
            // LocalRotation (yaw) is applied BEFORE fit/seat so the fit measures it final-facing.
            var opts = SkinOptions.Structure(s.sizeM);
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
            if (s.scaleX > 0f)   // explicit (non-uniform) scale overrides the uniform sizeM fit
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
        // through SkinOptions.Structure(sizeM) and registered as a solid structure (EnsureStructure
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
