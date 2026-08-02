// =============================================================================
// MobileSettings — applies the P0 mobile-readiness fixes from the audit.
// -----------------------------------------------------------------------------
// docs/audit/mobile-performance.md §1 finds the project configured desktop-first
// in the three places the v2 port-spec explicitly locks. This editor script
// drives the Unity C# settings APIs (PlayerSettings / QualitySettings / the URP
// pipeline asset) to switch on the mobile-correct configuration. Run once, from
// the main Unity session, either via the Defenders menu or the CLI:
//
//     -executeMethod DeNelle.Editor.MobileSettings.ApplyMobileSettings
//
// What it changes (audit references in brackets):
//   P0-1  Color space  -> Linear                                       (§1.1)
//   P0-2  Android scripting backend -> IL2CPP, ARM64 confirmed         (§1.2)
//   P0-3  Quality tiers -> Seeker_Low / Seeker_High / Desktop          (§1.4)
//   P1    URP-asset mobile tuning (shadows, HDR, MSAA, render scale,
//         intermediate-texture mode)                                   (§1.6)
//
// IDEMPOTENT — every step checks the current value before writing and logs only
// what it actually changed; a clean re-run reports "already correct".
//
// IMPORTANT (integrator note): switching color space to Linear triggers a FULL
// project asset reimport — Unity will churn for several minutes after this runs.
// That is expected, not a fault.
//
// This script does NOT hand-edit any ProjectSettings/*.asset YAML — a wrong
// field there breaks every project setting. It only calls the supported editor
// APIs, which validate and serialize correctly.
// =============================================================================

