// =============================================================================
// CrystalMineNode — the world Crystal Mine (WO-153): the persistent, renewable,
// upgradeable, region-graded crystal faucet of the outer world.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// RECONCILIATION (CLAUDE.md §9 — do NOT build a parallel node system):
//   • This is a THIN companion that REQUIRES a sibling MineNode (WO-142/WO-159) set
//     to Resource = AetherCrystal. MineNode already owns: banking into GameState
//     (BankYield), the per-extract region-danger bonus (EffectiveYield), the
//     cooldown + renewable respawn loop, depletion, and the worker/offline/settlement
//     seams (TryAutoExtract / ForceAutoExtract / DrainReserve / RatePerSecond).
//     CrystalMineNode adds ONLY the three "mine specifics" WO-153 calls for, on top:
//        (1) REGION-GRADED yield   — stamps the node with the region's CrystalGrade
//                                     (WO-144 CrystalRegion.TopGradeFor) for UI + the
//                                     forward-compatible grade dimension.
//        (2) RENEWABLE framing     — the mine is the *reliable* faucet: it forces the
//                                     legacy renewable mode (cooldown-respawn), NOT the
//                                     finite-reserve deplete-and-despawn model, so it
//                                     refills over time and never vanishes.
//        (3) UPGRADE TIERS         — passive yield/capacity scaling paid via
//                                     CrystalEconomy.TrySpend (salvages the old village
//                                     CrystalMine tier+prompt UX; code-built, no UXML).
//   • Banking stays MineNode's single path — this component NEVER writes the wallet
//     directly for harvest; it only bumps MineNode's public tuning fields on upgrade.
//   • Grade banks through the one AetherCrystals total today (CrystalEconomy). The
//     per-grade ledger is WO-144's job; this exposes Grade so WO-144 plugs in later.
//
// Assembly discipline: Village -> Core only. Region/grade reads come from
// DeNelle.Core.World (ZoneManager / CrystalRegion). Wallet spend goes through
// CrystalEconomy (DeNelle.Village). No HUD/Audio hard ref; cross-module via ?..
//
// Placement: attached by the world builder (OuterWorldBuilder) onto a crystal
// MineNode, OR — later — by build-mode (WO-108) when the player places a mine. The
// builder sets nothing here; sensible defaults derive the grade from the region.
// =============================================================================
using System.Collections;
using UnityEngine;
using DeNelle.Core.World;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// The world Crystal Mine behaviour (WO-153). Companion to a crystal
    /// <see cref="MineNode"/>: forces the renewable mode, stamps the region crystal
    /// <see cref="CrystalGrade"/>, and adds tier upgrades (paid via
    /// <see cref="CrystalEconomy"/>) that raise the sibling node's yield/capacity.
    /// Pure additive — it reuses MineNode's banking, cooldown and worker/offline seams.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MineNode))]
    public sealed class CrystalMineNode : MonoBehaviour
    {
        [Header("Renewable mine")]
        [Tooltip("Force the sibling MineNode into the legacy renewable (cooldown-respawn) " +
                 "mode at Awake. The world Crystal Mine is the RELIABLE faucet — a refilling " +
                 "vein that never despawns — NOT a finite reserve. Leave on unless a designer " +
                 "deliberately wants this mine to be a one-shot reserve.")]
        public bool ForceRenewable = true;

        [Header("Upgrade tiers (salvaged from the village CrystalMine pattern)")]
        [Tooltip("Mine tier, 1..MaxTier. Each tier raises the sibling node's yield-per-extract " +
                 "and total capacity (a richer, faster vein). Tier 1 = the as-placed yield.")]
        [Min(1)] public int Tier = 1;

        [Tooltip("Maximum upgrade tier.")]
        [Min(1)] public int MaxTier = 3;

        [Tooltip("Multiplier added to the node's base YieldPerExtract PER tier above 1 " +
                 "(e.g. 0.5 => +50% per tier: T1 ×1.0, T2 ×1.5, T3 ×2.0).")]
        [Min(0f)] public float YieldPerTier = 0.5f;

        [Tooltip("Multiplier added to the node's base capacity (TotalExtracts / ReserveTotal) " +
                 "PER tier above 1 — a higher tier holds more before it must refill.")]
        [Min(0f)] public float CapacityPerTier = 0.5f;

        [Tooltip("Crystal cost to upgrade FROM tier T to T+1. Index 0 = cost of T1->T2, " +
                 "index 1 = T2->T3, etc. Paid via CrystalEconomy.TrySpend. If the array is " +
                 "shorter than the tier ladder, the last entry is reused.")]
        public int[] UpgradeCosts = { 25, 60 };

        [Header("Interaction")]
        [Tooltip("How close the player must be to see the upgrade prompt / press [G].")]
        [Min(0.5f)] public float UpgradeRadius = 3.5f;

        [Tooltip("Key that upgrades the mine when in range (distinct from MineNode's [F] " +
                 "extract so the two verbs never collide on one node).")]
        public KeyCode UpgradeKey = KeyCode.G;

        // ── Runtime ──────────────────────────────────────────────────────────
        private MineNode _node;
        private CrystalGrade _grade;
        private int   _baseYield;        // YieldPerExtract as placed (tier-1 baseline)
        private int   _baseTotalExtracts;
        private int   _baseReserveTotal;
        private Transform _player;
        private bool  _inRange;
        private GameObject _prompt;
        private GameObject _feedback;     // WO-325: transient in-world "why nothing happened" bubble
        private Coroutine  _feedbackTimer;
        private float _nextCheck;
        private const float CheckInterval = 0.2f;
        private const float FeedbackSeconds = 2.0f;   // how long a feedback bubble lingers

        /// <summary>The crystal grade this mine yields, gated by its region's danger tier
        /// (WO-144). Read by UI today; by the WO-144 grade ledger later.</summary>
        public CrystalGrade Grade => _grade;

        /// <summary>Current mine tier (1..MaxTier).</summary>
        public int CurrentTier => Tier;

        /// <summary>True once the mine is at its top tier.</summary>
        public bool IsMaxTier => Tier >= MaxTier;

        private void Awake()
        {
            _node = GetComponent<MineNode>();
            if (_node == null)
            {
                Debug.LogWarning("[CrystalMineNode] No sibling MineNode — the world Crystal Mine " +
                                 "needs one (Resource=AetherCrystal). Disabling.");
                enabled = false;
                return;
            }

            // Make the mine a crystal source even if the builder forgot to set Resource.
            if (_node.Resource != MineResource.AetherCrystal)
                _node.Resource = MineResource.AetherCrystal;

            // (2) Renewable: the reliable faucet refills, it does not deplete-and-despawn.
            if (ForceRenewable && _node.UseFiniteReserve)
                _node.UseFiniteReserve = false;

            // Capture the as-placed baselines so tier scaling is idempotent (re-applying
            // a tier never compounds on an already-scaled value).
            _baseYield         = Mathf.Max(1, _node.YieldPerExtract);
            _baseTotalExtracts = _node.TotalExtracts;
            _baseReserveTotal  = _node.ReserveTotal;
        }

        private void Start()
        {
            // (1) Region-graded: stamp the grade from the node's world position danger tier.
            int tier = Mathf.Max(0, ZoneManager.DangerTierAt(transform.position));
            _grade = CrystalRegion.TopGradeFor(tier);

            ApplyTier();   // realise the current Tier onto the node's tuning fields.
            ResolvePlayer();
        }

        private void Update()
        {
            if (_node == null) return;

            if (Time.time >= _nextCheck)
            {
                _nextCheck = Time.time + CheckInterval;
                if (_player == null) ResolvePlayer();
                // Show the prompt within the WIDER of (upgrade range here, the sibling
                // MineNode's harvest range) so the bubble advertises BOTH verbs the moment
                // the player is close enough to do EITHER. Before WO-325 this used only
                // UpgradeRadius, so a player standing in harvest range (2.5m) but outside
                // upgrade range (3.5m) — or vice versa — could see no prompt at all, which
                // read as "nothing happens at the crystal node".
                float promptRange = PromptRange();
                bool nowIn = _player != null &&
                    (_player.position - transform.position).sqrMagnitude <= promptRange * promptRange;
                if (nowIn != _inRange)
                {
                    _inRange = nowIn;
                    if (_inRange) ShowPrompt();
                    else          { HidePrompt(); HideFeedback(); }
                }
            }

            if (_inRange && UnityEngine.Input.GetKeyDown(UpgradeKey))
            {
                FlowTrace.Step("CrystalMine", $"[G] upgrade interact received (tier {Tier}/{MaxTier}).");
                AttemptUpgrade();
            }
        }

        /// <summary>The radius at which the prompt bubble appears — the wider of this
        /// component's <see cref="UpgradeRadius"/> and the sibling MineNode's harvest
        /// <c>InteractRadius</c> — so the bubble shows whenever EITHER verb is available.</summary>
        private float PromptRange()
        {
            float harvest = _node != null ? _node.InteractRadius : 0f;
            return Mathf.Max(UpgradeRadius, harvest);
        }

        /// <summary>
        /// WO-325 — drive the [G] upgrade with NON-SILENT feedback for every outcome so the
        /// player never presses upgrade and sees "nothing happen". Each failure branch now
        /// raises a transient in-world bubble (reusing the prompt infra) AND a FlowTrace line,
        /// instead of the old Debug.Log-only paths that were invisible in a build.
        /// </summary>
        private void AttemptUpgrade()
        {
            if (IsMaxTier)
            {
                FlowTrace.Step("CrystalMine", "Upgrade ignored — already at max tier.");
                ShowFeedback($"✦  Already max tier ({Tier}/{MaxTier})");
                return;
            }

            var econ = CrystalEconomy.Instance;
            if (econ == null)
            {
                FlowTrace.Fail("CrystalMine", "Upgrade failed — CrystalEconomy.Instance is null.");
                ShowFeedback("⚠  Crystal wallet unavailable");
                return;
            }

            int cost = NextUpgradeCost();
            if (cost > 0 && !econ.CanAfford(cost))
            {
                FlowTrace.Warn("CrystalMine",
                    $"Upgrade can't afford — needs {cost}, have {econ.CurrentCrystals}.");
                ShowFeedback($"Need {cost} ✦  (have {econ.CurrentCrystals})");
                return;
            }

            FlowTrace.Step("CrystalMine", $"Upgrade started — spending {cost} ✦ for T{Tier}->{Tier + 1}.");
            if (TryUpgrade())
            {
                FlowTrace.Step("CrystalMine", $"Upgrade succeeded — now tier {Tier}/{MaxTier}.");
                HideFeedback();
                HidePrompt();
                ShowPrompt();   // refresh the bubble to the new tier / next cost
            }
            else
            {
                // TrySpend race / unexpected refusal — never silent.
                FlowTrace.Warn("CrystalMine", "Upgrade refused by TryUpgrade after affordability check.");
                ShowFeedback("Upgrade unavailable right now");
            }
        }

        // ── Tier scaling (re-derives node tuning from the captured baselines) ──

        /// <summary>Crystal cost to go from the current tier to the next (clamped to the
        /// last array entry if the ladder is longer than UpgradeCosts). 0 if at max.</summary>
        public int NextUpgradeCost()
        {
            if (IsMaxTier || UpgradeCosts == null || UpgradeCosts.Length == 0) return 0;
            int idx = Mathf.Clamp(Tier - 1, 0, UpgradeCosts.Length - 1);
            return Mathf.Max(0, UpgradeCosts[idx]);
        }

        /// <summary>
        /// Attempt to upgrade one tier, spending crystals via <see cref="CrystalEconomy"/>.
        /// Returns false at max tier or if the wallet can't afford it. On success it bumps
        /// the sibling MineNode's yield + capacity (the only mutation this component makes
        /// to the node) — banking itself stays MineNode's single path.
        /// </summary>
        public bool TryUpgrade()
        {
            if (IsMaxTier) { Debug.Log("[CrystalMineNode] Already at max tier."); return false; }

            int cost = NextUpgradeCost();
            var econ = CrystalEconomy.Instance;
            if (econ == null)
            {
                Debug.LogWarning("[CrystalMineNode] CrystalEconomy not found — cannot upgrade.");
                return false;
            }
            if (cost > 0 && !econ.TrySpend(cost))
            {
                Debug.Log($"[CrystalMineNode] Upgrade needs {cost} crystals — have {econ.CurrentCrystals}.");
                return false;
            }

            Tier++;
            ApplyTier();
            Debug.Log($"[CrystalMineNode] Upgraded to tier {Tier}/{MaxTier} " +
                      $"(grade {_grade}, yield now {_node.YieldPerExtract}).");
            return true;
        }

        /// <summary>Realise the current Tier onto the node's tuning fields from the
        /// captured baselines. Idempotent — always derives from base, never compounds.</summary>
        private void ApplyTier()
        {
            if (_node == null) return;
            int steps = Mathf.Max(0, Tier - 1);

            _node.YieldPerExtract = Mathf.Max(1, Mathf.RoundToInt(_baseYield * (1f + YieldPerTier * steps)));

            if (_baseTotalExtracts > 0)
                _node.TotalExtracts = Mathf.Max(1, Mathf.RoundToInt(_baseTotalExtracts * (1f + CapacityPerTier * steps)));
            if (_baseReserveTotal > 0)
                _node.ReserveTotal = Mathf.Max(1, Mathf.RoundToInt(_baseReserveTotal * (1f + CapacityPerTier * steps)));
        }

        // ── Player resolve + prompt (reuses the Village prompt-bubble pattern) ──

        private void ResolvePlayer()
        {
            var p = GameObject.FindWithTag("Player");
            _player = p != null ? p.transform : null;
        }

        private void ShowPrompt()
        {
            // WO-325 — advertise BOTH verbs. The sibling MineNode owns the [F] HARVEST
            // (extract crystals) and this component owns the [G] UPGRADE. The old prompt
            // showed ONLY the upgrade line, so a player at a crystal node never learned
            // they could harvest at all — it read as "nothing happens". Lead with harvest
            // (the primary verb) and append the upgrade affordance.
            string upgrade = IsMaxTier
                ? $"Mine maxed (T{Tier})"
                : $"〔 {UpgradeKey} 〕 Upgrade  T{Tier}->{Tier + 1} ({NextUpgradeCost()} ✦)";
            string label = $"〔 F / Tap 〕 Harvest {_grade} ✦      {upgrade}";
            _prompt = BuildBubble(label);
        }

        private void HidePrompt()
        {
            if (_prompt != null) Destroy(_prompt);
            _prompt = null;
        }

        // ── WO-325 transient feedback bubble (NON-SILENT failure responses) ────
        // Reuses the SAME BuildBubble world-space label the prompt uses (no new UI infra,
        // no UXML) but auto-dismisses after FeedbackSeconds. Every upgrade-failure branch
        // raises one of these so a press never produces a silent no-op (§12: no silent
        // failures). Fully self-contained — defines its own timer coroutine; calls only
        // BuildBubble (defined below) + Unity primitives.

        /// <summary>Raise a short-lived in-world message above the mine (e.g. "Need 25 ✦").
        /// Replaces any prior feedback bubble and auto-hides after <see cref="FeedbackSeconds"/>.</summary>
        private void ShowFeedback(string text)
        {
            HideFeedback();
            _feedback = BuildBubble(text);
            // Float it slightly above the standing prompt so the two never overlap.
            if (_feedback != null)
                _feedback.transform.localPosition = Vector3.up * 3.7f;
            if (isActiveAndEnabled)
                _feedbackTimer = StartCoroutine(FeedbackTimeout());
        }

        private IEnumerator FeedbackTimeout()
        {
            yield return new WaitForSeconds(FeedbackSeconds);
            _feedbackTimer = null;
            HideFeedback();
        }

        private void HideFeedback()
        {
            if (_feedbackTimer != null)
            {
                StopCoroutine(_feedbackTimer);
                _feedbackTimer = null;
            }
            if (_feedback != null) Destroy(_feedback);
            _feedback = null;
        }

        private void OnDisable()
        {
            // Coroutines stop on disable, but the bubble GameObjects must be cleaned up
            // explicitly so a disabled node leaves no orphaned world labels.
            HidePrompt();
            HideFeedback();
        }

        // A minimal world-space label above the mine — same shape as the village
        // CrystalMine bubble, reusing PromptBillboard (DeNelle.Village). Pack-agnostic,
        // no UXML (PIPELINE_STATE.md §8).
        private GameObject BuildBubble(string text)
        {
            var go = new GameObject("CrystalMinePrompt");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * 3.0f;

            float charsApprox = Mathf.Max(text.Length, 8);
            float w = Mathf.Clamp(charsApprox * 0.10f + 0.4f, 1.2f, 4.5f);
            float h = 0.38f;
            var bgColor      = new Color(0.06f, 0.02f, 0.14f, 0.96f);
            var outlineColor = new Color(0.65f, 0.45f, 1.00f);

            var outline = GameObject.CreatePrimitive(PrimitiveType.Quad);
            DestroyImmediate(outline.GetComponent<Collider>());
            outline.transform.SetParent(go.transform, false);
            outline.transform.localPosition = new Vector3(0f, 0f, 0.012f);
            outline.transform.localScale    = new Vector3(w + 0.06f, h + 0.06f, 1f);
            ApplyFlat(outline.GetComponent<Renderer>(), outlineColor);

            var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            DestroyImmediate(bg.GetComponent<Collider>());
            bg.transform.SetParent(go.transform, false);
            bg.transform.localPosition = new Vector3(0f, 0f, 0.006f);
            bg.transform.localScale    = new Vector3(w, h, 1f);
            ApplyFlat(bg.GetComponent<Renderer>(), bgColor);

            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(go.transform, false);
            txtGo.transform.localScale = Vector3.one * 0.055f;
            var tm = txtGo.AddComponent<TextMesh>();
            tm.text = text; tm.fontSize = 96; tm.characterSize = 0.30f;
            tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
            tm.color = new Color(0.95f, 0.90f, 1.00f);

            // PromptBillboard lives in DeNelle.Village (BuildingInteractable.cs) — keep
            // the bubble facing the camera, same as the village CrystalMine prompt.
            var billboard = go.AddComponent<PromptBillboard>();
            billboard.Camera = Camera.main;
            return go;
        }

        private static void ApplyFlat(Renderer renderer, Color colour)
        {
            if (renderer == null) return;
            // WO-395a — magenta-prompt fix. The upgrade bubble ("[G] Upgrade Mine T1->2")
            // rendered MAGENTA in builds: Shader.Find("Universal Render Pipeline/Unlit")
            // (and "Unlit/Color") return NULL in a player/WebGL build because those URP/
            // built-in shaders aren't in the Always-Included list (no project material
            // references them). The old code then `return`ed early, leaving the Quad
            // primitive on Unity's DEFAULT material (legacy Standard) which URP cannot
            // render → the pink error shader. The robust seam used by the sibling world
            // prompts (TownsfolkBubble / BuildingInteractable) ends the chain in
            // "Sprites/Default" — a shader Unity ALWAYS ships in the build — so the quad
            // never falls back to the magenta default. Match that pattern here.
            Shader s = Shader.Find("Universal Render Pipeline/Unlit")
                       ?? Shader.Find("Unlit/Color")
                       ?? Shader.Find("Sprites/Default");
            if (s == null) return;
            var mat = new Material(s) { color = colour };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", colour);
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color",     colour);
            renderer.sharedMaterial = mat;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.65f, 0.45f, 1.0f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, UpgradeRadius);
        }
#endif
    }
}
