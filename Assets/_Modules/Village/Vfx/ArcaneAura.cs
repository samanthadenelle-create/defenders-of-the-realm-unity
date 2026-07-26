// =============================================================================
// ArcaneAura - a persistent magical aura loop for arcane-type towers.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Owner 2026-07-15: "arcane towers should have an aura." This tiny self-managing
// MonoBehaviour holds ONE looping Hovl aura at the structure it is attached to -
// the same reuse pattern HealingFountain uses for its Fountain_Heal_Aura (spawn a
// loop via VFXManager.PlayKey, keep the VFXHandle, Stop() it on teardown). No new
// art is authored: it reuses the "Arcane_Aura" catalog key (a looping magic circle,
// HovlVfxCatalogGenerator Map).
//
// COLORBLIND-SAFE (owner is red/green colorblind): the aura reads by MOTION +
// LUMINANCE (a slow rotating rune ring at the tower base), NOT by hue - the violet
// tint is only a hint. So it never encodes meaning in colour alone.
//
// ATTACH: added in code by the arcane-tower spawn paths -
//   - ArcaneTower.cs (the combat spire behaviour)
//   - StructureFactory GameplayBuilding case "arcane-tower" (catalog/BaseLayout landmark)
//   - HubStructureVisualInjector arcane swap (the baked hub landmark)
// Each call site guards with GetComponentInChildren<ArcaneAura>() so a tower never
// gets two auras.
//
// Null-safe throughout: VFXManager.PlayKey no-ops (returns null) when the manager or
// the "Arcane_Aura" catalog row is not ready yet, so this compiles/runs regardless -
// the aura simply appears once the catalog row is authored (regen the Hovl catalog:
// Defenders/VFX/Generate Hovl VFX Catalog -> HOVL_VFX_CATALOG_OK).
// =============================================================================

