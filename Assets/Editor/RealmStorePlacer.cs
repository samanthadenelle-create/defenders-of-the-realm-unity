// =============================================================================
// RealmStorePlacer — bakes the Realm Store storefront into the hub.
// -----------------------------------------------------------------------------
// PROD-003, owner ruling 2026-08-18: placement option (a) — the south plaza,
// ACROSS the plaza from Coppin rather than beside him, so the two read as
// different establishments in one market rather than two stalls of the same one.
//
// ⛔ PLACEMENT IS CONSTRAINED BY A LESSON ALREADY PAID FOR. Coppin's market
// (Marketplace_Monetization) sits at (0, 0, 32) — due north. "Across the plaza"
// therefore points at (0, 0, -32), which is EXACTLY where the Jeweler used to be
// and was removed from, because it BLOCKED THE SOUTH DOOR (CastleHubBuilder's own
// comment: "Jeweler (Gems) REMOVED from the fixed ring — it was blocking the
// south door. Do NOT re-add a fixed Jeweler here."). The south gate reads from
// the recipe at x ~= -4.37, z ~= -40.6, so the doorway corridor runs up the
// middle of that face.
// The store therefore sits at (12, 0, -32): still the south plaza, still facing
// Coppin across the open centre, but offset ~16 units east of the gate corridor
// and ~14 from the nearest existing structure (Lumbermill at 22,0,-22). Putting
// the game's only storefront where it blocks the main entrance would be a
// self-inflicted wound of the same shape PROD-003 exists to avoid.
//
// ⛔ NOT ADDED TO structures-catalog.json, AND THAT IS THE POINT. A catalog row
// would put it in the build palette, making it sellable / movable / damageable —
// each of which is a way for the player to take their own store offline. It is
// baked furniture, like the Heart, and it is not an IDamageableStructure.
//
// Idempotent: re-running replaces the existing instance rather than stacking a
// second storefront in the scene.
// =============================================================================

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Editor
{
    /// <summary>Bakes the PROD-003 Realm Store storefront + its vendor into the hub scene.</summary>
    public static class RealmStorePlacer
    {
        private const string HubScene   = "Assets/Scenes/Main_Castle_Overworld.unity";
        private const string ObjectName = "RealmStore_Storefront";
        private const string OkMarker   = "REALM_STORE_PLACED_OK";

        /// <summary>
        /// FALLBACK ONLY — the owner-ruled (a) position, used when Offset Forge has no authored
        /// placement for this storefront.
        /// <para>
        /// ⛔ THE AUTHORED VALUE WINS. Owner principle, applied consistently all session: a value
        /// the owner tunes must live in DATA, not in code. Hardcoding the transform here would make
        /// every nudge a code edit and a round trip through me — the same mistake as the hardcoded
        /// asset paths that became AssetRoots. Offset Forge already stores pos/rot/scale by id and
        /// is the tool she tunes with; this reads what it authored.
        /// </para>
        /// </summary>
        private static readonly Vector3 FallbackPlacement = new Vector3(12f, 0f, -32f);

        /// <summary>Offset Forge key. Matches the asset name, which is how that file is keyed.</summary>
        private const string OffsetKey = "RealmStore";

        [MenuItem("Defenders/World/Place the Realm Store storefront")]
        public static void RunMenu() => Run();

        /// <summary>
        /// Reads a non-zero authored position for this storefront out of Offset Forge.
        /// <para>
        /// A ZERO pos is treated as "not authored" rather than "place it at the world origin".
        /// Every row in that file defaults to pos (0,0,0), so honouring a zero literally would drop
        /// the storefront on top of the Heart the moment anyone touched its rotation without
        /// intending to move it — a tuning tool must not be able to teleport a building by
        /// omission.
        /// </para>
        /// </summary>
        private static bool TryReadAuthoredPosition(out Vector3 pos)
        {
            pos = default;
            const string path = "Assets/OffsetForge/offsets.json";
            if (!System.IO.File.Exists(path)) return false;

            try
            {
                string json = System.IO.File.ReadAllText(path);
                // Locate this key's block, then the pos object inside it. Text-scanned rather than
                // deserialized so a schema addition to offsets.json cannot break the bake.
                int at = json.IndexOf($"\"id\": \"{OffsetKey}\"", System.StringComparison.Ordinal);
                if (at < 0) return false;

                int posAt = json.IndexOf("\"pos\"", at, System.StringComparison.Ordinal);
                if (posAt < 0) return false;

                var m = System.Text.RegularExpressions.Regex.Match(
                    json.Substring(posAt, System.Math.Min(220, json.Length - posAt)),
                    "\"x\"\\s*:\\s*(-?[0-9.eE+]+).*?\"y\"\\s*:\\s*(-?[0-9.eE+]+).*?\"z\"\\s*:\\s*(-?[0-9.eE+]+)",
                    System.Text.RegularExpressions.RegexOptions.Singleline);
                if (!m.Success) return false;

                var v = new Vector3(
                    float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
                    float.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture),
                    float.Parse(m.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture));

                if (v.sqrMagnitude < 0.0001f) return false;   // unauthored zero — see summary
                pos = v;
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[RealmStore] could not read the authored offset ({ex.Message}) — " +
                                 "falling back to the owner-ruled default.");
                return false;
            }
        }

        public static void Run()
        {
            string modelPath = DeNelle.Core.AssetRoots.StructureContent + "/RealmStore.fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (model == null)
            {
                Debug.LogError($"[RealmStore] model not found at '{modelPath}' — nothing placed. " +
                               "The art is owner-purchased and git-tracked; if it is missing the " +
                               "migration or a checkout has gone wrong.");
                return;
            }

            EditorSceneManager.OpenScene(HubScene, OpenSceneMode.Single);
            var scene = SceneManager.GetActiveScene();

            // Idempotent: drop any previous instance first, so re-running never stacks storefronts.
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t != null && t.name == ObjectName)
                    {
                        Debug.Log("[RealmStore] removing the previous storefront instance (idempotent re-run).");
                        Object.DestroyImmediate(t.gameObject);
                        break;
                    }
                }
            }

            // Read the AUTHORED placement first; fall back to the owner-ruled default.
            Vector3 placement = FallbackPlacement;
            bool authored = TryReadAuthoredPosition(out var authoredPos);
            if (authored)
            {
                placement = authoredPos;
                Debug.Log($"[RealmStore] using the AUTHORED position from Offset Forge: {placement}");
            }
            else
            {
                Debug.Log($"[RealmStore] no authored position for '{OffsetKey}' — using the owner-ruled " +
                          $"default {placement}. Tune it in Offset Forge and re-run to make it stick.");
            }

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            inst.name = ObjectName;
            inst.transform.position = placement;
            // Face the plaza centre so the shopfront looks at the player walking in from it,
            // rather than presenting its back to the town.
            inst.transform.rotation = Quaternion.LookRotation(
                new Vector3(0f, 0f, 0f) - placement, Vector3.up);

            // A collider so the player cannot walk through the building. NOT an
            // IDamageableStructure and NOT a catalog row — it blocks, it does not take damage.
            if (inst.GetComponentInChildren<Collider>() == null)
            {
                var box = inst.AddComponent<BoxCollider>();
                var rends = inst.GetComponentsInChildren<Renderer>();
                if (rends != null && rends.Length > 0)
                {
                    Bounds b = rends[0].bounds;
                    for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                    box.center = inst.transform.InverseTransformPoint(b.center);
                    box.size = b.size;
                }
            }

            // The door. PackStoreBootstrap already registers PanelId.RealmStore at boot, so this
            // component only has to call it.
            if (inst.GetComponent<DeNelle.Village.RealmStoreVendor>() == null)
                inst.AddComponent<DeNelle.Village.RealmStoreVendor>();

            EditorUtility.SetDirty(inst);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();

            // Verify the ARTIFACT, not the intent — the navmesh bake earlier today reported success
            // having written nothing, because nothing read the result back.
            bool present = false;
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == ObjectName) { present = true; break; }

            if (!present)
            {
                Debug.LogError("[RealmStore] storefront is NOT in the saved scene — placement failed.");
                return;
            }

            Debug.Log($"[RealmStore] placed at {placement} ({(authored ? "AUTHORED in Offset Forge" : "owner-ruled default")}) " +
                      "facing the plaza centre, with a collider and RealmStoreVendor. " +
                      "NOT in the build catalog, NOT damageable.");
            Debug.Log($"{OkMarker} at {placement}");
        }
    }
}
