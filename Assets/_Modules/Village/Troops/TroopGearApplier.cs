// =============================================================================
// TroopGearApplier — seats weapon / offhand meshes on a troop body (WO-troop-gear).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village
//
// TroopFactory skins the body only; this step attaches optional gear so a
// Spearman does not look identical to a bare Footman. Prefers Resources paths
// (TroopGear/* mirrored from Supercyan by SupercyanResourceWire). Falls back to
// a thin primitive prop when a path is missing so combat still reads as armed.
//
// Bones: Humanoid RightHand for main weapon, LeftHand for offhand/shield/bow.
// Grip transforms are coarse defaults for 1.8 m humanoids — tune via def fields
// later if needed. Colliders on gear are stripped (visual only).
// =============================================================================

using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>Attaches troop weapon/offhand visuals after the body is skinned.</summary>
    public static class TroopGearApplier
    {
        public static void Apply(GameObject visualRoot, TroopDef def)
        {
            if (visualRoot == null || def == null) return;

            string weapon = def.Weapon;
            string offhand = def.Offhand;
            if (string.IsNullOrEmpty(weapon) && string.IsNullOrEmpty(offhand)) return;

            var anim = visualRoot.GetComponentInChildren<Animator>(true);
            if (anim == null || !anim.isHuman)
            {
                FlowTrace.Warn("TroopGear",
                    $"id={def.Id}: no humanoid Animator — gear skipped " +
                    $"(weapon='{weapon ?? ""}' offhand='{offhand ?? ""}').");
                return;
            }

            if (!string.IsNullOrEmpty(weapon))
            {
                bool bow = weapon.IndexOf("bow", System.StringComparison.OrdinalIgnoreCase) >= 0
                           || weapon.IndexOf("Bow", System.StringComparison.OrdinalIgnoreCase) >= 0;
                HumanBodyBones bone = bow ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
                Attach(visualRoot, anim, bone, weapon, isOffhand: false, isBow: bow, troopId: def.Id);
            }

            if (!string.IsNullOrEmpty(offhand))
            {
                Attach(visualRoot, anim, HumanBodyBones.LeftHand, offhand,
                    isOffhand: true, isBow: false, troopId: def.Id);
            }
        }

        private static void Attach(GameObject visualRoot, Animator anim, HumanBodyBones boneId,
            string resourcesPath, bool isOffhand, bool isBow, string troopId)
        {
            Transform hand = null;
            try { hand = anim.GetBoneTransform(boneId); }
            catch { /* invalid avatar */ }

            if (hand == null)
            {
                FlowTrace.Warn("TroopGear",
                    $"id={troopId}: bone {boneId} missing — cannot attach '{resourcesPath}'.");
                return;
            }

            // Strip prior gear on this hand (reconfigure / re-skin).
            for (int i = hand.childCount - 1; i >= 0; i--)
            {
                var ch = hand.GetChild(i);
                if (ch != null && ch.name.StartsWith("TroopGear_", System.StringComparison.Ordinal))
                    Object.Destroy(ch.gameObject);
            }

            GameObject instance = null;
            var prefab = Resources.Load<GameObject>(resourcesPath);
            if (prefab != null)
            {
                instance = Object.Instantiate(prefab, hand, false);
                instance.name = "TroopGear_" + prefab.name;
            }
            else
            {
                instance = BuildPrimitiveFallback(resourcesPath, isOffhand, isBow);
                instance.transform.SetParent(hand, false);
                FlowTrace.Warn("TroopGear",
                    $"id={troopId}: Resources '{resourcesPath}' missing — primitive fallback.");
            }

            // Strip colliders — troops use the body capsule only.
            foreach (var c in instance.GetComponentsInChildren<Collider>(true))
                Object.Destroy(c);

            ApplyDefaultGrip(instance.transform, isOffhand, isBow, resourcesPath);
            FlowTrace.Step("TroopGear",
                $"id={troopId}: attached '{resourcesPath}' on {boneId} " +
                $"(src={(prefab != null ? "Resources" : "primitive")}).");
        }

        private static void ApplyDefaultGrip(Transform t, bool isOffhand, bool isBow, string path)
        {
            // Coarse grips for ~1.8 m Supercyan / Tripo humanoids.
            bool spear = path.IndexOf("spear", System.StringComparison.OrdinalIgnoreCase) >= 0
                         || path.IndexOf("Spear", System.StringComparison.OrdinalIgnoreCase) >= 0;
            bool staff = path.IndexOf("staff", System.StringComparison.OrdinalIgnoreCase) >= 0
                         || path.IndexOf("Staff", System.StringComparison.OrdinalIgnoreCase) >= 0;
            bool axe = path.IndexOf("axe", System.StringComparison.OrdinalIgnoreCase) >= 0
                       || path.IndexOf("Axe", System.StringComparison.OrdinalIgnoreCase) >= 0;
            bool shield = isOffhand || path.IndexOf("shield", System.StringComparison.OrdinalIgnoreCase) >= 0
                          || path.IndexOf("Shield", System.StringComparison.OrdinalIgnoreCase) >= 0;

            if (isBow)
            {
                // ⚠ KNOWN GAP, DELIBERATELY NOT CHANGED HERE (2026-08-16). The owner's canonical bow
                // rule is UNIVERSAL - "all players and enemies follow this rule" - and every OTHER
                // bow path now DERIVES its seat from the rig via
                // DeNelle.Core.Geometry.WeaponBoundsOrient.ComputeBowHeldRotation (read its header:
                // it carries her four-clause rule verbatim). The hero + enemy archers get it through
                // HeroBowAttachment; companions and non-ranger classes through
                // EquipmentController.AttachLoadedProp's bow branch, drawn AND sheathed.
                //
                // THIS line is the last dialed constant, and the Euler below is exactly the kind of
                // one-axis guess the rule rejects: it can only pitch the prop, so it has no answer
                // for which way the BELLY faces (clauses 2 and 4 - string parallel to and nearest
                // the person, hand on the curved edge furthest from the person). It is left alone
                // tonight for a REASON, not an oversight: TroopGearApplier instantiates the troop
                // prefab RAW - it never runs NormalizeInto - so prop-local +Y is not guaranteed to
                // be the limb span and the derivation's premise does not hold here. Routing this
                // through it means first normalising every troop prop, which re-sizes ALL troop
                // gear (this method also seats spears, staves, axes, shields off the same path).
                // That is its own lane with its own felt-check, not a rider on the bow fix.
                t.localPosition = new Vector3(0.02f, 0.04f, 0.02f);
                t.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                t.localScale = Vector3.one * 1.0f;
            }
            else if (shield)
            {
                t.localPosition = new Vector3(0.05f, 0.05f, 0.02f);
                t.localRotation = Quaternion.Euler(0f, 90f, 0f);
                t.localScale = Vector3.one * 1.0f;
            }
            else if (staff)
            {
                t.localPosition = new Vector3(0.01f, 0.03f, 0f);
                t.localRotation = Quaternion.Euler(0f, 0f, 90f);
                t.localScale = Vector3.one * 1.0f;
            }
            else if (spear)
            {
                t.localPosition = new Vector3(0.01f, 0.02f, 0f);
                t.localRotation = Quaternion.Euler(0f, 0f, 90f);
                t.localScale = Vector3.one * 1.0f;
            }
            else if (axe)
            {
                t.localPosition = new Vector3(0.02f, 0.03f, 0f);
                t.localRotation = Quaternion.Euler(0f, 0f, 80f);
                t.localScale = Vector3.one * 1.0f;
            }
            else
            {
                // Sword default
                t.localPosition = new Vector3(0f, 0.02f, 0f);
                t.localRotation = Quaternion.Euler(0f, 0f, 90f);
                t.localScale = Vector3.one * 1.0f;
            }
        }

        private static GameObject BuildPrimitiveFallback(string path, bool isOffhand, bool isBow)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "TroopGear_Fallback";
            bool spear = path != null && path.IndexOf("spear", System.StringComparison.OrdinalIgnoreCase) >= 0;
            bool staff = path != null && path.IndexOf("staff", System.StringComparison.OrdinalIgnoreCase) >= 0;
            bool shield = isOffhand || (path != null && path.IndexOf("shield", System.StringComparison.OrdinalIgnoreCase) >= 0);

            Vector3 scale;
            Color color;
            if (isBow)
            {
                scale = new Vector3(0.05f, 0.7f, 0.12f);
                color = new Color(0.45f, 0.32f, 0.18f);
            }
            else if (shield)
            {
                scale = new Vector3(0.45f, 0.55f, 0.08f);
                color = new Color(0.55f, 0.55f, 0.6f);
            }
            else if (staff)
            {
                scale = new Vector3(0.04f, 1.2f, 0.04f);
                color = new Color(0.5f, 0.4f, 0.28f);
            }
            else if (spear)
            {
                scale = new Vector3(0.03f, 1.4f, 0.03f);
                color = new Color(0.65f, 0.65f, 0.7f);
            }
            else
            {
                scale = new Vector3(0.04f, 0.7f, 0.04f);
                color = new Color(0.72f, 0.73f, 0.76f);
            }
            go.transform.localScale = scale;

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (sh != null)
                {
                    var mat = new Material(sh);
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                    if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
                    mr.sharedMaterial = mat;
                }
            }
            return go;
        }
    }
}
