// =============================================================================
// OnboardingIntegrator — WO-133: wires the first-run tutorial (OnboardingFlow)
// into the Village scene's gameplay.
// -----------------------------------------------------------------------------
// OnboardingFlow lives in DeNelle.Onboarding, which deliberately cannot see
// DeNelle.Village/.HUD (port-spec Part 2 isolation) — and DeNelle.Village does
// NOT reference DeNelle.Onboarding either. So this bridge resolves OnboardingFlow
// by full-name REFLECTION (held as UnityEngine.Object), exactly like
// WaveHudBridge / BuildMenuHudBridge / HeartHudBridge resolve the HUD across the
// Village↔HUD boundary (see reflection-bridge-pattern).
//
// The five seams (per OnboardingFlow's integrator notes, OnboardingFlow.cs:534-577):
//   Requests TO gameplay (tutorial → village):
//     OpenBuildMenuRequested → BuildMenu.Open
//     BeginWaveRequested     → WaveManager.BeginLoop().Forget()
//     TutorialClosed         → VillageController.OnOnboardingClosed
//   Gameplay events TO the tutorial (village → tutorial):
//     BuildMenu.BuildingPlaced → flow.NotifyTowerBuilt()
//     pet placed (PetDeployer) → flow.NotifyPetPlaced()
//
// NotifyTowerBuilt / NotifyPetPlaced are safe no-ops off-beat, so they are wired
// unconditionally. Every cross-module hop is null-guarded (CLAUDE.md §10).
//
// Attached at runtime by VillageController.EnsureOnboardingIntegrator (the scene
// is builder-baked; the curated-scene rule forbids re-running the builder for a
// component add). [DisallowMultipleComponent] keeps it idempotent.
// =============================================================================

using System;
using System.Reflection;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;

