// =============================================================================
// HonestFeedbackTuning (WO-1432) - the authored knobs for the honest-feedback
// thank-you offer. AggroTuning shape, deliberately.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Feedback
//
// Source of truth: Data/Canonical/honest-feedback.json, DUAL-COPIED to
//   Assets/Resources/Data/Canonical/honest-feedback.json      (WebGL-safe, wins)
//   Assets/StreamingAssets/Data/Canonical/honest-feedback.json (desktop source)
// Both copies must stay byte-identical - CanonicalJson.cs:11-18 explains why the
// Resources copy exists at all (WebGL has no filesystem).
//
// -----------------------------------------------------------------------------
// WHAT IS AUTHORED HERE, AND WHAT DELIBERATELY IS NOT
// -----------------------------------------------------------------------------
// AUTHORED: the "first few minutes" threshold. WO-1432 section 6 records it as
// UNPROVEN - the owner said "after first few minutes" and no number was ruled -
// so it is tuned in JSON rather than recompiled. 300 s is a reading of "a few
// minutes", not a measurement, and it is expected to move.
//
// ⛔ NOT AUTHORED: the 1000/1000/1000 grant. That number is the owner's, stated
// verbatim in the WO source quote, and WO-1432 section 5 requires a regression
// that asserts each delta is EXACTLY 1000. A JSON-authored amount would make
// that oracle self-fulfilling - it would read the same file the code read and
// agree with whatever was in it. The amounts are consts on HonestFeedbackGrant
// and the oracle asserts the literal.
//
// FOUR NON-NEGOTIABLES of this file family (AggroTuning.cs is the reference):
//   1. a `version` int on the doc,
//   2. `const string RelativePath`,
//   3. shipped-default consts so a missing/garbled file degrades to a PLAYABLE
//      state rather than a zero,
//   4. every fallback is a LOGGED Warn, never a silent default (CLAUDE.md sec.12).
//
// ASCII only. Instrumentation: FlowTrace tag "HonestFeedback". Never strip it.
// =============================================================================

using DeNelle.Core.Diagnostics;
using Newtonsoft.Json;
using UnityEngine;

namespace DeNelle.Village.Feedback
{
    /// <summary>Authored knobs for the WO-1432 honest-feedback offer. Read-only at runtime.</summary>
    public static class HonestFeedbackTuning
    {
        /// <summary>StreamingAssets-relative path handed to CanonicalJson.Read.</summary>
        public const string RelativePath = "Data/Canonical/honest-feedback.json";

        /// <summary>The schema version this code was written against. A mismatch loads anyway
        /// (the additive law) and warns - it never refuses the file.</summary>
        public const int ExpectedVersion = 1;

        // ── Shipped defaults (law 3: a missing file must still be playable) ───────

        /// <summary>Owner: "after first few minutes". 300 s is a READING of that, not a
        /// measurement - WO-1432 section 6 records the exact number as unproven.</summary>
        public const float DefaultMinSessionSeconds = 300f;

        /// <summary>How often the offer gate re-asks itself once a positive beat has landed.
        /// Deliberately coarse: this is a once-per-save panel, not a hot path.</summary>
        public const float DefaultRecheckIntervalSeconds = 4f;

        /// <summary>Below this many trimmed characters the Send button stays disabled.
        /// The server ALSO refuses an empty report (api/bug-report.js "Empty report" -> 400),
        /// so this is the polite half of a check that exists on both ends.</summary>
        public const int DefaultMinCharacters = 12;

        /// <summary>Client-side cap. api/bug-report.js truncates at 4000; the in-game box has
        /// always been 1000 (BugReportVM.NoteMaxChars) and there is no reason to differ.</summary>
        public const int DefaultMaxCharacters = 1000;

        /// <summary>
        /// The store destination. Defaults to the PUBLISHER SITE, which is the only
        /// listing-adjacent URL this repo can prove resolves anywhere
        /// (publishing/config.yaml:34; docs/SOLANA_STORE_LISTING.md).
        /// <para>⚠ RECORDED AS UNPROVEN, deliberately: the Solana dApp Store registers only the
        /// custom scheme <c>solanadappstore://details</c> and there is NO https listing host
        /// (verified on device 2026-08-19 via dumpsys, docs/SOLANA_STORE_LISTING.md). The exact
        /// QUERY SHAPE after <c>details</c> has never been captured, so this file does not guess
        /// one - see <see cref="StoreDeepLink"/>.</para>
        /// </summary>
        public const string DefaultStoreUrl = "https://echoes-of-elarion.vercel.app/";

        // ── Public reads ─────────────────────────────────────────────────────────

        /// <summary>Cumulative time in this session before the offer may appear at all.</summary>
        public static float MinSessionSeconds => Mathf.Max(0f, Doc().Offer.MinSessionSeconds);

        /// <summary>Seconds between offer-gate re-checks once a positive beat has landed.</summary>
        public static float RecheckIntervalSeconds => Mathf.Max(0.5f, Doc().Offer.RecheckIntervalSeconds);

