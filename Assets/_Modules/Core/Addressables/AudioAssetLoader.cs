// =============================================================================
// AudioAssetLoader — Tier-1 Addressables seam for AUDIO content.
// Sibling of HeroAssetLoader (WO-545) / EnemyAssetLoader / VfxAssetLoader;
// identical contract, audio address space.
// -----------------------------------------------------------------------------
// WHY THIS EXISTS: 111.4 MB of audio sits under a Resources/ folder, spread over
// EIGHT Resources roots (Assets/Resources, Assets/Audio/Resources,
// Assets/_Modules/Audio/Resources, ...), and Unity FORCE-INCLUDES everything under
// any folder named Resources in EVERY build — whether or not a single clip is ever
// played. Measured composition (verified from disk + the .meta files, 2026-08-17,
// do not re-derive):
//   • 54 clips / 111.4 MB under Resources; 78 more / 31.9 MB outside it.
//   • 20 music beds (39-320 s) = ~93 MB of that; the single largest is
//     Assets/Audio/Resources/Music/heartwood_collapse.wav, 31.8 MB / 173.8 s /
//     48 kHz stereo — loaded as the DEFEAT music track
//     (Assets/_Modules/Audio/AudioBootstrap.cs:126).
//   • ZERO scene, prefab or ScriptableObject references any of them by GUID —
//     every one is reached ONLY by a Resources.Load string key. That is what makes
//     the migration tractable: repoint the keys and the whole set can leave
//     Resources without a single broken reference.
//
// The fix is to move the audio into Addressable groups so it is pulled on demand —
// which requires a runtime SEAM the call sites can be pointed at BEFORE the assets
// physically move. This is that seam.
//
// ── KEY CONVENTION (DECIDED — read this before adding a call site) ───────────
//   The key is the FULL, extension-less, RESOURCES-RELATIVE path, used VERBATIM as
//   BOTH the Addressable address AND the Resources.Load key. e.g.
//       "title"                      "battle"          "whispering_pines"
//       "Music/echo_theme"           "Music/Raid/brass-rampart"
//       "Music/Battle/Overworld_Battle_1"
//       "Sfx/SwordSwing"             "Audio/Music/GameOver"
//
//   Unlike VfxAssetLoader there is NO single prefix to validate against: audio keys
//   are rooted at THREE different depths — bare at a Resources root ("title"),
//   one level down ("Sfx/SwordSwing", "Music/echo_theme") and two ("Music/Raid/
//   brass-rampart", "Audio/Music/GameOver"). Inventing or enforcing a prefix here
//   would query a DIFFERENT address than the grouper registered. The key is passed
//   through UNCHANGED, and AudioAddressablesGrouper computes its addresses from the
//   same Resources-relative extension-less path — never from a hardcoded table.
//
// CONTRACT (V1-SAFE, NON-NEGOTIABLE): Addressables-FIRST, Resources-FALLBACK.
//   • If the key is REGISTERED in the Addressables catalog (type-filtered), load it.
//   • Otherwise (the V1 default — nothing grouped yet) fall straight back to the
//     EXISTING Resources.Load<T>(key) path.
//
// V1-SAFE because NOTHING under any Resources folder is moved, deleted or
// re-imported by the code change that introduces this loader. The Resources copy
// remains the live path until the physical migration is run as a separate attended
// step, so every existing call site behaves EXACTLY as before — this only adds a
// silent probe in front of it.
//
// Synchronous surface (WaitForCompletion) so the existing sync call sites keep
// their shape (AudioBootstrap.TryAssignClip, GameSfx's lazy statics,
// BattleMusicManager.ResolveClips, ProceduralSfx.For).
//
// We deliberately check LoadResourceLocationsAsync FIRST rather than blindly calling
// LoadAssetAsync on a possibly-unregistered key: pre-migration NO audio address is
// registered, and a blind LoadAssetAsync on a missing key spams a red Addressables
// error on EVERY call. The locations probe is silent.
//
// ── THE EXPECTED-MISS PROBLEM (why Fail is de-duplicated per key) ────────────
//   Unlike heroes or VFX catalogs, a MISSING audio clip is a FIRST-CLASS, DESIGNED
//   state in this project: almost every SFX call site is written
//   `Resources.Load<AudioClip>("Sfx/X") ?? GenerateX()` — an authored clip is a
//   drop-in OVERRIDE over a procedural synth fallback (GameSfx.cs:69-238,
//   ProceduralSfx.cs:62, AbilityAudioBridge.cs:89). Keys such as "Sfx/TowerFire",
//   "Sfx/TowerPlace", "Sfx/WaveStart", "Sfx/PetHarvest", "Sfx/LevelUp" and
//   "Sfx/BuildDenied" have NO file on disk and are never meant to.
//   §12 still wants the hard miss logged — so the miss IS a FlowTrace.Fail, but it
//   fires ONCE PER KEY (see s_reportedMisses). A per-play Fail on a by-design synth
//   fallback would be pure spam and would train every seat to ignore the channel,
//   which is the opposite of what the instrumentation directive is for.
//
// ⚠ WEBGL CAVEAT (inherited from WO-545): WaitForCompletion is not supported on
// WebGL for a bundle that still has to be downloaded — once the audio assets are
// grouped, the audio bundle must be warmed async before these sync calls resolve.
//
// NOTE on handle lifetime: like Resources.Load (which never unloads), we do NOT
// release the asset handle — a music bed must outlive its crossfade and a cached
// SFX static must outlive the session. A future Tier-2 can add ref-counted release.
// =============================================================================

