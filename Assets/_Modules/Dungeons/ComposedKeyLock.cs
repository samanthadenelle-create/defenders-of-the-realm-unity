// =============================================================================
// ComposedKeyBag — WO-1001 slice 7 run-local key state for Pipeline A dungeons.
// -----------------------------------------------------------------------------
// Keys are RUN-LOCAL: a plain static set, never saved to disk, cleared every time
// ComposedDungeonBootstrap arms a composed scene. Carrying a key between runs
// would let a player unlock the deep floor of a crypt they have never explored.
//
// The two MonoBehaviours that used to live in this file are now in their OWN
// files (ComposedKeyPickup.cs, ComposedLockedPort.cs) because Unity matches a
// serialized MonoBehaviour to a script asset BY FILE NAME - while they were
// declared here they did not survive the scene load, so every baked key and lock
// silently vanished while the bake still reported saved=True. This class is a
// plain static and is not subject to that rule, so it stays.
// =============================================================================

using System.Collections.Generic;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Dungeons
{
    /// <summary>Run-local key bag for composed dungeons (not saved to disk).</summary>
    public static class ComposedKeyBag
    {
        private static readonly HashSet<string> Keys = new HashSet<string>();

        public static void Clear()
        {
            int had = Keys.Count;
            Keys.Clear();
            if (had > 0) FlowTrace.Step("ComposedKey", $"key bag cleared (dropped {had} run-local key(s))");
        }

        public static void Grant(string keyId)
        {
            if (string.IsNullOrEmpty(keyId)) return;
            if (Keys.Add(keyId))
                FlowTrace.Step("ComposedKey", $"granted key '{keyId}' (held={Keys.Count})");
        }

        /// <summary>Null/empty is ALWAYS false - an unset keyId must never open every locked port.</summary>
        public static bool Has(string keyId) =>
            !string.IsNullOrEmpty(keyId) && Keys.Contains(keyId);
    }
}
