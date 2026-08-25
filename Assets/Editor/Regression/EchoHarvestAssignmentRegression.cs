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

            if (failures.Count > 0)
            {
                reason = "ECHO_HARVEST_ASSIGNMENT_FAIL: " + string.Join(" | ", failures);
                return false;
            }
            reason = "ECHO_HARVEST_ASSIGNMENT_OK - Unity-null pruning, yield, replacement and recall pinned";
            return true;
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