using System.Collections.Generic;
using DeNelle.Core.Diagnostics;   // FlowTrace / Guard — §12 instrument the seam (Step hit / Warn fallback)
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace DeNelle.Core
{
    /// <summary>
    /// Addressables-first / Resources-fallback loader for audio content (music beds,
    /// SFX clips, the audio mixer, the SFX clip library asset). Drop-in for
    /// <c>Resources.Load&lt;T&gt;(key)</c>. V1-safe: an unregistered address silently
    /// falls back to the shipping Resources copy. Keys are FULL Resources-relative
    /// extension-less paths — see the header's KEY CONVENTION block.
    /// </summary>
    public static class AudioAssetLoader
    {
        /// <summary>FlowTrace system tag for every line this seam emits.</summary>
        public const string System = "AudioAssets";

        // Keys whose both-paths-missed Fail has already been reported. The miss is a
        // DESIGNED state for the synth-fallback SFX keys (see header), so the Fail is
        // emitted once per key instead of once per play.
        private static readonly HashSet<string> s_reportedMisses = new HashSet<string>();

        /// <summary>
        /// Load an <see cref="AudioClip"/> by its FULL Resources-relative key, e.g.
        /// <c>LoadClip("Sfx/SwordSwing")</c>, <c>LoadClip("Music/Raid/brass-rampart")</c>,
        /// <c>LoadClip("title")</c>. Addressables-first, Resources-fallback. Null when
        /// both paths miss — every audio call site already treats null as "use the synth
        /// fallback" or "play silent", so a null return is always safe.
        /// </summary>
        public static AudioClip LoadClip(string key) => Load<AudioClip>(key);

        /// <summary>
        /// Load any audio-adjacent asset by its FULL Resources-relative key — the
        /// AudioMixer (<c>"Audio/GameAudioMixer"</c>), the SfxClipLibrary
        /// ScriptableObject (<c>"Audio/SfxClipLibrary"</c>), the AudioService prefab
        /// (<c>"DeNelleAudioService"</c>). Addressables-first, Resources-fallback.
        /// </summary>
        public static T LoadAudioAsset<T>(string key) where T : Object => Load<T>(key);

        /// <summary>
        /// Try Addressables when <paramref name="key"/> (of type <typeparamref name="T"/>) is
        /// registered, else fall back to Resources.Load on the SAME key. Guarded — a throw at
        /// any step degrades to the Resources fallback so a cue is never left clipless.
        /// </summary>
        private static T Load<T>(string key) where T : Object
        {
            if (string.IsNullOrEmpty(key)) return null;

            T result = null;

            // ── Addressables-first (only when the address is actually registered) ──
            Guard.Try(System, $"Addressables resolve '{key}' ({typeof(T).Name})", () =>
            {
                if (!AddressableRegistered<T>(key)) return; // expected pre-migration — handled by the Step below

                var handle = Addressables.LoadAssetAsync<T>(key);
                result = handle.WaitForCompletion();
                // Intentionally NOT released — a music bed must outlive its crossfade and a
                // cached SFX static must outlive the session (parity with Resources.Load,
                // which never unloads). Tier-2 adds ref-counted release.
                if (result != null)
                    FlowTrace.Step(System, $"Addressables HIT '{key}' -> '{result.name}' ({typeof(T).Name}).");
            });
            if (result != null) return result;

            // ── Resources fallback (the pre-migration path — no Resources audio folder is
            //    moved by the code change that introduces this seam) ──
            // Distinguish the two cases for §12 hygiene: a clean "no address registered yet"
            // (expected, Step) vs an address that WAS registered but failed to resolve (anomaly, Warn).
            bool wasRegistered = false;
            Guard.Try(System, $"probe '{key}' registration", () => wasRegistered = AddressableRegistered<T>(key));
            if (wasRegistered)
                FlowTrace.Warn(System,
                    $"Addressables '{key}' is registered but resolved null — falling back to Resources.Load(\"{key}\").");
            else
                FlowTrace.Step(System,
                    $"no Addressables entry for '{key}' (expected pre-migration) — using Resources.Load(\"{key}\").");

            Guard.Try(System, $"Resources.Load {key} ({typeof(T).Name})", () =>
            {
                result = Resources.Load<T>(key);
            });

            if (result == null && s_reportedMisses.Add(key))
                FlowTrace.Fail(System,
                    $"audio asset '{key}' ({typeof(T).Name}) not found via Addressables OR Resources — caller falls " +
                    "back (synth SFX / silent cue). Reported ONCE per key: for the synth-fallback SFX keys this miss " +
                    "is by design (see AudioAssetLoader header), for a music key it means that track is SILENT.");

            return result;
        }

        /// <summary>
        /// True when the Addressables catalog has at least one location for <paramref name="address"/>
        /// providing type <typeparamref name="T"/>. Silent (no error spam) on the common
        /// pre-migration miss. Type-filtered so two locations sharing an address resolve apart.
        /// </summary>
        private static bool AddressableRegistered<T>(string address) where T : Object
        {
            AsyncOperationHandle<IList<IResourceLocation>> locHandle = default;
            bool found = false;
            try
            {
                locHandle = Addressables.LoadResourceLocationsAsync(address, typeof(T));
                IList<IResourceLocation> locs = locHandle.WaitForCompletion();
                found = locs != null && locs.Count > 0;
            }
            catch
            {
                found = false; // no catalog / not initialised / bad key — treat as unregistered
            }
            finally
            {
                if (locHandle.IsValid()) Addressables.Release(locHandle);
            }
            return found;
        }
    }
}
