// =============================================================================
// PoiBeacon (WO-VFX-POI) — an opt-in POI callout marker a point-of-interest attaches.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// A POI (MineNode reserve, HarvestSite, ResourceCollector, EnemyOutpost) self-attaches
// ONE of these to declare "I am worth a callout". It holds only DATA (tier, radii, tint,
// optional spent-predicate) and registers/unregisters with PoiRegistry in OnEnable/
// OnDisable. It owns NO VFX — the singleton PoiCalloutSystem reads the registry and
// drives the actual near-field aura / far-field pillar, capped to the loop budget.
//
// COLOR-BLIND CANON (owner red/green colorblind): callouts read by MOTION / SHAPE /
// LUMINANCE / VERTICALITY, never hue. The default Tint is high-luminance neutral
// (pale gold / white), NOT a semantic red/green.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>Opt-in callout descriptor a POI self-attaches. Two tiers:
    /// <see cref="PoiTier.Node"/> = near-field harvest node (small ground aura, discovery-
    /// budgeted), <see cref="PoiTier.Landmark"/> = far-field landmark (a tall pillar/beam
    /// visible from range, e.g. an enemy fortress). Data only — no VFX held here.</summary>
    [DisallowMultipleComponent]
    public sealed class PoiBeacon : MonoBehaviour
    {
        /// <summary>Node = near-field harvest aura (budgeted to the nearest few).
        /// Landmark = far-field pillar shown whenever it exists and is unspent.</summary>
        public enum PoiTier { Node, Landmark }

        [Tooltip("Node = near-field ground aura (budgeted). Landmark = far-field pillar/beam.")]
        public PoiTier Tier = PoiTier.Node;

        [Tooltip("Metres within which the callout appears. Node ~28m; Landmark large/effectively infinite.")]
        public float CalloutRadius = 28f;

        [Tooltip("Metres within which the callout FADES/STOPS (hero is basically on it). " +
                 "Node ~InteractRadius+1; Landmark ~35m (scale down as you arrive).")]
        public float HandoffRadius = 3.5f;

        [Tooltip("High-luminance NEUTRAL tint (pale gold / white). Owner is red/green colorblind — " +
                 "callouts read by motion/shape/luminance/verticality, never by hue.")]
        public Color Tint = new Color(1f, 0.94f, 0.72f, 1f);

        /// <summary>Optional predicate: true once the POI is SPENT (node depleted / reserve
        /// empty / outpost cleared / collector inactive). When it returns true the callout is
        /// suppressed. Null = never spent.</summary>
        public System.Func<bool> IsSpent;

        /// <summary>True when this beacon should currently show a callout (alive + not spent).</summary>
        public bool IsActiveCallout
        {
            get
            {
                if (IsSpent != null)
                {
                    bool spent;
                    try { spent = IsSpent(); }
                    catch { spent = false; }   // a throwing predicate never blanks the callout
                    if (spent) return false;
                }
                return true;
            }
        }

        private void OnEnable()  => PoiRegistry.Register(this);
        private void OnDisable() => PoiRegistry.Unregister(this);

        /// <summary>Convenience self-attach (mirrors MineNode.EnsureVisual): add-or-reuse a
        /// PoiBeacon on <paramref name="host"/> and configure it. Idempotent — a second call
        /// re-applies the config to the existing component. Returns the beacon.</summary>
        public static PoiBeacon Attach(GameObject host, PoiTier tier, float calloutRadius,
                                       float handoffRadius, Color tint, System.Func<bool> isSpent = null)
        {
            if (host == null) return null;
            var b = host.GetComponent<PoiBeacon>();
            if (b == null) b = host.AddComponent<PoiBeacon>();
            b.Tier          = tier;
            b.CalloutRadius = calloutRadius;
            b.HandoffRadius = handoffRadius;
            b.Tint          = tint;
            b.IsSpent       = isSpent;
            return b;
        }
    }
}
