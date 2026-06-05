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
// BOW SOURCE (KayKit, committed under Resources):
//   The KayKit Adventurers bow (bow_withString) was copied into the COMMITTED,
//   Resources-loadable folder Assets/Resources/Heroes/Props/ (the KayKit pack
//   itself is gitignored under /Assets/Models/*, so a committed copy is the only
//   build-safe path). BowPropBuilder turns it into Bow.prefab with a URP/Lit atlas
//   material. LoadBowPrefab() loads "Heroes/Props/Bow" FIRST; the procedural
//   low-poly bow below remains only as a fresh-clone / missing-asset fallback so
//   the archer always reads as an archer even if the prefab is absent.
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

        // The bow is first NORMALIZED (NormalizeInto) to the owner's spec — longest axis
        // (limbs) on local +Y, narrowest on +X, grip at the centre of the longest part,
        // scaled to BowHeldLength. These then place that normalized bow in the hand bone;
        // because the bow is normalized, GripLocalEuler is a single predictable hand-
        // orientation tweak (not a guess against an arbitrary FBX). Bone-local m / deg.
        private static readonly Vector3 GripLocalPosition = new Vector3(0f, 0f, 0f);
        private static readonly Vector3 GripLocalEuler    = new Vector3(0f, 0f, 0f);
        // Target held length of the longest (limb) axis, in metres.
        private const float BowHeldLength = 1.3f;

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

            // Auto-orient the bow to the owner's spec inside a grip root (deterministic,
            // FBX-orientation-independent): longest axis -> +Y, narrowest -> +X, centred.
            var bowRoot = new GameObject("BowProp");
            NormalizeInto(prop, bowRoot.transform, BowHeldLength);

            bowRoot.transform.SetParent(leftHand, false);
            bowRoot.transform.localPosition = GripLocalPosition;
            bowRoot.transform.localRotation = Quaternion.Euler(GripLocalEuler);

            _bow = bowRoot;
            Debug.Log("[HeroBowAttachment] Bow attached + auto-oriented (long axis vertical, narrow X, " +
                      "grip-centred) to LeftHand bone '" + leftHand.name + "'.");
            enabled = false;
        }

        /// <summary>
        /// Parents <paramref name="prop"/> under <paramref name="parent"/> and orients it to
        /// the owner's spec: the model's LONGEST axis -> parent +Y (vertical limbs), its
        /// NARROWEST axis -> parent +X (thin left-right; curve depth falls to +Z), the bounds
        /// CENTRE at the parent origin (hand grips the middle of the longest part), scaled so
        /// the longest axis is <paramref name="targetLength"/> m. Deterministic from renderer
        /// bounds — any bow/weapon FBX lands right without hand-guessed Euler angles.
        /// </summary>
        private static void NormalizeInto(GameObject prop, Transform parent, float targetLength)
        {
            prop.transform.SetParent(parent, false);
            prop.transform.localPosition = Vector3.zero;
            prop.transform.localRotation = Quaternion.identity;
            prop.transform.localScale = Vector3.one;

            if (!TryLocalBounds(prop, parent, out Bounds b0)) return;
            Vector3 sz = b0.size;
            int lng = (sz.x >= sz.y && sz.x >= sz.z) ? 0 : (sz.y >= sz.z ? 1 : 2);
            int sht = (sz.x <= sz.y && sz.x <= sz.z) ? 0 : (sz.y <= sz.z ? 1 : 2);
            if (sht == lng) sht = (lng + 1) % 3;

            // Longest axis -> +Y.
            Quaternion alignLong = Quaternion.FromToRotation(Axis(lng), Vector3.up);
            prop.transform.localRotation = alignLong;

            // Shortest axis -> +X (yaw only).
            Vector3 shortAfter = alignLong * Axis(sht); shortAfter.y = 0f;
            if (shortAfter.sqrMagnitude > 1e-5f)
                prop.transform.localRotation =
                    Quaternion.FromToRotation(shortAfter.normalized, Vector3.right) * alignLong;

            // Scale longest (now Y) to the target held length.
            if (TryLocalBounds(prop, parent, out Bounds b1) && b1.size.y > 1e-4f)
                prop.transform.localScale = Vector3.one * (targetLength / b1.size.y);

            // Recentre so the grip is the middle of the longest part.
            if (TryLocalBounds(prop, parent, out Bounds b2))
                prop.transform.localPosition -= b2.center;
        }

        private static Vector3 Axis(int i) => i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;

        /// <summary>Combined renderer bounds of <paramref name="prop"/> in <paramref name="parent"/>'s local space.</summary>
        private static bool TryLocalBounds(GameObject prop, Transform parent, out Bounds bounds)
        {
            bounds = new Bounds();
            bool any = false;
            foreach (var r in prop.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                Bounds wb = r.bounds;
                Vector3 c = parent.InverseTransformPoint(wb.center);
                Vector3 e = parent.InverseTransformVector(wb.extents);
                var lb = new Bounds(c, new Vector3(Mathf.Abs(e.x), Mathf.Abs(e.y), Mathf.Abs(e.z)) * 2f);
                if (!any) { bounds = lb; any = true; } else bounds.Encapsulate(lb);
            }
            return any;
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
