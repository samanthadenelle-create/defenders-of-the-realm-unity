// =============================================================================
// EnemyBodyColorGuard — the colour half of the enemy render-verify.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village (Village/Enemies). Armed ONLY by EnemyFactory.TrySkinBody,
// which is the single place an enemy body is skinned — so it covers the normal spawn
// path and the EnemyLateSkinner re-skin identically, by construction.
//
// ⛔ WHY THIS EXISTS (owner report 2026-08-20: "enemies not having coloring").
// EnemyFactory already RENDER-verifies every skinned body (VerifyVisualRenders:
// "does an enabled renderer carry a mesh?"). Nothing ever COLOUR-verified it. The
// captured session (logs/device/enemy-color.log, pid 6783) shows exactly the blind
// spot that produced:
//
//   14:07:55.492  [Flow:TripoMatFix] NO ALBEDO on 'Orc_Shaman(Clone)' ... tint=(0.45,0.30,0.20)
//                 - "the URP rebuild took but bound NO base map, so this mesh renders
//                    as flat tint. A shader-only VERIFY calls this OK."
//
// ...and, worse, says NOTHING AT ALL for the models that never get a
// TripoMaterialFixer. In that same capture 12 of the spawns were Skeleton_Minion
// (rig HumanoidMedium) and 4 were Skeleton_Rogue (SkeletonHumanoid): neither rig is
// in EnemyFactory's FixTripoMaterials branches, so for the MAJORITY of the enemies
// the owner actually saw there is not one line of colour evidence in the trace. A
// defect that cannot be seen in the data is the §12 failure, not just a bug.
//
// WHAT IT DOES, and the order matters:
//   1. WAITS one frame. TripoMaterialFixer rebuilds materials in its own Awake/Start
//      AFTER TrySkinBody returns (proof: the [Flow:TripoMatFix] "-> Run on 'Troll(Clone)'"
//      lines land after the "garrison albedo ... bound" line for the same body). Auditing
//      inside TrySkinBody would read a material the fixer is about to replace.
//   2. AUDITS THE FINAL RENDERED STATE — per renderer, per material slot: is a base map
//      actually bound, and what colour is the slot painted?
//   3. NAMES a slot that is textureless AND achromatic. That is "no colouring" in the
//      only sense a player can see: a white / grey / unpainted body.
//   4. REPAIRS it with the family tint via a MaterialPropertyBlock — per RENDERER, never
//      by touching a material. TripoMaterialFixer shares one Material instance across
//      every body with the same look (its MatKey cache, TripoMaterialFixer.cs:105); a
//      sharedMaterial write here would repaint every other enemy that shares it.
//
// ⛔ NOTHING HERE LOADS, DOWNLOADS OR BLOCKS. It reads material properties already in
// memory and writes a property block. It self-destructs after its second audit.
// =============================================================================

