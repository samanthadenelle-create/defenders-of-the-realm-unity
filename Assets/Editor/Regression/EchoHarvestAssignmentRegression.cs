using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DeNelle.Pets;
using DeNelle.Village;
using DeNelle.Village.World;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class EchoHarvestAssignmentRegression
    {
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var siteGo = new GameObject("echo-harvest-site-probe");
            var capSiteGo = new GameObject("echo-harvest-cap-probe");
            var workers = new List<GameObject>();
            try
            {
                var site = siteGo.AddComponent<HarvestSite>();
                site.ApplyWorldScaling = false;
                site.BaseYield = 10;
                site.YieldPerAssignedPet = 0.5f;
                site.MaxAssigned = 2;

                GameObject first = Worker("first", workers);
                GameObject stale = Worker("stale", workers);
                if (!site.AssignPet(first.transform) || !site.AssignPet(stale.transform))
                    failures.Add("setup could not fill the two-worker assignment cap");
                if (Yield(site) != 20) failures.Add("two live workers did not produce the expected yield");

                UnityEngine.Object.DestroyImmediate(stale);
                workers.Remove(stale);
                if (site.AssignedCount != 1 || Yield(site) != 15)
                    failures.Add("destroyed Unity-null worker still inflated count or yield");

                // Independent cap scenario: call AssignPet immediately after destruction,
                // before any getter/yield path has a chance to prune the Unity-null slot.
                var capSite = capSiteGo.AddComponent<HarvestSite>();
                capSite.ApplyWorldScaling = false;
                capSite.MaxAssigned = 1;
                GameObject capped = Worker("capped-stale", workers);
                capSite.AssignPet(capped.transform);
                UnityEngine.Object.DestroyImmediate(capped);
                workers.Remove(capped);
                GameObject replacement = Worker("replacement", workers);
                if (!capSite.AssignPet(replacement.transform) || capSite.AssignedCount != 1)
                    failures.Add("AssignPet did not prune a stale capped worker before rejecting replacement");

                // Exercise the real static subscription and one-owner teardown. A throwing
                // observer must be contained while the Harvest subscriber removes yield and
                // the Pet body is still destroyed.
                MethodInfo bootstrap = typeof(PetHarvestBootstrap).GetMethod("Bootstrap",
                    BindingFlags.Static | BindingFlags.NonPublic);
                if (bootstrap == null) throw new MissingMethodException(nameof(PetHarvestBootstrap), "Bootstrap");
                bootstrap.Invoke(null, null);
                MethodInfo unassign = typeof(PetHarvestBootstrap).GetMethod("UnassignDespawningEcho",
                    BindingFlags.Static | BindingFlags.NonPublic);
                if (unassign == null) throw new MissingMethodException(nameof(PetHarvestBootstrap), "UnassignDespawningEcho");
                var harvestObserver = (Action<Transform>)Delegate.CreateDelegate(typeof(Action<Transform>), unassign);
                PetDeployer.EchoBodyDespawning -= harvestObserver;
                GameObject echoGo = Worker("assigned-echo", workers);
                var echo = echoGo.AddComponent<Pet>();
                site.AssignPet(echo.transform);
                bool throwingObserverCalled = false;
                Action<Transform> throwingObserver = _ =>
                {
                    throwingObserverCalled = true;
                    throw new InvalidOperationException("oracle observer failure");
                };
                // Adverse order: the throwing observer runs FIRST. Per-subscriber
                // isolation must still allow the later Harvest observer to clean up.
                PetDeployer.EchoBodyDespawning += throwingObserver;
                PetDeployer.EchoBodyDespawning += harvestObserver;
                bool torn;
                try
                {
                    MethodInfo tearDown = typeof(PetDeployer).GetMethod("TearDownPetBody",
                        BindingFlags.Static | BindingFlags.NonPublic);
                    if (tearDown == null) throw new MissingMethodException(nameof(PetDeployer), "TearDownPetBody");
                    torn = (bool)tearDown.Invoke(null, new object[] { echo, new HashSet<int>() });
                }
                finally
                {
                    PetDeployer.EchoBodyDespawning -= throwingObserver;
                    PetDeployer.EchoBodyDespawning -= harvestObserver;
                    bootstrap.Invoke(null, null); // restore the canonical idempotent subscription
                }
                workers.Remove(echoGo);
                if (!throwingObserverCalled || !torn || echo != null)
                    failures.Add("throwing Echo observer vetoed or bypassed the real teardown path");
                if (RawAssignedCount(site) != 1)
                    failures.Add("Harvest observer did not remove the Echo from raw assignments before self-heal");
                if (site.AssignedCount != 1 || Yield(site) != 15)
                    failures.Add("real Echo teardown did not immediately remove its assigned yield");

                string deployer = File.ReadAllText("Assets/_Modules/Pets/PetDeployer.cs");
                if (!deployer.Contains("EchoBodyDespawning?.GetInvocationList()") ||
                    !deployer.Contains("Guard.Try(\"Echo\", \"notify echo body despawn observer\"") ||
                    !deployer.Contains("NotifyEchoBodyDespawning(pet.transform)") ||
                    !deployer.Contains("if (TearDownPetBody(pet, torn)) _deployed.RemoveAt(i);"))
                    failures.Add("observer failure is not isolated or slot removal bypasses teardown authority");
                string bootstrapSource = File.ReadAllText("Assets/_Modules/Village/World/PetHarvestBootstrap.cs");
                if (!bootstrapSource.Contains("PetDeployer.EchoBodyDespawning -= UnassignDespawningEcho") ||
                    !bootstrapSource.Contains("PetDeployer.EchoBodyDespawning += UnassignDespawningEcho"))
                    failures.Add("Harvest bootstrap no longer owns an idempotent Echo teardown subscription");
            }
            catch (Exception ex)
            {
                failures.Add("oracle threw " + ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                foreach (GameObject worker in workers)
                    if (worker != null) UnityEngine.Object.DestroyImmediate(worker);
                UnityEngine.Object.DestroyImmediate(siteGo);
                UnityEngine.Object.DestroyImmediate(capSiteGo);
            }

            CheckSiloOverflowStays(failures);

            if (failures.Count > 0)
            {
                reason = "ECHO_HARVEST_ASSIGNMENT_FAIL: " + string.Join(" | ", failures);
                return false;
            }
            reason = "ECHO_HARVEST_ASSIGNMENT_OK - Unity-null pruning, yield, replacement, recall and [silo-overflow-stays] pinned";
            return true;
        }

        // =====================================================================
        //  [silo-overflow-stays] -- WO-1392: the Echo silo NEVER burns a clamped remainder.
        // =====================================================================
        //
        // Owner's Seeker, 2026-09-04: COLLECT dumped 2393 wood from the Echo silo into a bank at
        // 2021/4000. TownBankCapacity.ClampGrant applied 1979; EchoService.DumpSilos then did
        // `s.SiloResources -= pool` -- subtracting the REQUEST -- so the 414 that never entered the
        // bank were destroyed. The covenant (HarvestBoostService header): never burn silently.
        //
        // Fixture: the REAL DumpSilos / GrantSpendable / ClampGrant against a throwaway GameState
        // (editmode never runs Awake, so the singletons are installed by reflection exactly as
        // EchoSpecializationRegression does). One Echo assigned to wood so the whole pool is a wood
        // request; the wallet is seated at (MaxOf(Wood) - 1979) so the clamp banks exactly 1979 of a
        // 2393 silo whatever storage-caps.json authors as the base cap.
        //   dump 1 : wallet +1979, silo keeps 414
        //   spend 500, dump 2 : wallet +414, silo empty
        // RED BY: restoring `s.SiloResources -= pool;` in EchoService.DumpSilos (the named mutation)
        // -- dump 1 then leaves 0 in the silo and dump 2 banks 0.
        private const string SaveKey = "dotr-save";

        private static void CheckSiloOverflowStays(List<string> failures)
        {
            const int siloWood = 2393;
            const int expectBanked = 1979;
            const int expectStays = siloWood - expectBanked;   // 414
            const int spend = 500;

            bool hadSave = PlayerPrefs.HasKey(SaveKey);
            string rawSave = hadSave ? PlayerPrefs.GetString(SaveKey, null) : null;
            DeNelle.Core.State.GameStateService priorInstance = DeNelle.Core.State.GameStateService.Instance;
            EchoService priorEcho = EchoService.Instance;
            EconomyService priorEco = EconomyService.Instance;

            GameObject gssGo = null;
            GameObject svcGo = null;
            DeNelle.Core.State.GameState throwaway = null;
            bool installedState = false;
            bool installedEcho = false;
            bool installedEco = false;
            try
            {
                throwaway = ScriptableObject.CreateInstance<DeNelle.Core.State.GameState>();
                gssGo = new GameObject("GameStateService (silo-overflow-oracle)");
                var gss = gssGo.AddComponent<DeNelle.Core.State.GameStateService>();
                var stateField = typeof(DeNelle.Core.State.GameStateService).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance);
                if (stateField == null || !TrySetGssInstance(gss))
                {
                    failures.Add("[silo-overflow-stays] GameStateService _state/_instance seam not found by reflection -- the fixture cannot install a headless state");
                    return;
                }
                stateField.SetValue(gss, throwaway);
                installedState = true;
                var state = gss.State;
                if (state == null)
                {
                    failures.Add("[silo-overflow-stays] throwaway GameState did not install");
                    return;
                }

                svcGo = new GameObject("EchoDump (silo-overflow-oracle)");
                var echo = svcGo.AddComponent<EchoService>();
                var eco = svcGo.AddComponent<EconomyService>();
                if (EchoService.Instance == null) installedEcho = TrySetStaticProperty(typeof(EchoService), "Instance", echo);
                if (EconomyService.Instance == null) installedEco = TrySetStaticProperty(typeof(EconomyService), "Instance", eco);
                if (EchoService.Instance == null || EconomyService.Instance == null)
                {
                    failures.Add("[silo-overflow-stays] EchoService/EconomyService Instance seam not installable headless");
                    return;
                }

                // One Echo, harvesting WOOD (a match bonus is never a lock -- any Echo may pick wood),
                // so HarvestTargetWeights routes the ENTIRE pool to the wood request.
                state.EchoCount = 1;
                state.EchoLanes = "wood:1";
                var weights = EchoBonusCalculator.HarvestTargetWeights();
                float woodW = weights.TryGetValue(HarvestTarget.Wood, out var vw) ? vw : 0f;
                float otherW = 0f;
                foreach (var kv in weights) if (kv.Key != HarvestTarget.Wood) otherW += kv.Value;
                if (woodW <= 0f || otherW > 0f)
                {
                    failures.Add($"[silo-overflow-stays] fixture could not route the pool to wood (wood weight {woodW:0.###}, other {otherW:0.###}) -- the case never reached the state it pins");
                    return;
                }

                int max = DeNelle.Core.Economy.TownBankCapacity.MaxOf(DeNelle.Core.Economy.BankResource.Wood);
                if (max == int.MaxValue || max <= expectBanked)
                {
                    failures.Add($"[silo-overflow-stays] Wood is not capped, or MaxOf(Wood)={max} leaves no room for the fixture -- the town bank cap is the premise");
                    return;
                }
                state.Wood = max - expectBanked;            // 2021 at a 4000 bank; the same 1979 of room at any authored cap
                state.SiloResources = siloWood;

                int woodBefore = state.Wood;
                int banked1 = echo.DumpSilos();
                int dWood1 = state.Wood - woodBefore;
                double siloAfter1 = state.SiloResources;

                if (dWood1 != expectBanked)
                    failures.Add($"[silo-overflow-stays] dump 1 moved the wallet by {dWood1}, expected {expectBanked} (wallet {woodBefore}/{max}, silo {siloWood})");
                if (banked1 != dWood1)
                    failures.Add($"[silo-overflow-stays] dump 1 returned {banked1} but the wallet moved {dWood1} -- the return must be what was BANKED (WO-1207)");
                if (Math.Abs(siloAfter1 - expectStays) > 0.5)
                    failures.Add($"[silo-overflow-stays] dump 1 left {siloAfter1:0} in the silo, expected {expectStays} -- the clamped remainder was BURNED "
                               + "(EchoService.DumpSilos is subtracting the request, not the applied basket: `s.SiloResources -= pool`)");

                // The player spends, then dumps again: the retained 414 banks now.
                state.Wood -= spend;
                int woodBefore2 = state.Wood;
                int banked2 = echo.DumpSilos();
                int dWood2 = state.Wood - woodBefore2;
                if (dWood2 != expectStays)
                    failures.Add($"[silo-overflow-stays] dump 2 (after spending {spend}) moved the wallet by {dWood2}, expected the retained {expectStays}");
                if (banked2 != dWood2)
                    failures.Add($"[silo-overflow-stays] dump 2 returned {banked2} but the wallet moved {dWood2}");
                if (state.SiloResources > 0.5)
                    failures.Add($"[silo-overflow-stays] dump 2 left {state.SiloResources:0} in the silo, expected it empty");
            }
            catch (Exception ex)
            {
                failures.Add("[silo-overflow-stays] oracle threw " + ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                if (svcGo != null) UnityEngine.Object.DestroyImmediate(svcGo);
                if (installedEcho) TrySetStaticProperty(typeof(EchoService), "Instance", priorEcho);
                if (installedEco) TrySetStaticProperty(typeof(EconomyService), "Instance", priorEco);
                if (gssGo != null) UnityEngine.Object.DestroyImmediate(gssGo);
                if (throwaway != null) UnityEngine.Object.DestroyImmediate(throwaway);
                if (installedState) TrySetGssInstance(priorInstance);
                // GrantSpendable Save()d the throwaway -- restore the persisted blob.
                if (hadSave) PlayerPrefs.SetString(SaveKey, rawSave);
                else PlayerPrefs.DeleteKey(SaveKey);
                PlayerPrefs.Save();
            }
        }

        private static bool TrySetGssInstance(DeNelle.Core.State.GameStateService svc)
        {
            var f = typeof(DeNelle.Core.State.GameStateService).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
            if (f == null) return false;
            f.SetValue(null, svc);
            return true;
        }

        private static bool TrySetStaticProperty(Type type, string name, object value)
        {
            try
            {
                var p = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (p != null && p.CanWrite) { p.SetValue(null, value, null); return true; }
                var f = type.GetField($"<{name}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Static);
                if (f == null) return false;
                f.SetValue(null, value);
                return true;
            }
            catch { return false; }
        }

        private static GameObject Worker(string name, List<GameObject> workers)
        {
            var go = new GameObject(name);
            workers.Add(go);
            return go;
        }

        private static int Yield(HarvestSite site)
        {
            MethodInfo method = typeof(HarvestSite).GetMethod("CalculateYield",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (method == null) throw new MissingMethodException(nameof(HarvestSite), "CalculateYield");
            return (int)method.Invoke(site, null);
        }

        private static int RawAssignedCount(HarvestSite site)
        {
            FieldInfo field = typeof(HarvestSite).GetField("_assignedWorkers",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new MissingFieldException(nameof(HarvestSite), "_assignedWorkers");
            return ((List<Transform>)field.GetValue(site)).Count;
        }
    }
}