namespace DeNelle.Village
{
    /// <summary>
    /// Bridges <c>DeNelle.Onboarding.OnboardingFlow</c> (resolved by reflection)
    /// to the village's <see cref="BuildMenu"/> / <see cref="WaveManager"/> /
    /// <see cref="VillageController"/> so the first-run tutorial can drive — and be
    /// driven by — gameplay without an asmdef cross-reference (WO-133).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OnboardingIntegrator : MonoBehaviour
    {
        private const string TypeOnboardingFlow = "DeNelle.Onboarding.OnboardingFlow";

        private UnityEngine.Object _flow;             // OnboardingFlow (no DeNelle.Onboarding ref)
        private MethodInfo _notifyTowerBuilt;         // OnboardingFlow.NotifyTowerBuilt()
        private MethodInfo _notifyPetPlaced;          // OnboardingFlow.NotifyPetPlaced()

        private BuildMenu _buildMenu;
        private WaveManager _waveManager;
        private VillageController _controller;
        private DeNelle.Pets.PetDeployer _petDeployer;

        // Listeners we add to the flow's UnityEvents — cached so OnDisable can
        // cleanly remove them (the flow may outlive a village reload).
        private UnityAction _openBuildMenu;
        private UnityAction _beginWave;
        private UnityAction _tutorialClosed;

        private bool _buildMenuHooked;
        private bool _petNotified;
        private bool _wired;

        private void Start()
        {
            Wire();
        }

        private void Update()
        {
            // Pets auto-deploy on PetDeployer.Start(); there is no per-pet "placed"
            // event. Fire NotifyPetPlaced once the first pet is on the field so the
            // "Place a pet" beat advances. No-op off-beat (the flow guards it).
            if (_wired && !_petNotified && _petDeployer != null
                && _petDeployer.DeployedPets != null && _petDeployer.DeployedPets.Count > 0)
            {
                _petNotified = true;
                _notifyPetPlaced?.Invoke(_flow, null);
            }
        }

        private void Wire()
        {
            if (_wired) return;

            // ── Resolve the village-side gameplay components (same assembly) ──
            _buildMenu    = FindObjectOfType<BuildMenu>();
            _waveManager  = FindObjectOfType<WaveManager>();
            _controller   = GetComponent<VillageController>() ?? FindObjectOfType<VillageController>();
            _petDeployer  = FindObjectOfType<DeNelle.Pets.PetDeployer>();

            // ── Resolve OnboardingFlow by reflection (cross-asmdef) ──────────
            Type flowType = ResolveType(TypeOnboardingFlow);
            if (flowType == null)
            {
                // DeNelle.Onboarding not loaded — nothing to wire. Not an error:
                // the village still runs; the FTUE simply never shows.
                Debug.Log("[OnboardingIntegrator] OnboardingFlow type not found — " +
                          "no first-run tutorial to wire (returning-player path is unaffected).");
                _wired = true;
                return;
            }

            _flow = FindObjectOfType(flowType);
            if (_flow == null)
            {
                Debug.LogWarning("[OnboardingIntegrator] No OnboardingFlow in the scene — " +
                                 "FTUE seams not wired (check the builder placed the tutorial object).");
                _wired = true;
                return;
            }

            _notifyTowerBuilt = flowType.GetMethod("NotifyTowerBuilt", BindingFlags.Public | BindingFlags.Instance);
            _notifyPetPlaced  = flowType.GetMethod("NotifyPetPlaced",  BindingFlags.Public | BindingFlags.Instance);

            // ── Requests TO gameplay (tutorial → village) ────────────────────
            if (_buildMenu != null)
            {
                _openBuildMenu = _buildMenu.Open;
                AddUnityEventListener(flowType, "OpenBuildMenuRequested", _openBuildMenu);
            }

            if (_waveManager != null)
            {
                _beginWave = () => _waveManager.BeginLoop().Forget();
                AddUnityEventListener(flowType, "BeginWaveRequested", _beginWave);
            }

            if (_controller != null)
            {
                _tutorialClosed = _controller.OnOnboardingClosed;
                AddUnityEventListener(flowType, "TutorialClosed", _tutorialClosed);
            }

            // ── Gameplay events TO the tutorial (village → tutorial) ─────────
            // BuildMenu.BuildingPlaced is a C# event — subscribe directly (same
            // assembly). Forward unconditionally; NotifyTowerBuilt is a no-op off-beat.
            if (_buildMenu != null && _notifyTowerBuilt != null)
            {
                _buildMenu.BuildingPlaced += OnBuildingPlaced;
                _buildMenuHooked = true;
            }

            _wired = true;
            Debug.Log("[OnboardingIntegrator] FTUE seams wired — " +
                      $"buildMenu={(_buildMenu != null)}, waveManager={(_waveManager != null)}, " +
                      $"controller={(_controller != null)}, petDeployer={(_petDeployer != null)}.");
        }

        private void OnBuildingPlaced(Building building, BuildingDef def)
        {
            _notifyTowerBuilt?.Invoke(_flow, null);
        }

        private void OnDisable()
        {
            // Detach from the flow's UnityEvents and the BuildMenu C# event so a
            // stale delegate can't fire into a torn-down integrator across a reload.
            if (_flow != null)
            {
                Type flowType = _flow.GetType();
                if (_openBuildMenu  != null) RemoveUnityEventListener(flowType, "OpenBuildMenuRequested", _openBuildMenu);
                if (_beginWave      != null) RemoveUnityEventListener(flowType, "BeginWaveRequested", _beginWave);
                if (_tutorialClosed != null) RemoveUnityEventListener(flowType, "TutorialClosed", _tutorialClosed);
            }

            if (_buildMenuHooked && _buildMenu != null)
            {
                _buildMenu.BuildingPlaced -= OnBuildingPlaced;
                _buildMenuHooked = false;
            }
        }

        // =====================================================================
        //  Reflection helpers (UnityEvent fields on OnboardingFlow)
        // =====================================================================

        private void AddUnityEventListener(Type flowType, string fieldName, UnityAction action)
        {
            var evt = GetUnityEvent(flowType, fieldName);
            evt?.AddListener(action);
        }

        private void RemoveUnityEventListener(Type flowType, string fieldName, UnityAction action)
        {
            var evt = GetUnityEvent(flowType, fieldName);
            evt?.RemoveListener(action);
        }

        private UnityEvent GetUnityEvent(Type flowType, string fieldName)
        {
            if (flowType == null || _flow == null) return null;
            FieldInfo field = flowType.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            return field?.GetValue(_flow) as UnityEvent;
        }

        private static Type ResolveType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }
    }
}
