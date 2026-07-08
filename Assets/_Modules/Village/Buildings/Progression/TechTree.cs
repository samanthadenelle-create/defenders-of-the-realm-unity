// =============================================================================
// TechTree — the Magic-gated tech-tree node ledger (DEF-121 / WO-230).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Buildings.Progression
//
// Magic is a building-UPGRADE tech axis, NOT a harvestable. Buying a Magic-gated
// building-upgrade tier (see ResourceBuildingProgression's arcane Forge tier)
// UNLOCKS a node here. This is the minimal "tech tree" surface the economy
// correction requires: a persisted set of unlocked node ids, plus an event so UI
// can light a node up. Kept deliberately small (scope discipline — NOT an MMO):
// it is a lit/unlit ledger, not a dependency graph engine — a fuller graph can
// fold in later without changing callers.
//
// PERSISTENCE: unlocked nodes persist in PlayerPrefs (namespaced key), mirroring
// ResourceBuildingState's level persistence and its documented "fold into
// GameState/SaveSchema later" follow-up. Self-contained, survives a session.
//
// Village -> Core only (asmdef rule). No scene wiring — a static ledger any
// caller (the upgrade flow, a future tech-tree panel) reads consistently.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village.Buildings.Progression
{
    /// <summary>
    /// Static ledger of unlocked Magic tech-tree nodes. A node is unlocked when the
    /// player buys the Magic-gated building-upgrade tier that grants it.
    /// </summary>
    public static class TechTree
    {
        private const string PrefsPrefix = "dotr.tech.node.";

        /// <summary>Canonical id of the Forge's Magic-gated tech node (the Arcane Forge).</summary>
        public const string ArcaneForgeNodeId = "arcane_forge";

        /// <summary>Raised after a node is unlocked. Arg = node id.</summary>
        public static event Action<string> NodeUnlocked;

        /// <summary>True once <paramref name="nodeId"/> has been unlocked.</summary>
        public static bool IsUnlocked(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return false;
            return PlayerPrefs.GetInt(Key(nodeId), 0) == 1;
        }

        /// <summary>
        /// Unlocks <paramref name="nodeId"/> (idempotent), persists, and raises
        /// <see cref="NodeUnlocked"/>. No-op for a null/empty id or an already-lit node.
        /// </summary>
        public static void Unlock(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId) || IsUnlocked(nodeId)) { FlowTrace.Step("TechTree", $"Unlock('{nodeId ?? "<null>"}') no-op (empty or already-lit)"); return; }
            PlayerPrefs.SetInt(Key(nodeId), 1);
            PlayerPrefs.Save();
            FlowTrace.Step("TechTree", $"Unlocked node '{nodeId}'");
            Debug.Log($"[TechTree] Unlocked tech node '{nodeId}'.");
            NodeUnlocked?.Invoke(nodeId);
        }

        /// <summary>The set of currently-unlocked node ids (defensive copy).</summary>
        public static IReadOnlyCollection<string> UnlockedNodes
        {
            get
            {
                var set = new HashSet<string>();
                if (IsUnlocked(ArcaneForgeNodeId)) set.Add(ArcaneForgeNodeId);
                return set;
            }
        }

        /// <summary>Clears all unlocked nodes (New Game / dev reset).</summary>
        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(Key(ArcaneForgeNodeId));
            PlayerPrefs.Save();
        }

        private static string Key(string nodeId) => PrefsPrefix + nodeId;
    }
}