using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DeNelle.Editor
{
    /// <summary>
    /// Editor utility that applies the audit's P0 mobile-readiness configuration
    /// via the supported PlayerSettings / QualitySettings / URP-asset C# APIs.
    /// Entry point: <see cref="ApplyMobileSettings"/>.
    /// </summary>
    public static class MobileSettings
    {
        // ── URP pipeline asset (created/assigned by UrpActivator) ────────────
        private const string PipelinePath = "Assets/Settings/DeNelle-URP.asset";

        /// <summary>
        /// One quality tier's mobile-tuning values, per the audit §1.4 table.
        /// MSAA / render-scale / shadow values live on the URP asset (§1.6), so
        /// each tier carries the full set and the tier's own URP-asset variant
        /// is configured to match.
        /// </summary>
        private struct Tier
        {
            public string Name;
            public bool SoftShadows;
            public int MsaaSamples;          // 1 = off, 2, 4, 8
            public float RenderScale;
            public int TargetFps;
            public int ShadowmapResolution;  // main-light shadow atlas px
            public float ShadowDistance;     // metres
            public bool Hdr;
            public UnityEngine.ShadowQuality ShadowKind; // Disable / HardOnly / All(soft)
            public UnityEngine.ShadowResolution ShadowResEnum;       // QualitySettings tier shadow res
            public AnisotropicFiltering Anisotropic;
            public int PixelLightCount;
            public int TextureLimit;         // QualitySettings masterTextureLimit (0 = full)
        }

        // The three named tiers from audit §1.4. Order matters: the runtime
        // SeekerBootstrap and the audit both treat Seeker_High as the Android
        // default, so it is created/ordered explicitly below.
        private static readonly Tier SeekerLow = new Tier
        {
            Name = "Seeker_Low",
            SoftShadows = false,
            MsaaSamples = 1,            // MSAA off
            RenderScale = 0.85f,
            TargetFps = 30,
            ShadowmapResolution = 512,
            ShadowDistance = 25f,
            Hdr = false,
            ShadowKind = UnityEngine.ShadowQuality.HardOnly,   // no real-time soft shadows
            ShadowResEnum = UnityEngine.ShadowResolution.Low,
            Anisotropic = AnisotropicFiltering.Disable,
            PixelLightCount = 1,
            TextureLimit = 0,
        };

        private static readonly Tier SeekerHigh = new Tier
        {
            Name = "Seeker_High",
            SoftShadows = true,
            MsaaSamples = 2,            // 2x — cheap on tile GPUs
            RenderScale = 1.0f,
            TargetFps = 60,             // stretch 90 handled at runtime
            ShadowmapResolution = 1024,
            ShadowDistance = 30f,
            Hdr = false,                // HDR off on Seeker tiers (§1.6)
            ShadowKind = UnityEngine.ShadowQuality.All,
            ShadowResEnum = UnityEngine.ShadowResolution.Medium,
            Anisotropic = AnisotropicFiltering.Enable,
            PixelLightCount = 2,
            TextureLimit = 0,
        };

        private static readonly Tier Desktop = new Tier
        {
            Name = "Desktop",
            SoftShadows = true,
            MsaaSamples = 4,            // 4x for desktop / Vercel parity
            RenderScale = 1.0f,
            TargetFps = 60,
            ShadowmapResolution = 2048,
            ShadowDistance = 50f,
            Hdr = true,                 // keep HDR on Desktop only (§1.6)
            ShadowKind = UnityEngine.ShadowQuality.All,
            ShadowResEnum = UnityEngine.ShadowResolution.High,
            Anisotropic = AnisotropicFiltering.ForceEnable,
            PixelLightCount = 4,
            TextureLimit = 0,
        };

        // Tier creation order in QualitySettings (index 0..2).
        private static readonly Tier[] OrderedTiers = { SeekerLow, SeekerHigh, Desktop };

        // =====================================================================
        //  Entry point
        // =====================================================================

        [MenuItem("Defenders/Setup/Apply Mobile Settings")]
        public static void ApplyMobileSettings()
        {
            var log = new StringBuilder();
            log.AppendLine("[MobileSettings] === Applying P0 mobile-readiness fixes (audit §1) ===");

            int changes = 0;
            changes += ApplyColorSpace(log);                 // P0-1
            changes += ApplyAndroidScriptingBackend(log);    // P0-2
            changes += ApplyQualityTiers(log);               // P0-3
            changes += ApplyUrpMobileTuning(log);            // P1 §1.6

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            log.AppendLine(changes == 0
                ? "[MobileSettings] No changes — project already matches the mobile spec."
                : $"[MobileSettings] === Done — {changes} setting group(s) changed. ===");
            log.AppendLine("[MobileSettings] NOTE: a Linear color-space switch triggers a full " +
                           "asset reimport — expect Unity to churn for several minutes.");
            Debug.Log(log.ToString());
        }

        // =====================================================================
        //  P0-1 — Color space = Linear (§1.1)
        // =====================================================================

        private static int ApplyColorSpace(StringBuilder log)
        {
            if (PlayerSettings.colorSpace == ColorSpace.Linear)
            {
                log.AppendLine("[MobileSettings] P0-1 color space: already Linear — no change.");
                return 0;
            }

            log.AppendLine($"[MobileSettings] P0-1 color space: {PlayerSettings.colorSpace} -> Linear " +
                           "(URP requirement; triggers a full asset reimport).");
            PlayerSettings.colorSpace = ColorSpace.Linear;
            return 1;
        }

        // =====================================================================
        //  P0-2 — Android scripting backend = IL2CPP, ARM64 confirmed (§1.2)
        // =====================================================================

        private static int ApplyAndroidScriptingBackend(StringBuilder log)
        {
            int changes = 0;
            var android = NamedBuildTarget.Android;

            // Scripting backend -> IL2CPP.
            if (PlayerSettings.GetScriptingBackend(android) != ScriptingImplementation.IL2CPP)
            {
                log.AppendLine($"[MobileSettings] P0-2 Android scripting backend: " +
                               $"{PlayerSettings.GetScriptingBackend(android)} -> IL2CPP.");
                PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
                changes++;
            }
            else
            {
                log.AppendLine("[MobileSettings] P0-2 Android scripting backend: already IL2CPP.");
            }

            // IL2CPP code generation -> Faster (smaller) builds for the foundation
            // milestone. Unity 6 sets this per-platform via PlayerSettings
            // (EditorUserBuildSettings.il2CppCodeGeneration is deprecated).
            if (PlayerSettings.GetIl2CppCodeGeneration(android) != Il2CppCodeGeneration.OptimizeSize)
            {
                log.AppendLine("[MobileSettings] P0-2 IL2CPP code generation -> " +
                               "OptimizeSize (Faster/smaller builds).");
                PlayerSettings.SetIl2CppCodeGeneration(android, Il2CppCodeGeneration.OptimizeSize);
                changes++;
            }

            // C++ compiler configuration -> Release for the acceptance build.
            if (PlayerSettings.GetIl2CppCompilerConfiguration(android) != Il2CppCompilerConfiguration.Release)
            {
                log.AppendLine("[MobileSettings] P0-2 IL2CPP C++ compiler configuration -> Release.");
                PlayerSettings.SetIl2CppCompilerConfiguration(android, Il2CppCompilerConfiguration.Release);
                changes++;
            }

            // Managed stripping level -- APK size lever. Bumped Low -> Medium
            // (audit sec 1.2 originally said Low once IL2CPP is on; Medium is the
            // next low-risk download-shrink step). Medium is SAFE here ONLY
            // because Assets/link.xml preserve="all"s the Newtonsoft.Json engine,
            // Assembly-CSharp, and every DeNelle runtime data/service assembly --
            // those hold the JSON-reflection-deserialized catalogs + cross-asmdef
            // reflection bridges the static stripper cannot see. Without that
            // link.xml, Medium would silently strip JSON models and break catalog
            // loading in the built player. Do NOT raise to High. Idempotent:
            // only writes (and logs) when the level is below Medium.
            // ⚠ LOWERED Medium -> Low (2026-08-02, WO-766 Solana SDK integration).
            // CAPTURED FAILURE (Builds/android-build.log, this date):
            //   Fatal error in Unity CIL Linker
            //   Mono.Cecil.AssemblyResolutionException: Failed to resolve assembly:
            //     'BouncyCastle.Cryptography, Version=2.0.0.0'
            // The Solana SDK ships BouncyCastle.Crypto.dll whose INTERNAL assembly name is
            // BouncyCastle.Cryptography, referenced by Solana.Unity.{Wallet,KeyStore,Dex}.
            // At Medium the linker must fully resolve every reference across all assemblies
            // and dies on that one; the WINDOWS player stripped the SAME dll fine
            // (Library/Bee/artifacts/WinPlayerBuildProgram/ManagedStripped/BouncyCastle.Crypto.dll),
            // which is what isolates the LEVEL - not the file - as the variable. Nothing
            // under artifacts/Android ever received it: the link step died first.
            // Low still strips (engine-code stripping stays ON via stripEngineCode) and
            // link.xml still guards the JSON/reflection surface; the cost is APK size.
            // FOLLOW-UP: restore Medium once the SDK's BouncyCastle resolves (WO-848).
            if (PlayerSettings.GetManagedStrippingLevel(android) != ManagedStrippingLevel.Low)
            {
                log.AppendLine($"[MobileSettings] P0-2 Android managed stripping level: " +
                               $"{PlayerSettings.GetManagedStrippingLevel(android)} -> Low " +
                               "(Medium breaks the Solana SDK BouncyCastle resolve - WO-766).");
                PlayerSettings.SetManagedStrippingLevel(android, ManagedStrippingLevel.Low);
                changes++;
            }
            else
            {
                log.AppendLine("[MobileSettings] P0-2 Android managed stripping level: already Low -- no change.");
            }

            // Strip unused mesh components -- a low-risk size lever that drops
            // vertex channels (colors, extra UVs, tangents) no material/shader
            // actually reads, project-wide. Idempotent: only writes when off.
            if (!PlayerSettings.stripUnusedMeshComponents)
            {
                log.AppendLine("[MobileSettings] P0-2 stripUnusedMeshComponents: false -> true.");
                PlayerSettings.stripUnusedMeshComponents = true;
                changes++;
            }
            else
            {
                log.AppendLine("[MobileSettings] P0-2 stripUnusedMeshComponents: already true -- no change.");
            }

            // ARM64 — audit §1.2 verified this GOOD; confirm and re-assert, do not weaken.
            var arch = PlayerSettings.Android.targetArchitectures;
            if ((arch & AndroidArchitecture.ARM64) == 0)
            {
                log.AppendLine($"[MobileSettings] P0-2 Android target architectures: {arch} " +
                               "-> ARM64 (was missing ARM64 — corrected).");
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                changes++;
            }
            else if (arch != AndroidArchitecture.ARM64)
            {
                log.AppendLine($"[MobileSettings] P0-2 Android target architectures: {arch} " +
                               "-> ARM64-only (dropped ARMv7 bloat per audit).");
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                changes++;
            }
            else
            {
                log.AppendLine("[MobileSettings] P0-2 Android target architectures: already ARM64-only " +
                               "(audit-verified GOOD).");
            }

            return changes > 0 ? 1 : 0;
        }

        // =====================================================================
        //  P0-3 — Quality tiers Seeker_Low / Seeker_High / Desktop (§1.4)
        // =====================================================================

        private static int ApplyQualityTiers(StringBuilder log)
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                log.AppendLine($"[MobileSettings] P0-3 ERROR: URP asset not found at '{PipelinePath}' " +
                               "— run Defenders/Setup/Activate URP first. Tiers NOT created.");
                return 0;
            }

            string[] existing = QualitySettings.names;
            bool namesAlreadyCorrect =
                existing.Length == OrderedTiers.Length &&
                existing[0] == SeekerLow.Name &&
                existing[1] == SeekerHigh.Name &&
                existing[2] == Desktop.Name;

            int structureChanges = 0;
            if (!namesAlreadyCorrect)
            {
                // Resize the QualitySettings level array to exactly three slots
                // and name them, via the QualitySettings SerializedObject — the
                // same serialization API the Quality inspector uses (Unity has
                // no public add/remove-level API). This is NOT hand-editing the
                // YAML: Unity re-serializes the asset correctly afterwards.
                structureChanges = RebuildQualityLevelArray(log, existing);
            }
            else
            {
                log.AppendLine("[MobileSettings] P0-3 quality tiers: the three named tiers " +
                               "already exist — values re-asserted.");
            }

            // Configure each tier's values through the runtime QualitySettings
            // API (these setters write into the currently-selected level).
            int remember = QualitySettings.GetQualityLevel();
            for (int i = 0; i < OrderedTiers.Length && i < QualitySettings.names.Length; i++)
            {
                ConfigureQualityTier(i, OrderedTiers[i], pipeline);
                if (structureChanges > 0)
                    log.AppendLine($"[MobileSettings] P0-3   slot {i} -> '{OrderedTiers[i].Name}' " +
                                   $"(MSAA {Describe(OrderedTiers[i].MsaaSamples)}, " +
                                   $"renderScale {OrderedTiers[i].RenderScale}, " +
                                   $"target {OrderedTiers[i].TargetFps} FPS).");
            }
            QualitySettings.SetQualityLevel(
                Mathf.Clamp(remember, 0, QualitySettings.names.Length - 1), false);

            SetAndroidDefaultTier(log, SeekerHigh.Name);
            return structureChanges > 0 ? 1 : 0;
        }

        /// <summary>
        /// Resizes the QualitySettings level array to exactly the three named
        /// tiers and names each slot, through the QualitySettings
        /// SerializedObject. Returns 1 if the array structure changed.
        /// </summary>
        private static int RebuildQualityLevelArray(StringBuilder log, string[] existing)
        {
            log.AppendLine($"[MobileSettings] P0-3 quality tiers: rebuilding " +
                            $"[{string.Join(", ", existing)}] -> " +
                            $"[{SeekerLow.Name}, {SeekerHigh.Name}, {Desktop.Name}].");

            var qsObjects = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset");
            if (qsObjects == null || qsObjects.Length == 0)
            {
                log.AppendLine("[MobileSettings] P0-3 WARNING: QualitySettings asset not loadable — " +
                               "quality tiers NOT rebuilt. Create the three tiers manually.");
                return 0;
            }

            var so = new SerializedObject(qsObjects[0]);
            var levels = so.FindProperty("m_QualitySettings");
            if (levels == null || !levels.isArray)
            {
                log.AppendLine("[MobileSettings] P0-3 WARNING: m_QualitySettings array not found — " +
                               "quality tiers NOT rebuilt. Create the three tiers manually.");
                return 0;
            }

            // Resize the array to exactly three levels. arraySize on a
            // SerializedProperty grows by duplicating the last element and
            // shrinks by truncation — both fine here; we overwrite every name.
            levels.arraySize = OrderedTiers.Length;
            for (int i = 0; i < OrderedTiers.Length; i++)
            {
                var element = levels.GetArrayElementAtIndex(i);
                var nameProp = element.FindPropertyRelative("name");
                if (nameProp != null) nameProp.stringValue = OrderedTiers[i].Name;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return 1;
        }

        /// <summary>Writes one tier's values into the QualitySettings slot at <paramref name="index"/>.</summary>
        private static void ConfigureQualityTier(int index, Tier tier, UniversalRenderPipelineAsset pipeline)
        {
            QualitySettings.SetQualityLevel(index, false);

            // QualitySettings.renderPipeline shadows the global URP asset for
            // this tier. The project ships a single shared DeNelle-URP asset;
            // §1.6 notes each tier ideally points at its own URP variant. We
            // assign the shared asset here (UrpActivator already did this) and
            // tune it once in ApplyUrpMobileTuning — per-tier URP variants are a
            // documented follow-up in port-notes/mobile-settings.md.
            QualitySettings.renderPipeline = pipeline;

            QualitySettings.shadows = tier.ShadowKind;
            QualitySettings.shadowResolution = tier.ShadowResEnum;
            QualitySettings.shadowDistance = tier.ShadowDistance;
            QualitySettings.softParticles = false;                 // mobile: off
            QualitySettings.anisotropicFiltering = tier.Anisotropic;
            QualitySettings.pixelLightCount = tier.PixelLightCount;
            QualitySettings.globalTextureMipmapLimit = tier.TextureLimit;
            QualitySettings.antiAliasing = tier.MsaaSamples == 1 ? 0 : tier.MsaaSamples;
            QualitySettings.realtimeReflectionProbes = tier.Name == Desktop.Name;
            QualitySettings.billboardsFaceCameraPosition = true;
            QualitySettings.skinWeights = SkinWeights.TwoBones;

            // vSync OFF on every tier — the runtime SeekerBootstrap makes
            // Application.targetFrameRate authoritative (audit §1.5).
            QualitySettings.vSyncCount = 0;
        }

        /// <summary>Points the Android platform's default quality level at the named tier.</summary>
        private static void SetAndroidDefaultTier(StringBuilder log, string tierName)
        {
            int idx = System.Array.IndexOf(QualitySettings.names, tierName);
            if (idx < 0)
            {
                log.AppendLine($"[MobileSettings] P0-3 WARNING: tier '{tierName}' not found — " +
                               "Android default NOT set.");
                return;
            }

            // QualitySettings.SetQualityLevel only sets the CURRENT level; the
            // per-platform default is stored separately in
            // m_PerPlatformDefaultQuality. Unity exposes no public setter for
            // it, so we drive it through the QualitySettings SerializedObject —
            // the same serialization API the Quality inspector itself uses
            // (this is NOT hand-editing the YAML; Unity re-serializes correctly).
            SetPlatformDefaultViaSerializedObject(log, "Android", idx, tierName);
        }

        /// <summary>
        /// Sets a platform's default quality index via the QualitySettings
        /// SerializedObject — the same API path the Quality inspector uses. This
        /// is NOT hand-editing the YAML file; it is the supported editor API and
        /// Unity re-serializes the asset correctly.
        /// </summary>
        private static void SetPlatformDefaultViaSerializedObject(
            StringBuilder log, string platform, int index, string tierName)
        {
            var qsObjects = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset");
            if (qsObjects == null || qsObjects.Length == 0)
            {
                log.AppendLine("[MobileSettings] P0-3 WARNING: QualitySettings asset not loadable — " +
                               $"set the {platform} default tier to '{tierName}' manually " +
                               "(Project Settings > Quality).");
                return;
            }

            var so = new SerializedObject(qsObjects[0]);
            var perPlatform = so.FindProperty("m_PerPlatformDefaultQuality");
            if (perPlatform == null || !perPlatform.isArray)
            {
                log.AppendLine("[MobileSettings] P0-3 WARNING: m_PerPlatformDefaultQuality not found — " +
                               $"set the {platform} default tier to '{tierName}' manually.");
                return;
            }

            bool found = false;
            for (int i = 0; i < perPlatform.arraySize; i++)
            {
                var entry = perPlatform.GetArrayElementAtIndex(i);
                var key = entry.FindPropertyRelative("first");
                var val = entry.FindPropertyRelative("second");
                if (key != null && key.stringValue == platform && val != null)
                {
                    if (val.intValue != index)
                        val.intValue = index;
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                perPlatform.arraySize++;
                var entry = perPlatform.GetArrayElementAtIndex(perPlatform.arraySize - 1);
                var key = entry.FindPropertyRelative("first");
                var val = entry.FindPropertyRelative("second");
                if (key != null) key.stringValue = platform;
                if (val != null) val.intValue = index;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            log.AppendLine($"[MobileSettings] P0-3 {platform} default quality tier -> " +
                           $"'{tierName}' (index {index}).");
        }

        // =====================================================================
        //  P1 §1.6 — URP-asset mobile tuning
        // =====================================================================

        private static int ApplyUrpMobileTuning(StringBuilder log)
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                log.AppendLine($"[MobileSettings] §1.6 ERROR: URP asset not found at '{PipelinePath}' " +
                               "— run Defenders/Setup/Activate URP first. URP tuning skipped.");
                return 0;
            }

            // The audit's mobile-correct values target the SHARED DeNelle-URP
            // asset, tuned to the Seeker_High profile (the Android default tier):
            //   HDR off, MSAA 2x, render scale 1.0, main-light shadow res 1024,
            //   shadow distance 30 m, intermediate-texture mode = Auto.
            // Per-tier URP variants (Seeker_Low at 0.85 / MSAA off, Desktop at
            // MSAA 4x / HDR on) are a documented follow-up — see
            // port-notes/mobile-settings.md.
            var so = new SerializedObject(pipeline);
            int changed = 0;

            changed += SetBoolProp(so, "m_SupportsHDR", false, "HDR", log);                // §1.6 P1: HDR off
            changed += SetIntProp(so, "m_MSAA", SeekerHigh.MsaaSamples,                     // §1.6 P2: 2x
                                  "MSAA sample count", log);
            changed += SetFloatProp(so, "m_RenderScale", SeekerHigh.RenderScale,
                                    "render scale", log);
            changed += SetIntProp(so, "m_MainLightShadowmapResolution",
                                  SeekerHigh.ShadowmapResolution,
                                  "main-light shadowmap resolution", log);                  // §1.6 P1: 2048 -> 1024
            changed += SetFloatProp(so, "m_ShadowDistance", SeekerHigh.ShadowDistance,
                                    "shadow distance", log);                                // §1.6 P1: 50 -> 30
            changed += SetBoolProp(so, "m_SoftShadowsSupported", SeekerHigh.SoftShadows,
                                   "soft shadows", log);                                    // §1.6 P2: on for Seeker_High
            // Soft-shadow quality 2 (High) -> 0 (Low) for mobile (§1.6 P2).
            changed += SetIntProp(so, "m_SoftShadowQuality", 0, "soft-shadow quality", log);
            // Intermediate texture: 1 (Always) -> 0 (Auto) — §1.6 P1 / Risk 7.
            changed += SetIntProp(so, "m_IntermediateTextureMode", 0,
                                  "intermediate-texture mode (Always -> Auto)", log);

            if (changed > 0)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(pipeline);
                log.AppendLine($"[MobileSettings] §1.6 URP asset tuned for mobile — " +
                               $"{changed} field(s) changed (tuned to the Seeker_High profile).");
                return 1;
            }

            log.AppendLine("[MobileSettings] §1.6 URP asset: already mobile-tuned — no change.");
            return 0;
        }

        // ── SerializedProperty helpers (idempotent, log-on-change) ───────────
        private static int SetBoolProp(SerializedObject so, string path, bool value,
                                        string label, StringBuilder log)
        {
            var p = so.FindProperty(path);
            if (p == null)
            {
                log.AppendLine($"[MobileSettings] §1.6 WARNING: URP property '{path}' not found — " +
                               $"'{label}' not set.");
                return 0;
            }
            bool current = p.boolValue;
            if (current == value) return 0;
            log.AppendLine($"[MobileSettings] §1.6   {label}: {current} -> {value}.");
            p.boolValue = value;
            return 1;
        }

        private static int SetIntProp(SerializedObject so, string path, int value,
                                       string label, StringBuilder log)
        {
            var p = so.FindProperty(path);
            if (p == null)
            {
                log.AppendLine($"[MobileSettings] §1.6 WARNING: URP property '{path}' not found — " +
                               $"'{label}' not set.");
                return 0;
            }
            int current = p.intValue;
            if (current == value) return 0;
            log.AppendLine($"[MobileSettings] §1.6   {label}: {current} -> {value}.");
            p.intValue = value;
            return 1;
        }

        private static int SetFloatProp(SerializedObject so, string path, float value,
                                         string label, StringBuilder log)
        {
            var p = so.FindProperty(path);
            if (p == null)
            {
                log.AppendLine($"[MobileSettings] §1.6 WARNING: URP property '{path}' not found — " +
                               $"'{label}' not set.");
                return 0;
            }
            float current = p.floatValue;
            if (Mathf.Approximately(current, value)) return 0;
            log.AppendLine($"[MobileSettings] §1.6   {label}: {current} -> {value}.");
            p.floatValue = value;
            return 1;
        }

        private static string Describe(int msaa) => msaa <= 1 ? "off" : $"{msaa}x";
    }
}
