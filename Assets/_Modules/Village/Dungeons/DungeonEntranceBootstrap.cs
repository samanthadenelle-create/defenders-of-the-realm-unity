// =============================================================================
// DungeonEntranceBootstrap — WO-19: places the dungeon entrances in the village
// at runtime. The village scene is baked by the edit-time VillageSceneBuilder
// (curated-scene rule forbids re-running it), so — exactly like the WO-08 gate
// openers and the WO-20 HeartHudBridge — the entrances are layered on at runtime
// instead of being scene-baked.
//
// Entrances are built procedurally from primitives (a 2-post + lintel doorway)
// with a URP/Lit material, so there is NO dependency on a KayKit model under the
// gitignored Assets/Models (the fresh-clone trap hit in WO-05/10/18). Swapping
// in real KayKit art is a clean follow-up: give the entrance a child mesh.
//
// Dungeon data is loaded from Resources/Dungeons/*.asset (DungeonDef); if none
// are authored yet, an inline fallback covers the dungeon scenes that exist.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace DeNelle.Village
{
    [DisallowMultipleComponent]
    public sealed class DungeonEntranceBootstrap : MonoBehaviour
    {
        [SerializeField] private float _ringRadius = 25f;   // distance from the Heart
        [SerializeField] private float _doorwayHeight = 4f;

        private bool _placed;

        private void Start() => PlaceEntrances();

        public void PlaceEntrances()
        {
            if (_placed) return;
            _placed = true;

            var defs = LoadDefs();
            if (defs.Count == 0)
            {
                Debug.LogWarning("[DungeonEntranceBootstrap] No DungeonDefs found or built — no entrances placed.");
                return;
            }

            Vector3 center = Vector3.zero;
            var heart = FindAnyObjectByType<HeartController>();
            if (heart != null) center = heart.transform.position;

            var parent = new GameObject("DungeonEntrances").transform;
            parent.SetParent(transform, false);

            int n = defs.Count;
            for (int i = 0; i < n; i++)
            {
                // Even spread clockwise around the perimeter starting at North.
                float ang = (Mathf.PI * 2f) * (i / (float)Mathf.Max(1, n));
                Vector3 pos = center + new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang)) * _ringRadius;
                BuildEntrance(defs[i], pos, center, parent);
            }
            Debug.Log($"[DungeonEntranceBootstrap] Placed {n} dungeon entrance(s) at radius {_ringRadius}m.");
        }

        // ── Data ──────────────────────────────────────────────────────────────

        private static List<DungeonDef> LoadDefs()
        {
            var list = new List<DungeonDef>(Resources.LoadAll<DungeonDef>("Dungeons"));
            if (list.Count > 0) return list;

            // Inline fallback — the dungeon scenes that actually exist in the build
            // (others are scaffold, authored as Resources assets when their scenes land).
            Debug.Log("[DungeonEntranceBootstrap] No Resources/Dungeons defs — using inline fallback (existing scenes only).");
            list.Add(MakeDef("HealersCottage", "Dungeon_HealersCottage", "Healer's Cottage", new Color(1f, 0.82f, 0.48f)));
            list.Add(MakeDef("FolksGranary",   "Dungeon_FolksGranary",   "Folk's Granary",   new Color(0.60f, 0.90f, 0.70f)));
            return list;
        }

        private static DungeonDef MakeDef(string id, string scene, string name, Color accent)
        {
            var d = ScriptableObject.CreateInstance<DungeonDef>();
            d.DungeonId = id; d.SceneName = scene; d.DisplayName = name;
            d.AccentColor = accent; d.SceneExists = true;
            return d;
        }

        // ── Procedural entrance ─────────────────────────────────────────────────

        private void BuildEntrance(DungeonDef def, Vector3 pos, Vector3 lookAt, Transform parent)
        {
            var root = new GameObject($"DungeonEntrance_{def.DungeonId}");
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            Vector3 flatDir = lookAt - pos; flatDir.y = 0f;
            if (flatDir.sqrMagnitude > 0.001f)
                root.transform.rotation = Quaternion.LookRotation(flatDir.normalized);

            float h = _doorwayHeight;

            // Owner Tripo portal model replaces the placeholder doorway frame when
            // present (Resources/Structures/Portal.fbx); falls back to the box
            // posts + lintel otherwise. The cube fallback keeps dungeons reachable
            // even if the art asset is missing on a fresh clone (gitignored).
            var portalPrefab = Resources.Load<GameObject>("Structures/Portal");
            if (portalPrefab != null)
            {
                var portal = Instantiate(portalPrefab, root.transform);
                portal.name = "PortalVisual";
                portal.transform.localPosition = Vector3.zero;
                portal.transform.localRotation = Quaternion.identity;
                FitHeight(portal, h * 1.8f);
                foreach (var c in portal.GetComponentsInChildren<Collider>()) Destroy(c);
                // Tripo FBX -> URP material fix at runtime (same net the gate uses).
                portal.AddComponent<DeNelle.Core.TripoMaterialFixer>();
            }
            else
            {
                // Shared per-entrance URP material (emissive on, so the MPB accent reads).
                Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
                Material mat = lit != null ? new Material(lit) : null;
                if (mat != null) mat.EnableKeyword("_EMISSION");
                BuildBox(root.transform, new Vector3(-0.9f, h * 0.5f, 0f), new Vector3(0.3f, h, 0.3f), mat);          // left post
                BuildBox(root.transform, new Vector3(0.9f, h * 0.5f, 0f),  new Vector3(0.3f, h, 0.3f), mat);          // right post
                BuildBox(root.transform, new Vector3(0f, h + 0.15f, 0f),   new Vector3(2.1f, 0.3f, 0.3f), mat);       // lintel
            }

            BuildPrompt(root.transform, new Vector3(0f, h + 0.7f, 0f));

            // Proximity trigger + kinematic RB (the hero is a solid, RB-less, transform-moved
            // collider, so the trigger side must carry the Rigidbody — see WO-08).
            var sphere = root.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = 2.5f;
            sphere.center = new Vector3(0f, 1f, 0f);
            var rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            // Add the driver LAST so its Awake sees the visuals + prompt, then Configure.
            var entrance = root.AddComponent<DungeonEntrance>();
            entrance.Configure(def);
        }

        private static void BuildBox(Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Frame";
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col); // doorway shouldn't block movement; the trigger is the only collider
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        /// <summary>Uniformly scales <paramref name="go"/> so its combined renderer
        /// bounds stand <paramref name="targetHeight"/> metres tall (Tripo models
        /// import at arbitrary native sizes).</summary>
        private static void FitHeight(GameObject go, float targetHeight)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return;
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            if (b.size.y > 0.001f)
                go.transform.localScale *= targetHeight / b.size.y;
        }

        private static void BuildPrompt(Transform parent, Vector3 localPos)
        {
            var go = new GameObject("Prompt");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * 0.08f;
            var tm = go.AddComponent<TextMesh>();
            tm.text = string.Empty;
            tm.characterSize = 0.5f;
            tm.fontSize = 72;
            tm.anchor = TextAnchor.LowerCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = Color.white;
            go.AddComponent<DungeonPromptBillboard>();
            go.SetActive(false); // shown by DungeonEntrance while the hero is in range
        }
    }

    /// <summary>Faces the prompt text at the main camera each frame (cheap billboard).</summary>
    [DisallowMultipleComponent]
    public sealed class DungeonPromptBillboard : MonoBehaviour
    {
        private Transform _cam;

        private void LateUpdate()
        {
            if (_cam == null)
            {
                var c = Camera.main;
                if (c == null) return;
                _cam = c.transform;
            }
            transform.rotation = Quaternion.LookRotation(transform.position - _cam.position);
        }
    }
}
