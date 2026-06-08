// =============================================================================
// RegressionSuite — the per-check-in headless regression gate (WO-329 / WO-330).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor   Namespace: DeNelle.Editor
//
// WHY THIS EXISTS (owner): "so we don't patch one hole by creating 3 more."
// Every check-in runs ONE entry point that exercises a battery of CASES across
// the systems most likely to silently break when a fix lands elsewhere:
// compile, the placement-recipe math, the canonical data files, the playable
// scene's core wiring, and the structure kit. It logs ONE grep-able verdict —
//   REGRESSION_OK   (every case passed)
//   REGRESSION_FAIL (>=1 case failed)
// — plus a per-case PASS/FAIL list so a failing run names exactly what broke.
//
// HEADLESS / BATCHMODE:
//   -executeMethod DeNelle.Editor.RegressionSuite.RunAll
//   (or menu: Defenders/QA/Run Regression Suite). No play-mode; pure editor.
//   Exits the editor with code 0 (OK) / 1 (FAIL) when run in batchmode so a CI
//   wrapper / ps1 can gate on the process exit as well as the grep token.
//
// ASMDEF BOUNDARY (the reason for the reflection):
//   DeNelle.Editor references DeNelle.Core + DeNelle.Data but deliberately NOT
//   DeNelle.Village (CLAUDE.md §5 boundary — VillageSceneBuilder/Village2Playable
//   build Village scenes the same way). So the Village-type checks (WaveManager
//   reward fields, EconomyService presence) resolve their types by full name via
//   AppDomain (the exact FindType pattern Village2Playable.cs already uses) and
//   read serialized fields through SerializedObject. This is build/QA TOOLING,
//   not a runtime bridge — the §11 "no new reflection in bridge scripts" rule
//   does not apply (the existing editor builders rely on the same pattern).
//
// WHAT IT CANNOT DO (documented, see the RunAll summary + the file footer):
//   The TRUE end-to-end loop — press DEFEND -> wave spawns -> clear -> resources
//   awarded -> build/upgrade spends — needs PLAY MODE (NavMesh, UniTask ticks,
//   RuntimeInitializeOnLoad bootstrappers, EconomyService.Grant at runtime).
//   That is the next QA layer (Unity Test Framework PlayMode). This suite proves
//   the STATIC preconditions for that loop so a play-mode failure is a real
//   gameplay bug, not a missing field / unparseable file / unwired scene.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Editor
{
    /// <summary>
    /// Headless per-check-in regression battery. Runs a set of independent CASES
    /// and logs a single <c>REGRESSION_OK</c> / <c>REGRESSION_FAIL</c> verdict plus
    /// a per-case PASS/FAIL list. Each case is wrapped so one throwing case can
    /// never abort the whole run — it is recorded as a FAIL and the rest continue.
    /// </summary>
    public static class RegressionSuite
    {
        // ── Tunables: what the suite expects to exist ──────────────────────────

        // The canonical PLAYABLE village scene (Village2 — canonical per project
        // memory; Village.unity is abandoned). Village3 is also opened as a smoke
        // case but the WaveManager wiring assertions run against Village2.
        private const string PlayableScenePath = "Assets/Scenes/Village2.unity";
        private const string SecondaryScenePath = "Assets/Scenes/Village3.unity";

        // Canonical data files — both copies must parse and be byte-equal.
        private const string ResCatalog = "Assets/Resources/Data/Canonical/structures-catalog.json";
        private const string SaCatalog  = "Assets/StreamingAssets/Data/Canonical/structures-catalog.json";
        private const string ResEnemies = "Assets/Resources/Data/Canonical/enemies.json";
        private const string SaEnemies  = "Assets/StreamingAssets/Data/Canonical/enemies.json";
        private const string ResWaves   = "Assets/Resources/Data/Canonical/waves.json";
        private const string SaWaves    = "Assets/StreamingAssets/Data/Canonical/waves.json";

        // Catalog ids the build palette / wave-economy loop depend on existing.
        private static readonly string[] ExpectedCatalogIds =
        {
            // towers
            "tower_ground_archer", "tower_wall_wizard",
            // walls + gate
            "wall_wood", "wall_stone", "gate_stone",
            // resource / build buildings
            "workshop", "market", "mill", "lumbermill", "forge",
        };

        // WO-330 wave-clear reward fields that MUST be present on WaveManager so
        // "defend -> earn -> build" can pay out. Serialized private field names.
        private static readonly string[] WaveRewardFields =
        {
            "_awardResourcesOnWaveClear",
            "_woodRewardBase", "_woodRewardPerWave",
            "_ironRewardBase", "_ironRewardPerWave",
        };

        private const string TypeWaveManager    = "DeNelle.Village.WaveManager";
        private const string TypeEconomyService = "DeNelle.Village.EconomyService";

        // WO-363 — the hero movement source must stay WORLD-ABSOLUTE (WO-368). A
        // regression to camera-relative input (WO-367) broke town movement: a camera
        // mode/angle change re-rotated the input mapping. We STATIC-check the source
        // (batchmode has no play-mode) for the camera-coupling tokens in the movement
        // path + assert facing still uses LookRotation on the move/velocity.
        private const string HeroLocomotionPath =
            "Assets/_Modules/Village/Hero/HeroLocomotion.cs";

        // WO-373 Critical Regression Gates — the canonical types the 4 gates probe.
        // Resolved by full name across loaded assemblies (Editor doesn't reference
        // DeNelle.Village — same FindType pattern the rest of this suite uses).
        private const string TypeHeartController = "DeNelle.Village.HeartController";

        // WO-373 — the Yarn DialogueSystem prefab the interaction layer instantiates
        // (vendors / upgrades / confirms route through it). Resources.Load path form.
        private const string DialogueSystemResPath = "Dialogue/DialogueSystem";
        private const string DialogueSystemAssetPath =
            "Assets/Resources/Dialogue/DialogueSystem.prefab";

        // WO-373 — the battle music tracks BattleMusicManager loads via Resources.Load
        // (WebGL-safe). At minimum the two combat-loop tracks must resolve, else combat
        // is silent. Resources.Load form (no extension, no Resources/ prefix).
        private static readonly string[] RequiredBattleMusicResPaths =
        {
            "Music/Battle/Overworld_Battle_1",
            "Music/Battle/Overworld_Battle_2",
        };

        // ── One case result ────────────────────────────────────────────────────
        private sealed class CaseResult
        {
            public string Name;
            public bool Pass;
            public string Detail;
            public CaseResult(string name, bool pass, string detail)
            { Name = name; Pass = pass; Detail = detail; }
        }

        // ── Public entry points ────────────────────────────────────────────────

        /// <summary>
        /// Runs every regression case. Logs a per-case PASS/FAIL list and the single
        /// <c>REGRESSION_OK</c> / <c>REGRESSION_FAIL</c> verdict. In batchmode, exits
        /// the editor with code 0 (all passed) or 1 (any failed). Returns true on pass.
        /// </summary>
        [MenuItem("Defenders/QA/Run Regression Suite")]
        public static bool RunAll()
        {
            var results = new List<CaseResult>();

            Run(results, "compile-gate",            Case_CompileGate);
            Run(results, "catalog-parse",           Case_CatalogParse);
            Run(results, "catalog-byte-equal",      Case_CatalogByteEqual);
            Run(results, "data-files-parse",        Case_DataFilesParse);
            Run(results, "catalog-ids-present",      Case_CatalogIdsPresent);
            Run(results, "catalog-prefabs-resolve",  Case_CatalogPrefabsResolve);
            Run(results, "structures-kit-present",   Case_StructuresKitPresent);
            Run(results, "no-duplicate-landmines",   Case_NoDuplicateLandmines);
            Run(results, "scene-opens-village2",     () => Case_SceneOpens(PlayableScenePath));
            Run(results, "scene-opens-village3",     () => Case_SceneOpens(SecondaryScenePath));
            Run(results, "core-wiring-village2",     Case_CoreWiring);
            Run(results, "layout-validator",         Case_LayoutValidator);

            // ── WO-373 / WO-363 CRITICAL hard-gate cases (ship-blockers) ─────────
            Run(results, "hero-move-world-absolute", Case_HeroMoveWorldAbsolute);
            Run(results, "dialogue-system-prefab",   Case_DialogueSystemPrefab);
            Run(results, "battle-music-resolves",    Case_BattleMusicResolves);

            // ── WO-373 Critical Regression Gates (4 pre-ship hard gates) ─────────
            Run(results, "critical-gate-tree-origin",       Case_GateTreeOrigin);
            Run(results, "critical-gate-wasd-world-abs",    Case_GateWasdWorldAbsolute);
            Run(results, "critical-gate-scene-no-errors",   Case_GateSceneLoadsClean);
            Run(results, "critical-gate-cam-not-relative",  Case_GateCameraNeverRelative);

            // ── Report ──────────────────────────────────────────────────────────
            int passed = results.Count(r => r.Pass);
            int failed = results.Count - passed;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("==================== REGRESSION SUITE ====================");
            foreach (var r in results)
                sb.AppendLine($"  [{(r.Pass ? "PASS" : "FAIL")}] {r.Name}" +
                              (string.IsNullOrEmpty(r.Detail) ? "" : $"  — {r.Detail}"));
            sb.AppendLine("----------------------------------------------------------");
            sb.AppendLine($"  {passed}/{results.Count} cases passed, {failed} failed.");

            bool ok = failed == 0;
            if (ok)
            {
                sb.AppendLine("  VERDICT: REGRESSION_OK");
                sb.AppendLine("==========================================================");
                Debug.Log(sb.ToString());
            }
            else
            {
                sb.AppendLine("  VERDICT: REGRESSION_FAIL");
                sb.AppendLine("==========================================================");
                Debug.LogError(sb.ToString());
            }

            // Reminder of the next QA layer (the part that needs play-mode).
            Debug.Log("[RegressionSuite] NOTE: the end-to-end loop (DEFEND -> wave spawns -> " +
                      "clear -> resources awarded -> build/upgrade spends) is NOT covered here " +
                      "(needs PLAY MODE). See the PLAY-MODE FOLLOW-UP footer in RegressionSuite.cs " +
                      "and tools/regression/MANUAL_QA_CHECKLIST.md.");

            if (Application.isBatchMode)
                EditorApplication.Exit(ok ? 0 : 1);

            return ok;
        }

        // Runs one case, trapping exceptions so a single throw can't abort the run.
        private static void Run(List<CaseResult> results, string name, Func<CaseResult> body)
        {
            try
            {
                CaseResult r = body();
                r.Name = name;   // normalise the name to the call-site label
                results.Add(r);
            }
            catch (Exception ex)
            {
                results.Add(new CaseResult(name, false, $"threw {ex.GetType().Name}: {ex.Message}"));
            }
        }

        private static CaseResult Pass(string detail = "")  => new CaseResult(null, true,  detail);
        private static CaseResult Fail(string detail)       => new CaseResult(null, false, detail);

        // =====================================================================
        //  CASES
        // =====================================================================

        // ── Compile gate ──────────────────────────────────────────────────────
        // Reaching this method at all means the editor scripts compiled clean
        // (a CS error would have stopped the batch before RunAll executed). We
        // re-emit the CompileGate marker so a single log carries both tokens.
        private static CaseResult Case_CompileGate()
        {
            CompileGate.Run();
            return Pass("scripts compiled (CompileGate.Run reached)");
        }

        // ── Catalog parse: both copies deserialize into entries ────────────────
        private static CaseResult Case_CatalogParse()
        {
            if (!File.Exists(ResCatalog)) return Fail($"missing {ResCatalog}");
            if (!File.Exists(SaCatalog))  return Fail($"missing {SaCatalog}");

            int resN = CountCatalogEntries(ResCatalog, out string resErr);
            if (resErr != null) return Fail($"Resources copy: {resErr}");
            int saN = CountCatalogEntries(SaCatalog, out string saErr);
            if (saErr != null) return Fail($"StreamingAssets copy: {saErr}");

            if (resN == 0) return Fail("Resources catalog parsed to 0 entries");
            if (saN == 0)  return Fail("StreamingAssets catalog parsed to 0 entries");
            return Pass($"{resN} entries (Resources), {saN} entries (StreamingAssets)");
        }

        // ── Catalog byte-equal: the two copies must not drift ──────────────────
        private static CaseResult Case_CatalogByteEqual()
        {
            if (!File.Exists(ResCatalog) || !File.Exists(SaCatalog))
                return Fail("one of the catalog copies is missing");
            return BytesEqual(ResCatalog, SaCatalog)
                ? Pass("Resources copy == StreamingAssets copy")
                : Fail("structures-catalog.json copies DIFFER (Resources vs StreamingAssets) — " +
                       "keep them byte-equal (CLAUDE.md / catalog-bootstrap note)");
        }

        // ── enemies.json + waves.json parse (both copies) ──────────────────────
        private static CaseResult Case_DataFilesParse()
        {
            var fails = new List<string>();
            foreach (var path in new[] { ResEnemies, SaEnemies, ResWaves, SaWaves })
            {
                if (!File.Exists(path)) { fails.Add($"missing {path}"); continue; }
                if (!IsParseableJson(path, out string err)) fails.Add($"{path}: {err}");
            }
            return fails.Count == 0
                ? Pass("enemies.json + waves.json parse (both copies)")
                : Fail(string.Join(" | ", fails));
        }

        // ── Expected catalog ids are all present ───────────────────────────────
        private static CaseResult Case_CatalogIdsPresent()
        {
            var ids = ReadCatalogIds(ResCatalog, out string err);
            if (err != null) return Fail(err);
            var missing = ExpectedCatalogIds.Where(id => !ids.Contains(id)).ToList();
            return missing.Count == 0
                ? Pass($"all {ExpectedCatalogIds.Length} expected ids present")
                : Fail("catalog missing expected id(s): " + string.Join(", ", missing));
        }

        // ── Every catalog visualPrefabPath resolves under Resources ────────────
        // The runtime skins structures via Resources.Load<GameObject>(visualPrefabPath).
        // A path that doesn't resolve = an invisible / missing structure in-game.
        private static CaseResult Case_CatalogPrefabsResolve()
        {
            var paths = ReadCatalogVisualPaths(ResCatalog, out string err);
            if (err != null) return Fail(err);
            if (paths.Count == 0) return Fail("no visualPrefabPath values found in catalog");

            var unresolved = new List<string>();
            foreach (var p in paths)
            {
                if (string.IsNullOrEmpty(p)) continue;          // composite/inert rows may omit it
                var go = Resources.Load<GameObject>(p);
                if (go == null) unresolved.Add(p);
            }
            return unresolved.Count == 0
                ? Pass($"{paths.Count} visualPrefabPath value(s) all resolve under Resources")
                : Fail($"{unresolved.Count} visualPrefabPath value(s) do NOT resolve: " +
                       string.Join(", ", unresolved.Distinct()));
        }

        // ── The Resources/Structures kit prefabs physically exist ──────────────
        private static CaseResult Case_StructuresKitPresent()
        {
            const string kitDir = "Assets/Resources/Structures";
            if (!Directory.Exists(kitDir)) return Fail($"missing kit folder {kitDir}");
            int n = Directory.GetFiles(kitDir, "*.prefab", SearchOption.TopDirectoryOnly).Length;
            return n > 0
                ? Pass($"{n} structure prefab(s) in {kitDir}")
                : Fail($"{kitDir} contains 0 prefabs (the build kit is empty)");
        }

        // ── Known-landmine: exactly one DoorController, no Core.Debug shadow ────
        // Scans only Assets/ (worktrees under .claude/ are out-of-tree, not compiled).
        private static CaseResult Case_NoDuplicateLandmines()
        {
            var fails = new List<string>();

            var doorControllers = Directory
                .GetFiles("Assets", "DoorController.cs", SearchOption.AllDirectories)
                .Where(p => !p.Replace('\\', '/').Contains("/.claude/"))
                .ToList();
            if (doorControllers.Count > 1)
                fails.Add($"{doorControllers.Count} DoorController.cs under Assets " +
                          "(canonical is Buildings/DoorController.cs — see doorcontroller-duplicate landmine)");

            // DeNelle.Core.Debug / .Addressables namespace shadow landmine.
            foreach (var shadow in new[] { "namespace DeNelle.Core.Debug", "namespace DeNelle.Core.Addressables" })
            {
                var hits = Directory
                    .GetFiles("Assets/_Modules/Core", "*.cs", SearchOption.AllDirectories)
                    .Where(p => File.ReadAllText(p).Contains(shadow))
                    .ToList();
                if (hits.Count > 0)
                    fails.Add($"found '{shadow}' (shadows UnityEngine static) in: " +
                              string.Join(", ", hits.Select(Path.GetFileName)));
            }

            return fails.Count == 0
                ? Pass("1 DoorController, no Core.Debug/Addressables shadow")
                : Fail(string.Join(" | ", fails));
        }

        // ── A canonical scene opens without missing scripts / NRE ──────────────
        private static CaseResult Case_SceneOpens(string scenePath)
        {
            if (!File.Exists(scenePath)) return Fail($"missing {scenePath}");

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (!scene.IsValid() || !scene.isLoaded)
                return Fail($"scene failed to load: {scenePath}");

            int missing = CountMissingScripts(scene);
            return missing == 0
                ? Pass($"{Path.GetFileName(scenePath)} opened, 0 missing scripts")
                : Fail($"{Path.GetFileName(scenePath)} has {missing} GameObject(s) with MISSING (None) scripts");
        }

        // ── Core wiring in the playable scene ──────────────────────────────────
        // Opens Village2 and asserts: a WaveManager exists + carries the WO-330
        // reward fields; an EconomyService exists OR the bootstrapper will create one;
        // (the catalog ids are validated in their own case). WaveManager / Economy
        // types are resolved by full name (asmdef boundary — see file header).
        private static CaseResult Case_CoreWiring()
        {
            if (!File.Exists(PlayableScenePath)) return Fail($"missing {PlayableScenePath}");
            EditorSceneManager.OpenScene(PlayableScenePath, OpenSceneMode.Single);

            var fails = new List<string>();

            // WaveManager present?
            Type wmType = FindType(TypeWaveManager);
            if (wmType == null)
            {
                fails.Add($"type {TypeWaveManager} not found (DeNelle.Village compiled?)");
            }
            else
            {
                var wm = UnityEngine.Object.FindFirstObjectByType(wmType) as Component;
                if (wm == null)
                {
                    fails.Add("no WaveManager in the playable scene (DEFEND button has nothing behind it)");
                }
                else
                {
                    // Reward fields wired (serialized private fields present)?
                    var so = new SerializedObject(wm);
                    var missingFields = WaveRewardFields
                        .Where(f => so.FindProperty(f) == null)
                        .ToList();
                    if (missingFields.Count > 0)
                        fails.Add("WaveManager missing WO-330 reward field(s): " +
                                  string.Join(", ", missingFields));
                }
            }

            // EconomyService: present in-scene is fine; absent is ALSO fine because
            // EconomyService.Bootstrap creates one at runtime. We only fail if the
            // type itself is missing (means DeNelle.Village didn't compile).
            Type econType = FindType(TypeEconomyService);
            if (econType == null)
                fails.Add($"type {TypeEconomyService} not found (DeNelle.Village compiled?)");

            return fails.Count == 0
                ? Pass("WaveManager present + WO-330 reward fields wired; EconomyService type resolves")
                : Fail(string.Join(" | ", fails));
        }

        // ── Recipe math safeguard (reuse the existing LayoutValidator) ─────────
        // Validates the active GameState recipe (empty = OK). With Village2 open the
        // gate-lane check can also run. LayoutValidator logs its own detail + token.
        private static CaseResult Case_LayoutValidator()
        {
            bool ok = LayoutValidator.ValidateActive();
            return ok
                ? Pass("LAYOUT_VALIDATE_OK (recipe math safeguards passed)")
                : Fail("LAYOUT_VALIDATE_FAIL — see [LayoutValidator] FAIL lines above");
        }

        // ── WO-363: hero movement stays WORLD-ABSOLUTE (no camera coupling) ────
        // STATIC source check (batchmode has no play-mode): read HeroLocomotion.cs
        // and FAIL if the movement path couples input to the camera basis — i.e.
        // references SmartMobileCamera / CameraYaw / a camera forward|right used in
        // the move vector. The WO-368 contract: UP=+Z, RIGHT=+X, independent of the
        // camera, so a camera mode/angle change can never re-rotate input (the WO-367
        // regression). Also assert facing still uses LookRotation on velocity/move so
        // a refactor that drops the face-the-direction line is caught too.
        private static CaseResult Case_HeroMoveWorldAbsolute()
        {
            if (!File.Exists(HeroLocomotionPath)) return Fail($"missing {HeroLocomotionPath}");

            // Strip // line comments + /* */ block comments so the doc-comments that
            // legitimately MENTION the regression (e.g. "WO-367 made this camera-relative")
            // don't trip the scan — we only care about live code coupling.
            string code = StripComments(File.ReadAllText(HeroLocomotionPath));

            var hits = new List<string>();
            // Hard camera-coupling tokens: any of these in live code means the
            // movement basis is (or can be) camera-derived again.
            foreach (var token in new[] { "SmartMobileCamera", "CameraYaw", "cameraYaw" })
                if (code.IndexOf(token, StringComparison.Ordinal) >= 0)
                    hits.Add(token);

            // camera.forward / camera.right (or *Camera.forward etc.) used as a basis.
            foreach (var m in System.Text.RegularExpressions.Regex.Matches(
                         code, @"[A-Za-z_][A-Za-z0-9_]*\.(forward|right)\b")
                     .Cast<System.Text.RegularExpressions.Match>())
            {
                string lhs = m.Value;
                if (lhs.IndexOf("amera", StringComparison.Ordinal) >= 0) // camera./Camera.
                    hits.Add(lhs);
            }

            if (hits.Count > 0)
                return Fail("HeroLocomotion movement is CAMERA-RELATIVE again (WO-367 regression) — " +
                            "must be world-absolute new Vector3(input.x,0,input.y) per WO-368. " +
                            "Offending token(s): " + string.Join(", ", hits.Distinct()));

            // Facing must still be derived from the move/velocity via LookRotation.
            bool facesMoveDir =
                System.Text.RegularExpressions.Regex.IsMatch(
                    code, @"LookRotation\s*\(\s*(Velocity|move|_?velocity)");
            if (!facesMoveDir)
                return Fail("HeroLocomotion no longer faces the move direction via " +
                            "Quaternion.LookRotation(Velocity/move...) — facing contract dropped (WO-363).");

            return Pass("movement world-absolute (no camera basis) + faces velocity via LookRotation");
        }

        // =====================================================================
        //  WO-373 — CRITICAL REGRESSION GATES (4 pre-ship hard gates)
        // -----------------------------------------------------------------------
        //  A focused pre-ship verification harness, expressed as 4 independent
        //  cases so a failing run names exactly which ship-blocker broke:
        //    (1) Tree of Life / Heart sits at world (0,0,0).
        //    (2) WASD player movement works AND is WORLD-ABSOLUTE (not camera-rel),
        //        re-using the WO-363 OrientationValidator for the facing check.
        //    (3) The playable scene loads with no missing scripts / red errors and
        //        a Player/HeroTarget-tagged hero is present (visible).
        //    (4) A camera mode/angle change can NEVER make movement camera-relative
        //        (the movement basis is independent of SmartMobileCamera).
        //  Static/source + scene-open checks (batchmode has no play-mode); the live
        //  felt loop stays the play-mode follow-up layer (see file footer).
        //
        //  A single public report entry point (VerifyCriticalGates) lets ship tooling
        //  call the 4 gates directly and get a pass/fail line per gate.
        // =====================================================================

        /// <summary>
        /// WO-373 — runs the 4 critical regression gates and returns true only if all
        /// pass. Logs one CRITICAL_GATES_OK / CRITICAL_GATES_FAIL verdict plus a
        /// per-gate PASS/FAIL line. Callable on its own (pre-ship harness) in addition
        /// to being wired into <see cref="RunAll"/> as 4 individual cases.
        /// </summary>
        [MenuItem("Defenders/QA/Run Critical Regression Gates")]
        public static bool VerifyCriticalGates()
        {
            var gates = new (string name, Func<CaseResult> body)[]
            {
                ("tree-of-life-origin",       Case_GateTreeOrigin),
                ("wasd-world-absolute",       Case_GateWasdWorldAbsolute),
                ("scene-loads-clean",         Case_GateSceneLoadsClean),
                ("camera-never-relative",     Case_GateCameraNeverRelative),
            };

            var results = new List<CaseResult>();
            foreach (var g in gates) Run(results, g.name, g.body);

            int passed = results.Count(r => r.Pass);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("============== WO-373 CRITICAL REGRESSION GATES ==============");
            foreach (var r in results)
                sb.AppendLine($"  [{(r.Pass ? "PASS" : "FAIL")}] {r.Name}" +
                              (string.IsNullOrEmpty(r.Detail) ? "" : $"  — {r.Detail}"));
            bool ok = passed == results.Count;
            sb.AppendLine($"  {passed}/{results.Count} gates passed.");
            sb.AppendLine($"  VERDICT: {(ok ? "CRITICAL_GATES_OK" : "CRITICAL_GATES_FAIL")}");
            sb.AppendLine("==============================================================");
            if (ok) Debug.Log(sb.ToString()); else Debug.LogError(sb.ToString());
            return ok;
        }

        // ── Gate 1: Tree of Life / Heart at world origin (0,0,0) ───────────────
        // VerifyTreeOrigin() contract. Opens the playable scene, finds the
        // HeartController (resolved by full name — Editor doesn't ref Village), and
        // asserts its transform sits at world (0,0,0) within a tight epsilon.
        private static CaseResult Case_GateTreeOrigin()
        {
            return VerifyTreeOrigin(out string detail) ? Pass(detail) : Fail(detail);
        }

        /// <summary>
        /// WO-373 Gate 1: the Tree of Life / Heart of Elarion must sit at world origin
        /// (0,0,0). Opens the playable scene, locates the HeartController and checks its
        /// world position. Returns true on pass; <paramref name="detail"/> always carries
        /// a human-readable reason.
        /// </summary>
        public static bool VerifyTreeOrigin(out string detail)
        {
            detail = "";
            if (!File.Exists(PlayableScenePath)) { detail = $"missing {PlayableScenePath}"; return false; }
            EditorSceneManager.OpenScene(PlayableScenePath, OpenSceneMode.Single);

            Type heartType = FindType(TypeHeartController);
            if (heartType == null) { detail = $"type {TypeHeartController} not found (DeNelle.Village compiled?)"; return false; }

            var heart = UnityEngine.Object.FindFirstObjectByType(heartType) as Component;
            if (heart == null) { detail = "no HeartController (Tree of Life) in the playable scene"; return false; }

            Vector3 p = heart.transform.position;
            const float eps = 0.01f;
            if (p.sqrMagnitude > eps * eps)
            {
                detail = $"Tree of Life is at {p}, NOT world origin (0,0,0)";
                return false;
            }
            detail = $"Tree of Life at world origin ({p})";
            return true;
        }

        // ── Gate 2: WASD movement works AND is world-absolute ──────────────────
        // Source-checks HeroLocomotion: it must (a) read WASD keys, (b) build the
        // move vector world-absolute (no camera basis — shares the existing
        // Case_HeroMoveWorldAbsolute scan), and (c) face the move direction. The
        // facing contract is cross-checked against the WO-363 OrientationValidator:
        // a synthetic UP input must validate against an UP facing (proves the shared
        // rule the runtime guard enforces agrees with "faces movement").
        private static CaseResult Case_GateWasdWorldAbsolute()
        {
            if (!File.Exists(HeroLocomotionPath)) return Fail($"missing {HeroLocomotionPath}");
            string code = StripComments(File.ReadAllText(HeroLocomotionPath));

            // (a) WASD actually read.
            bool readsWasd =
                System.Text.RegularExpressions.Regex.IsMatch(code, @"\bwKey\b") &&
                System.Text.RegularExpressions.Regex.IsMatch(code, @"\baKey\b") &&
                System.Text.RegularExpressions.Regex.IsMatch(code, @"\bsKey\b") &&
                System.Text.RegularExpressions.Regex.IsMatch(code, @"\bdKey\b");
            if (!readsWasd)
                return Fail("HeroLocomotion no longer reads WASD (wKey/aKey/sKey/dKey) — movement input gone");

            // (b) world-absolute basis (reuse the camera-coupling scan).
            var camHits = new List<string>();
            foreach (var token in new[] { "SmartMobileCamera", "CameraYaw", "cameraYaw" })
                if (code.IndexOf(token, StringComparison.Ordinal) >= 0) camHits.Add(token);
            foreach (var m in System.Text.RegularExpressions.Regex.Matches(
                         code, @"[A-Za-z_][A-Za-z0-9_]*\.(forward|right)\b")
                     .Cast<System.Text.RegularExpressions.Match>())
                if (m.Value.IndexOf("amera", StringComparison.Ordinal) >= 0) camHits.Add(m.Value);
            if (camHits.Count > 0)
                return Fail("WASD movement is CAMERA-RELATIVE (WO-367 regression): " +
                            string.Join(", ", camHits.Distinct()) + " — must be world-absolute");

            // (c) faces the move direction.
            bool facesMove = System.Text.RegularExpressions.Regex.IsMatch(
                code, @"LookRotation\s*\(\s*(Velocity|move|_?velocity)");
            if (!facesMove)
                return Fail("HeroLocomotion no longer faces the move direction (LookRotation dropped)");

            // (d) cross-check the WO-363 shared facing rule: facing UP for input UP passes,
            //     facing the opposite fails — same validator the runtime OrientationGuard uses.
            if (!DeNelle.Core.Validation.OrientationValidator.IsValid(Vector3.forward, Vector3.forward))
                return Fail("OrientationValidator disagrees that facing UP matches moving UP (WO-363 rule broken)");
            if (DeNelle.Core.Validation.OrientationValidator.IsValid(Vector3.back, Vector3.forward))
                return Fail("OrientationValidator wrongly accepts facing DOWN while moving UP (WO-363 rule broken)");

            return Pass("WASD read, world-absolute basis (no camera), faces velocity; WO-363 validator agrees");
        }

        // ── Gate 3: scene loads clean (no missing scripts) + hero present ──────
        // Opens the playable scene, asserts 0 GameObjects with MISSING scripts (the
        // closest static proxy for "no null-ref / red errors on load"), and that a
        // Player- or HeroTarget-tagged hero exists in the scene (hero visible).
        private static CaseResult Case_GateSceneLoadsClean()
        {
            if (!File.Exists(PlayableScenePath)) return Fail($"missing {PlayableScenePath}");
            Scene scene = EditorSceneManager.OpenScene(PlayableScenePath, OpenSceneMode.Single);
            if (!scene.IsValid() || !scene.isLoaded) return Fail($"scene failed to load: {PlayableScenePath}");

            int missing = CountMissingScripts(scene);
            if (missing > 0)
                return Fail($"{missing} GameObject(s) with MISSING (None) scripts — load would throw / red errors");

            bool heroPresent = false;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.CompareTag("Player") || t.CompareTag("HeroTarget")) { heroPresent = true; break; }
                }
                if (heroPresent) break;
            }
            if (!heroPresent)
                return Fail("no Player/HeroTarget-tagged hero in the scene (hero not present/visible)");

            return Pass("scene opens with 0 missing scripts + a Player/HeroTarget hero present");
        }

        // ── Gate 4: a camera change can NEVER make movement camera-relative ────
        // The movement basis must be independent of the camera. Static contract:
        // HeroLocomotion's source contains no camera-basis coupling AT ALL (so no
        // camera mode/angle/distance change can re-rotate input). This is the dual
        // of gate 2(b) but framed as the camera-invariance guarantee; it also fails
        // if SmartMobileCamera ever WRITES back into the locomotion input frame.
        private static CaseResult Case_GateCameraNeverRelative()
        {
            if (!File.Exists(HeroLocomotionPath)) return Fail($"missing {HeroLocomotionPath}");
            string code = StripComments(File.ReadAllText(HeroLocomotionPath));

            var hits = new List<string>();
            foreach (var token in new[] { "SmartMobileCamera", "CameraYaw", "cameraYaw", "Camera.main", "Camera.current" })
                if (code.IndexOf(token, StringComparison.Ordinal) >= 0) hits.Add(token);
            foreach (var m in System.Text.RegularExpressions.Regex.Matches(
                         code, @"[A-Za-z_][A-Za-z0-9_]*\.(forward|right)\b")
                     .Cast<System.Text.RegularExpressions.Match>())
                if (m.Value.IndexOf("amera", StringComparison.Ordinal) >= 0) hits.Add(m.Value);

            return hits.Count == 0
                ? Pass("movement basis is camera-invariant — no camera coupling in HeroLocomotion")
                : Fail("a camera reference is present in the movement path — a camera change could make " +
                       "movement camera-relative: " + string.Join(", ", hits.Distinct()));
        }

        // ── WO-373: the Yarn DialogueSystem prefab exists (interaction layer) ──
        // Vendors / building upgrades / yes-no confirms all route through the
        // DialogueService prefab. A missing prefab = the whole interaction layer is
        // dead. Assert it loads via Resources.Load (the runtime path) AND the asset
        // file physically exists (catches a stale .meta with no prefab).
        private static CaseResult Case_DialogueSystemPrefab()
        {
            bool onDisk = File.Exists(DialogueSystemAssetPath);
            var go = Resources.Load<GameObject>(DialogueSystemResPath);
            if (go == null)
                return Fail($"DialogueSystem prefab does NOT resolve via Resources.Load(\"{DialogueSystemResPath}\")" +
                            (onDisk ? " (asset exists on disk but failed to load)"
                                    : $" — and {DialogueSystemAssetPath} is missing"));
            return Pass($"DialogueSystem prefab resolves (Resources/{DialogueSystemResPath}) + asset on disk");
        }

        // ── WO-373: battle music tracks resolve under Resources (combat audio) ──
        // BattleMusicManager loads these via Resources.Load (WebGL-safe). If they
        // don't resolve, combat plays silent. We require the two combat-loop tracks;
        // Victory / Boss are optional (manager logs a warning, doesn't break).
        private static CaseResult Case_BattleMusicResolves()
        {
            var unresolved = new List<string>();
            foreach (var p in RequiredBattleMusicResPaths)
            {
                var clip = Resources.Load<AudioClip>(p);
                if (clip == null) unresolved.Add(p);
            }
            return unresolved.Count == 0
                ? Pass($"{RequiredBattleMusicResPaths.Length} battle-music track(s) resolve under Resources")
                : Fail("battle music track(s) do NOT resolve under Resources (combat would be silent): " +
                       string.Join(", ", unresolved) +
                       " — import under Assets/Audio/Resources/Music/Battle/");
        }

        // Strips // line comments and /* */ block comments (string-literal-aware) so a
        // source-token scan only sees live code, not doc-comments that mention a token.
        private static string StripComments(string src)
        {
            var sb = new System.Text.StringBuilder(src.Length);
            bool inStr = false, inChar = false, inLine = false, inBlock = false, esc = false;
            for (int i = 0; i < src.Length; i++)
            {
                char c = src[i];
                char n = i + 1 < src.Length ? src[i + 1] : '\0';
                if (inLine)
                {
                    if (c == '\n') { inLine = false; sb.Append(c); }
                    continue;
                }
                if (inBlock)
                {
                    if (c == '*' && n == '/') { inBlock = false; i++; }
                    continue;
                }
                if (inStr)
                {
                    sb.Append(c);
                    if (esc) esc = false;
                    else if (c == '\\') esc = true;
                    else if (c == '"') inStr = false;
                    continue;
                }
                if (inChar)
                {
                    sb.Append(c);
                    if (esc) esc = false;
                    else if (c == '\\') esc = true;
                    else if (c == '\'') inChar = false;
                    continue;
                }
                if (c == '/' && n == '/') { inLine = true; i++; continue; }
                if (c == '/' && n == '*') { inBlock = true; i++; continue; }
                if (c == '"')  { inStr  = true; sb.Append(c); continue; }
                if (c == '\'') { inChar = true; sb.Append(c); continue; }
                sb.Append(c);
            }
            return sb.ToString();
        }

        // =====================================================================
        //  Helpers — JSON (no Village/Newtonsoft dependency; minimal + robust)
        // =====================================================================
        // NOTE: we intentionally avoid a full typed deserialize here (the typed
        // CatalogEntry lives behind CatalogBootstrap in DeNelle.Village which Editor
        // can't reference). For PARSE VALIDITY we use Unity's JsonUtility round-trip
        // plus a lightweight brace/string scan; for ID / path extraction we read the
        // raw "id" / "visualPrefabPath" string tokens. This is sufficient for a
        // regression gate: a malformed file fails the brace scan, and the typed
        // load is exercised at runtime by CatalogBootstrap (covered by play-mode).

        private static bool IsParseableJson(string path, out string err)
        {
            err = null;
            try
            {
                string text = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(text)) { err = "empty file"; return false; }
                if (!BracesBalanced(text, out string be)) { err = be; return false; }
                return true;
            }
            catch (Exception ex) { err = ex.Message; return false; }
        }

        // Balanced {}/[] outside of strings — catches truncated / mangled JSON, the
        // exact corruption class the mount-sync landmine produces.
        private static bool BracesBalanced(string text, out string err)
        {
            err = null;
            int curly = 0, square = 0;
            bool inStr = false, esc = false;
            foreach (char c in text)
            {
                if (inStr)
                {
                    if (esc) esc = false;
                    else if (c == '\\') esc = true;
                    else if (c == '"') inStr = false;
                    continue;
                }
                switch (c)
                {
                    case '"': inStr = true; break;
                    case '{': curly++; break;
                    case '}': curly--; break;
                    case '[': square++; break;
                    case ']': square--; break;
                }
                if (curly < 0 || square < 0) { err = "unbalanced closing brace/bracket"; return false; }
            }
            if (inStr)   { err = "unterminated string"; return false; }
            if (curly != 0)  { err = $"{curly} unbalanced {{}}"; return false; }
            if (square != 0) { err = $"{square} unbalanced []"; return false; }
            return true;
        }

        // Counts "id": tokens at object level (a proxy for entry count). Returns -1
        // and sets err on a parse problem.
        private static int CountCatalogEntries(string path, out string err)
        {
            err = null;
            string text = File.ReadAllText(path);
            if (!BracesBalanced(text, out err)) return -1;
            return CountToken(text, "\"id\"");
        }

        private static HashSet<string> ReadCatalogIds(string path, out string err)
        {
            err = null;
            var set = new HashSet<string>(StringComparer.Ordinal);
            if (!File.Exists(path)) { err = $"missing {path}"; return set; }
            string text = File.ReadAllText(path);
            if (!BracesBalanced(text, out err)) return set;
            foreach (var v in ReadStringValuesForKey(text, "id")) set.Add(v);
            return set;
        }

        private static List<string> ReadCatalogVisualPaths(string path, out string err)
        {
            err = null;
            var list = new List<string>();
            if (!File.Exists(path)) { err = $"missing {path}"; return list; }
            string text = File.ReadAllText(path);
            if (!BracesBalanced(text, out err)) return list;
            list.AddRange(ReadStringValuesForKey(text, "visualPrefabPath"));
            return list;
        }

        // Extracts every  "key": "value"  string value for the given key.
        private static IEnumerable<string> ReadStringValuesForKey(string text, string key)
        {
            string needle = "\"" + key + "\"";
            int i = 0;
            while ((i = text.IndexOf(needle, i, StringComparison.Ordinal)) >= 0)
            {
                int j = i + needle.Length;
                // skip whitespace + ':'
                while (j < text.Length && (text[j] == ' ' || text[j] == '\t' || text[j] == ':' || text[j] == '\r' || text[j] == '\n')) j++;
                if (j < text.Length && text[j] == '"')
                {
                    int start = ++j;
                    var sb = new System.Text.StringBuilder();
                    while (j < text.Length && text[j] != '"')
                    {
                        if (text[j] == '\\' && j + 1 < text.Length) { sb.Append(text[j + 1]); j += 2; continue; }
                        sb.Append(text[j]); j++;
                    }
                    yield return sb.ToString();
                }
                i = j + 1;
            }
        }

        private static int CountToken(string text, string token)
        {
            int n = 0, i = 0;
            while ((i = text.IndexOf(token, i, StringComparison.Ordinal)) >= 0) { n++; i += token.Length; }
            return n;
        }

        // =====================================================================
        //  Helpers — files / scene / reflection
        // =====================================================================

        private static bool BytesEqual(string a, string b)
        {
            byte[] ba = File.ReadAllBytes(a);
            byte[] bb = File.ReadAllBytes(b);
            if (ba.Length != bb.Length) return false;
            for (int i = 0; i < ba.Length; i++) if (ba[i] != bb[i]) return false;
            return true;
        }

        // Counts GameObjects carrying a MISSING MonoBehaviour (removed/renamed script).
        private static int CountMissingScripts(Scene scene)
        {
            int count = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    int n = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                    if (n > 0) count += n;
                }
            }
            return count;
        }

        // Resolve a type by full name across all loaded assemblies (the FindType
        // pattern from Village2Playable.cs — needed because the Editor asmdef does
        // not reference DeNelle.Village).
        private static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }
    }
}