        /// <summary>Minimum trimmed characters before Send is enabled.</summary>
        public static int MinCharacters => Mathf.Max(1, Doc().Feedback.MinCharacters);

        /// <summary>Hard character cap on the input field.</summary>
        public static int MaxCharacters => Mathf.Clamp(Doc().Feedback.MaxCharacters, 1, 4000);

        /// <summary>
        /// The URL the secondary, UNREWARDED store button opens.
        /// <see cref="StoreDeepLink"/> wins when it is authored; otherwise the web URL.
        /// </summary>
        public static string StoreUrl
        {
            get
            {
                var deep = StoreDeepLink;
                if (!string.IsNullOrWhiteSpace(deep)) return deep.Trim();
                var url = Doc().StoreLink.Url;
                return string.IsNullOrWhiteSpace(url) ? DefaultStoreUrl : url.Trim();
            }
        }

        /// <summary>
        /// The platform deep link, EMPTY BY DEFAULT AND THAT IS THE POINT. A
        /// <c>solanadappstore://details?...</c> string typed from memory would be a guess
        /// shipped as a fact (CLAUDE.md section 11B); the only honest default is nothing, and
        /// the button then opens the proven web URL. One device check closes it: fill this
        /// field, no recompile.
        /// </summary>
        public static string StoreDeepLink => Doc().StoreLink.DeepLink;

        /// <summary>The loaded schema version (the fallback doc reports <see cref="ExpectedVersion"/>).</summary>
        public static int Version => Doc().Version;

        /// <summary>Drop the cached doc so the next read reloads from disk (headless oracle hook).</summary>
        public static void Reload() => _doc = null;

        // ── Load ─────────────────────────────────────────────────────────────────

        private static TuningDoc _doc;

        private static TuningDoc Doc()
        {
            if (_doc != null) return _doc;
            _doc = Guard.Try("HonestFeedback", "load honest-feedback.json", Load,
                fallback: (TuningDoc)null) ?? Fallback();
            return _doc;
        }

        private static TuningDoc Load()
        {
            string json = DeNelle.Core.CanonicalJson.Read(RelativePath);
            if (string.IsNullOrEmpty(json))
            {
                FlowTrace.Warn("HonestFeedback",
                    $"honest-feedback.json not found ({RelativePath}) -- using shipped defaults " +
                    $"(minSessionSeconds {DefaultMinSessionSeconds:0.#}, minCharacters {DefaultMinCharacters}). " +
                    "The offer still works; only the tuning is stock.");
                return null;
            }

            var d = JsonConvert.DeserializeObject<TuningDoc>(json);
            if (d == null || d.Offer == null || d.Feedback == null || d.StoreLink == null)
            {
                FlowTrace.Warn("HonestFeedback",
                    "honest-feedback.json parsed empty or is missing a section -- using shipped defaults.");
                return null;
            }
            if (d.Version != ExpectedVersion)
                FlowTrace.Warn("HonestFeedback",
                    $"honest-feedback.json version {d.Version} != expected {ExpectedVersion} -- loading anyway (additive).");

            FlowTrace.Step("HonestFeedback",
                $"HonestFeedbackTuning loaded (version {d.Version}): minSessionSeconds " +
                $"{d.Offer.MinSessionSeconds:0.#} recheck {d.Offer.RecheckIntervalSeconds:0.#}s " +
                $"minChars {d.Feedback.MinCharacters} maxChars {d.Feedback.MaxCharacters} " +
                $"storeDeepLink={(string.IsNullOrWhiteSpace(d.StoreLink.DeepLink) ? "unauthored" : "authored")}.");
            return d;
        }

        private static TuningDoc Fallback() => new TuningDoc();

        // ── DTOs (Newtonsoft, [JsonProperty]-mapped, defaults = the shipped consts) ──

        private sealed class TuningDoc
        {
            [JsonProperty("version")] public int Version = ExpectedVersion;
            [JsonProperty("offer")] public OfferDoc Offer = new OfferDoc();
            [JsonProperty("feedback")] public FeedbackDoc Feedback = new FeedbackDoc();
            [JsonProperty("storeLink")] public StoreLinkDoc StoreLink = new StoreLinkDoc();
        }

        private sealed class OfferDoc
        {
            [JsonProperty("minSessionSeconds")] public float MinSessionSeconds = DefaultMinSessionSeconds;
            [JsonProperty("recheckIntervalSeconds")] public float RecheckIntervalSeconds = DefaultRecheckIntervalSeconds;
        }

        private sealed class FeedbackDoc
        {
            [JsonProperty("minCharacters")] public int MinCharacters = DefaultMinCharacters;
            [JsonProperty("maxCharacters")] public int MaxCharacters = DefaultMaxCharacters;
        }

        private sealed class StoreLinkDoc
        {
            [JsonProperty("url")] public string Url = DefaultStoreUrl;
            [JsonProperty("deepLink")] public string DeepLink = "";
        }
    }
}