using System.Collections;        // tier idle-pulse coroutine (WO tower-vfx escalation)
using UnityEngine;
using DeNelle.Core.Combat;       // IDamageableStructure - owner-liveness orphan guard (Village -> Core, section 5)
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>Holds one looping arcane aura VFX at the tower it is attached to.
    /// Spawns on enable, stops on disable/destroy (loop-handle lifecycle).</summary>
    [DisallowMultipleComponent]
    public sealed class ArcaneAura : MonoBehaviour
    {
        [Tooltip("Hovl catalog loop key for the aura. Reused looping magic circle; " +
                 "PlayKey no-ops if the row is not authored yet.")]
        [SerializeField] private string _auraKey = "Arcane_Aura";

        [Tooltip("Metres above the tower origin to seat the aura ring.")]
        [SerializeField] private float _height = 0.4f;

        [Tooltip("Uniform scale for the aura loop (0 = catalog DefaultScale).")]
        [SerializeField] private float _scale = 2.2f;

        // HDR arcane violet - a HINT only. The aura's meaning is carried by MOTION +
        // LUMINANCE (rotating ring), never by this hue (owner colorblind).
        [SerializeField] private Color _tint = new Color(0.6f, 0.4f, 1f, 1f);

        private VFXHandle _handle;
        private bool _started;

        // ── Tier escalation (owner felt-test 2026-07-17: "more/better VFX at higher tower
        // levels" — upgrading must FEEL rewarding). The idle aura visibly ESCALATES with the
        // tower's level: a bigger ring at L2, and a bigger ring PLUS a slow rising idle PULSE
        // at L3. COLORBLIND-SAFE (owner red/green): escalation reads by SIZE + MOTION + an extra
        // luminous layer, NEVER by hue. Driven by ApplyLevel(level), called on placement +
        // upgrade from Tower.Upgrade / StructureFactory.ReskinForLevel via the EscalateTo seam.
        //
        // Clean level->VFX table IN CODE (not JSON): the tower-vfx config does not currently
        // live in data, so per the owner directive a well-commented in-code tier table is the
        // right call (avoids a new loader + Data/Canonical dual-copy risk). L1 keeps the current
        // serialized baseline look. Extra idle layers are ONE-SHOTS (cap-40, auto-return) not new
        // loops, so a wall of L3 towers can never blow the global loop cap (20).
        private int _level = 1;
        private Coroutine _pulseCo;

        private readonly struct Tier
        {
            public readonly float  AuraScale;      // uniform scale of the held aura loop
            public readonly float  PulseInterval;  // seconds between idle pulses (0 = none)
            public readonly string PulseKey;       // Hovl one-shot key for the idle pulse
            public readonly float  PulseScale;     // scale of the idle pulse one-shot
            public Tier(float auraScale, float pulseInterval, string pulseKey, float pulseScale)
            {
                AuraScale = auraScale; PulseInterval = pulseInterval;
                PulseKey = pulseKey; PulseScale = pulseScale;
            }
        }

        /// <summary>Per-level idle-aura recipe. L1 ~= the serialized baseline (2.2) so a freshly
        /// placed tower is unchanged; L2 grows the ring; L3 grows it further AND adds a slow rising
        /// pulse one-shot (LevelUp_Burst) so a maxed tower reads as dramatically empowered.</summary>
        private static Tier TierFor(int level) => level switch
        {
            >= 3 => new Tier(3.8f, 3.5f, "LevelUp_Burst", 1.2f),
            2    => new Tier(3.0f, 0f,   null,            0f),
            _    => new Tier(2.2f, 0f,   null,            0f),
        };

        // ORPHAN GUARD (F8 owner felt-test 2026-07-15 "i see a vfx but no tower, maybe
        // destroyed?"): the aura is a POOLED Hovl loop parented to the tower. OnDisable/
        // OnDestroy cover the DESTROY + DISABLE death paths, but a tower that BREAKS to an
        // inoperable shell keeps its root ACTIVE (no lifecycle event fires), and a body that
        // failed to spawn / rendered invisible leaves the ring seated over nothing. There is
        // NO Unity lifecycle callback for either, so a throttled owner-liveness + visible-body
        // self-check is the catch-all that guarantees the aura can never outlive the tower.
        private const float OwnerCheckInterval = 0.5f;   // throttle for the self-check
        private const float FirstCheckGrace    = 1.5f;   // let the body model spawn / re-skin first
        private IDamageableStructure _owner;             // null for a pure landmark (no HP model)
        private float _checkTimer;
        private bool  _bodyConfirmed;                    // a visible body was seen at least once

        private void Start()
        {
            _started = true;
            _owner = GetComponentInParent<IDamageableStructure>();
            _checkTimer = FirstCheckGrace;
            StartAura();
        }

        private void OnEnable()
        {
            // Re-acquire on re-enable (only after the first Start so we do not spawn
            // before the transform is seated). Idempotent via the handle guard.
            if (_started) { StartAura(); RestartPulse(TierFor(_level)); }
        }

        // Immediate stop on every lifecycle teardown: the pooled loop returns to the pool
        // NOW (no 2.5s graceful strand that could linger as a detached, still-playing loop).
        private void OnDisable() => StopAura(immediate: true);
        private void OnDestroy() => StopAura(immediate: true);

        private void Update()
        {
            // Only a live handle can be orphaned; skip the walk entirely otherwise.
            if (_handle == null || !_handle.IsAlive) return;

            _checkTimer -= Time.deltaTime;
            if (_checkTimer > 0f) return;
            _checkTimer = OwnerCheckInterval;

            // The tower is a broken/dead shell (root still active, so no OnDisable/OnDestroy)?
            bool ownerDead = _owner != null && !_owner.IsAlive;
            // The body mesh never spawned / is disabled (ring seated over nothing)? Cache the
            // first positive so a healthy tower stops paying for the renderer walk.
            if (!_bodyConfirmed) _bodyConfirmed = HasVisibleBody();
            bool noBody = !_bodyConfirmed;

            if (ownerDead || noBody)
            {
                // Section 12 smoking gun: Fail lands in the errors-only break-log. This single
                // line disambiguates the cause on the next capture: ownerDead => broken-shell
                // not torn down; noVisibleBody => body failed to spawn / invisible; owner absent
                // => pure landmark whose body vanished.
                FlowTrace.Fail("ArcaneAura",
                    $"'{name}' ORPHAN aura STOPPED: aura loop was playing with no live tower body " +
                    $"(ownerDead={ownerDead}, ownerPresent={_owner != null}, noVisibleBody={noBody}) - " +
                    "matches F8 'i see a vfx but no tower'.");
                StopAura(immediate: true);
            }
        }

        /// <summary>True when a non-particle body renderer (the tower mesh) is live under this
        /// structure. ParticleSystemRenderers are excluded so the aura's OWN VFX (and any other
        /// effect) never counts as the body.</summary>
        private bool HasVisibleBody()
        {
            var rends = GetComponentsInChildren<Renderer>(false);
            for (int i = 0; i < rends.Length; i++)
            {
                var r = rends[i];
                if (r == null || r is ParticleSystemRenderer) continue;
                if (r.enabled && r.gameObject.activeInHierarchy) return true;
            }
            return false;
        }

        private void StartAura()
        {
            if (_handle != null) return;   // already holding the loop
            _handle = VFXManager.PlayKey(
                _auraKey,
                transform.position + Vector3.up * _height,
                Quaternion.identity,
                transform,     // parent so the aura tracks the tower
                _tint,         // HDR violet tint (a hint; motion carries the read)
                _scale);
            FlowTrace.Step("ArcaneTower",
                $"'{name}' arcane aura '{_auraKey}' " +
                (_handle != null ? "spawned (loop held)."
                                 : "no-op (VFXManager/catalog not ready or key unauthored) - aura will appear once the row exists."));
        }

        private void StopAura(bool immediate = false)
        {
            // Stop the L3 idle pulse first so it can't fire against a torn-down aura.
            if (_pulseCo != null) { StopCoroutine(_pulseCo); _pulseCo = null; }
            if (_handle == null) return;
            _handle.Stop(immediate);
            _handle = null;
        }

        /// <summary>
        /// Retarget this aura to a DIFFERENT catalog loop key (owner 2026-07-24: the node /
        /// Cathedral of Magic / combat Arcane Spire must each read as a SUBTLE, DISTINCT aura —
        /// they used to all resolve to the one "Magic circle sun loop" prefab via Arcane_Aura/
        /// Poi_NodeAura). Idempotent (no-op when the key is unchanged); if a loop is already held
        /// it is restarted so the new key takes effect live. Colorblind-safe: each key still reads
        /// by MOTION + LUMINANCE, never hue. These are SWAPPABLE DEFAULTS — the owner may retag any
        /// surface in the VFX Caster later and regen the catalog.
        /// </summary>
        public void SetAuraKey(string auraKey)
        {
            if (string.IsNullOrEmpty(auraKey) || auraKey == _auraKey) return;
            _auraKey = auraKey;
            if (_handle != null) { StopAura(immediate: true); StartAura(); }   // live retarget
        }

        // ── Tier escalation API ───────────────────────────────────────────────

        /// <summary>
        /// Set the tower's VFX tier (1..3) and re-apply the idle aura to match: the held aura
        /// loop is restarted at the tier's scale, and the L3 idle pulse is (re)armed. Called on
        /// placement + on each upgrade via <see cref="EscalateTo"/>. Idempotent; safe to call
        /// before Start (Start's StartAura then picks up the tier scale). Reads by SIZE + MOTION,
        /// colorblind-safe (§7 owner red/green).
        /// </summary>
        public void ApplyLevel(int level)
        {
            _level = Mathf.Clamp(level, 1, 3);
            var tier = TierFor(_level);
            _scale = tier.AuraScale;

            // Live restart at the new scale ONLY when a loop is currently held (a live tower).
            // If none is held we leave StartAura (via Start/OnEnable) to spawn it at _scale, and
            // never respawn over a stopped/orphaned shell.
            if (_handle != null) { StopAura(immediate: true); StartAura(); }

            RestartPulse(tier);

            FlowTrace.Step("TowerVfx",
                $"'{name}' idle aura level={_level} aura='{_auraKey}' scale={_scale:0.0} " +
                $"pulse={(tier.PulseInterval > 0f && !string.IsNullOrEmpty(tier.PulseKey) ? $"'{tier.PulseKey}'@{tier.PulseInterval:0.0}s" : "none")}");
        }

        /// <summary>The single seam the tower upgrade/placement paths call to escalate a tower's
        /// idle aura to <paramref name="level"/>. When <paramref name="ensure"/> is true and the
        /// tower has no aura yet, one is attached first (mage/arcane/wizard/spire towers); otherwise
        /// only an aura that already exists is escalated. Null-safe (no-op on a null root or a tower
        /// with no aura when ensure is false).</summary>
        public static void EscalateTo(GameObject root, int level, bool ensure)
        {
            if (root == null) return;
            var aura = root.GetComponentInChildren<ArcaneAura>(true);
            if (aura == null)
            {
                if (!ensure) return;
                Ensure(root);
                aura = root.GetComponentInChildren<ArcaneAura>(true);
            }
            aura?.ApplyLevel(level);
        }

        // (Re)arm the L3 idle pulse coroutine. Stops any existing pulse first; starts a new one
        // only when the tier defines a pulse and this component is live.
        private void RestartPulse(in Tier tier)
        {
            if (_pulseCo != null) { StopCoroutine(_pulseCo); _pulseCo = null; }
            if (tier.PulseInterval > 0f && !string.IsNullOrEmpty(tier.PulseKey) && isActiveAndEnabled)
                _pulseCo = StartCoroutine(PulseLoop(tier.PulseInterval, tier.PulseKey, tier.PulseScale));
        }

        // A slow rising idle pulse (L3 only) — a ONE-SHOT (cap-safe, auto-returns) fired every
        // PulseInterval so a maxed tower reads as continuously empowered. Guarded so a bad key
        // logs + skips (never blanks/kills the tower). Skips while the aura loop is not alive.
        private IEnumerator PulseLoop(float interval, string key, float scale)
        {
            var wait = new WaitForSeconds(interval);
            while (true)
            {
                yield return wait;
                if (_handle == null || !_handle.IsAlive) continue;   // aura gone -> no pulse
                Guard.Try("TowerVfx", $"idle pulse '{key}'", () =>
                    VFXManager.PlayKey(key, transform.position + Vector3.up * _height,
                                       Quaternion.identity, null, _tint, scale));
            }
        }

        /// <summary>
        /// External teardown (structure-death cleanup, owner felt-test 2026-07-15:
        /// "tower was destroyed ... but the vfx ... still exist"). Because the tower
        /// goes to a broken SHELL on death (no Destroy/disable of the root), this
        /// component's OnDisable/OnDestroy never fire and the aura loop would keep
        /// running. The break observer calls this to Stop the loop and disable the
        /// component so OnEnable cannot re-acquire it over a dead shell. Re-enable the
        /// component (on repair) to bring the aura back.
        /// </summary>
        public void StopAndDisable()
        {
            StopAura(immediate: true);
            enabled = false;
        }

        /// <summary>Attach an <see cref="ArcaneAura"/> to <paramref name="root"/> once
        /// (idempotent - skips if one already lives in the hierarchy). The single seam the
        /// arcane-tower spawn paths call so the aura wiring stays in one place.</summary>
        public static void Ensure(GameObject root)
        {
            if (root == null) return;
            if (root.GetComponentInChildren<ArcaneAura>(true) != null) return;
            root.AddComponent<ArcaneAura>();
        }

        /// <summary>
        /// Ensure an <see cref="ArcaneAura"/> on <paramref name="root"/> AND pin it to a specific
        /// catalog loop <paramref name="auraKey"/> — the seam each arcane SURFACE uses to declare
        /// its own subtle, DISTINCT aura (owner 2026-07-24). Authoritative on the key: if an aura
        /// already exists (e.g. a default one the tier-escalation path Ensured first) its key is
        /// UPDATED to <paramref name="auraKey"/>, so the surface-specific call always wins
        /// regardless of call order. Null-/empty-safe. Current assignments:
        ///   - combat Arcane Spire (ArcaneTower.Awake) .......... "Aura_HeartPulse"   (gentle pulse)
        ///   - Cathedral of Magic (StructureFactory arcane-tower,
        ///     HubStructureVisualInjector ArcaneTower_MagicUpgrades) "Fountain_Heal_Aura" (soft shimmer)
        /// (Harvest NODES use their own key in PoiCalloutSystem, not this component.)
        /// </summary>
        public static void Ensure(GameObject root, string auraKey)
        {
            if (root == null) return;
            var aura = root.GetComponentInChildren<ArcaneAura>(true);
            if (aura == null) aura = root.AddComponent<ArcaneAura>();
            aura.SetAuraKey(auraKey);
        }
    }
}
