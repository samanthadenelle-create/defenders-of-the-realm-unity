// =============================================================================
// HeroBowAttachment — attaches a visible BOW prop to the Ranger/Archer hero's
// bow hand so the archer reads as an archer (he fires arrows via the projectile
// system but previously held nothing). COSMETIC ONLY — no combat logic.
// -----------------------------------------------------------------------------
// WHY THE LEFT HAND:
//   A bow is held in the off/bow hand (the LEFT for a right-handed archer) while
//   the RIGHT hand draws the string. HeroAimIK already aims the RightHand IK goal
//   at the target (the "draw" hand), so the bow grip belongs on the LeftHand bone.
//   We resolve the bone via Animator.GetBoneTransform(HumanBodyBones.LeftHand);
//   the Ranger body is a CC5/AccuRIG Humanoid rig (HeroBodySwapper), so the bone
//   exists. If the rig is generic / the bone is missing, we LogWarning and skip —
//   never crash, never block the hero.
//
// WHY A CODE-MESH BOW (not a KayKit/polyperfect asset):
//   The project DOES contain KayKit bows (Assets/Models/KayKit/.../bow_withString.fbx)
//   but that pack is GITIGNORED and lives OUTSIDE any Resources/ folder, so it is
//   NOT Resources.Load-able at runtime and is absent on fresh clones / in builds.
//   Rather than reference a path that resolves on one machine and 404s everywhere
//   else, this builds a lightweight procedural low-poly bow (curved riser + two
//   limbs + a thin string) at runtime — it always renders, in every build, with no
//   asset dependency. If the owner later drops a real bow FBX into
//   Resources/Heroes/Props/Bow.prefab, set _resourcesBowPath and it is used instead.
//
// HOOK-UP:
//   HeroBodySwapper.Start() calls AttachTo(heroRoot, bodyRoot) for the Ranger
//   after the body + animator are wired. The component also self-bootstraps via a
//   short retry in case it is added before the Animator finishes Rebind().
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Instantiates a bow prop under the hero's LEFT-hand (bow-hand) bone for the
    /// Ranger/Archer class. Cosmetic only — does not touch the projectile system.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeroBowAttachment : MonoBehaviour
    {
        // Optional override: if a real bow prefab is dropped here it is loaded
        // instead of the procedural mesh. No such asset is committed under Resources
        // today (see file header), so LoadBowPrefab() simply returns null then.
        private const string _resourcesBowPath = "Heroes/Props/Bow";

        // Local transform of the bow under the LEFT-hand bone. Tuned so the riser
        // sits in the closed fist with the limbs running vertically (bow held
        // upright in the off hand). Units are bone-local metres / degrees.
        private static readonly Vector3 GripLocalPosition = new Vector3(0.02f, 0.0f, 0.04f);
        private static readonly Vector3 GripLocalEuler    = new Vector3(0f, 0f, 90f);
        private static readonly Vector3 GripLocalScale    = new Vector3(1f, 1f, 1f);

        private Animator _animator;
        private GameObject _bow;
        private int _retries;

        /// <summary>
        /// Entry point from HeroBodySwapper. Adds the component to the hero root (if
        /// absent) and points it at the swapped-in body's Animator, then attaches.
        /// </summary>
        public static void AttachTo(GameObject heroRoot, GameObject body)
        {
            if (heroRoot == null) return;
            var comp = heroRoot.GetComponent<HeroBowAttachment>();
            if (comp == null) comp = heroRoot.AddComponent<HeroBowAttachment>();
            comp._animator = body != null ? body.GetComponentInChildren<Animator>() : null;
            comp.TryAttach();
        }

        private void Awake()
        {
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
        }

        private void Start()
        {
            // If AttachTo wired us late (after Start), TryAttach already ran; this
            // catches the self-bootstrap case where the component is added in-editor.
            if (_bow == null) TryAttach();
        }

        private void Update()
        {
            // Brief retry window: the Animator's Humanoid bones aren't queryable until
            // HeroBodySwapper finishes Rebind(). Poll a few frames, then give up quietly.
            if (_bow != null) { enabled = false; return; }
            if (_retries > 120) { enabled = false; return; }
            _retries++;
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
            TryAttach();
            if (_bow != null) enabled = false; // done — stop polling
        }

        /// <summary>Resolves the LeftHand bone and parents a bow prop under it. Idempotent.</summary>
        private void TryAttach()
        {
            if (_bow != null) return;
            if (_animator == null) return;

            if (!_animator.isHuman)
            {
                Debug.LogWarning("[HeroBowAttachment] Hero Animator is not Humanoid — " +
                                 "cannot resolve the LeftHand bone for the bow. Skipping (cosmetic only).");
                enabled = false;
                return;
            }

            Transform leftHand = _animator.GetBoneTransform(HumanBodyBones.LeftHand);
            if (leftHand == null)
            {
                Debug.LogWarning("[HeroBowAttachment] Humanoid rig has no LeftHand bone mapped — " +
                                 "bow not attached (cosmetic only, no crash).");
                enabled = false;
                return;
            }

            GameObject prop = LoadBowPrefab();
            if (prop == null) prop = BuildProceduralBow();
            if (prop == null)
            {
                Debug.LogWarning("[HeroBowAttachment] Could not load or build a bow prop — none attached.");
                enabled = false;
                return;
            }

            prop.name = "BowProp";
            // Strip any colliders/rigidbodies a real prefab might carry — purely visual.
            foreach (var c in prop.GetComponentsInChildren<Collider>(true)) if (c != null) Destroy(c);
            foreach (var rb in prop.GetComponentsInChildren<Rigidbody>(true)) if (rb != null) Destroy(rb);

            prop.transform.SetParent(leftHand, false);
            prop.transform.localPosition = GripLocalPosition;
            prop.transform.localRotation = Quaternion.Euler(GripLocalEuler);
            prop.transform.localScale = GripLocalScale;

            _bow = prop;
            Debug.Log("[HeroBowAttachment] Bow attached to LeftHand bone '" + leftHand.name +
                      "' (local pos " + GripLocalPosition + ", euler " + GripLocalEuler + ").");
            enabled = false;
        }

        /// <summary>Loads an optional committed bow prefab from Resources; null when absent.</summary>
        private static GameObject LoadBowPrefab()
        {
            if (string.IsNullOrEmpty(_resourcesBowPath)) return null;
            var prefab = Resources.Load<GameObject>(_resourcesBowPath);
            return prefab != null ? Instantiate(prefab) : null;
        }

        /// <summary>
        /// Builds a simple low-poly bow: a curved wooden riser+limbs (an arc swept
        /// into a thin ribbon) plus a straight string spanning the limb tips. One
        /// GameObject, one MeshRenderer, no asset dependency. ~0.9 m tall.
        /// </summary>
        private static GameObject BuildProceduralBow()
        {
            var root = new GameObject("ProceduralBow");

            // --- Bow stave (the curved C-shape) -----------------------------------
            // Sweep an arc in the local XY plane; give it a small thickness in Z so it
            // reads as a flat limb. The arc spans ~200 deg so the limbs curve forward.
            const int segments = 14;
            const float radius = 0.45f;     // half-height ~ bow radius
            const float arcDeg = 200f;
            const float thickness = 0.04f;  // limb depth (Z)
            const float width = 0.025f;     // limb width (in-plane)

            var verts = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();

            float startDeg = -arcDeg * 0.5f;
            float stepDeg = arcDeg / segments;
            // Build a ribbon: for each arc point emit an inner/outer pair (in-plane
            // width) and extrude along Z for thickness -> a thin curved box strip.
            for (int s = 0; s <= segments; s++)
            {
                float a = (startDeg + stepDeg * s) * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                Vector3 center = dir * radius;
                Vector3 inAxis = dir * (width * 0.5f);
                // Front face pair
                verts.Add(center - inAxis + Vector3.forward * (thickness * 0.5f));
                verts.Add(center + inAxis + Vector3.forward * (thickness * 0.5f));
                // Back face pair
                verts.Add(center - inAxis - Vector3.forward * (thickness * 0.5f));
                verts.Add(center + inAxis - Vector3.forward * (thickness * 0.5f));
            }
            for (int s = 0; s < segments; s++)
            {
                int b = s * 4;
                int n = b + 4;
                // front quad
                AddQuad(tris, b + 0, b + 1, n + 1, n + 0);
                // back quad
                AddQuad(tris, n + 2, n + 3, b + 3, b + 2);
                // outer edge
                AddQuad(tris, b + 1, b + 3, n + 3, n + 1);
                // inner edge
                AddQuad(tris, n + 0, n + 2, b + 2, b + 0);
            }

            var mesh = new Mesh { name = "ProceduralBowMesh" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var mf = root.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            var mr = root.AddComponent<MeshRenderer>();
            mr.sharedMaterial = MakeMaterial(new Color(0.36f, 0.22f, 0.10f)); // wood brown

            // --- Bowstring (thin line from top limb tip to bottom limb tip) --------
            float topA = (startDeg) * Mathf.Deg2Rad;
            float botA = (startDeg + arcDeg) * Mathf.Deg2Rad;
            Vector3 topTip = new Vector3(Mathf.Cos(topA), Mathf.Sin(topA), 0f) * radius;
            Vector3 botTip = new Vector3(Mathf.Cos(botA), Mathf.Sin(botA), 0f) * radius;
            var stringGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stringGo.name = "BowString";
            var sc = stringGo.GetComponent<Collider>();
            if (sc != null) Destroy(sc);
            stringGo.transform.SetParent(root.transform, false);
            Vector3 mid = (topTip + botTip) * 0.5f;
            float len = Vector3.Distance(topTip, botTip);
            stringGo.transform.localPosition = mid;
            stringGo.transform.localRotation = Quaternion.FromToRotation(Vector3.up, (topTip - botTip).normalized);
            stringGo.transform.localScale = new Vector3(0.006f, len * 0.5f, 0.006f);
            var smr = stringGo.GetComponent<MeshRenderer>();
            if (smr != null) smr.sharedMaterial = MakeMaterial(new Color(0.85f, 0.83f, 0.75f)); // pale string

            return root;
        }

        private static void AddQuad(System.Collections.Generic.List<int> tris, int a, int b, int c, int d)
        {
            tris.Add(a); tris.Add(b); tris.Add(c);
            tris.Add(a); tris.Add(c); tris.Add(d);
        }

        /// <summary>Builds a URP/Lit (or fallback) material of the given colour so the bow renders in builds.</summary>
        private static Material MakeMaterial(Color color)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit")
                        ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                        ?? Shader.Find("Standard")
                        ?? Shader.Find("Sprites/Default");
            var m = new Material(sh) { name = "BowMat" };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.2f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            return m;
        }
    }
}
