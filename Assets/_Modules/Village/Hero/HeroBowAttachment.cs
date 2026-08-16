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
using DeNelle.Core.Diagnostics;

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

        // The bow is first NORMALIZED (WeaponBoundsOrient.NormalizeInto) to the owner's spec —
        // longest axis (limbs) on local +Y, narrowest on +X, grip on the STAVE SURFACE at the
        // midpoint of the long axis (WO-1105 R4 GripAnchor.BowGrip — not the bounds centre, which
        // is the hollow between string and belly), scaled to BowHeldLength. Because it does that
        // deterministic bounds-based fit (longest/limbs -> +Y, narrowest -> +X, grip derived,
        // scaled to target) independent of the FBX's own pivot/orient/scale, the bow arrives in the
        // GRIP ROOT'S OWN FRAME already oriented to spec.
        //
        // ⚠ CORRECTED 2026-08-16. This block used to continue "...the bow arrives in the hand
        // ALREADY oriented to spec — so GripLocalEuler stays ZERO", and that premise was FALSE and
        // was the bug. NormalizeInto solves in the GRIP ROOT's frame and has no knowledge of the
        // BONE the root is about to be parented to, so a ZERO euler maps the limb span onto the
        // hand bone's raw +Y — the fist axis — and the bow lay HORIZONTALLY across the body
        // ("rotated roughly 90 degrees about the grip point", owner). The hand-local seat is now
        // DERIVED from the rig by WeaponBoundsOrient.ComputeBowHeldRotation (see TryAttach), and
        // GripLocalEuler survives as what it was always described to be: a felt-tune nudge ON TOP,
        // still zero. A previous +91 Z tweak was a guess at this same symptom and stays removed —
        // the answer is a derivation, not a dialed constant. If a small hand-fit nudge is ever
        // needed, tune GripLocalEuler against a screenshot rather than guessing.
        private static readonly Vector3 GripLocalPosition = new Vector3(0f, 0f, 0f);
        private static readonly Vector3 GripLocalEuler    = new Vector3(0f, 0f, 0f);
        // Target held length of the longest (limb) axis, in metres.
        // Reviewed + fixed: 1.3f was massively oversized vs ~1.8-2m hero (ranger body
        // TargetHeightMeters=2.0f base). Realistic held bow (nock-to-nock) is 0.9-1.0m.
        // NormalizeInto measures renderer bounds post-load (works for both KayKit Bow.prefab
        // and the procedural fallback), orients longest axis to +Y, narrowest to +X, centres
        // grip at origin, then scales Y to this target. Grip* applied after on the LeftHand bone.
        private const float BowHeldLength = 0.92f;

        // Scale-sanity band (owner 2026-08-06). Generous on purpose - a catastrophe
        // detector, not a tuning assert. 0.92m intended => pass anything 0.23m..2.76m.
        private const float ScaleSanityMin = 0.25f;
        private const float ScaleSanityMax = 3.0f;
        // Any single bounds component beyond this is nonsense for a hand prop and means
        // the renderer's bounds are corrupt rather than merely mis-scaled.
        private const float AbsurdBoundsMeters = 10f;

        /// <summary>
        /// Measures what the attached prop ACTUALLY renders as and rejects it if that bears no
        /// relation to <see cref="BowHeldLength"/>. Returns true to keep the prop.
        /// Never throws: a prop we cannot measure is KEPT (fail-open), because stripping a
        /// weapon on a measurement failure would be a worse regression than the bug it guards.
        /// </summary>
        private static bool PassesScaleSanity(GameObject bowRoot, Transform hand)
        {
            if (bowRoot == null) return false;

            Bounds wb = default;
            bool has = false;
            foreach (var r in bowRoot.GetComponentsInChildren<Renderer>())
            {
                if (r == null) continue;
                if (!has) { wb = r.bounds; has = true; } else wb.Encapsulate(r.bounds);
            }
            if (!has)
            {
                FlowTrace.Warn("Equip", "bow scale check: prop has NO renderer to measure - kept (fail-open).");
                return true;
            }

            Vector3 s = wb.size;
            float longest = Mathf.Max(s.x, Mathf.Max(s.y, s.z));

            // Corrupt bounds: NaN/Inf, or a hand prop claiming to be bigger than a building.
            bool finite = !(float.IsNaN(longest) || float.IsInfinity(longest))
                       && !(float.IsNaN(wb.center.y) || float.IsInfinity(wb.center.y));
            if (!finite || longest > AbsurdBoundsMeters || Mathf.Abs(wb.min.y) > AbsurdBoundsMeters)
            {
                FlowTrace.Fail("Equip",
                    $"bow REMOVED - corrupt bounds. size={s:0.##} min.y={wb.min.y:0.##} " +
                    $"(limit {AbsurdBoundsMeters}m). This is the -33.56m class of defect: the prop's " +
                    "renderer bounds do not describe its geometry. Showing nothing beats showing that.");
                return false;
            }

            float ratio = longest / Mathf.Max(0.0001f, BowHeldLength);
            if (ratio < ScaleSanityMin || ratio > ScaleSanityMax)
            {
                FlowTrace.Fail("Equip",
                    $"bow REMOVED - scale out of band. rendered longest={longest:0.###}m vs intended " +
                    $"{BowHeldLength:0.##}m (ratio {ratio:0.##}x, allowed {ScaleSanityMin:0.##}..{ScaleSanityMax:0.##}x). " +
                    $"handLossy={(hand != null ? hand.lossyScale.y : 1f):0.###}.");
                return false;
            }

            FlowTrace.Step("Equip",
                $"bow scale OK: rendered longest={longest:0.###}m vs intended {BowHeldLength:0.##}m (ratio {ratio:0.##}x).");
            return true;
        }

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

            using var _ = FlowTrace.Enter("Equip", $"HeroBowAttachment.TryAttach on '{name}'");

            if (!_animator.isHuman)
            {
                // Not Humanoid yet (rig still rebinding) OR a generic avatar. Update()'s retry
                // re-calls this; only the retry-exhausted state is a real miss. NOT a hard fail.
                FlowTrace.Warn("Equip", "bow: Animator not Humanoid yet — will retry (cosmetic only)");
                return;
            }

            Transform leftHand = FlowTrace.Try("Equip", "GetBoneTransform(LeftHand)",
                () => _animator.GetBoneTransform(HumanBodyBones.LeftHand), null);
            if (leftHand == null)
            {
                FlowTrace.Fail("Equip", $"bow: Humanoid rig on '{name}' has NO LeftHand bone — " +
                    "bow NOT attached (this is the null-bone cause if it fires).");
                enabled = false;
                return;
            }
            FlowTrace.Step("Equip", $"bow: LeftHand bone resolved -> '{leftHand.name}'");

            GameObject prop = LoadBowPrefab();
            if (prop == null) { FlowTrace.Warn("Equip", "bow: Resources prefab absent -> procedural fallback"); prop = BuildProceduralBow(); }
            if (prop == null)
            {
                FlowTrace.Fail("Equip", "bow: could not load OR build a bow prop — none attached.");
                enabled = false;
                return;
            }

            prop.name = "BowProp";
            // Strip any colliders/rigidbodies a real prefab might carry — purely visual.
            foreach (var c in prop.GetComponentsInChildren<Collider>(true)) if (c != null) Destroy(c);
            foreach (var rb in prop.GetComponentsInChildren<Rigidbody>(true)) if (rb != null) Destroy(rb);

            // Auto-orient the bow to the owner's spec inside a grip root (deterministic,
            // FBX-orientation-independent): longest axis -> +Y, narrowest -> +X.
            //
            // WO-1105 R4 (owner rule): the seat is NO LONGER the bounds CENTRE. A bow's bounding-box
            // centre sits in the HOLLOW between string and belly - empty air beside the wood - so the
            // hand held nothing. WeaponBoundsOrient.GripAnchor.BowGrip keeps the same midpoint on the
            // long axis and projects it PERPENDICULAR out to the first real surface (the stave),
            // which is where a hand can close. Derived from the measured mesh every attach.
            //
            // This ALSO retires this file's private copy of NormalizeInto: there is now ONE grip
            // solver (WeaponBoundsOrient) instead of two that could drift apart. The Core solver
            // scales by the LONGEST MEASURED axis rather than blindly by Y (the 2026-07-06 shield
            // RCA), which the local copy never picked up. resolveBladeUpFromHilt stays FALSE - a bow
            // has no hilt end to resolve, and flipping it would be a behaviour change, not a fix.
            var bowRoot = new GameObject("BowProp");
            DeNelle.Core.Geometry.WeaponBoundsOrient.NormalizeInto(
                prop, bowRoot.transform, BowHeldLength,
                DeNelle.Core.Geometry.WeaponBoundsOrient.GripAnchor.BowGrip,
                resolveBladeUpFromHilt: false);

            bowRoot.transform.SetParent(leftHand, false);
            bowRoot.transform.localPosition = GripLocalPosition;

            // ================================================================
            // THE BOW STANDS UPRIGHT (owner defect 2026-08-16: the bow lay HORIZONTALLY across
            // Sylas' body, "rotated roughly 90 degrees about the grip point").
            // ----------------------------------------------------------------
            // THIS IS NOT THE GRIP-POSITION BUG. The grip POINT is correct and measured
            // (RangedPrimaryRegression 'bow-grip-apex': seat=(0,0,-0.3) err=0m, commit 14a2c66e).
            // What was wrong is the ORIENTATION ONCE SEATED, and the cause was THIS LINE, which
            // used to read:
            //     localRotation = EquipmentController.ApplyGlobalWeaponYaw(Quaternion.Euler(GripLocalEuler));
            // With GripLocalEuler == (0,0,0) that is an IDENTITY hand-local seat, which maps the
            // bow's prop-local +Y (the limb span, put there by NormalizeInto) straight onto the
            // LeftHand bone's OWN local +Y. On this rig that bone axis is the "points out of the
            // fist" direction — correct for a SWORD, which continues the fist, and wrong by ~90
            // degrees for a BOW, whose hand closes AROUND the riser so the limbs run PERPENDICULAR
            // to the fist. Same defect class EquipmentController._staffGripEuler's RC5 note already
            // records for the melee families ("inherited the bone's raw local axes and read
            // SIDEWAYS across the torso"); the bow is the one long-axis family that never got a
            // correction, because ComputeMeleeGripRotation is gated on melee `kind` and the hero's
            // bow never goes through EquipmentController at all.
            //
            // The header above ("the bow arrives in the hand ALREADY oriented to spec — so
            // GripLocalEuler stays ZERO") was the false premise. NormalizeInto orients the bow in
            // the GRIP ROOT's frame; it has no knowledge of the bone. Identity is only right if the
            // bone's +Y happens to be the limb line, which is exactly the assumption
            // ComputeMeleeGripRotation was written to reject.
            //
            // DERIVED, not nudged: ComputeBowHeldRotation builds the target in WORLD from the
            // BODY's own axes (limbs -> body.up, belly -> body.forward) and expresses it in the
            // bone's LOCAL frame — the identical construction ComputeSheathRotation uses, and for
            // the identical reason. GripLocalEuler is KEPT as the felt-tune nudge on top (still
            // zero; tune it against a screenshot, never guess).
            //
            // ApplyGlobalWeaponYaw is DELIBERATELY NOT composed here. That 180-degree yaw exists to
            // correct grips that INHERITED the raw bone axes; a fully-derived world target already
            // points where it should, and yawing it would swing the belly to face BACKWARD, away
            // from the aim. Precedent: ComputeSheathRotation's derived result is likewise used
            // without the yaw (EquipmentController :2029, :2699), while the raw-euler seats beside
            // it (:2694) still take it.
            // ================================================================
            bowRoot.transform.localRotation =
                DeNelle.Core.Geometry.WeaponBoundsOrient.ComputeBowHeldRotation(
                    leftHand, _animator != null ? _animator.transform : transform)
                * Quaternion.Euler(GripLocalEuler);

            // ================================================================
            // THE FIX THAT NEVER REACHED THIS FILE (owner, 2026-08-06: "We had this
            // problem, and we fixed it. But I don't think that fix applied here.")
            // ----------------------------------------------------------------
            // NormalizeInto sizes the bow at the WORLD ORIGIN at unit scale, then
            // SetParent(bone, false) PRESERVES LOCAL scale -- so the rendered size gets
            // multiplied by the bone's lossyScale, which carries VisualFactory.Fit's
            // body-normalization factor. That is exactly the defect
            // EquipmentController.cs:1913-1919 documents and fixes at :913 for hero
            // weapons; this separate attach path never got it.
            //
            // Measured on the owner's device (313794): [Flow:EnemySize] orc-shaman
            // scale=1.887 -- so a 0.92m BowHeldLength rendered at 0.92 * 1.887 = 1.74m,
            // a bow nearly as tall as the 1.90m orc carrying it. She read it as a staff.
            // The same line mis-scales the HERO's own Ranger bow by the hero body's Fit
            // factor, so this is not enemy-only.
            //
            // Divide the parent's lossy scale back out so the world-size solve survives
            // parenting. Applied AFTER pos/rot, which do not depend on scale.
            //
            // ParentScaleCompensation, NOT CompensateParentScale: the latter is PRIVATE to
            // EquipmentController (:1940). The former is deliberately `internal static`
            // (:1932) for exactly this - a same-asmdef, same-namespace third caller - and
            // returns 1/parent.lossyScale, guarding against a degenerate (near-zero) scale.
            // bowRoot's own localScale is 1 here (NormalizeInto scaled the CHILD prop, not
            // this root), so assigning the compensation directly is correct; there is no
            // owner-dialed authored scale on this path to preserve.
            bowRoot.transform.localScale = EquipmentController.ParentScaleCompensation(leftHand);
            // ================================================================

            // ================================================================
            // LAST-MINUTE SCALE SANITY CHECK (owner, 2026-08-06: "or enforce a last
            // minute scaling check, if it fails remove weapon").
            // ----------------------------------------------------------------
            // The compensation above fixes the KNOWN cause. This catches the UNKNOWN
            // ones. A prop whose rendered size bears no relation to BowHeldLength is
            // broken art or broken bounds, and showing it is worse than showing
            // nothing -- on device a BowProp reported bounds.min.y = -33.56m, which
            // then dragged the ground-snap 36m and was only survivable because of a
            // MaxFootGap clamp in Enemy.cs. A guard that strips the prop turns a
            // silent visual catastrophe into a loud, single log line.
            //
            // Deliberately generous band: this is a CATASTROPHE detector, not a
            // tuning assert. Anything inside 0.25x..3x of the intended held length
            // passes untouched.
            if (!PassesScaleSanity(bowRoot, leftHand))
            {
                Destroy(bowRoot);
                _bow = null;
                enabled = false;
                return;
            }

            _bow = bowRoot;
            // §12: report the RESOLVED seat, not the authored constants. `nudge` is still the
            // authored GripLocalEuler, but `localEuler` is what the bow actually got — and
            // `limbTiltFromVertical` is the defect number itself, re-measured on the FINAL
            // transform after parenting + scale compensation, so nothing downstream of the solve
            // can quietly re-tip it. Upright reads ~0 deg; the pre-fix horizontal seat read ~90.
            Vector3 limbWorld = bowRoot.transform.rotation * Vector3.up;
            Transform bodyT = _animator != null ? _animator.transform : transform;
            FlowTrace.Step("Equip", $"bow ATTACHED + auto-oriented to LeftHand '{leftHand.name}' " +
                $"(pos={GripLocalPosition} nudge={GripLocalEuler} " +
                $"localEuler={bowRoot.transform.localRotation.eulerAngles:0.#}, " +
                $"limbAxisWorld={limbWorld:0.##} " +
                $"limbTiltFromVertical={Vector3.Angle(limbWorld, bodyT.up):0.#}deg, " +
                $"parentLossy={leftHand.lossyScale.y:0.###} divided out -> localScale={bowRoot.transform.localScale.y:0.###})");
            enabled = false;
        }

        // NOTE (WO-1105 R4): this file's private NormalizeInto / TryLocalBounds / Axis copies were
        // DELETED. They duplicated DeNelle.Core.Geometry.WeaponBoundsOrient and had already drifted
        // from it (they scaled by the post-align Y extent rather than the LONGEST MEASURED axis —
        // the exact bug the 2026-07-06 shield RCA fixed in the Core solver). TryAttach now calls the
        // Core solver with GripAnchor.BowGrip. One grip solver, one place to fix.

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
        /// GameObject, one MeshRenderer, no asset dependency. Native arc ~0.9m; the
        /// caller (NormalizeInto) forces exact BowHeldLength (0.92m) via bounds scale.
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