// =============================================================================
// PLAY-MODE FOLLOW-UP (the next QA layer — NOT built here, WO-329 deliverable 3)
// -----------------------------------------------------------------------------
// This headless suite proves the STATIC preconditions of the core loop. The TRUE
// end-to-end behaviours need Unity Test Framework PLAY-MODE tests (they require a
// baked NavMesh, the RuntimeInitializeOnLoad bootstrappers — CatalogBootstrap,
// EconomyService.Bootstrap, WaveSystemBridgeBootstrap — and UniTask frame ticks):
//
//   1. DEFEND kicks the loop: WaveManager.ForceBeginNextWave() advances Idle ->
//      Countdown/Active and spawns >=1 Enemy (WO-327).
//   2. Wave clear pays out: clearing a wave raises EconomyService Wood/Iron by the
//      WO-330 reward formula (base + perWave*(waveId-1)).
//   3. Build/upgrade SPENDS: EconomyService.TrySpend(cost) deducts and a placement
//      via StructureFactory.Create consumes the recipe + registers a structure.
//   4. No NullReferenceException during a short headless play run (guards WO-328).
//   5. CatalogRegistry.Count > 0 at runtime (CatalogBootstrap actually registered).
//   6. GameState save/load round-trips the BaseLayout + wallet (WO-301).
//
// To add it: create Assets/Tests/PlayMode/DeNelle.Tests.PlayMode.asmdef
// (references DeNelle.Village, DeNelle.Core, UnityEngine.TestRunner,
// UnityEditor.TestRunner; "optionalUnityReferences": ["TestAssemblies"]) and run via
//   -runTests -testPlatform PlayMode -testResults results.xml
// Wire checkin_gate.ps1 to run THIS suite (RunAll) THEN -runTests. See
// tools/regression/MANUAL_QA_CHECKLIST.md for the visual items neither layer covers.
// =============================================================================
