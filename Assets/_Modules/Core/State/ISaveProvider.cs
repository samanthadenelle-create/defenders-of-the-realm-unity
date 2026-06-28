// =============================================================================
// ISaveProvider — the pluggable save-IO seam (Tier-2, DATA_ARCHITECTURE_DECISION
// 2026-06-27).
// -----------------------------------------------------------------------------
// This interface isolates the RAW byte/string IO of the save system — read,
// write, exists, delete — from the serialization layer (SaveSchema <-> JSON,
// which stays in GameStateService). Today the only implementation is
// LocalSaveProvider (PlayerPrefs), so behaviour is identical to before the seam.
// A future cloud DB / Solana-backed provider is a one-line swap at
// GameStateService.Provider — no change to serialization, migration or callers.
//
//   slot  = the storage key/identifier (today: the PlayerPrefs key "dotr-save").
//           Named "slot" rather than "key" so a future multi-slot / per-wallet
//           provider can map it however it likes.
// =============================================================================

namespace DeNelle.Core.State
{
    /// <summary>
    /// The pluggable save-IO contract. Implementations persist/restore the
    /// already-serialized save JSON for a given <paramref name="slot"/>; they do
    /// NOT know about SaveSchema, migration or validation (that stays in the save
    /// manager). Swap the implementation to retarget where saves live (local disk,
    /// cloud DB, Solana) without touching the serialization layer.
    /// </summary>
    public interface ISaveProvider
    {
        /// <summary>True when a save exists for <paramref name="slot"/>.</summary>
        bool Exists(string slot);

        /// <summary>
        /// Returns the raw save JSON for <paramref name="slot"/>, or an empty
        /// string when none exists. Never throws for "absent" — that is Exists.
        /// </summary>
        string Read(string slot);

        /// <summary>Persists the raw save <paramref name="json"/> under <paramref name="slot"/>.</summary>
        void Write(string slot, string json);

        /// <summary>Removes the save for <paramref name="slot"/> if present.</summary>
        void Delete(string slot);
    }
}
