// =============================================================================
// HeroContentPrewarmer — downloads the CHOSEN hero's remote art during the
// post-class-select load screen, and REFUSES to enter the world if it can't
// (WO-1187, owner ruling 2026-09-03).
// -----------------------------------------------------------------------------
// WHY THIS EXISTS (CLAUDE.md §16, read it before editing):
// hero bodies + atlases now live in REMOTE R2 bundles with NO local copy. Remote
// art in this project fails SILENTLY — a bundle that was never pushed produces a
// tinted CAPSULE and NO error on screen, and the only detector left is the owner's
// eyes. That is precisely what §14 exists to never rely on. So this class is the
// gate: the load screen awaits Prewarm(), and on failure the player is held on the
// load screen with a WORDED message instead of being dropped in as a pill.
//
// ⚠ THE FAILURE MESSAGE IS WORDS, NEVER A COLOUR. The owner is red/green
// colourblind (memory: owner-colorblind-delegate-visual-creative). A red banner or
// a red/green dot is NOT a failure state she can read. StatusText always says, in
// plain language, what failed and what to do about it.
//
// WHY A BLOCKING PREWARM AND NOT LAZY LOADING: HeroAssetLoader resolves
// synchronously (WaitForCompletion) because its call sites — AtbCombatantSwapper,
// HeroBodySwapper, StoryCompanionInjector — are all sync. WaitForCompletion on an
// UNCACHED remote bundle stalls the main thread for the length of the download,
// i.e. a frozen game. Prewarming here means the bundle is already in the Addressables
// cache by the time anything calls the loader, so that sync call is a cache hit.
// Do NOT remove the prewarm and expect the loader to cope.
//
// This is presentation-agnostic on purpose (HP B2B, CLAUDE.md architecture law): it
// owns the DOWNLOAD and the STATUS STRING, and knows nothing about any panel. The
// load screen reads State/Progress/StatusText and draws them.
// =============================================================================

