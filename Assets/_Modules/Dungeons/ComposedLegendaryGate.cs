// =============================================================================
// ComposedLegendaryGate — WO-1001 slice 6: deepboss loot gated on darkness.
// -----------------------------------------------------------------------------
// "Legendary loot only reachable past the dark floors" — while the Keeper has
// never been critically low on oil this visit, deepboss breakables stay inert
// (collider off). After IsInDarkness once (or ambush director flag), they arm.
// =============================================================================

using DeNelle.Core.Diagnostics;
using DeNelle.Village;
using UnityEngine;

namespace DeNelle.Dungeons
{
    /// <summary>Gates a BreakableContainer until the composed run has seen darkness.</summary>
    [DisallowMultipleComponent]
    public sealed class ComposedLegendaryGate : MonoBehaviour
    {
        private BreakableContainer _breakable;
        private Collider _col;
        private bool _armed;
        private ComposedAmbushDirector _ambush;

        private void Awake()
        {
            _breakable = GetComponent<BreakableContainer>();
            _col = GetComponent<Collider>();
            SetArmed(false);
        }

        private void Update()
        {
            if (_armed) return;
            if (_ambush == null) _ambush = FindFirstObjectByType<ComposedAmbushDirector>();
            bool dark = _ambush != null && _ambush.HasBeenInDarkness;
            if (!dark)
            {
                var lantern = FindFirstObjectByType<Lantern>();
                dark = lantern != null && lantern.IsInDarkness;
            }
            if (!dark) return;
            SetArmed(true);
            FlowTrace.Step("ComposedDungeon",
                $"LEGENDARY GATE opened on '{name}' — darkness condition met (deepboss loot armable)");
        }

        private void SetArmed(bool on)
        {
            _armed = on;
            if (_col != null) _col.enabled = on;
            if (_breakable != null) _breakable.enabled = on;
        }
    }
}
