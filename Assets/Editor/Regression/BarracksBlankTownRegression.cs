// =============================================================================
// BarracksBlankTownRegression — WO-950 oracle: the drillmaster + once-teach + the
// phantom footprint on a BLANK-TOWN save (owner felt-report 2026-08-10).
// -----------------------------------------------------------------------------
// The captured bug, twice over:
//   (1) BarracksNpcInjector.Inject (sceneLoaded) found the still-ACTIVE baked
//       'CastleBarracks' on a save whose EverBuiltStructureIds was EMPTY, seated
//       the drillmaster, and burned the 'barracks_intro' once-teach — pointing
//       the player at a building she never built (WO-834 gate bypassed on the
//       scene-load path; only the 1 Hz poll carried it).
//   (2) The gate-suppressed bake kept SOLID colliders — an invisible building
//       body-blocking the hero at ~(16,0,-4) (F8 seq 2267, "feels like a
//       building is here").
//
// PROBES (edit-mode headless; the felt halves — no visible NPC, no toast — are
// play-mode and ride the owner/AutoPilot pass; here we pin the seams they hang on):
//   A. CATALOG AUTHORITY (WO-950 item 2) — 'barracks' is a swept repo.singleton
//      row authoring baked twin 'CastleBarracks': the ONE owner of the rule the
//      injector-side gate queries.
//   B. PURE GATE TRUTH for 'barracks' — blank+migrated refuses, ever-built and
//      pre-migration surface (the WO-834 table, pinned on this id).
//   C. BLANK-TOWN FIXTURE (everBuilt empty, migrated, Onboarded) — the live gate
//      refuses; Enforce('barracks') deactivates the twin AND leaves ZERO enabled
//      non-trigger colliders + no live nav obstacle (WO-950 item 4/5); a seeded
//      mis-burned 'barracks_intro' is CLEARED by ResetMisburnedOnceTeach (item 3
//      — the honest reset that self-heals the owner's burned save).
//   D. LEGIT FIXTURES — with 'barracks' ever-built (gate open) or a placed
//      BaseLayout record, ResetMisburnedOnceTeach REFUSES: a legitimate burn is
//      untouchable.
//   E. RESTORE DISCIPLINE — RestoreBakedTwinPhysics re-enables trigger colliders,
//      the fitted StructureCollider and nav obstacles, but keeps the BAKED solid
//      body collider DOWN on the setLocalPos skin row (Ticket #10: re-enabling it
//      would resurrect the phantom wall at the root while the visual stands
//      elsewhere).
//   F. SOURCE LINTS — the three seams stay wired (the CheckBlankTownGate Lint
//      pattern): the Inject-path gate, the standdown physics suppression, the
//      surfacing restore.
//
// Contract mirrors the other suites: public static bool Run(out string reason);
// markers BARRACKS_BLANKTOWN_OK (Debug.Log) / BARRACKS_BLANKTOWN_FAIL (LogError).
// Standalone: run-unity-method.ps1 -Method DeNelle.Editor.BarracksBlankTownRegression.RunStandalone
// =============================================================================
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;
using UnityEngine.AI;
using DeNelle.Core.Catalog;
using DeNelle.Village;

namespace DeNelle.Editor
{
    public static class BarracksBlankTownRegression
    {
        private const string Id       = "barracks";
        private const string TwinName = "CastleBarracks";
        private const string TeachKey = "barracks_intro";

        /// <summary>Batchmode entry point (run-unity-method.ps1).</summary>
        public static void RunStandalone()
        {
            Run(out _);   // Run() emits the BARRACKS_BLANKTOWN_OK / BARRACKS_BLANKTOWN_FAIL marker
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("=== BarracksBlankTownRegression (WO-950): no drillmaster / no teach / no phantom on a blank town ===");

            try
            {
                EnsureCatalogLoaded(failures, log);
                ProbeCatalogAuthority(failures, log);
                ProbePureGateTruth(failures, log);
                ProbeBlankTownFixture(failures, log);
                ProbeLegitBurnUntouchable(failures, log);
                ProbeRestoreDiscipline(failures, log);
                ProbeSourceSeams(failures, log);
            }
            catch (System.Exception ex)
            {
                failures.Add($"BarracksBlankTownRegression threw: {ex.GetType().Name}: {ex.Message}");
            }

            return Verdict(failures, log, out reason);
        }

