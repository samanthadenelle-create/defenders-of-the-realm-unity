// =============================================================================
// TalkPromptRegistry — the talkable NPCs currently IN RANGE of the hero.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHY: the HUD's right-edge Talk button must only activate when the player is close
// enough to a target NPC (owner directive). Per ARCHITECTURE_PRINCIPLES §2 the HUD is
// a pure reader — it must not scan for NPCs. Per §2b.1 there must be ONE owner of the
// proximity, not a duplicate system. So NPCs self-register here FROM THEIR EXISTING
// in-range code (the same distance check that already drives MobileInteractButton) —
// NO new per-frame proximity scan is introduced (that was the OuterWorld-leak anti-
// pattern). TalkHudBridge reads this registry to gate the button + route the talk.
//
// Village-internal: exposed to no layer above. Mirrors the O(1) self-registering
// registry pattern used by Tower.ActiveCount / StoryCompanion.Active.
// =============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace DeNelle.Village
{
    public static class TalkPromptRegistry
    {
        private sealed class Entry { public Transform Node; public System.Action Talk; }
        private static readonly List<Entry> _entries = new List<Entry>();

        /// <summary>How many talkable NPCs are in range right now (O(1) read).</summary>
        public static int Count => _entries.Count;

        /// <summary>NPC enters range: register (or refresh) its talk action. Idempotent per node.</summary>
        public static void Register(Transform node, System.Action talk)
        {
            if (node == null || talk == null) return;
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].Node == node) { _entries[i].Talk = talk; return; }
            _entries.Add(new Entry { Node = node, Talk = talk });
        }

        /// <summary>NPC leaves range / is disabled: drop it. Safe if not present.</summary>
        public static void Deregister(Transform node)
        {
            for (int i = _entries.Count - 1; i >= 0; i--)
                if (_entries[i].Node == null || _entries[i].Node == node) _entries.RemoveAt(i);
        }

        /// <summary>Talk action of the entry nearest <paramref name="from"/>, or null if none.</summary>
        public static System.Action NearestTalk(Vector3 from)
        {
            System.Action best = null;
            float bestSqr = float.MaxValue;
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                var e = _entries[i];
                if (e.Node == null) { _entries.RemoveAt(i); continue; }
                float d = (e.Node.position - from).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = e.Talk; }
            }
            return best;
        }
    }
}
