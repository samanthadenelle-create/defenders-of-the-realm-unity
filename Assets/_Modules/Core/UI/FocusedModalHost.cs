using System;
using UnityEngine;

namespace DeNelle.Core.UI
{
    /// <summary>One modal-arbiter record and one renewable pause lease across nested card pages.</summary>
    public sealed class FocusedModalHost : MonoBehaviour
    {
        public const string HoldReason = "focused-card-modal";
        private PanelHandle _panel;
        private WorldHold.Handle _hold;
        private bool _open;
        private int _depth;
        public bool IsOpen => _open;
        public int NavigationDepth => _depth;

        public bool Open(string panelName = "Card Collection")
        {
            if (_open) return true;
            _open = true; _depth = 1;
            _hold = AcquireHold();
            _panel ??= PanelManager.Register(panelName, Close, () => _open);
            if (!PanelManager.NotifyOpened(_panel)) { Close(); return false; }
            return true;
        }

        /// <summary>Acquire the same nested pause lifetime when the surface already owns a
        /// PanelManager handle. This avoids registering a second handle that would close its host.</summary>
        public bool OpenUnderExistingPanel()
        {
            if (_open) return true;
            _open = true; _depth = 1;
            _hold = AcquireHold();
            return true;
        }

        /// <summary>
        /// WO-1471: PLAYER-OWNED, not the bounded default. A card modal is dismissed by the player,
        /// so elapsed time is never evidence that this hold leaked - the 180s ceiling would thaw the
        /// world underneath an open card. Both Open paths funnel here so the two call sites cannot
        /// drift apart. The probe reuses the SAME expression PanelManager.Register is given
        /// (<c>() =&gt; _open</c>) plus this component's own existence: it asks "does its owner still
        /// exist", never "is this old" (WO-1369).
        /// </summary>
        private WorldHold.Handle AcquireHold()
        {
            return WorldHold.AcquirePlayerOwned(HoldReason, () => this != null && _open);
        }

        public void Push() { if (_open) _depth++; }
        public void Pop() { if (!_open) return; if (_depth > 1) _depth--; else Close(); }

        public void Close()
        {
            if (!_open && _hold == null) return;
            _open = false; _depth = 0;
            if (_panel != null) PanelManager.NotifyClosed(_panel);
            _hold?.Dispose();
            _hold = null;
        }

        // WO-1471: the per-frame renew Update is DELETED - it was the workaround for
        // the bounded ceiling, and a player-owned hold has no ceiling to outrun.
        private void OnDisable() => Close();
        private void OnDestroy() => Close();
    }
}