        // =====================================================================
        //  A. Catalog authority — ONE owner per concern (WO-950 item 2)
        // =====================================================================
        private static void ProbeCatalogAuthority(List<string> failures, StringBuilder log)
        {
            var entry = CatalogRegistry.Get(Id);
            if (entry == null || entry.repo == null || !entry.repo.singleton)
            { failures.Add($"WO-950 A: catalog row '{Id}' missing or not repo.singleton - the swept authority the injector gate queries is gone"); return; }
            bool twinAuthored = false;
            if (entry.repo.bakedTwins != null)
                foreach (var t in entry.repo.bakedTwins) if (t == TwinName) { twinAuthored = true; break; }
            if (!twinAuthored)
                failures.Add($"WO-950 A: catalog row '{Id}' no longer authors baked twin '{TwinName}' - EnforceAll would stop sweeping the bake and the blank-town gate loses its subject");
            else
                log.AppendLine($"  A: '{Id}' is repo.singleton and authors baked twin '{TwinName}' (the ONE authority the injector queries).");
        }

        // =====================================================================
        //  B. Pure gate truth on THIS id (the WO-834 table, barracks leg)
        // =====================================================================
        private static void ProbePureGateTruth(List<string> failures, StringBuilder log)
        {
            var none = new List<string>();
            var built = new List<string> { Id };
            if (StructureSingleton.MayBakedTwinSurface(Id, none, true))
                failures.Add("WO-950 B: migrated + empty everBuilt must REFUSE the baked barracks (the owner's exact save shape)");
            if (!StructureSingleton.MayBakedTwinSurface(Id, built, true))
                failures.Add("WO-950 B: ever-built 'barracks' must SURFACE on a migrated save (Default-Town template grant / post-placement)");
            if (!StructureSingleton.MayBakedTwinSurface(Id, none, false))
                failures.Add("WO-950 B: pre-migration save must SURFACE (the bake owns the town before handover)");
            if (failures.Count == 0)
                log.AppendLine("  B: pure gate truth holds for 'barracks' (blank refuses; ever-built + pre-migration surface).");
        }

        // =====================================================================
        //  C. Blank-town fixture — gate refuses, suppression strips physics,
        //     the mis-burned once-teach resets (WO-950 items 1/3/4/5)
        // =====================================================================
        private static void ProbeBlankTownFixture(List<string> failures, StringBuilder log)
        {
            RunWithHeadlessState(failures, log, "C", everBuiltBarracks: false, placedRecord: false,
                (state, twin, solid, trigger, obstacle) =>
            {
                // The live gate must read the fixture as CLOSED.
                if (StructureSingleton.MayBakedTwinSurface(Id))
                { failures.Add("WO-950 C: live MayBakedTwinSurface read TRUE on a blank migrated save - the fixture (or the gate) is broken"); return; }

                // The enforcement sweep on the blank town: twin down + physics stripped.
                StructureSingleton.Enforce(Id);
                if (twin.activeSelf)
                    failures.Add("WO-950 C: Enforce left the baked twin ACTIVE on a blank town - the WO-834 suppression branch did not fire");
                int enabledSolid = CountEnabledColliders(twin, wantTrigger: false);
                int enabledTrig  = CountEnabledColliders(twin, wantTrigger: true);
                if (enabledSolid != 0)
                    failures.Add($"WO-950 C: THE PHANTOM FOOTPRINT - a gate-suppressed twin still carries {enabledSolid} enabled non-trigger collider(s); an invisible building would body-block the hero (F8 seq 2267)");
                if (enabledTrig != 0)
                    failures.Add($"WO-950 C: a suppressed twin still carries {enabledTrig} enabled TRIGGER collider(s) - its NPC point is not legitimately live while suppressed");
                if (obstacle.enabled)
                    failures.Add("WO-950 C: a suppressed twin still carries a LIVE NavMeshObstacle - it would keep carving the navmesh under an invisible building");

                // The mis-burned once-teach resets (the owner's burned save self-heals).
                state.SeenTutorials[TeachKey] = true;
                bool cleared = BarracksNpcInjector.ResetMisburnedOnceTeach();
                bool stillSeen = state.SeenTutorials.TryGetValue(TeachKey, out bool v) && v;
                if (!cleared || stillSeen)
                    failures.Add("WO-950 C: ResetMisburnedOnceTeach did not clear a mis-burned 'barracks_intro' while the gate is closed - the owner's burned save would never re-arm the teach");
                else
                    log.AppendLine("  C: blank town - gate refuses; suppressed twin inactive with 0 enabled colliders + no live nav obstacle; mis-burned once-teach cleared.");
            });
        }

