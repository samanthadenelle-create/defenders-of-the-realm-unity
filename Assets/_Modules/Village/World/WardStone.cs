// =============================================================================
// WardStone — one ward-stone in the field (WO-112).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// The in-world half of the ward-tether. A ward-stone carries lit/unlit state, draws
// its glow (a child point-light + emissive tint), shows a "relight" affordance when
// the Keeper is near and it's still cold, and reports its lit state to the
// WardTetherService so per-march reach recomputes.
//
// RECONCILIATION (no parallel systems, CLAUDE.md §9):
//   • Created at runtime by WardTetherService (code-built cube + light), exactly as
//     RegionMobSpawner / TribeManager build their world objects — NO prefab/SO/scene
//     wiring, NO bake. Placement rides the OuterWorld coordinate space (the two-scene
//     world); it does NOT go through VillageSceneBuilder (the serialization bottleneck).
//   • Lit state PERSISTS via WardStoneState in GameState.Wards (the save owner wires
//     the schema round-trip — see WardContent.cs / GameState.Wards notes).
//   • SFX uses the same DeNelle.Audio.AudioService path VFXManager uses (Village can
//     reach DeNelle.Audio); the cross-asmdef HUD calls stay in WardTetherService.
//
// This component is intentionally thin: the reach math, forgetting, kindle wave and
// node-claim hook all live in WardTetherService. The WardStone just holds state +
// presentation and raises the relight request.
// =============================================================================
using UnityEngine;
using DeNelle.Core.World;

namespace DeNelle.Village
{
    /// <summary>
    /// A single in-field ward-stone (WO-112): lit/unlit state, glow, a proximity relight
    /// affordance, and a report-up to <see cref="WardTetherService"/> when its state changes.
    /// Built at runtime by the service — no prefab/scene wiring.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WardStone : MonoBehaviour
    {
        /// <summary>The authoring def this stone was built from (region, order, reach, cost, node).</summary>
        public WardStoneDef Def { get; private set; }

        /// <summary>The persisted record (lit flag) mirrored into <c>GameState.Wards</c>.</summary>
        public WardStoneState Record { get; private set; }

        /// <summary>True once relit. Pure read; toggled via <see cref="SetLit"/>.</summary>
        public bool IsLit { get; private set; }

        /// <summary>The region (march) this ward belongs to.</summary>
        public RegionId Region { get; private set; }

        /// <summary>Stable ward id / save key.</summary>
        public string Id => Def != null ? Def.Id : name;

        // ── Presentation ──────────────────────────────────────────────────────
        private Light _glow;
        private Renderer _renderer;
        private Material _mat;
        private static readonly Color LitColor  = new Color(0.55f, 0.85f, 1.00f);   // warm ward-blue
        private static readonly Color ColdColor = new Color(0.18f, 0.20f, 0.26f);   // dead stone-grey

        /// <summary>
        /// Initialise this ward from its def + bound record. Called by WardTetherService
        /// immediately after AddComponent. Restores lit visuals if the record says lit
        /// (save-restore on load) WITHOUT replaying the relight SFX/flavour.
        /// </summary>
        public void Initialise(WardStoneDef def, WardStoneState record)
        {
            Def = def;
            Record = record;
            Region = ResolveRegion(def);
            BuildVisuals();
            // Restore persisted lit state silently (no kindle, no SFX) on first init.
            ApplyLitVisuals(record != null && record.Lit);
            IsLit = record != null && record.Lit;
        }

        /// <summary>
        /// Toggle lit/unlit. Plays the ward SFX, refreshes the glow, writes the record,
        /// and tells the tether service to recompute reach. <paramref name="silent"/> skips
        /// SFX (used for save-restore, though Initialise already handles that path).
        /// </summary>
        public void SetLit(bool lit, bool silent = false)
        {
            IsLit = lit;
            if (Record != null) Record.Lit = lit;
            ApplyLitVisuals(lit);

            if (!silent)
            {
                // Same audio path VFXManager uses (Village reaches DeNelle.Audio directly).
                DeNelle.Audio.AudioService.Instance?.PlaySfxAtPosition(
                    lit ? DeNelle.Audio.SfxId.WardLit : DeNelle.Audio.SfxId.WardDim,
                    transform.position);
            }

            // Resolve into the tether service so reach recomputes + persists + HUD refreshes.
            if (WardTetherService.Instance != null)
                WardTetherService.Instance.OnWardStateChanged(this);
        }

        /// <summary>The relight cost (coins, crystals) from the def — read by the service.</summary>
        public (int coins, int crystals) Cost =>
            Def != null ? (Def.CoinCost, Def.CrystalCost) : (0, 0);

        // ── Visuals ───────────────────────────────────────────────────────────

        private void BuildVisuals()
        {
            _renderer = GetComponent<Renderer>();
            if (_renderer != null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (sh != null)
                {
                    _mat = new Material(sh) { name = "WardStone_" + Id };
                    _renderer.sharedMaterial = _mat;
                }
            }

            // A child point-light for the glow (off when cold).
            var lightGo = new GameObject("WardGlow");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            _glow = lightGo.AddComponent<Light>();
            _glow.type = LightType.Point;
            _glow.range = 12f;
            _glow.intensity = 0f;
            _glow.color = LitColor;
        }

        private void ApplyLitVisuals(bool lit)
        {
            Color c = lit ? LitColor : ColdColor;
            if (_mat != null)
            {
                if (_mat.HasProperty("_BaseColor")) _mat.SetColor("_BaseColor", c);
                else _mat.color = c;
                // Emissive so a lit ward reads as glowing even before the point-light.
                if (_mat.HasProperty("_EmissionColor"))
                {
                    _mat.EnableKeyword("_EMISSION");
                    _mat.SetColor("_EmissionColor", lit ? LitColor * 1.4f : Color.black);
                }
            }
            if (_glow != null) _glow.intensity = lit ? 2.2f : 0f;
        }

        private static RegionId ResolveRegion(WardStoneDef def)
        {
            if (def != null &&
                System.Enum.TryParse(def.RegionKey, out RegionId r))
                return r;
            return RegionId.Village;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = IsLit
                ? new Color(0.55f, 0.85f, 1f, 0.5f)
                : new Color(0.4f, 0.4f, 0.45f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, 3f);   // ~relight proximity
        }
#endif
    }
}