using System.Collections;
using System.Collections.Generic;
using DeNelle.Core.Diagnostics;   // FlowTrace / Guard — §12
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace DeNelle.Core
{
    /// <summary>How the chosen hero's remote art download is going. Read by the load screen.</summary>
    public enum HeroPrewarmState
    {
        /// <summary>Nothing requested yet.</summary>
        Idle = 0,
        /// <summary>Download in flight — <see cref="HeroContentPrewarmer.Progress"/> is meaningful.</summary>
        Downloading = 1,
        /// <summary>Art is cached locally; it is safe to enter the world.</summary>
        Ready = 2,
        /// <summary>Every attempt failed. DO NOT enter the world — show StatusText + a Retry.</summary>
        Failed = 3,
    }

    /// <summary>
    /// Downloads one hero's remote Addressables content up-front, so the synchronous
    /// <see cref="HeroAssetLoader"/> call sites hit a warm cache. Gate the world entry on
    /// <see cref="State"/> == <see cref="HeroPrewarmState.Ready"/>.
    /// </summary>
    public static class HeroContentPrewarmer
    {
        /// <summary>Address prefix of the shared hero atlas bundle (HeroTextureLoader's keys).</summary>
        public const string TexAddrPrefix = "Heroes/Textures/";

        /// <summary>How many times a failed download is retried before we word the failure.</summary>
        public const int MaxAttempts = 3;

        /// <summary>Seconds between retry attempts (a flaky mobile connection usually recovers).</summary>
        private const float RetryDelaySeconds = 2f;

        /// <summary>Current state of the prewarm. The load screen gates entry on this.</summary>
        public static HeroPrewarmState State { get; private set; } = HeroPrewarmState.Idle;

        /// <summary>0..1 download progress while <see cref="State"/> is Downloading.</summary>
        public static float Progress { get; private set; }

        /// <summary>
        /// Plain-language status for the player. ALWAYS WORDS — never rely on colour to convey
        /// failure (the owner is red/green colourblind). Safe to display verbatim.
        /// </summary>
        public static string StatusText { get; private set; } = string.Empty;

        /// <summary>The slug of the last hero we prewarmed (or tried to).</summary>
        public static string LastSlug { get; private set; } = string.Empty;

        /// <summary>True when the last requested hero's art is cached and the world may load.</summary>
        public static bool IsReady(string slug) =>
            State == HeroPrewarmState.Ready &&
            string.Equals(LastSlug, slug, System.StringComparison.Ordinal);

        /// <summary>
        /// Download every remote bundle the given hero needs. Drive this from the load screen
        /// that runs after class select:
        /// <code>yield return HeroContentPrewarmer.Prewarm(slug);
        /// if (HeroContentPrewarmer.State != HeroPrewarmState.Ready) { /* show StatusText + Retry */ }</code>
        /// Never throws — a hard failure lands as <see cref="HeroPrewarmState.Failed"/> plus a worded
        /// <see cref="StatusText"/>, which the caller MUST honour by not entering the world.
        /// </summary>
        public static IEnumerator Prewarm(string slug)
        {
            LastSlug = slug ?? string.Empty;
            Progress = 0f;

            if (string.IsNullOrEmpty(slug))
            {
                // Nothing asked for = nothing to download. Not an error; don't block the load screen.
                State = HeroPrewarmState.Ready;
                StatusText = string.Empty;
                FlowTrace.Step("HeroPrewarm", "no slug supplied — nothing to prewarm (treated as Ready).");
                yield break;
            }

            // Collect the keys this hero needs: its own body address + the shared atlas bundle.
            List<object> keys = CollectKeys(slug);
            if (keys.Count == 0)
            {
                // No REMOTE entry for this hero — the normal state in the editor's
                // "Use Asset Database" play mode, and for any hero still served from
                // Resources (Props/Emotes/SC_*). Nothing to download, so do not block.
                State = HeroPrewarmState.Ready;
                StatusText = string.Empty;
                FlowTrace.Step("HeroPrewarm",
                    $"no Addressables keys for '{slug}' — nothing remote to fetch (editor/asset-database " +
                    "play mode or a deliberately-local hero). Treated as Ready.");
                yield break;
            }

            // How many bytes are actually missing? Zero => already cached from a previous session,
            // which is the common case on the owner's second launch.
            long bytes = 0;
            yield return GetDownloadSize(keys, size => bytes = size);

            if (bytes <= 0)
            {
                State = HeroPrewarmState.Ready;
                Progress = 1f;
                StatusText = string.Empty;
                FlowTrace.Step("HeroPrewarm", $"'{slug}' art already cached (0 bytes to download) — Ready.");
                yield break;
            }

            float mb = bytes / (1024f * 1024f);
            FlowTrace.Step("HeroPrewarm", $"'{slug}' needs {mb:0.0} MB from the CDN across {keys.Count} key(s).");

            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                State = HeroPrewarmState.Downloading;
                StatusText = attempt == 1
                    ? $"Preparing your {slug}… downloading {mb:0.0} MB."
                    : $"Connection problem. Retrying your {slug} download ({attempt} of {MaxAttempts})…";

                bool ok = false;
                bool faulted = false;
                string error = null;

                AsyncOperationHandle handle = default;
                bool started = Guard.Try("HeroPrewarm", $"DownloadDependenciesAsync '{slug}' attempt {attempt}", () =>
                {
                    handle = Addressables.DownloadDependenciesAsync(keys, Addressables.MergeMode.Union, false);
                });

                if (started && handle.IsValid())
                {
                    while (handle.IsValid() && !handle.IsDone)
                    {
                        Progress = Mathf.Clamp01(handle.PercentComplete);
                        StatusText = $"Preparing your {slug}… {Mathf.RoundToInt(Progress * 100f)}% of {mb:0.0} MB.";
                        yield return null;
                    }

                    if (handle.IsValid())
                    {
                        ok = handle.Status == AsyncOperationStatus.Succeeded;
                        if (!ok)
                        {
                            faulted = true;
                            error = handle.OperationException != null
                                ? handle.OperationException.Message
                                : "unknown Addressables download failure";
                        }
                        // Release the DOWNLOAD handle only — this frees the operation, not the cached
                        // bundle. The bytes stay in the Addressables cache, which is the whole point.
                        Guard.Try("HeroPrewarm", "release download handle", () => Addressables.Release(handle));
                    }
                    else
                    {
                        faulted = true;
                        error = "download handle became invalid mid-flight";
                    }
                }
                else
                {
                    faulted = true;
                    error = "DownloadDependenciesAsync could not be started";
                }

                if (ok)
                {
                    State = HeroPrewarmState.Ready;
                    Progress = 1f;
                    StatusText = string.Empty;
                    FlowTrace.Step("HeroPrewarm", $"'{slug}' art downloaded and cached on attempt {attempt} — Ready.");
                    yield break;
                }

                FlowTrace.Warn("HeroPrewarm",
                    $"'{slug}' art download attempt {attempt}/{MaxAttempts} FAILED: {error}");

                if (faulted && attempt < MaxAttempts)
                {
                    float until = Time.realtimeSinceStartup + RetryDelaySeconds;
                    while (Time.realtimeSinceStartup < until) yield return null;
                }
            }

            // ── Out of attempts. FAIL LOUDLY AND IN WORDS. ──────────────────────────
            // The caller MUST NOT enter the world now: with no local copy of the hero art,
            // proceeding is exactly the "tinted capsule, no error on screen" outcome §16 warns
            // about. Holding the player on the load screen with this sentence is the feature.
            State = HeroPrewarmState.Failed;
            StatusText =
                $"Could not download your {slug}. Your hero's artwork is missing, so the game has stopped " +
                "here instead of dropping you in without it. Check your internet connection and tap Retry.";

            FlowTrace.Fail("HeroPrewarm",
                $"'{slug}' art could not be downloaded after {MaxAttempts} attempts — world entry BLOCKED. " +
                "Most likely cause: the hero bundles were never pushed to R2 for THIS build (CLAUDE.md §16 — " +
                "bundle names are content-hashed, so every content build needs its own push).");
        }

        /// <summary>
        /// Read the hero the player actually chose out of the save and prewarm its art. This is the
        /// entry point the load screen / SceneRouter uses; it keeps the "which slug?" question in one
        /// place instead of duplicating HeroBodySwapper's resolution at every call site.
        /// </summary>
        public static IEnumerator PrewarmChosenHero()
        {
            string cls = null;
            Guard.Try("HeroPrewarm", "read chosen HeroClass from save", () =>
            {
                // HeroClassOpt is a plain ENUM whose None member is the "not chosen yet" sentinel
                // (Assets/_Modules/Core/State/GameState.cs:45). The ToNullable() extension used by
                // HeroBodySwapper lives in the Village assembly and is NOT visible from Core.
                var svc = DeNelle.Core.State.GameStateService.Instance;
                var st = svc != null ? svc.State : null;
                if (st != null && st.HeroClass != DeNelle.Core.State.HeroClassOpt.None)
                    cls = st.HeroClass.ToString();
            });

            if (string.IsNullOrEmpty(cls))
            {
                // No class chosen yet — the front-end / splash scenes. Nothing to fetch.
                State = HeroPrewarmState.Ready;
                StatusText = string.Empty;
                FlowTrace.Step("HeroPrewarm", "no HeroClass in save yet — nothing to prewarm (pre-class-select scene).");
                yield break;
            }

            yield return Prewarm(cls);
        }

        /// <summary>
        /// The body slugs that could be requested for a class. HeroBodySwapper does NOT simply ask for
        /// the class name: for a Knight it asks for "KnightV3" (FeatureFlags.KnightV3, default ON) or
        /// "KnightPackage" (FeatureFlags.HeroPackage) before falling back to "Knight"
        /// (Assets/_Modules/Village/Hero/HeroBodySwapper.cs:101/112/73). Prewarming only the class name
        /// would therefore download the WRONG bundle for a Knight and leave the one actually loaded
        /// uncached — a main-thread stall or a capsule. We fetch every REGISTERED variant for the class;
        /// unregistered names are skipped, so this costs nothing for the single-variant classes.
        /// FOLLOW-UP: mirror the feature flags here to stop fetching the ~14 MB of unused Knight
        /// variants; correctness first, bytes second.
        /// </summary>
        private static IEnumerable<string> BodySlugCandidates(string heroClass)
        {
            yield return heroClass;
            if (string.Equals(heroClass, "Knight", System.StringComparison.OrdinalIgnoreCase))
            {
                yield return "KnightV3";
                yield return "KnightPackage";
                yield return "knightV2";
            }
        }

        /// <summary>Reset to Idle so the load screen's Retry button can call <see cref="Prewarm"/> again.</summary>
        public static void Reset()
        {
            State = HeroPrewarmState.Idle;
            Progress = 0f;
            StatusText = string.Empty;
        }

        /// <summary>
        /// The keys this hero's art spans: its body address "Heroes/&lt;slug&gt;" plus every
        /// "Heroes/Textures/*" key in the catalog (the atlases HeroTextureLoader paints on ride in
        /// one shared bundle, so any of its keys pulls the bundle). Only keys the catalog actually
        /// knows are returned — an unregistered key would fault the whole download operation.
        /// </summary>
        private static List<object> CollectKeys(string slug)
        {
            var keys = new List<object>();
            var seen = new HashSet<string>(System.StringComparer.Ordinal);

            Guard.Try("HeroPrewarm", $"collect Addressables keys for '{slug}'", () =>
            {
                foreach (string candidate in BodySlugCandidates(slug))
                {
                    string body = HeroAssetLoader.HeroAddrPrefix + candidate;
                    if (KeyRegistered(body) && seen.Add(body)) keys.Add(body);
                }

                foreach (var locator in Addressables.ResourceLocators)
                {
                    if (locator?.Keys == null) continue;
                    foreach (object key in locator.Keys)
                    {
                        if (!(key is string s)) continue;
                        if (!s.StartsWith(TexAddrPrefix, System.StringComparison.Ordinal)) continue;
                        if (seen.Add(s)) keys.Add(s);
                    }
                }
            });

            return keys;
        }

        /// <summary>True when any locator can locate <paramref name="key"/> (type-agnostic).</summary>
        private static bool KeyRegistered(string key)
        {
            foreach (var locator in Addressables.ResourceLocators)
            {
                if (locator == null) continue;
                if (locator.Locate(key, null, out IList<IResourceLocation> locs) && locs != null && locs.Count > 0)
                    return true;
            }
            return false;
        }

        /// <summary>Await GetDownloadSizeAsync and hand the byte count to <paramref name="sink"/>. 0 on any error.</summary>
        private static IEnumerator GetDownloadSize(List<object> keys, System.Action<long> sink)
        {
            AsyncOperationHandle<long> handle = default;
            bool started = Guard.Try("HeroPrewarm", "GetDownloadSizeAsync", () =>
            {
                handle = Addressables.GetDownloadSizeAsync((IEnumerable<object>)keys);
            });

            if (!started || !handle.IsValid())
            {
                // Unknown size. Treat as "something to download" so we still attempt the fetch —
                // never as zero, which would wave a missing bundle straight through the gate.
                sink(1);
                yield break;
            }

            while (handle.IsValid() && !handle.IsDone) yield return null;

            long result = 1;
            if (handle.IsValid())
            {
                result = handle.Status == AsyncOperationStatus.Succeeded ? handle.Result : 1;
                Guard.Try("HeroPrewarm", "release size handle", () => Addressables.Release(handle));
            }
            sink(result);
        }
    }
}