        // =====================================================================
        //  D. A LEGITIMATE burn is untouchable (the reset self-guard, item 3)
        // =====================================================================
        private static void ProbeLegitBurnUntouchable(List<string> failures, StringBuilder log)
        {
            // Gate OPEN (ever-built): the reset must refuse.
            RunWithHeadlessState(failures, log, "D1", everBuiltBarracks: true, placedRecord: false,
                (state, twin, solid, trigger, obstacle) =>
            {
                if (!StructureSingleton.MayBakedTwinSurface(Id))
                { failures.Add("WO-950 D1: live gate read CLOSED with 'barracks' ever-built - fixture or gate broken"); return; }
                state.SeenTutorials[TeachKey] = true;
                bool cleared = BarracksNpcInjector.ResetMisburnedOnceTeach();
                bool stillSeen = state.SeenTutorials.TryGetValue(TeachKey, out bool v) && v;
                if (cleared || !stillSeen)
                    failures.Add("WO-950 D1: ResetMisburnedOnceTeach CLEARED a legitimate burn (gate open) - the once-teach would re-fire on every legit save");
                else
                    log.AppendLine("  D1: gate open (ever-built) - the reset refused; the legitimate burn survives.");
            });

            // PLACED record (gate technically closed in the fixture, but a player-owned
            // barracks stands): the reset must refuse on the IsPlayerBuilt guard.
            RunWithHeadlessState(failures, log, "D2", everBuiltBarracks: false, placedRecord: true,
                (state, twin, solid, trigger, obstacle) =>
            {
                if (!StructureSingleton.IsPlayerBuilt(Id))
                { failures.Add("WO-950 D2: a BaseLayout record did not read as player-built - fixture or IsPlayerBuilt broken"); return; }
                state.SeenTutorials[TeachKey] = true;
                bool cleared = BarracksNpcInjector.ResetMisburnedOnceTeach();
                bool stillSeen = state.SeenTutorials.TryGetValue(TeachKey, out bool v) && v;
                if (cleared || !stillSeen)
                    failures.Add("WO-950 D2: ResetMisburnedOnceTeach CLEARED the burn with a placed barracks standing - a legit drillmaster seat would re-teach");
                else
                    log.AppendLine("  D2: placed record - the reset refused; the legitimate burn survives.");
            });
        }

        // =====================================================================
        //  E. Restore discipline — colliders come back on surfacing, EXCEPT the
        //     baked solid body on a setLocalPos skin row (Ticket #10)
        // =====================================================================
        private static void ProbeRestoreDiscipline(List<string> failures, StringBuilder log)
        {
            var created = new List<GameObject>();
            try
            {
                // The barracks shape: suppressed twin carrying a LightSkin_ marker (its
                // setLocalPos row moved the visual) + the fitted StructureCollider.
                var twin = new GameObject(TwinName); created.Add(twin);
                var bakedBody = new GameObject("BakedBody"); bakedBody.transform.SetParent(twin.transform, false);
                var bodyCol = bakedBody.AddComponent<BoxCollider>();
                var trigGo = new GameObject("NpcPoint"); trigGo.transform.SetParent(twin.transform, false);
                var trigCol = trigGo.AddComponent<SphereCollider>(); trigCol.isTrigger = true;
                var marker = new GameObject("LightSkin_" + TwinName); marker.transform.SetParent(twin.transform, false);
                var markerCol = marker.AddComponent<BoxCollider>();
                var fitted = new GameObject("StructureCollider"); fitted.transform.SetParent(twin.transform, false);
                var fittedCol = fitted.AddComponent<BoxCollider>();
                var obstacle = twin.AddComponent<NavMeshObstacle>();

                HubStructureVisualInjector.SuppressBakedTwinPhysics(twin, "regression fixture");
                if (bodyCol.enabled || trigCol.enabled || markerCol.enabled || fittedCol.enabled || obstacle.enabled)
                { failures.Add("WO-950 E: SuppressBakedTwinPhysics left something enabled - the strip is not total"); return; }

                HubStructureVisualInjector.RestoreBakedTwinPhysics(twin, TwinName);
                if (!trigCol.enabled)
                    failures.Add("WO-950 E: restore did not re-enable the TRIGGER collider (NPC interact point stays dead on a surfaced barracks)");
                if (!fittedCol.enabled)
                    failures.Add("WO-950 E: restore did not re-enable the fitted StructureCollider (the visible building would be walk-through)");
                if (!markerCol.enabled)
                    failures.Add("WO-950 E: restore did not re-enable the skinned visual's own collider (under the LightSkin marker)");
                if (!obstacle.enabled)
                    failures.Add("WO-950 E: restore did not re-enable the NavMeshObstacle");
                if (bodyCol.enabled)
                    failures.Add("WO-950 E: restore RE-ENABLED the baked solid body collider on a setLocalPos row - the Ticket #10 phantom wall is back at the root while the visual stands elsewhere");

                // A NON-setLocalPos twin (no marker): everything comes back.
                var plain = new GameObject("Windmill_Food_Storefront"); created.Add(plain);
                var plainCol = plain.AddComponent<BoxCollider>();
                HubStructureVisualInjector.SuppressBakedTwinPhysics(plain, "regression fixture");
                HubStructureVisualInjector.RestoreBakedTwinPhysics(plain, "Windmill_Food_Storefront");
                if (!plainCol.enabled)
                    failures.Add("WO-950 E: restore left a co-located (non-setLocalPos) storefront's collider disabled - a surfaced store would be walk-through");

                log.AppendLine("  E: suppress strips everything; restore brings back trigger/fitted/marker/nav, keeps the setLocalPos baked body down, and fully restores a co-located twin.");
            }
            finally
            {
                foreach (var go in created)
                    if (go != null) Object.DestroyImmediate(go);
            }
        }

