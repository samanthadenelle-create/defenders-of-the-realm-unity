// =============================================================================
// GearVisualApplier — attaches simple visual representations for equipped gear.
// -----------------------------------------------------------------------------
// Called from HeroBodySwapper after the class FBX body + animator + textures are
// ready (so bones exist), and from GearLoadout after manual shop/equip changes.
// Uses HumanBodyBones where possible for hand/back placement. Falls back to
// local offsets on the body root.
//
// Produces "sword on hand, staff, mace, shield accent for knight, plate/leather/robes accents"
// (bow visuals intentionally skipped here -- see AttachWeaponVisual).
// Uses primitive meshes + URP Lit mats (colored to read as metal/leather/cloth).
// Children are tagged for easy clear on re-apply.
//
// Ranger bow is provided by dedicated HeroBowAttachment (LeftHand bone, real/procedural
// KayKit prop, auto-sized via NormalizeInto to ~0.92m held). We skip the legacy
// back-sheathed primitive in the applier to avoid duplicate bows on the ranger.
// Sword/staff/mace use RightHand bone + tuned localScale/Pos/Euler for ~1.8m hero
// proportions (sword ~0.65m long/thin, etc).
//
// This satisfies the "visually attach them via HeroBodySwapper / VisualFactory"
// requirement for default gear + shop equip without requiring new imported meshes.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    public static class GearVisualApplier
    {
        private const string VisualTag = "GearVisual";

        /// <summary>
        /// Master switch for the placeholder PRIMITIVE gear (cubes for sword/shield/chest).
        /// Default OFF: until real low-poly gear meshes exist, these cubes read as "a square
        /// stuck inside the hero" — especially when the Humanoid avatar is invalid and bone
        /// lookups fail. Set true to re-enable the stand-in cubes; swap for real meshes later.
        /// </summary>
        public static bool EnablePrimitiveGear = false;

        /// <summary>Clear previous gear attachments under the body, then attach fresh based on loadout.</summary>
        public static void Apply(Transform body, GearLoadout loadout)
        {
            if (body == null) return;
            ClearExisting(body);   // always clear stale cubes (removes any existing squares too)

            if (!EnablePrimitiveGear) return;
            if (loadout == null) return;

            // Ensure animator is available for bone lookups (post-swapper).
            var anim = body.GetComponentInChildren<Animator>();
            if (anim == null)
            {
                // Try on parent (hero root often carries after re-cache).
                var heroRoot = body.parent != null ? body.parent : body;
                anim = heroRoot.GetComponentInChildren<Animator>();
            }

            // Weapon visuals (hand or back for bow).
            if (loadout.EquippedWeapon != null)
            {
                AttachWeaponVisual(body, anim, loadout.EquippedWeapon);
            }

            // Armor visuals (accents + shield for knight).
            if (loadout.EquippedArmor != null)
            {
                AttachArmorVisual(body, anim, loadout.EquippedArmor);
            }
        }

        public static void ReapplyForHero(GearLoadout loadout)
        {
            if (loadout == null) return;
            var body = loadout.transform.Find("HeroBody");
            if (body != null)
                Apply(body, loadout);
        }

        private static void ClearExisting(Transform body)
        {
            // Destroy prior tagged children (idempotent re-apply).
            for (int i = body.childCount - 1; i >= 0; i--)
            {
                var c = body.GetChild(i);
                if (c != null && c.name != null && c.name.Contains(VisualTag))
                {
                    Object.Destroy(c.gameObject);
                }
            }
            // Also scan one level under body children (bones).
            var anim = body.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                var root = anim.transform;
                for (int i = root.childCount - 1; i >= 0; i--)
                {
                    var c = root.GetChild(i);
                    if (c != null && c.name != null && c.name.Contains(VisualTag))
                        Object.Destroy(c.gameObject);
                }
            }
        }

        private static void AttachWeaponVisual(Transform body, Animator anim, WeaponDef w)
        {
            if (w == null || string.IsNullOrEmpty(w.id)) return;
            string id = w.id.ToLowerInvariant();

            Transform parent = null;
            Vector3 localPos = Vector3.zero;
            Vector3 localEuler = Vector3.zero;
            Vector3 localScale = Vector3.one;
            Color color = new Color(0.75f, 0.75f, 0.78f);
            string name = "GearVisual_Weapon";

            bool isBow = id.Contains("bow");
            bool isStaff = id.Contains("staff");
            bool isMace = id.Contains("mace");

            if (isBow)
            {
                // SKIP: Ranger bow visuals are exclusively handled by HeroBowAttachment.
                // It attaches a proper (KayKit or procedural) bow to the LeftHand bone
                // (HumanBodyBones.LeftHand) using NormalizeInto + Grip* + BowHeldLength=0.92f.
                // A back-sheathed primitive here would duplicate the held bow (and the old
                // scale 0.9/pos 0.9 was massively oversized anyway). Ranger's equipped
                // "starter_ranger_bow" still triggers this path via GearLoadout, so we
                // intentionally produce no GearVisual_* for bows. Melee weapons (sword for
                // knight, staff mage, mace cleric) use RightHand. Confirmed bones:
                // RightHand for melee, LeftHand/Spine (but spine skipped for bow) for bow.
                // If a small sheathed back bow is desired later for idle stances, re-enable
                // a reduced version (e.g. scale Y~0.4) behind an "isSheathed" check.
            }
            else
            {
                // Sword / staff / mace in right hand. (Attachment uses HumanBodyBones.RightHand.)
                parent = anim != null ? anim.GetBoneTransform(HumanBodyBones.RightHand) : null;
                // If the hand bone isn't found (e.g. avatar not a valid Humanoid), skip rather
                // than dumping a cube on the body root where it reads as a square in the torso.
                if (parent == null) return;
                if (isStaff)
                {
                    localPos = new Vector3(0.01f, 0.03f, 0.0f);
                    localEuler = new Vector3(0f, 0f, 88f);
                    localScale = new Vector3(0.04f, 1.20f, 0.04f); // ~1.20m long staff, thin (was 1.35/0.06 oversized for 1.8m hero)
                    color = new Color(0.6f, 0.55f, 0.45f);
                    name = "GearVisual_Staff";
                }
                else if (isMace)
                {
                    localPos = new Vector3(0.02f, 0.04f, 0.0f);
                    localEuler = new Vector3(-5f, 0f, 82f);
                    localScale = new Vector3(0.055f, 0.62f, 0.055f); // ~0.62m mace, reasonably thin (was 0.55/0.09 stubby)
                    color = new Color(0.7f, 0.65f, 0.55f);
                    name = "GearVisual_Mace";
                }
                else
                {
                    // Default sword (knight). ~0.65m long / thin blade per spec for 1.8m hero.
                    localPos = new Vector3(0.0f, 0.01f, 0.0f);
                    localEuler = new Vector3(0f, 0f, 90f);
                    localScale = new Vector3(0.035f, 0.65f, 0.035f); // was 0.85/0.07 -- too long + thick
                    color = new Color(0.72f, 0.73f, 0.76f);
                    name = "GearVisual_Sword";
                }
            }

            if (isBow || parent == null)
            {
                // No weapon visual cube for bow (ranger handled separately) or missing bone.
                return;
            }

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            go.transform.localScale = localScale;

            // Metal/wood look via URP Lit.
            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (sh != null)
                {
                    var mat = new Material(sh);
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                    if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
                    // (isBow branch removed: bows no longer produce GearVisual cubes here;
                    // ranger bow is the separate normalized prop from HeroBowAttachment.)
                    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.65f);
                    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.55f);
                    mr.sharedMaterial = mat;
                }
            }

            // (Legacy bow string cross-piece block removed: bows no longer create
            // any GearVisual geometry in this applier; the ranger's held bow is
            // attached+strung via the dedicated HeroBowAttachment + its own procedural
            // or prefab bow.)
        }

        private static void AttachArmorVisual(Transform body, Animator anim, ArmorDef a)
        {
            if (a == null || string.IsNullOrEmpty(a.id)) return;
            string id = a.id.ToLowerInvariant();

            // Plate / shield accent for knight (or leather/robes tint + simple geo).
            bool isPlate = id.Contains("plate") || id.Contains("knight");
            bool isShield = id.Contains("shield");
            bool isLeather = id.Contains("leather");
            bool isRobes = id.Contains("robe");

            // Torso accent (plate/leather/robes "chest").
            Transform chestParent = anim != null ? anim.GetBoneTransform(HumanBodyBones.Chest) : null;
            if (chestParent == null) chestParent = anim != null ? anim.GetBoneTransform(HumanBodyBones.Spine) : null;
            if (chestParent == null) return; // no torso bone (invalid avatar) -> don't place a cube on the root

            Color accentColor = isPlate ? new Color(0.55f, 0.56f, 0.60f) :
                                isLeather ? new Color(0.45f, 0.32f, 0.22f) :
                                isRobes ? new Color(0.35f, 0.28f, 0.42f) : new Color(0.5f, 0.5f, 0.52f);

            var chest = GameObject.CreatePrimitive(PrimitiveType.Cube);
            chest.name = "GearVisual_ArmorChest";
            chest.transform.SetParent(chestParent, false);
            chest.transform.localPosition = new Vector3(0f, 0.08f, 0.0f);
            chest.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            chest.transform.localScale = isRobes ? new Vector3(0.42f, 0.38f, 0.22f) : new Vector3(0.38f, 0.32f, 0.18f);

            var cmr = chest.GetComponent<MeshRenderer>();
            if (cmr != null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (sh != null)
                {
                    var cmat = new Material(sh);
                    if (cmat.HasProperty("_BaseColor")) cmat.SetColor("_BaseColor", accentColor);
                    if (cmat.HasProperty("_Color")) cmat.SetColor("_Color", accentColor);
                    if (isPlate)
                    {
                        if (cmat.HasProperty("_Metallic")) cmat.SetFloat("_Metallic", 0.7f);
                        if (cmat.HasProperty("_Smoothness")) cmat.SetFloat("_Smoothness", 0.45f);
                    }
                    cmr.sharedMaterial = cmat;
                }
            }

            // Knight shield (left forearm or hip when "shield" armor selected, or always for plate).
            if (isPlate || isShield)
            {
                Transform left = anim != null ? anim.GetBoneTransform(HumanBodyBones.LeftLowerArm) : null;
                if (left == null) left = body;
                var shield = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shield.name = "GearVisual_Shield";
                shield.transform.SetParent(left, false);
                shield.transform.localPosition = new Vector3(-0.08f, 0.0f, 0.0f);
                shield.transform.localRotation = Quaternion.Euler(0f, -15f, -20f);
                shield.transform.localScale = new Vector3(0.12f, 0.55f, 0.42f);

                var smr = shield.GetComponent<MeshRenderer>();
                if (smr != null)
                {
                    var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    if (sh != null)
                    {
                        var smat = new Material(sh);
                        var sc = new Color(0.58f, 0.60f, 0.64f);
                        if (smat.HasProperty("_BaseColor")) smat.SetColor("_BaseColor", sc);
                        if (smat.HasProperty("_Color")) smat.SetColor("_Color", sc);
                        if (smat.HasProperty("_Metallic")) smat.SetFloat("_Metallic", 0.55f);
                        if (smat.HasProperty("_Smoothness")) smat.SetFloat("_Smoothness", 0.4f);
                        smr.sharedMaterial = smat;
                    }
                }
            }

            // Optional pauldron accents for plate.
            if (isPlate)
            {
                Transform rShoulder = anim != null ? anim.GetBoneTransform(HumanBodyBones.RightUpperArm) : null;
                if (rShoulder != null)
                {
                    var pauldron = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    pauldron.name = "GearVisual_PauldronR";
                    pauldron.transform.SetParent(rShoulder, false);
                    pauldron.transform.localPosition = new Vector3(0.0f, 0.12f, 0.0f);
                    pauldron.transform.localScale = new Vector3(0.22f, 0.18f, 0.16f);
                    var pmr = pauldron.GetComponent<MeshRenderer>();
                    if (pmr != null)
                    {
                        var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                        if (sh != null)
                        {
                            var pmat = new Material(sh);
                            var pc = new Color(0.52f, 0.53f, 0.57f);
                            if (pmat.HasProperty("_BaseColor")) pmat.SetColor("_BaseColor", pc);
                            if (pmat.HasProperty("_Color")) pmat.SetColor("_Color", pc);
                            if (pmat.HasProperty("_Metallic")) pmat.SetFloat("_Metallic", 0.6f);
                            pmr.sharedMaterial = pmat;
                        }
                    }
                }
            }
        }
    }
}