using DeNelle.Core;
using DeNelle.Core.Diagnostics;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>How one material slot on an enemy body reads to a player.</summary>
    public enum EnemySlotColor
    {
        /// <summary>A base map is bound — the authored skin is what renders.</summary>
        Textured,
        /// <summary>No base map, but a deliberate chromatic tint (the WO-790 family colours).</summary>
        Painted,
        /// <summary>Emissive/glow slot — colour comes from emission, not albedo. Left alone.</summary>
        Emissive,
        /// <summary>⛔ No base map AND no chroma: a white/grey body. THE DEFECT.</summary>
        Unpainted
    }

    /// <summary>
    /// Audits — and repairs — the final albedo of a skinned enemy body, so "enemies not
    /// having coloring" names itself in the trace instead of being felt by the owner.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyBodyColorGuard : MonoBehaviour
    {
        /// <summary>
        /// Below this max-minus-min channel spread a colour is ACHROMATIC — white, grey,
        /// black. Every authored family tint clears it with room to spare (the tightest is
        /// the ogre grey 0.48/0.47/0.52 at 0.05, and the Warlord slate 0.22/0.20/0.26 at
        /// 0.06), while TripoMaterialFixer's own unpainted default — a flat 0.5/0.5/0.5
        /// (TripoMaterialFixer.cs:121) — scores 0.00 and is caught. Deliberately keyed on
        /// CHROMA, not brightness: the mid-grey default is exactly as much "no colouring"
        /// as solid white, and a brightness rule would have missed it.
        /// </summary>
        public const float ChromaFloor = 0.04f;

        /// <summary>Seconds before the second (final) audit. One late texture bind — an
        /// atlas that resolves a frame or two after the rebuild — must not be reported as a
        /// permanent defect, so the verdict is taken twice and the LAST one wins.</summary>
        public const float SettleSeconds = 0.35f;

        // ── Headless-readable proof counters (regression + AutoPilot oracles) ──────
        /// <summary>Slots seen textureless + achromatic across the session.</summary>
        public static int UnpaintedSlotsFound { get; private set; }
        /// <summary>Slots this guard repainted with a family tint.</summary>
        public static int SlotsRepaired { get; private set; }
        /// <summary>The most recent audit line, verbatim.</summary>
        public static string LastReport { get; private set; } = "";

        /// <summary>Zero the counters (headless fixtures re-run the same scene).</summary>
        public static void ResetCounters()
        {
            UnpaintedSlotsFound = 0;
            SlotsRepaired = 0;
            LastReport = "";
        }

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private EnemyDef _def;
        private string _model;
        private string _rig;
        private Color _familyTint = HostilePalette.PlaceholderBodyTint;
        private int _audits;
        private float _nextAuditAt;

        /// <summary>
        /// PURE classifier — no Unity objects, so the EditMode regression can assert BOTH
        /// directions (a textured slot passes, an unpainted one FAILS) with no scene.
        /// </summary>
        /// <param name="hasAlbedo">A base map (_BaseMap or _MainTex) is bound on the slot.</param>
        /// <param name="baseColor">The slot's albedo colour (_BaseColor, else _Color/material.color).</param>
        /// <param name="emissive">The slot emits — its look does not come from albedo.</param>
        public static EnemySlotColor Classify(bool hasAlbedo, Color baseColor, bool emissive)
        {
            if (hasAlbedo) return EnemySlotColor.Textured;
            if (emissive) return EnemySlotColor.Emissive;

            float max = Mathf.Max(baseColor.r, Mathf.Max(baseColor.g, baseColor.b));
            float min = Mathf.Min(baseColor.r, Mathf.Min(baseColor.g, baseColor.b));
            return (max - min) < ChromaFloor ? EnemySlotColor.Unpainted : EnemySlotColor.Painted;
        }

        /// <summary>
        /// Arm the colour audit on a freshly skinned body. Idempotent; a re-skin just
        /// refreshes the model + tint and restarts the two-audit cycle.
        /// </summary>
        internal static void Arm(GameObject visual, EnemyDef def, string model, string rig, Color familyTint)
        {
            if (visual == null) return;

            var guard = visual.GetComponent<EnemyBodyColorGuard>();
            if (guard == null) guard = visual.AddComponent<EnemyBodyColorGuard>();
            guard._def = def;
            guard._model = model;
            guard._rig = rig;
            guard._familyTint = familyTint;
            guard._audits = 0;
            guard._nextAuditAt = 0f;
        }

        private void LateUpdate()
        {
            // First audit runs on the frame AFTER the skin, so TripoMaterialFixer's
            // Awake/Start rebuild has already happened and we read the FINAL material.
            if (_audits == 0)
            {
                Audit(final: false);
                _audits = 1;
                _nextAuditAt = Time.realtimeSinceStartup + SettleSeconds;
                return;
            }

            if (Time.realtimeSinceStartup < _nextAuditAt) return;
            Audit(final: true);
            Destroy(this);
        }

        private void Audit(bool final)
        {
            string id = _def != null ? (_def.Id ?? "?") : "?";
            int textured = 0, painted = 0, emissive = 0, unpainted = 0, repaired = 0;

            Guard.Try("EnemyColor", $"colour-audit '{_model}' (id '{id}')", () =>
            {
                var renderers = GetComponentsInChildren<Renderer>(true);
                foreach (var r in renderers)
                {
                    if (r == null || !r.enabled) continue;

                    var mats = r.sharedMaterials;
                    if (mats == null) continue;

                    for (int slot = 0; slot < mats.Length; slot++)
                    {
                        var m = mats[slot];
                        if (m == null) continue;

                        bool hasAlbedo =
                            (m.HasProperty(BaseMapId) && m.GetTexture(BaseMapId) != null) ||
                            (m.HasProperty(MainTexId) && m.GetTexture(MainTexId) != null);

                        Color baseColor =
                            m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) :
                            m.HasProperty(ColorId) ? m.GetColor(ColorId) : Color.white;

                        bool emits = m.IsKeywordEnabled("_EMISSION") &&
                                     m.HasProperty(EmissionColorId) &&
                                     m.GetColor(EmissionColorId).maxColorComponent > 0.01f;

                        var verdict = Classify(hasAlbedo, baseColor, emits);
                        switch (verdict)
                        {
                            case EnemySlotColor.Textured: textured++; continue;
                            case EnemySlotColor.Painted:  painted++;  continue;
                            case EnemySlotColor.Emissive: emissive++; continue;
                        }

                        // ── UNPAINTED: name it, then paint it. ────────────────────
                        unpainted++;
                        FlowTrace.Warn("EnemyColor",
                            $"NO COLOURING on '{_model}' (id '{id}', rig {_rig}) renderer '{r.name}' slot {slot}: " +
                            $"material='{m.name}' shader='{(m.shader != null ? m.shader.name : "<null>")}' has NO base map " +
                            $"and an ACHROMATIC albedo {baseColor} (chroma < {ChromaFloor:0.00}) — this body renders " +
                            "WHITE/GREY to the player. VerifyVisualRenders passes it because a mesh IS present; only " +
                            $"this check sees the colour. Repainting with the family tint {_familyTint}. Permanent fix = " +
                            $"ship a basecolor for '{_model}' (Enemies/TripoTex/{_model}_basecolor or Enemies/OrcTex/…).");

                        // MaterialPropertyBlock, NOT sharedMaterial: TripoMaterialFixer hands
                        // the SAME Material instance to every body with an identical look
                        // (its MatKey cache), so writing the material would repaint unrelated
                        // enemies. A block is per-renderer and cannot leak.
                        var block = new MaterialPropertyBlock();
                        int colorId = m.HasProperty(BaseColorId) ? BaseColorId : ColorId;
                        if (mats.Length == 1)
                        {
                            // SINGLE-SLOT BODIES USE THE RENDERER-LEVEL BLOCK ON PURPOSE.
                            // EnemyHitReaction's flash reads and writes the renderer-level block
                            // (no slot index); a per-slot override lives on a different channel
                            // and the two would not see each other — the flash would not restore
                            // this repaint, and the repaint would not survive the flash. Every
                            // enemy body in the 2026-08-20 capture is single-slot per renderer,
                            // so this is the path that actually runs.
                            r.GetPropertyBlock(block);
                            block.SetColor(colorId, _familyTint);
                            r.SetPropertyBlock(block);
                        }
                        else
                        {
                            r.GetPropertyBlock(block, slot);
                            block.SetColor(colorId, _familyTint);
                            r.SetPropertyBlock(block, slot);
                        }
                        repaired++;
                    }
                }
            });

            UnpaintedSlotsFound += unpainted;
            SlotsRepaired += repaired;

            string report =
                $"colour audit ({(final ? "FINAL" : "first")}) '{_model}' (id '{id}', rig {_rig}): " +
                $"textured={textured} painted={painted} emissive={emissive} unpainted={unpainted} repaired={repaired}";
            LastReport = report;

            if (unpainted > 0)
                FlowTrace.Step("EnemyColor", report + " — the unpainted slots were repainted; the body is no longer white/grey.");
            else if (final)
                // ONCE per model: the healthy case must be VISIBLE in the trace too, or an
                // absent warning is indistinguishable from an audit that never ran.
                FlowTrace.Once("EnemyColor", "colour-ok-" + _model, report + " — every slot carries a skin or a deliberate tint.");
        }
    }
}