        // =====================================================================
        //  F. Source lints — the three WO-950 seams stay wired
        // =====================================================================
        private static void ProbeSourceSeams(List<string> failures, StringBuilder log)
        {
            Lint(failures, "Assets/_Modules/Village/NPCs/BarracksNpcInjector.cs",
                "Inject refused the BAKED CastleBarracks",
                "the sceneLoaded Inject path must gate the baked fallback on MayBakedTwinSurface (WO-950 item 1)");
            Lint(failures, "Assets/_Modules/Village/NPCs/BarracksNpcInjector.cs",
                "ResetMisburnedOnceTeach",
                "the gate-refusal path must re-arm a mis-burned once-teach (WO-950 item 3)");
            Lint(failures, "Assets/_Modules/Village/BuildMode/StructureSingleton.cs",
                "SuppressBakedTwinPhysics",
                "StandDownBakedTwins must strip a suppressed twin's physics (WO-950 item 4)");
            Lint(failures, "Assets/_Modules/Village/HubStructureVisualInjector.cs",
                "RestoreBakedTwinPhysics",
                "surfacing must restore the stripped physics (WO-950 item 4)");
            if (failures.Count == 0)
                log.AppendLine("  F: all three WO-950 seams present at source (gate, suppress, restore).");
        }

        private static void Lint(List<string> failures, string path, string needle, string why)
        {
            if (!System.IO.File.Exists(path))
            { failures.Add($"WO-950 F: {path} is gone - {why}"); return; }
            if (!System.IO.File.ReadAllText(path).Contains(needle))
                failures.Add($"WO-950 F: {path} no longer contains '{needle}' - {why}");
        }

        // =====================================================================
        //  Fixture plumbing — headless GameStateService + a physical twin
        //  (the DestroyedStructureRegression reflection seams; editor-only suite,
        //  never a runtime bridge).
        // =====================================================================
        private delegate void FixtureBody(DeNelle.Core.State.GameState state, GameObject twin,
            BoxCollider solid, SphereCollider trigger, NavMeshObstacle obstacle);

        private static void RunWithHeadlessState(List<string> failures, StringBuilder log, string tag,
            bool everBuiltBarracks, bool placedRecord, FixtureBody body)
        {
            var instField = typeof(DeNelle.Core.State.GameStateService).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
            var stateField = typeof(DeNelle.Core.State.GameStateService).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance);
            if (instField == null || stateField == null)
            { log.AppendLine($"  {tag}: SKIPPED - GameStateService reflection seams moved (see DestroyedStructureRegression)"); return; }

