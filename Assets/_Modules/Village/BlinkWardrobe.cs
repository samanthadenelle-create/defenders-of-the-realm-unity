// =============================================================================
// BlinkWardrobe — the DRESSABLE capability for skinned humanoid bodies (TKT-2).
// -----------------------------------------------------------------------------
// Owner architecture (2026-06-20): dressing a character is a CAPABILITY that lives
// at the SAME level as the animation rig — i.e. right where VisualFactory.Skin builds
// the skinned body — NOT a feature bolted onto HeroArmorVisual (a gameplay layer that
// only some bodies carry). A body either IS dressable (it ships outfit-set renderers)
// or it isn't (a skeleton/animal enemy). So the wardrobe is generic + capability-gated:
//   * Hero, companions, arena fighters, and any FUTURE human-skinned enemy get a default
//     outfit for free — no per-spawn code.
//   * Non-human bodies are not dressable → DressInStarter no-ops on them safely.
//
// The Blink human body is modular: a bare-skin mannequin (Arms/Legs/Chest/Feet + skin
// meshes) PLUS swappable OUTFIT sets whose renderers are name-prefixed (Starter_*,
// Cloth1_*, Cloth2_*, Cloth3_*). With NO outfit shown the body reads as underwear (bare
// skin). DressInStarter puts the body into its DRESSED default: the Starter outfit + skin
// + bare arms (Starter is sleeveless — a look the owner likes), hiding the bare torso/legs
// the outfit covers and every OTHER outfit set.
//
// DATA-READY (the next layer — WO-456): the default outfit is a PARAMETER, not hardcoded.
// A per-character wardrobe JSON (default + owned outfits) will drive it, and that same
// collection feeds the cosmetic store (buy -> add to the collection -> equippable). This
// file is the rig-level SEAM that system plugs into; today it defaults to "Starter".
// =============================================================================

using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    public static class BlinkWardrobe
    {
        // The default outfit set every dressable body wears until a wardrobe choice / armor overrides
        // it. WO-456 replaces this constant with a per-character JSON field.
        public const string DefaultOutfit = "Starter";

        // CAPABILITY GATE: a body is DRESSABLE if it ships any outfit-set renderer (Starter_*/Cloth*_*).
        // A skeleton/animal/structure body has none → not dressable → the wardrobe leaves it untouched.
        public static bool IsDressable(GameObject body)
        {
            if (body == null) return false;
            foreach (var r in body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (r != null && IsOutfitPart(r.name)) return true;
            return false;
        }

        // Put the body into its canonical DRESSED state for the given outfit set: that outfit's pieces
        // + skin (head/hands/face) + bare arms (outfit sets here are sleeveless), hiding the bare
        // torso/legs/feet the outfit covers AND every OTHER outfit set. Deterministic by renderer name
        // (no snapshot), idempotent, so it can run on rig build, on unequip, and under pieces armor.
        public static void DressInStarter(GameObject body) => Dress(body, DefaultOutfit);

        public static void Dress(GameObject body, string outfit)
        {
            if (body == null) return;
            if (string.IsNullOrEmpty(outfit)) outfit = DefaultOutfit;

            int shown = 0, hidden = 0;
            var shownNames = new System.Text.StringBuilder();
            foreach (var r in body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r == null) continue;
                bool show = IsSkinRenderer(r.name) || IsOutfitOf(r.name, outfit) || IsBareArm(r.name);
                if (r.enabled != show) r.enabled = show;
                if (show)
                {
                    shown++;
                    if (shownNames.Length < 200)
                    { if (shownNames.Length > 0) shownNames.Append(", "); shownNames.Append(r.name); }
                }
                else hidden++;
            }

            FlowTrace.Step("Wardrobe",
                $"Dress '{body.name}' in '{outfit}': outfit + skin + bare arms shown ({shown}: [{shownNames}]), " +
                $"hid {hidden} bare-torso/legs + other outfit set(s) — never underwear.");
        }

        // ── Renderer-name vocabulary (the single home; HeroArmorVisual references these) ──────────

        // SKIN meshes that always stay visible under any outfit/armor (head/hands/face/hair…).
        public static bool IsSkinRenderer(string n)
        {
            if (string.IsNullOrEmpty(n)) return false;
            n = n.ToLowerInvariant();
            return n.Contains("head") || n.Contains("hand") || n.Contains("neck") ||
                   n.Contains("face") || n.Contains("ear")  || n.Contains("eye")  ||
                   n.Contains("brow") || n.Contains("lash") || n.Contains("hair") ||
                   n.Contains("beard") || n.Contains("moustache") || n.Contains("mustache") ||
                   n.Contains("teeth") || n.Contains("tongue");
        }

        // Any swappable OUTFIT-set piece (set-prefixed: Starter_Chest, Cloth1_Pants…). Used by the
        // capability gate and to hide the non-chosen sets.
        public static bool IsOutfitPart(string n)
        {
            if (string.IsNullOrEmpty(n)) return false;
            n = n.ToLowerInvariant();
            return n.StartsWith("starter") || n.StartsWith("cloth");
        }

        // A piece of the SPECIFIC chosen outfit set (prefix match, e.g. "starter" -> Starter_Chest).
        public static bool IsOutfitOf(string n, string outfit)
        {
            if (string.IsNullOrEmpty(n) || string.IsNullOrEmpty(outfit)) return false;
            return n.ToLowerInvariant().StartsWith(outfit.ToLowerInvariant());
        }

        // The bare ARMS mesh (single anatomy token, no set-prefix). The Blink outfit sets are sleeveless
        // so the arms stay bare skin under them (a look the owner likes). Other bare anatomy
        // (Chest/Legs/Feet) is HIDDEN because the outfit covers it.
        public static bool IsBareArm(string n)
        {
            if (string.IsNullOrEmpty(n) || n.Contains("_")) return false;
            n = n.ToLowerInvariant();
            return n == "arm" || n == "arms";
        }
    }
}
