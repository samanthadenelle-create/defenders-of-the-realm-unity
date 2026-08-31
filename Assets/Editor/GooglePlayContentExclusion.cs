// =============================================================================
// GooglePlayContentExclusion — WO-1282 Lane D. Keeps the FORCE-INCLUDED wallet
// payloads (Resources/ + StreamingAssets/) OUT of a GOOGLE_PLAY artifact and IN
// the DAPP_STORE (Seeker) artifact, from ONE declaration.
// -----------------------------------------------------------------------------
// WHY THIS FILE EXISTS — the door an .asmdef cannot reach.
//
// GooglePlayPackagingGate.InspectSourceIsolation() passed on 2026-08-30 and the
// AAB it let through was STILL dirty. The reason is structural, not a bug:
//
//   * Everything under a folder named `Resources` is packed into EVERY player by
//     construction. It is reached by NAME (Resources.Load), so no assembly
//     reference exists for a define constraint to sever. Same force-include
//     hazard SupercyanGearAddressableMarker's header documents (WO-191/408).
//   * Everything under `Assets/StreamingAssets` is copied verbatim into the
//     artifact's `assets/` tree. There is no removal API: BuildPlayerContext
//     offers AddAdditionalPathToStreamingAssets and nothing that subtracts.
//
// MEASURED — the exact entries a scan of Builds/Android/EchoesOfElarion-GooglePlay.aab
// (523 MB, built 2026-08-30 13:31 WITH source isolation green) attributes to this door:
//
//   base/assets/Data/Canonical/wallets.json          <- StreamingAssets copy
//     tokens: solana, solflare, seed vault, SKRbvo6Gf7GondiT3BbTfuRDPqLWei4j2Qy2NPGZhW3
//   base/assets/bin/Data/06e217da8217426d8e0cc825d97a3f19   <- Resources copy of the same file
//     tokens: solana, solflare, seed vault, SKRbvo6...
//   base/assets/bin/Data/2daec0b3f6c729e48b0dbb41f3f97b71   <- Resources/SolanaUnitySDK prefab
//   base/assets/bin/Data/9521afa2c179ff8428e9468eaa0d237c   <- Resources/SolanaUnitySDK prefab
//     tokens: solana, walletadapter
//   base/assets/bin/Data/globalgamemanagers(.assets.split0) <- the Resources NAME TABLE,
//     which literally spells "SolanaUnitySDK/WalletAdapterButton" in the artifact
//
// THIS FILE DOES NOT MAKE THE AAB CLEAN ON ITS OWN, AND MUST NOT BE READ AS IF IT
// DOES. Two further doors are outside its reach and are recorded here so the next
// reader does not re-discover them the expensive way:
//   (1) the `com.solana.unity_sdk` UPM package — its assemblies are compiled and
//       shipped regardless of any asmdef, so global-metadata.dat, libil2cpp.so,
//       ScriptingAssemblies.json, RuntimeInitializeOnLoads.json,
//       Managed/Resources/Solana.Unity.*.dll-resources.dat and even
//       BUNDLE-METADATA/com.unity/dependencies.pb all carry the token. Removing it
//       needs a manifest.json swap + package resolve + full recompile, which cannot
//       happen inside a build callback, AND it is blocked on UniTask: the package
//       VENDORS the "UniTask" assembly that 16 first-party asmdefs (DeNelle.Core and
//       DeNelle.Village included) reference.
//   (2) authoring prose + rail-only UI strings inside live canonical JSON
//       (canon-strings.json, en.json, packs.json, skin.json, skr_staking.json,
//       stake-rewards.json). Those files must SHIP; only their contents can change.
//
// SEEKER SAFETY — the failure mode that would be WORSE than the one we close.
// A Seeker/dApp-Store build must still carry the wallet. Four guarantees, copied
// deliberately from MobileWalletAdapterPlayExclusion:
//   a. State is DERIVED on every Android build from THAT build's defines, never
//      toggled blind — a Seeker build re-asserts "whole" whatever it found.
//   b. OnPostprocessBuild restores every quarantined asset.
//   c. A LEDGER FILE on disk (not SessionState) records every move, so a build that
//      dies — or an editor that is killed — is repairable in a LATER editor session.
//      SessionState alone dies with the process and would lose the evidence.
//   d. RepairAfterInterruptedBuild() runs on domain load and says so LOUDLY, and
//      AndroidBuild calls EnsureTreeIsWhole() before it builds ANYTHING, because the
//      Addressables content build runs BEFORE PrepareForBuild and would otherwise
//      bake a wallet-less tree into the Seeker bundles.
//
// A move failure FAILS THE BUILD (BuildFailedException). A half-quarantined tree that
// kept building would ship exactly the artifact this file exists to prevent.
//
// EDITOR-ONLY. Idempotent. Does not build, does not commit.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>
    /// Owns the per-artifact inclusion of the force-included wallet payloads under
    /// <c>Resources/</c> and <c>StreamingAssets/</c>: relocates them out of the
    /// force-include paths for the duration of a <c>GOOGLE_PLAY</c> build and puts
    /// them back afterwards.
    /// </summary>
    public static class GooglePlayContentExclusion
    {
        /// <summary>The define that means "this artifact is bound for Google Play review".</summary>
        public const string PlayDefine = "GOOGLE_PLAY";

        /// <summary>Where quarantined assets live for the duration of a Play build.
        /// Deliberately NOT under a folder named Resources and NOT under StreamingAssets —
        /// that is the whole point. A prefab/TextAsset in a plain Assets folder enters a
        /// player only when a scene, a Resources folder or an Addressables group references
        /// it, and nothing references these by anything but a Resources path string.</summary>
        public const string QuarantineRoot = "Assets/PlayQuarantine";

        /// <summary>Repo-relative ledger of in-flight moves. Survives an editor crash, which
        /// is the entire reason it is a FILE and not SessionState.</summary>
        public const string LedgerPath = "Builds/play-content-quarantine.txt";
        public const string RewriteLedgerPath = "Builds/play-neutral-rewrite-ledger.txt";
        public const string RewriteBackupRoot = "Builds/play-neutral-rewrite-backups";

        private const string LogTag = "[PlayContentExclusion]";

        /// <summary>Set while a build in THIS editor session legitimately owns the quarantine,
        /// so the domain-load repair does not fight an in-flight build.</summary>
        private const string ExclusionActiveKey = "GooglePlayContentExclusion.Active";

        /// <summary>
        /// The force-included payloads a Play artifact must not carry. Each entry is an asset
        /// path — a folder or a single file. Both are legal for AssetDatabase.MoveAsset.
        /// </summary>
        /// <remarks>
        /// EVERY ENTRY MUST BE DEAD CODE IN A GOOGLE_PLAY BUILD. Verified at source
        /// 2026-08-30:
        ///  - Resources/SolanaUnitySDK/*.prefab — loaded by name from the Solana SDK package
        ///    only. Nothing in DeNelle.* resolves them.
        ///  - wallets.json (both copies) — the ONLY runtime reader is
        ///    DeNelle.Wallet.WalletRegistry.LoadRegistry(), and DeNelle.Wallet.asmdef carries
        ///    defineConstraints ["!GOOGLE_PLAY"], so that assembly does not exist in a Play
        ///    player. There is no enumerating loader over Data/Canonical — CanonicalJson.Read
        ///    takes a NAMED relative path — so removing one file cannot starve another catalog.
        ///    WalletRegistry additionally falls back to hard-coded public addresses when the
        ///    file is absent, so even the editor path degrades to a warning, never a null-ref.
        /// </remarks>
        internal static readonly string[] PlayExcludedAssetPaths =
        {
            "Assets/Resources/SolanaUnitySDK",
            "Assets/Resources/Data/Canonical/wallets.json",
            "Assets/StreamingAssets/Data/Canonical/wallets.json",
            // Crypto-rail catalogs and mixed presentation tables. All are either unused by
            // GOOGLE_PLAY assemblies or have code fallbacks (skin resolves to the neutral
            // store skin). Keep BOTH force-included mirrors paired: StreamingAssets creates
            // readable paths while Resources creates opaque hashed player-data blobs.
            "Assets/Resources/Data/Canonical/battle_monthly.json",
            "Assets/StreamingAssets/Data/Canonical/battle_monthly.json",
            "Assets/Resources/Data/Canonical/battle_monthly_packs.sample.json",
            "Assets/StreamingAssets/Data/Canonical/battle_monthly_packs.sample.json",
            "Assets/Resources/Data/Canonical/skin.json",
            "Assets/StreamingAssets/Data/Canonical/skin.json",
            "Assets/Resources/Data/Canonical/skr_staking.json",
            "Assets/StreamingAssets/Data/Canonical/skr_staking.json",
            "Assets/Resources/Data/Canonical/skr_store.json",
            "Assets/StreamingAssets/Data/Canonical/skr_store.json",
            "Assets/Resources/Data/Canonical/stake-rewards.json",
            "Assets/StreamingAssets/Data/Canonical/stake-rewards.json",
        };

        private static readonly string[][] PlayNeutralMirrorPairs =
        {
            new[] { "Assets/Resources/Data/Canonical/canon-strings.json", "Assets/StreamingAssets/Data/Canonical/canon-strings.json" },
            new[] { "Assets/Resources/Data/Canonical/en.json", "Assets/StreamingAssets/Data/Canonical/en.json" },
            new[] { "Assets/Resources/Data/Canonical/packs.json", "Assets/StreamingAssets/Data/Canonical/packs.json" },
        };

        private static readonly string[] PlayNeutralUxmlPaths =
        {
            "Assets/_Modules/Onboarding/UI/TitleScreen.uxml",
            "Assets/_Modules/Onboarding/UI/HeroSelectScreen.uxml",
        };

        // -------------------------------------------------------------------------
        // Enforcement
        // -------------------------------------------------------------------------

        /// <summary>
        /// Derives — never toggles — the tree's shape from the define set this build will
        /// compile with. Returns true when the payloads are INCLUDED (Seeker), false when
        /// they are quarantined (Play).
        /// </summary>
        internal static bool ApplyForDefines(IReadOnlyList<string> defines)
        {
            var set = new HashSet<string>(defines ?? Array.Empty<string>(), StringComparer.Ordinal);
            bool isPlay = set.Contains(PlayDefine);
            string defineList = string.Join(", ", defines ?? Array.Empty<string>());

            if (!isPlay)
            {
                // A Seeker build RE-ASSERTS the whole tree rather than assuming it. If a
                // previous Play build died, this is the moment that would otherwise ship a
                // Seeker APK with no wallet.
                RestoreNeutralRewrites("non-Play Android build");
                RestoreAll("non-Play Android build");
                Debug.Log($"{LogTag} PLAY_CONTENT_INCLUDED — defines=[{defineList}]. " +
                          $"{PlayExcludedAssetPaths.Length} wallet payload(s) stay in Resources/StreamingAssets. " +
                          "This is the Seeker/dApp-Store shape and MUST carry the Solana rail.");
                return true;
            }

            Quarantine(defineList);
            try { ApplyNeutralRewrites(); }
            catch
            {
                RestoreNeutralRewrites("aborted neutral rewrite");
                RestoreAll("aborted neutral rewrite");
                throw;
            }
            return false;
        }

        private static void ApplyNeutralRewrites()
        {
            RestoreNeutralRewrites("pre-rewrite sweep");
            Directory.CreateDirectory(RewriteBackupRoot);
            var ledger = new List<string>();
            try
            {
                foreach (string[] pair in PlayNeutralMirrorPairs)
                {
                    foreach (string path in pair)
                    {
                        if (!File.Exists(path))
                            throw new BuildFailedException($"{LogTag} PLAY_NEUTRAL_SOURCE_MISSING - {path}");
                        string backup = Path.Combine(RewriteBackupRoot, path.Replace('/', '_').Replace('\\', '_') + ".bytes");
                        File.WriteAllBytes(backup, File.ReadAllBytes(path));
                        ledger.Add(path + "\t" + backup);
                        File.WriteAllText(RewriteLedgerPath, string.Join(Environment.NewLine, ledger));

                        JObject root = JObject.Parse(File.ReadAllText(path));
                        string file = Path.GetFileName(path);
                        if (file == "canon-strings.json")
                        {
                            root["_nightMarketNote"] = "Google Play store presentation.";
                            root["storeBuyWalletRequiredCta"] = "Continue";
                        }
                        else if (file == "en.json")
                        {
                            root["swap.poweredBy"] = "Store service";
                        }
                        else if (file == "packs.json")
                        {
                            JObject notes = root["_schemaNotes"] as JObject;
                            if (notes != null)
                            {
                                foreach (JProperty property in notes.Properties().ToArray())
                                    if (ContainsForbiddenAuthoringToken(property.Value?.ToString()))
                                        property.Remove();
                            }
                            JProperty disclaimer = root.Property("currencyDisclaimer");
                            if (disclaimer != null && ContainsForbiddenAuthoringToken(disclaimer.Value?.ToString()))
                                disclaimer.Remove();
                            foreach (JObject pack in (root["packs"] as JArray ?? new JArray()).OfType<JObject>())
                            {
                                JObject pricing = pack["pricing"] as JObject;
                                pricing?.Property("usdc")?.Remove();
                                pricing?.Property("sol")?.Remove();
                                pricing?.Property("skr")?.Remove();
                            }
                        }
                        File.WriteAllText(path, root.ToString(Formatting.Indented) + Environment.NewLine);
                        JObject.Parse(File.ReadAllText(path));
                    }
                }
                foreach (string path in PlayNeutralUxmlPaths)
                {
                    if (!File.Exists(path))
                        throw new BuildFailedException($"{LogTag} PLAY_NEUTRAL_SOURCE_MISSING - {path}");
                    string backup = Path.Combine(RewriteBackupRoot, path.Replace('/', '_').Replace('\\', '_') + ".bytes");
                    File.WriteAllBytes(backup, File.ReadAllBytes(path));
                    ledger.Add(path + "\t" + backup);
                    File.WriteAllText(RewriteLedgerPath, string.Join(Environment.NewLine, ledger));
                    string uxml = File.ReadAllText(path);
                    if (uxml.IndexOf("Connect Wallet", StringComparison.Ordinal) < 0)
                        throw new BuildFailedException($"{LogTag} PLAY_NEUTRAL_UXML_ANCHOR_MISSING - {path}");
                    File.WriteAllText(path, uxml.Replace("Connect Wallet", "Continue with Google"));
                    if (File.ReadAllText(path).IndexOf("Connect Wallet", StringComparison.OrdinalIgnoreCase) >= 0)
                        throw new BuildFailedException($"{LogTag} PLAY_NEUTRAL_UXML_REWRITE_FAILED - {path}");
                }
                ValidateNeutralMirrorEquality("rewrite");
                AssetDatabase.Refresh();
                Debug.Log($"{LogTag} PLAY_NEUTRAL_REWRITE_OK - rewrote {ledger.Count} mirrored catalogs; ledger={RewriteLedgerPath}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"{LogTag} PLAY_NEUTRAL_REWRITE_FAIL - {ex.Message}");
                throw new BuildFailedException($"Play-neutral catalog rewrite failed: {ex.Message}");
            }
        }

        private static bool ContainsForbiddenAuthoringToken(string value)
        {
            string text = value ?? string.Empty;
            string[] tokens = { "solana", "jupiter", "$skr", " skr", "usdc", "crypto", "web3", "wallet" };
            return tokens.Any(token => text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        internal static void RestoreNeutralRewrites(string reason)
        {
            if (!File.Exists(RewriteLedgerPath)) return;
            foreach (string raw in File.ReadAllLines(RewriteLedgerPath))
            {
                string[] parts = raw.Split('\t');
                if (parts.Length != 2 || !File.Exists(parts[1]))
                {
                    Debug.LogError($"{LogTag} PLAY_NEUTRAL_RESTORE_FAILED - invalid/missing backup row '{raw}'.");
                    continue;
                }
                File.WriteAllBytes(parts[0], File.ReadAllBytes(parts[1]));
                if (!File.ReadAllBytes(parts[0]).SequenceEqual(File.ReadAllBytes(parts[1])))
                    throw new BuildFailedException($"{LogTag} PLAY_NEUTRAL_BYTE_RESTORE_MISMATCH - {parts[0]}");
            }
            ValidateNeutralMirrorEquality("restore");
            File.Delete(RewriteLedgerPath);
            if (Directory.Exists(RewriteBackupRoot)) Directory.Delete(RewriteBackupRoot, true);
            AssetDatabase.Refresh();
            Debug.Log($"{LogTag} PLAY_NEUTRAL_RESTORED - original catalog bytes restored ({reason}).");
        }

        private static void ValidateNeutralMirrorEquality(string phase)
        {
            foreach (string[] pair in PlayNeutralMirrorPairs)
            {
                byte[] left = File.ReadAllBytes(pair[0]);
                byte[] right = File.ReadAllBytes(pair[1]);
                if (!left.SequenceEqual(right))
                    throw new BuildFailedException($"{LogTag} PLAY_NEUTRAL_MIRROR_MISMATCH ({phase}) - {pair[0]} != {pair[1]}");
            }
        }

        private static void Quarantine(string defineList)
        {
            EnsureAssetFolder(QuarantineRoot);

            var moved = new List<string>();
            var skipped = new List<string>();

            foreach (string source in PlayExcludedAssetPaths)
            {
                if (!AssetExists(source))
                {
                    // Not a failure: the payload may already be quarantined by an earlier
                    // PrepareForBuild in the same session, or genuinely absent on a clone.
                    skipped.Add(source);
                    continue;
                }

                string destination = QuarantinePathFor(source);
                EnsureAssetFolder(ParentFolderOf(destination));

                string error = AssetDatabase.MoveAsset(source, destination);
                if (!string.IsNullOrEmpty(error))
                {
                    string message = $"{LogTag} PLAY_CONTENT_QUARANTINE_FAILED — could not move " +
                                     $"'{source}' to '{destination}': {error}. REFUSING the build: a " +
                                     "half-quarantined tree would produce exactly the Play artifact this " +
                                     "file exists to prevent, and would do it with every other marker green.";
                    Debug.LogError(message);

                    // Put back whatever already moved before bailing — a failed build must not
                    // leave a Seeker-breaking tree behind.
                    WriteLedger(moved);
                    RestoreAll("aborted quarantine");
                    throw new BuildFailedException(message);
                }

                moved.Add(destination + "\t" + source);
            }

            WriteLedger(moved);
            SessionState.SetBool(ExclusionActiveKey, true);
            AssetDatabase.Refresh();

            Debug.Log($"{LogTag} PLAY_CONTENT_EXCLUDED — defines=[{defineList}]; " +
                      $"quarantined {moved.Count} payload(s) into {QuarantineRoot}" +
                      (skipped.Count > 0 ? $"; absent/already-moved: {string.Join(", ", skipped)}" : string.Empty) +
                      $". Ledger: {LedgerPath}. They are restored by OnPostprocessBuild, and by " +
                      "RepairAfterInterruptedBuild() on the next domain load if this build dies.");
        }

        /// <summary>Restores every ledgered move. Deterministic, so it repairs an interrupted
        /// Play build as well as ending a successful one.</summary>
        internal static void RestoreAll(string reason)
        {
            var entries = ReadLedger();
            SessionState.SetBool(ExclusionActiveKey, false);

            if (entries.Count == 0)
            {
                DeleteLedger();
                PruneQuarantineRoot();
                return;
            }

            int restored = 0;
            foreach (var pair in entries)
            {
                string quarantined = pair.Key;
                string original = pair.Value;

                if (!AssetExists(quarantined))
                {
                    if (AssetExists(original))
                        continue; // already back where it belongs

                    Debug.LogError($"{LogTag} PLAY_CONTENT_RESTORE_LOST — '{quarantined}' is gone and " +
                                   $"'{original}' is not back. The Seeker artifact WILL be missing this " +
                                   "payload. Restore it from git before building anything for the dApp Store.");
                    continue;
                }

                EnsureAssetFolder(ParentFolderOf(original));
                string error = AssetDatabase.MoveAsset(quarantined, original);
                if (!string.IsNullOrEmpty(error))
                {
                    Debug.LogError($"{LogTag} PLAY_CONTENT_RESTORE_FAILED — '{quarantined}' -> " +
                                   $"'{original}': {error}. DO NOT build the Seeker APK until this is " +
                                   "resolved by hand; it would ship without the wallet payload.");
                    continue;
                }

                restored++;
            }

            DeleteLedger();
            AssetDatabase.Refresh();
            PruneQuarantineRoot();

            Debug.Log($"{LogTag} PLAY_CONTENT_RESTORED — {restored}/{entries.Count} payload(s) are back " +
                      $"in Resources/StreamingAssets ({reason}).");
        }

        /// <summary>
        /// Called by AndroidBuild BEFORE anything else, for BOTH artifacts. The Addressables
        /// content build runs before PrepareForBuild, so a leftover quarantine would be baked
        /// into the bundles before the build hook ever got a chance to repair it.
        /// </summary>
        public static void EnsureTreeIsWhole()
        {
            RestoreNeutralRewrites("pre-build sweep");
            if (!File.Exists(LedgerPath))
                return;

            Debug.LogWarning($"{LogTag} PLAY_CONTENT_LEDGER_FOUND — {LedgerPath} exists before a build " +
                             "started. A previous GOOGLE_PLAY build did not finish. Restoring the wallet " +
                             "payloads now, before the content build reads the tree.");
            RestoreAll("pre-build sweep");
        }

        /// <summary>
        /// A Play build that dies between preprocess and postprocess would leave the payloads
        /// outside Resources/StreamingAssets in a TRACKED tree — which would silently strip the
        /// wallet from the next Seeker APK. That is worse than the gap this WO closes, so the
        /// repair runs on every domain load and is deliberately loud.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void ScheduleRepairAfterInterruptedBuild()
        {
            // The asset database is not guaranteed usable during InitializeOnLoad, and a failed
            // move there would read as "nothing to repair" — a silent miss on exactly the case
            // this guard exists for. Defer one editor tick.
            EditorApplication.delayCall += RepairAfterInterruptedBuild;
        }

        private static void RepairAfterInterruptedBuild()
        {
            if (SessionState.GetBool(ExclusionActiveKey, false))
                return; // A build in this editor session owns the quarantine right now.

            if (!File.Exists(LedgerPath) && !File.Exists(RewriteLedgerPath))
                return;

            Debug.LogWarning($"{LogTag} PLAY_CONTENT_REPAIRED — {LedgerPath} survived an interrupted " +
                             "GOOGLE_PLAY build. The wallet payloads were left OUTSIDE " +
                             "Resources/StreamingAssets, which would silently strip the Solana rail from " +
                             "the next Seeker/dApp-Store APK. Restoring them now; re-check `git status` " +
                             $"and that {QuarantineRoot} is gone before committing.");
            RestoreNeutralRewrites("interrupted-build repair");
            RestoreAll("interrupted-build repair");
        }

        // -------------------------------------------------------------------------
        // Ledger
        // -------------------------------------------------------------------------

        private static void WriteLedger(IReadOnlyList<string> lines)
        {
            string dir = Path.GetDirectoryName(LedgerPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            if (lines == null || lines.Count == 0)
            {
                DeleteLedger();
                return;
            }

            File.WriteAllLines(LedgerPath, lines.ToArray());
        }

        /// <summary>quarantined-path -&gt; original-path, in move order.</summary>
        private static List<KeyValuePair<string, string>> ReadLedger()
        {
            var result = new List<KeyValuePair<string, string>>();
            if (!File.Exists(LedgerPath))
                return result;

            foreach (string raw in File.ReadAllLines(LedgerPath))
            {
                string line = (raw ?? string.Empty).Trim();
                if (line.Length == 0)
                    continue;

                string[] parts = line.Split('\t');
                if (parts.Length != 2 || parts[0].Length == 0 || parts[1].Length == 0)
                {
                    Debug.LogError($"{LogTag} PLAY_CONTENT_LEDGER_CORRUPT — unreadable line '{raw}' in " +
                                   $"{LedgerPath}. Restore the wallet payloads by hand (git checkout) " +
                                   "before building the Seeker APK.");
                    continue;
                }

                result.Add(new KeyValuePair<string, string>(parts[0], parts[1]));
            }

            return result;
        }

        private static void DeleteLedger()
        {
            if (File.Exists(LedgerPath))
                File.Delete(LedgerPath);
        }

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        /// <summary>Flattens an asset path into a single quarantine leaf so two payloads with
        /// the same file name (there are two wallets.json) cannot collide.</summary>
        internal static string QuarantinePathFor(string assetPath)
        {
            string relative = assetPath.StartsWith("Assets/", StringComparison.Ordinal)
                ? assetPath.Substring("Assets/".Length)
                : assetPath;

            // '__' and not '~': Unity IGNORES any asset path element that ENDS in '~', and a
            // separator that can land at the end of a name is a trap waiting for the first
            // payload whose path ends in a slash.
            return QuarantineRoot + "/" + relative.Replace("/", "__");
        }

        private static string ParentFolderOf(string assetPath)
        {
            int slash = assetPath.LastIndexOf('/');
            return slash <= 0 ? "Assets" : assetPath.Substring(0, slash);
        }

        private static bool AssetExists(string assetPath)
        {
            return File.Exists(assetPath) || Directory.Exists(assetPath);
        }

        private static void EnsureAssetFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || AssetDatabase.IsValidFolder(folder))
                return;

            string parent = ParentFolderOf(folder);
            EnsureAssetFolder(parent);

            string leaf = folder.Substring(folder.LastIndexOf('/') + 1);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        /// <summary>Removes the quarantine folder (and its .meta) once empty, so a clean tree
        /// stays clean and `git status` never shows a build artifact under Assets/.</summary>
        private static void PruneQuarantineRoot()
        {
            if (!AssetDatabase.IsValidFolder(QuarantineRoot))
                return;

            bool empty = !Directory.EnumerateFileSystemEntries(QuarantineRoot)
                                   .Any(entry => !entry.EndsWith(".meta", StringComparison.OrdinalIgnoreCase));
            if (!empty)
            {
                Debug.LogWarning($"{LogTag} PLAY_CONTENT_QUARANTINE_NOT_EMPTY — {QuarantineRoot} still " +
                                 "holds assets after a restore. Inspect it: something is not back where " +
                                 "it belongs and the Seeker artifact may be missing a payload.");
                return;
            }

            AssetDatabase.DeleteAsset(QuarantineRoot);
        }
    }

    /// <summary>
    /// Applies <see cref="GooglePlayContentExclusion"/> before the player is built.
    /// BuildPlayerProcessor rather than IPreprocessBuildWithReport for the same reason
    /// MobileWalletAdapterPlayExclusion uses it: it is the only build hook that exposes
    /// <c>BuildPlayerOptions.extraScriptingDefines</c>, and GOOGLE_PLAY lives there, not in
    /// PlayerSettings (AndroidBuild.ArtifactScriptingDefines).
    /// </summary>
    public sealed class GooglePlayContentExclusionBuildProcessor : BuildPlayerProcessor
    {
        // Just after the plugin exclusion (-1000) and well before anything reads the asset
        // tree: Resources/StreamingAssets must be settled before the player data is packed.
        public override int callbackOrder => -999;

        public override void PrepareForBuild(BuildPlayerContext context)
        {
            BuildPlayerOptions options = context.BuildPlayerOptions;
            if (options.target != BuildTarget.Android)
                return;

            string[] defines = MobileWalletAdapterPlayExclusion.ComposeBuildDefines(options.extraScriptingDefines);
            GooglePlayContentExclusion.ApplyForDefines(defines);
        }
    }

    /// <summary>Puts the quarantined payloads back after the build, so the tracked tree never
    /// carries a transient exclusion.</summary>
    public sealed class GooglePlayContentExclusionRestore : IPostprocessBuildWithReport
    {
        public int callbackOrder => 1001;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report != null && report.summary.platform != BuildTarget.Android)
                return;

            GooglePlayContentExclusion.RestoreNeutralRewrites("post-build");
            GooglePlayContentExclusion.RestoreAll("post-build");
        }
    }
}