            var prior = instField.GetValue(null) as DeNelle.Core.State.GameStateService;
            GameObject gssGo = null, twinGo = null;
            DeNelle.Core.State.GameState throwaway = null;
            try
            {
                throwaway = ScriptableObject.CreateInstance<DeNelle.Core.State.GameState>();
                gssGo = new GameObject($"GameStateService (WO-950 {tag})");
                var gss = gssGo.AddComponent<DeNelle.Core.State.GameStateService>();
                stateField.SetValue(gss, throwaway);
                instField.SetValue(null, gss);
                var state = gss.State;

                state.Onboarded = true;                       // founding complete (the owner's save)
                state.StrategicPlacementMigrated = true;      // migrated = the gate reads everBuilt
                state.EverBuiltStructureIds = new List<string>();
                if (everBuiltBarracks) state.EverBuiltStructureIds.Add(Id);
                if (placedRecord)
                    state.BaseLayout.Add(new DeNelle.Core.State.PlacedStructureData(Id, 2, 2, 0, 1));

                // The physical twin: an active bake carrying a solid collider, a trigger
                // (NPC point) and a nav obstacle - the shipped CastleBarracks shape.
                twinGo = new GameObject(TwinName);
                var solid = twinGo.AddComponent<BoxCollider>();
                var trigChild = new GameObject("NpcPoint"); trigChild.transform.SetParent(twinGo.transform, false);
                var trigger = trigChild.AddComponent<SphereCollider>(); trigger.isTrigger = true;
                var obstacle = twinGo.AddComponent<NavMeshObstacle>();

                body(state, twinGo, solid, trigger, obstacle);
            }
            finally
            {
                if (twinGo != null) Object.DestroyImmediate(twinGo);
                if (gssGo != null) Object.DestroyImmediate(gssGo);
                if (throwaway != null) Object.DestroyImmediate(throwaway);
                instField.SetValue(null, prior);
            }
        }

        private static int CountEnabledColliders(GameObject root, bool wantTrigger)
        {
            int n = 0;
            foreach (var c in root.GetComponentsInChildren<Collider>(true))
                if (c != null && c.enabled && c.isTrigger == wantTrigger) n++;
            return n;
        }

        /// <summary>CatalogRegistry is play-mode-bootstrapped; hydrate it from the SAME
        /// canonical JSON the real CatalogBootstrap uses when the barracks row is absent
        /// (the StrategicPlacementRegression pattern).</summary>
        private static void EnsureCatalogLoaded(List<string> failures, StringBuilder log)
        {
            if (CatalogRegistry.Get(Id) != null) return;

            string json = DeNelle.Core.CanonicalJson.Read("Data/Canonical/structures-catalog.json");
            if (string.IsNullOrEmpty(json))
            { failures.Add("WO-950: structures-catalog.json unreadable - cannot hydrate CatalogRegistry"); return; }
            try
            {
                var settings = new JsonSerializerSettings
                {
                    Converters = { new StringEnumConverter() },
                    NullValueHandling = NullValueHandling.Ignore,
                    MissingMemberHandling = MissingMemberHandling.Ignore,
                };
                var file = JsonConvert.DeserializeObject<StructuresFile>(json, settings);
                int added = 0;
                if (file != null && file.Entries != null)
                    foreach (var e in file.Entries)
                        if (e != null && !string.IsNullOrEmpty(e.id) && CatalogRegistry.Get(e.id) == null)
                        { CatalogRegistry.Register(e); added++; }
                log.AppendLine($"  hydrated CatalogRegistry with {added} entrie(s) from structures-catalog.json");
            }
            catch (System.Exception ex)
            { failures.Add($"WO-950: structures-catalog.json failed to parse: {ex.Message}"); }
        }

        [System.Serializable]
        private sealed class StructuresFile
        {
            [JsonProperty("version")] public int Version;
            [JsonProperty("entries")] public List<CatalogEntry> Entries = new List<CatalogEntry>();
        }

        // =====================================================================
        //  Verdict + markers
        // =====================================================================
        private static bool Verdict(List<string> failures, StringBuilder log, out string reason)
        {
            if (failures.Count == 0)
            {
                reason = "BARRACKS BLANK-TOWN OK - the swept 'barracks' row authors CastleBarracks; the gate refuses a blank town; " +
                         "a suppressed twin is inactive with zero enabled colliders and no live nav obstacle; a mis-burned once-teach " +
                         "resets (and a legitimate burn is untouchable); restore honours the Ticket #10 setLocalPos rule";
                Debug.Log("BARRACKS_BLANKTOWN_OK\n" + log);
                return true;
            }
            reason = $"BARRACKS BLANK-TOWN: {failures.Count} failure(s): " + string.Join(" | ", failures);
            Debug.LogError($"BARRACKS_BLANKTOWN_FAIL: {failures.Count} failure(s)\n" + log +
                           "\n - " + string.Join("\n - ", failures));
            return false;
        }
    }
}
