// =============================================================================
// LocalSaveProvider — the default ISaveProvider: local PlayerPrefs IO.
// -----------------------------------------------------------------------------
// This is the EXACT local IO the game has always used. It wraps the same
// PlayerPrefs surface GameStateService.Load()/Save() called directly:
//
//   Exists(slot) -> PlayerPrefs.HasKey(slot)
//   Read(slot)   -> PlayerPrefs.GetString(slot)
//   Write(slot)  -> PlayerPrefs.SetString(slot, json) + PlayerPrefs.Save()
//   Delete(slot) -> PlayerPrefs.DeleteKey(slot)        + PlayerPrefs.Save()
//
// Pass slot = SaveSchema.PlayerPrefsKey ("dotr-save") and the round-trip is
// byte-identical to the pre-seam behaviour. Nothing here knows about SaveSchema.
// =============================================================================

using UnityEngine;

namespace DeNelle.Core.State
{
    /// <summary>
    /// Default <see cref="ISaveProvider"/> — persists the save JSON to the local
    /// device via <see cref="PlayerPrefs"/>, exactly as the game did before the
    /// IO seam was introduced. Used as <c>GameStateService.Provider</c>'s default.
    /// </summary>
    public sealed class LocalSaveProvider : ISaveProvider
    {
        /// <inheritdoc/>
        public bool Exists(string slot) => PlayerPrefs.HasKey(slot);

        /// <inheritdoc/>
        public string Read(string slot) => PlayerPrefs.GetString(slot);

        /// <inheritdoc/>
        public void Write(string slot, string json)
        {
            PlayerPrefs.SetString(slot, json);
            PlayerPrefs.Save();
        }

        /// <inheritdoc/>
        public void Delete(string slot)
        {
            if (PlayerPrefs.HasKey(slot)) PlayerPrefs.DeleteKey(slot);
            PlayerPrefs.Save();
        }
    }
}
