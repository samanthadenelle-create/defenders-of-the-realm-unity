using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    // =====================================================================
    //  WO-189b — data-driven city population from CityManifest.json
    // =====================================================================
    // Reads a pre-authored manifest (Assets/Editor/CityManifest.json, a copy
    // of the repo-root CityManifest.draft.json authored under WO-189a) and
    // instantiates the full Elarion roster — ~29 buildings, ~100 props,
    // 6 wardens, 4 bridges — under a dedicated CityManifestRoot child of the
    // village root. This is the "empty-city fix" (DESIGN_ELARION_CITY.md §0/§6):
    // every rebake now repopulates the city from durable data instead of the
    // hand-coded 5-building array alone.
    //
    // Reuses the existing Content.cs / Helpers.cs placement helpers
    // (LoadModel, NormalizeProp, SnapFeetToParent, StripColliders,
    // StripRigidbodies, NewChild) so manifest-placed objects ground + scale
    // exactly like the rest of the village.
    //
    // Roads are NOT placed here — BuildRoads (Content.cs) already lays the
    // hex-road cross to the four gates; the manifest's "roads" list is a
    // cobble-overlay note only (per the README). We parse it for completeness
    // but place nothing for it.
    public static partial class VillageSceneBuilder
    {
        // Asset-path roots. Manifest prefab strings are stored with a short
        // "polyperfect/" or "kaykit_hex/" prefix (see the manifest README);
        // we expand them to full project asset paths here.
        private const string CityManifestPolyRoot =
            "Assets/polyperfect/Low Poly Ultimate Pack/_M/Prefabs_M/";
        private const string CityManifestKayKitRoot =
            "Assets/Models/KayKit/KayKit Medieval Hexagon Pack 1.0.1/Assets/fbx(unity)/";

        // Manifest building ids that duplicate the existing hand-placed 5
        // gameplay buildings (Content.cs Buildings[] — pet-house / arcane-tower /
        // workshop / farm / market). Skipped so BuildBuildings + the manifest
        // don't double up at the same plots.
        private static readonly HashSet<string> CityManifestSkipBuildingIds =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "PetHouse", "ArcaneLibrary", "Forge", "Farm", "Market",
            };

        // -----------------------------------------------------------------
        //  Serializable manifest schema (JsonUtility root types)
        // -----------------------------------------------------------------
        // NOTE: JsonUtility deserializes top-level array fields fine, but each
        // ELEMENT type must be [Serializable]. Positions are float[3] (x,0,z).

        [Serializable]
        private class CityManifest
        {
            public CityBuilding[] buildings;
            public CityProp[] props;
            public CityWarden[] wardens;
            public CityBridge[] bridges;
            // "roads" / "meta" intentionally omitted — roads are laid by
            // BuildRoads, meta is documentation. JsonUtility ignores unmapped
            // fields, so leaving them out is harmless.
        }

        [Serializable]
        private class CityBuilding
        {
            public string id;
            public string prefab;
            public string prefabKind;
            public float[] pos;
            public float rotY;
            public string district;
            public string purpose;
            public string secondaryPrefab;
        }

        [Serializable]
        private class CityProp
        {
            public string prefab;
            public string prefabKind;
            public float[] pos;
            public float rotY;
            public string group;
            public string note;
        }

        [Serializable]
        private class CityWarden
        {
            public string npc;
            public string prefab;
            public string prefabKind;
            public string atBuilding;
            public string heldProp;
            public string anim;
            public float[] pos;
            public float rotY;
            public string note;
        }

        [Serializable]
        private class CityBridge
        {
            public string gate;
            public string prefab;
            public string prefabKind;
            public float[] pos;
            public float rotY;
            public string notes;
        }

        // -----------------------------------------------------------------
        //  Public entry — called once from BuildVillage()
        // -----------------------------------------------------------------
        private static void BuildCityFromManifest(Transform parentRoot)
        {
            if (parentRoot == null)
            {
                Debug.LogWarning("[CityManifest] parentRoot is null — manifest population skipped.");
                return;
            }

            CityManifest manifest = LoadCityManifest();
            if (manifest == null) return;

            var cityRoot = NewChild(parentRoot, "CityManifestRoot");

            int buildings = 0, props = 0, wardens = 0, bridges = 0;

            // ── Buildings ────────────────────────────────────────────────
            var buildingRoot = NewChild(cityRoot, "Buildings");
            if (manifest.buildings != null)
            {
                foreach (var b in manifest.buildings)
                {
                    if (b == null) continue;
                    if (!string.IsNullOrEmpty(b.id) &&
                        CityManifestSkipBuildingIds.Contains(b.id))
                        continue; // duplicates an existing hand-placed building
                    try
                    {
                        // WO-181 P3: buildings scaled −20% (NormalizeProp 7f → 5.6f).
                        // WO-181 P2: addFootprint:true → a footprint BoxCollider so the
                        // hero/enemies + NavMesh treat manifest buildings as solid.
                        var go = PlaceManifestEntry(buildingRoot, b.prefab, b.prefabKind,
                            b.pos, b.rotY, $"Building-{b.id}", 5.6f, true, addFootprint: true);
                        if (go != null)
                        {
                            buildings++;
                            if (!string.IsNullOrEmpty(b.secondaryPrefab))
                            {
                                var sec = PlaceManifestEntry(go.transform, b.secondaryPrefab,
                                    b.prefabKind, new float[] { 3f, 0f, 0f }, 0f,
                                    $"{b.id}-secondary", 4f, true, addFootprint: true);
                                _ = sec; // companion structure; not separately tallied
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[CityManifest] building '{b?.id}' failed: {e.Message}");
                    }
                }
            }

            // ── Props ────────────────────────────────────────────────────
            var propRoot = NewChild(cityRoot, "Props");
            if (manifest.props != null)
            {
                foreach (var p in manifest.props)
                {
                    if (p == null) continue;
                    try
                    {
                        var go = PlaceManifestEntry(propRoot, p.prefab, p.prefabKind,
                            p.pos, p.rotY, p.group ?? "prop", 0f, false);
                        if (go != null) props++;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[CityManifest] prop '{p?.prefab}' failed: {e.Message}");
                    }
                }
            }

            // ── Bridges ──────────────────────────────────────────────────
            var bridgeRoot = NewChild(cityRoot, "Bridges");
            if (manifest.bridges != null)
            {
                foreach (var br in manifest.bridges)
                {
                    if (br == null) continue;
                    try
                    {
                        var go = PlaceManifestEntry(bridgeRoot, br.prefab, br.prefabKind,
                            br.pos, br.rotY, $"Bridge-{br.gate}", 0f, false);
                        if (go != null) bridges++;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[CityManifest] bridge '{br?.gate}' failed: {e.Message}");
                    }
                }
            }

            // ── Wardens (NPCs + optional held prop) ──────────────────────
            var wardenRoot = NewChild(cityRoot, "Wardens");
            if (manifest.wardens != null)
            {
                foreach (var w in manifest.wardens)
                {
                    if (w == null) continue;
                    try
                    {
                        // WO-181 P3: wardens were placed at authored scale (0f = no
                        // normalize) and rendered oversized vs the hero — the blacksmith
                        // dwarf especially. Normalize each warden to ~1.8 m so they match
                        // the hero rig (BuildHero normalizes its body to 2.0 m), i.e.
                        // human/hero proportion. snapFeet:true so they stand on the ground.
                        var go = PlaceManifestEntry(wardenRoot, w.prefab, w.prefabKind,
                            w.pos, w.rotY, $"Warden-{w.npc}", 1.8f, true);
                        if (go == null) continue;
                        wardens++;

                        if (!string.IsNullOrEmpty(w.heldProp))
                        {
                            // No reliable hand-bone lookup helper exists in the
                            // builder, and polyperfect People_M rigs vary, so we
                            // parent the held prop near the NPC's hand height and
                            // leave a TODO for CLI/animation to bind it to a bone.
                            var held = PlaceManifestEntry(go.transform, w.heldProp,
                                w.prefabKind, new float[] { 0.3f, 1.1f, 0.3f }, 0f,
                                $"{w.npc}-heldProp", 0f, false);
                            if (held != null)
                                Debug.Log($"[CityManifest] TODO: bind '{w.npc}' held prop " +
                                          "to a hand bone (currently parented near hand height).");
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[CityManifest] warden '{w?.npc}' failed: {e.Message}");
                    }
                }
            }

            Debug.Log($"[CityManifest] placed {buildings} buildings, {props} props, " +
                      $"{wardens} wardens, {bridges} bridges (from Assets/Editor/CityManifest.json).");
        }

        // -----------------------------------------------------------------
        //  Helpers
        // -----------------------------------------------------------------

        private static CityManifest LoadCityManifest()
        {
            string path = Path.Combine(Application.dataPath, "Editor/CityManifest.json");
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[CityManifest] manifest not found at '{path}' — " +
                                 "city population skipped (copy CityManifest.draft.json there).");
                return null;
            }
            try
            {
                string json = File.ReadAllText(path);
                var manifest = JsonUtility.FromJson<CityManifest>(json);
                if (manifest == null)
                    Debug.LogWarning("[CityManifest] JsonUtility returned null — manifest unparseable.");
                return manifest;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CityManifest] failed to read/parse manifest: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Resolves a manifest prefab string ("polyperfect/..." or "kaykit_hex/...")
        /// to a full project asset path.
        /// </summary>
        private static string ResolveManifestPrefabPath(string prefab, string prefabKind)
        {
            if (string.IsNullOrEmpty(prefab)) return null;

            if (string.Equals(prefabKind, "kaykit_hex", StringComparison.OrdinalIgnoreCase))
            {
                string rel = prefab.StartsWith("kaykit_hex/", StringComparison.OrdinalIgnoreCase)
                    ? prefab.Substring("kaykit_hex/".Length)
                    : prefab;
                return CityManifestKayKitRoot + rel;
            }

            // default: polyperfect
            string pRel = prefab.StartsWith("polyperfect/", StringComparison.OrdinalIgnoreCase)
                ? prefab.Substring("polyperfect/".Length)
                : prefab;
            return CityManifestPolyRoot + pRel;
        }

        /// <summary>
        /// Loads + instantiates one manifest entry at world (x,0,z) with yaw rotY,
        /// parents it under <paramref name="parent"/>, normalizes/snaps/strips it,
        /// and returns the plot GameObject (or null on a missing prefab — logged
        /// as a warning, never an error, since the pack may not be imported).
        /// </summary>
        /// <param name="normalizeSize">Target longest-edge size for NormalizeProp;
        /// pass 0 to skip normalization (props keep their authored scale).</param>
        /// <param name="snapFeet">When true, snaps the model's feet to the parent
        /// floor after normalization (buildings/wardens); props skip it.</param>
        /// <param name="addFootprint">WO-181: when true, adds a footprint BoxCollider
        /// (via <see cref="AddBuildingFootprintCollider"/>) to the plot root sized to
        /// the visual's base — so the building blocks the hero/enemies and the NavMesh
        /// routes around it. Only buildings pass true; props/wardens/bridges stay
        /// collider-free so they can't block pathing.</param>
        private static GameObject PlaceManifestEntry(Transform parent, string prefab,
            string prefabKind, float[] pos, float rotY, string label,
            float normalizeSize, bool snapFeet, bool addFootprint = false)
        {
            string assetPath = ResolveManifestPrefabPath(prefab, prefabKind);
            if (string.IsNullOrEmpty(assetPath)) return null;

            var model = LoadModel(assetPath);
            if (model == null)
            {
                Debug.LogWarning($"[CityManifest] prefab not found: '{assetPath}' " +
                                 $"({label}) — skipped (pack may not be imported).");
                return null;
            }

            float x = (pos != null && pos.Length > 0) ? pos[0] : 0f;
            float z = (pos != null && pos.Length > 2) ? pos[2] : 0f;

            // Plot root carries the authored world transform; the visual child
            // carries any normalize/snap adjustments so it isn't scaled twice.
            var plot = new GameObject(label);
            plot.transform.SetParent(parent, false);
            plot.transform.localPosition = new Vector3(x, 0f, z);
            plot.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);

            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
            if (visual == null)
            {
                Debug.LogWarning($"[CityManifest] InstantiatePrefab returned null for '{assetPath}' ({label}).");
                UnityEngine.Object.DestroyImmediate(plot);
                return null;
            }
            visual.name = model.name;
            visual.transform.SetParent(plot.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            if (normalizeSize > 0.001f)
                NormalizeProp(visual, normalizeSize);

            // Strip native colliders/rigidbodies so manifest dressing can't
            // block pathing or fall through physics (matches Content.cs policy).
            StripColliders(visual);
            StripRigidbodies(visual);

            if (snapFeet)
                SnapFeetToParent(visual);

            // WO-181 P2: footprint collider on manifest buildings so the hero/enemies
            // can't walk through them and the NavMesh routes around them. Added to the
            // plot root (carries the world transform), measured from the post-scale
            // visual — exactly as BuildBuildings does for the hand-placed plots.
            if (addFootprint)
                AddBuildingFootprintCollider(plot, visual);

            return plot;
        }
    }
}
