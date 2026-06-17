// =============================================================================
// TowerSwapService — WO7: Instant Tower Swap via Solana Pay.
// -----------------------------------------------------------------------------
// Manages the full lifecycle of an instant tower swap:
//   1. Listens for Tower.AnyLongPressed (static event, fires after 0.6s hold).
//   2. Opens TowerSwapMenu for that tower (wallet balance + type picker).
//   3. On player confirmation: validates cooldown + wave limit → PayFlat 2.5 USDC
//      → Tower.SwapToType(newData) → VFX → backend log → EventTracker.
//
// RULES (locked spec):
//   • Cost: 2.5 USDC (or SCR equivalent via Jupiter — not yet wired; Phase 2).
//   • Cooldown: 45 seconds per tower slot (keyed by Tower instance ID).
//   • Max per wave: 4 swaps. Resets on WaveManager.OnWaveCleared.
//   • SCR path: currently routed as USDC for Phase 1; Jupiter routing added in
//     Phase 2 when the Jupiter API integration is complete.
//
// INSPECTOR SETUP:
//   • _swappableTowers: assign all 4 TowerData assets (Arcane, Frost, Flame, Arrow).
//   • _wallet: assign the scene's WalletService via a [SerializeField] or find from
//     a global provider. The PackStore already holds one — wire in inspector.
//   • _menu: assign the TowerSwapMenu component in scene.
//
// BACKEND CONTRACT:
//   POST api/tower-swap/log
//   Body: { playerId, waveId, fromTower, toTower, currency, txSig, costUsdc }
// =============================================================================

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DeNelle.Core.Analytics;
using DeNelle.Core.State;
using DeNelle.Core.Data;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace DeNelle.Village
{
    /// <summary>
    /// Singleton service that owns the instant tower swap payment flow.
    /// Place on a persistent scene GameObject (Village scene root recommended).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TowerSwapService : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Tooltip("All swappable tower types. Assign all 4 TowerData assets.")]
        [SerializeField] private TowerData[] _swappableTowers;

        [Tooltip("TowerSwapMenu UI component in scene.")]
        [SerializeField] private TowerSwapMenu _menu;

        [Tooltip("WalletService reference — wire from the WalletConnectDialog or PackStore.")]
        [SerializeField] private DeNelle.Wallet.WalletService _wallet;

        // ── Constants ─────────────────────────────────────────────────────────

        private const double SwapCostUsdc          = 2.5;
        private const float  CooldownSeconds       = 45f;
        private const int    MaxSwapsPerWave       = 4;
        private const string BackendSwapLogUrl     = "https://defenders-of-the-realm-v2.vercel.app/api/tower-swap/log";

        // ── Runtime state ─────────────────────────────────────────────────────

        private static TowerSwapService _instance;
        public  static TowerSwapService Instance => _instance;

        // Tower instance ID → realtime clock when cooldown expires.
        private readonly Dictionary<int, float> _cooldowns = new Dictionary<int, float>();
        private int _swapsThisWave;
        private WaveManager _waveManager;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(this); return; }
            _instance = this;
        }

        private void OnEnable()
        {
            Tower.AnyLongPressed += OnTowerLongPressed;
            // Prefer the canonical singleton (active-scene WaveManager); fall back to Find.
            _waveManager = WaveManager.Instance ?? FindObjectOfType<WaveManager>();
            if (_waveManager != null)
                _waveManager.OnWaveCleared.AddListener(OnWaveCleared);

            // Try to find WalletService if not wired in inspector.
            if (_wallet == null)
                _wallet = FindObjectOfType<DeNelle.Wallet.WalletConnectDialog>()
                    ?.GetComponentInParent<DeNelle.Wallet.WalletService>();
        }

        private void OnDisable()
        {
            Tower.AnyLongPressed -= OnTowerLongPressed;
            if (_waveManager != null)
                _waveManager.OnWaveCleared.RemoveListener(OnWaveCleared);
        }

        private void OnWaveCleared(int _)
        {
            _swapsThisWave = 0;
        }

        // ── Entry point — long press ──────────────────────────────────────────

        private void OnTowerLongPressed(Tower tower)
        {
            if (tower == null) return;

            if (_menu == null)
            {
                Debug.LogWarning("[TowerSwapService] TowerSwapMenu not wired — swap UI unavailable.");
                return;
            }

            // Evaluate state before opening.
            GetSwapState(tower, out bool onCooldown, out float cooldownRemaining, out bool waveLimitHit);

            _menu.Open(tower, _swappableTowers, SwapCostUsdc,
                       onCooldown, cooldownRemaining, waveLimitHit,
                       onConfirm: (targetData, currency) => ExecuteSwapAsync(tower, targetData, currency).Forget());
        }

        // ── Validation ────────────────────────────────────────────────────────

        private void GetSwapState(Tower tower, out bool onCooldown, out float cooldownRemaining, out bool waveLimitHit)
        {
            int id = tower.GetInstanceID();
            if (_cooldowns.TryGetValue(id, out float endTime) && Time.realtimeSinceStartup < endTime)
            {
                onCooldown = true;
                cooldownRemaining = endTime - Time.realtimeSinceStartup;
            }
            else
            {
                onCooldown = false;
                cooldownRemaining = 0f;
                _cooldowns.Remove(id);
            }
            waveLimitHit = _swapsThisWave >= MaxSwapsPerWave;
        }

        /// <summary>Seconds remaining on the cooldown for a given tower (0 if ready).</summary>
        public float GetCooldownRemaining(Tower tower)
        {
            if (tower == null) return 0f;
            int id = tower.GetInstanceID();
            if (_cooldowns.TryGetValue(id, out float endTime))
                return Mathf.Max(0f, endTime - Time.realtimeSinceStartup);
            return 0f;
        }

        // ── Execution ─────────────────────────────────────────────────────────

        private async UniTaskVoid ExecuteSwapAsync(
            Tower                  fromTower,
            TowerData              toData,
            DeNelle.Wallet.CurrencyKind currency)
        {
            // Re-validate (menu may have been open for a while).
            GetSwapState(fromTower, out bool onCooldown, out _, out bool waveLimitHit);

            if (onCooldown)       { _menu.ShowError("This tower is still cooling down."); return; }
            if (waveLimitHit)     { _menu.ShowError($"Max {MaxSwapsPerWave} swaps per wave reached."); return; }
            if (_wallet == null)  { _menu.ShowError("Wallet not connected."); return; }
            if (!_wallet.IsConnected) { _menu.ShowError("Connect your wallet first."); return; }

            // Balance check — fetch live.
            _menu.SetLoading(true, "Checking balance…");
            var balance = await _wallet.GetBalance();
            double available = currency == DeNelle.Wallet.CurrencyKind.Usdc ? balance.Usdc : balance.Skr;
            if (available < SwapCostUsdc)
            {
                _menu.SetLoading(false);
                _menu.ShowError($"Insufficient balance ({available:F2} available, {SwapCostUsdc} required).");
                return;
            }

            // Payment.
            _menu.SetLoading(true, "Awaiting wallet confirmation…");
            string txId = $"tower_swap_{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            var result = await _wallet.PayFlat(txId, currency, SwapCostUsdc);

            if (!result.Ok)
            {
                _menu.SetLoading(false);
                _menu.ShowError($"Transaction failed: {result.Error}");
                return;
            }

            // Apply swap.
            string fromName = fromTower.Data != null ? fromTower.Data.towerName : "Unknown";
            int waveId = _waveManager != null ? _waveManager.CurrentWaveId : 0;

            fromTower.SwapToType(toData);

            // Record cooldown + wave count.
            _cooldowns[fromTower.GetInstanceID()] = Time.realtimeSinceStartup + CooldownSeconds;
            _swapsThisWave++;

            // Save state.
            GameStateService.Instance?.Save();

            // VFX — swap flash (procedural if no prefab).
            SpawnSwapVFX(fromTower.transform.position);

            _menu.SetLoading(false);
            _menu.ShowSuccess($"Swapped to {toData.towerName}!");

            // Analytics.
            EventTracker.Track("tower_swap_completed", new
            {
                fromTower  = fromName,
                toTower    = toData.towerName,
                waveId,
                currency   = currency.ToString(),
                costUsdc   = SwapCostUsdc,
                txSig      = result.TxSignature,
            });

            // Backend log (fire-and-forget; non-critical).
            LogSwapToBackendAsync(fromName, toData.towerName, waveId,
                                  currency.ToString(), result.TxSignature).Forget();

            Debug.Log($"[TowerSwapService] Swap complete: {fromName} → {toData.towerName} | tx: {result.TxSignature}");
        }

        // ── VFX ───────────────────────────────────────────────────────────────

        private void SpawnSwapVFX(Vector3 worldPos)
        {
            // Procedural burst: violet ring of particles rising from the tower base.
            var go = new GameObject("[SwapVFX]");
            go.transform.position = worldPos;
            var ps = go.AddComponent<ParticleSystem>();

            var main  = ps.main;
            main.duration        = 0.8f;
            main.loop            = false;
            main.startLifetime   = 0.6f;
            main.startSpeed      = 4f;
            main.startSize       = 0.18f;
            main.startColor      = new Color(0.55f, 0.25f, 1.0f); // violet
            main.gravityModifier = -0.3f;                          // float upward
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.SetBurst(0, new ParticleSystem.Burst(0f, 40));
            emission.rateOverTime = 0;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius    = 0.8f;

            ps.Play();
            Destroy(go, 2f);
        }

        // ── Backend log ───────────────────────────────────────────────────────

        private async UniTaskVoid LogSwapToBackendAsync(
            string fromTower, string toTower, int waveId, string currency, string txSig)
        {
            var playerId = GameStateService.Instance?.State?.BoundWallet ?? "anonymous";
            var payload = JsonConvert.SerializeObject(new
            {
                playerId,
                waveId,
                fromTower,
                toTower,
                currency,
                costUsdc = SwapCostUsdc,
                txSig,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            });

            var bodyRaw = System.Text.Encoding.UTF8.GetBytes(payload);
            using var req = new UnityWebRequest(BackendSwapLogUrl, "POST");
            req.uploadHandler   = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            try { await req.SendWebRequest(); }
            catch { /* non-critical — EventTracker already captured the event */ }
        }
    }
}
