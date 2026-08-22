// =============================================================================
// HeadlessState — install a throwaway GameState for an editmode oracle (WO-1026).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor   Namespace: DeNelle.Editor
//
// WHY THIS EXISTS: editmode batchmode NEVER runs GameStateService.Awake (Awake fires
// only in play mode / on ExecuteAlways), so a bare AddComponent<GameStateService>()
// leaves Instance AND State null — the historic cause of the false-FAIL
// "no GameStateService/State available". The fix is to set the private static
// _instance and the [SerializeField] _state by reflection, exactly as Awake would.
//
// The pattern is verbatim from OfflineHarvestRegression / CoreSaveContractRegression;
// it is factored out here because WO-1026 needed it in TWO oracles and a third
// copy-paste of the same reflection is how the copies drift apart (the same
// duplicated-state failure CLAUDE.md §2/§5/§16 each record).
//
// ⚠ CALLERS MUST RESTORE IN A finally: TrySetInstance(prior) puts the live service
//   back for the batch's later oracles (DestroyImmediate may have nulled the static
//   via OnDestroy, so it must be set back EXPLICITLY), and RestoreSave() puts back the
//   persisted blob that any Save() during the run overwrote.
//
// A missing seam returns FALSE with a NAMED reason so the caller NAMED-SKIPs
// (return true) rather than false-FAILing the gate — that harness-integrity rule is
// OfflineHarvestRegression's and it holds here.
// =============================================================================

using System.Reflection;
using DeNelle.Core.State;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>Reflection helpers for installing a throwaway GameState in editmode.</summary>
    public static class HeadlessState
    {
        /// <summary>The PlayerPrefs key holding the live save (SaveSchema.PlayerPrefsKey).</summary>
        public const string SaveKey = "dotr-save";

        /// <summary>Installs <paramref name="state"/> on <paramref name="svc"/> and promotes
        /// <paramref name="svc"/> to the live singleton. False (with a named reason) if either
        /// seam was renamed/removed.</summary>
        public static bool TryInstall(GameStateService svc, GameState state, out string err)
        {
            err = null;
            var stateField = typeof(GameStateService).GetField("_state",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (stateField == null)
            { err = "GameStateService._state field not found by reflection (state seam renamed/removed)"; return false; }
            stateField.SetValue(svc, state);
            if (!TrySetInstance(svc))
            { err = "GameStateService._instance static not found by reflection (singleton seam renamed/removed)"; return false; }
            return true;
        }

        /// <summary>Sets the private static <c>GameStateService._instance</c> (null allowed, to
        /// restore). False only if the field seam is gone.</summary>
        public static bool TrySetInstance(GameStateService svc)
        {
            var f = typeof(GameStateService).GetField("_instance",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (f == null) return false;
            f.SetValue(null, svc);
            return true;
        }

        /// <summary>Snapshots the persisted save blob so an oracle's Save() calls can be undone.
        /// Pair with <see cref="RestoreSave"/> in a finally.</summary>
        public static string SnapshotSave(out bool hadSave)
        {
            hadSave = PlayerPrefs.HasKey(SaveKey);
            return hadSave ? PlayerPrefs.GetString(SaveKey, null) : null;
        }

        /// <summary>Restores (or removes) the save blob captured by <see cref="SnapshotSave"/>.</summary>
        public static void RestoreSave(bool hadSave, string raw)
        {
            if (hadSave) PlayerPrefs.SetString(SaveKey, raw);
            else PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
        }
    }
}
