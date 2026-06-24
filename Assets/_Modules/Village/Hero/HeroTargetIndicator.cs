// =============================================================================
// HeroTargetIndicator — a camera-facing reticle billboard over the hero's
// current target, with manual target cycling, so open-world combat is readable
// and controllable (see what you'll hit, and switch which enemy that is).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Pairs with the open-world targeting fix (Enemy auto-carries the EnemyDamageable
// IDamageable adapter). Each scan it gathers the alive Hostile IDamageables in
// range (the same detection the hero's melee/ability sweeps use) and parks a ring
// billboard over the current target's head.
//
// TARGET LOCK / CYCLE: by default the reticle tracks the NEAREST hostile. CYCLE to
// the next hostile in range with the right shoulder (gamepad) or RIGHT-CLICK (desktop,
// WO-497); that manual lock turns the reticle red and is fed to
// HeroAbilities.AimPointOverride so ranged spells aim at it. WO-497 also adds a DIRECT
// pick: TAP (mobile) or LEFT-click an enemy to lock THAT enemy (raycast on the Enemy
// layer), bypassing the AUTO forward-arc/LoS gates like a manual cycle; a tap on empty
// space clears the lock back to auto-nearest. The lock auto-clears (back to nearest)
// when the target dies or leaves range. Self-installed on the hero by HeroControlEnsurer
// — no scene edit, no art asset (ring drawn at runtime).
//
// The transparent-material setup mirrors PetDeployer.BuildSpriteBillboard, which
// is proven to render in WebGL builds (URP/Unlit transparent, double-sided).
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using DeNelle.Core.Combat;

namespace DeNelle.Village
{
    /// <summary>
    /// Shows a camera-facing reticle over the hero's current hostile target and
    /// lets the player cycle targets. Attach to the hero root (HeroControlEnsurer
    /// does this automatically).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeroTargetIndicator : MonoBehaviour
    {
        // 2026-06-02: the registry (TargetManager) makes the lock range FREE to widen
        // (no physics-buffer overflow), so push it well past the open-world spawn ring
        // so roaming mobs in OuterWorld lock from a comfortable distance.
        // DEF-269 (owner playtest): 70m let the hero snipe-from-distance and made the
        // flee-and-spam exploit reach far. Dialed back to 45m (~36% shorter) so combat
        // is up-close and committed. Manual Tab-cycle still reaches anything in this range.
        [Tooltip("Radius (m) within which hostiles can be targeted.")]
        [SerializeField, Min(1f)] private float _acquireRange = 45f;

        // DEF-269: forward-arc facing gate for AUTO target acquisition. The hero may only
        // auto-acquire/auto-fire on a hostile roughly IN FRONT of them (Vector3.Dot of the
        // hero's forward vs the to-target direction > this cos threshold). 0.35 ≈ within a
        // ~70° half-angle (~140° total arc) of facing. Kills "flee and spam a target behind
        // you" — run away and the engagement ends. A MANUAL Tab/shoulder lock bypasses this
        // (deliberate player intent to hold a specific foe). Null-safe: degrades to "always
        // in front" if the hero transform has no meaningful forward.
        [Tooltip("Forward-arc dot threshold for AUTO targeting. 0.35 ≈ within ~70° of the " +
                 "hero's facing. Higher = tighter front cone; manual Tab-lock ignores this.")]
        [SerializeField, Range(-1f, 1f)] private float _facingDot = 0.35f;

        [Tooltip("Seconds between target re-scans (the reticle follows every frame).")]
        [SerializeField, Min(0.02f)] private float _scanInterval = 0.12f;

        // WO-449: line-of-sight gate. The hero could lock + attack THROUGH a wall because
        // acquisition was range + forward-arc only (no LoS). This mask names the blockers
        // that should occlude targeting — wall/structure/Default layers — NOT the Enemy or
        // Player layers (or the target's own collider would block itself). When unset
        // (value == 0) the LoS gate degrades OFF so a misconfigured mask never blanks ALL
        // targets (see HasLoS / the call-site degrade guards).
        [Tooltip("WO-449: layers that BLOCK line-of-sight to a target (walls/structures/" +
                 "Default). Do NOT include Enemy or Player. Leave empty to disable the LoS gate.")]
        [SerializeField] private LayerMask _losMask;

        [Tooltip("Height (m) above the target's position to float the reticle.")]
        [SerializeField] private float _headHeight = 2.2f;

        // DEF (owner playtest 2026-06-05): 1.6m read as a "giant circle" over mobs.
        // Dialed to 0.8m for a small, focused reticle that pinpoints the target
        // instead of haloing it. Runtime-attached (HeroControlEnsurer), so the code
        // default is what ships — no prefab override to chase.
        [Tooltip("Reticle world size (m).")]
        [SerializeField, Min(0.1f)] private float _size = 0.5f;  // 2026-06-06: smaller, focused target

        [Tooltip("Reticle tint when auto-tracking the nearest hostile.")]
        [SerializeField] private Color _autoTint = new Color(1f, 0.88f, 0.30f, 0.95f);

        [Tooltip("Reticle tint when the player has manually locked a target.")]
        [SerializeField] private Color _lockTint = new Color(1f, 0.32f, 0.28f, 0.98f);

        /// <summary>The hostile the hero is currently targeting (locked or nearest), or null.</summary>
        public IDamageable CurrentTarget { get; private set; }

        // ── WO-512 lock-owner API (THE single lock owner) ────────────────────────
        // The HUD (BattleHud9Zone) and BattleArena route ALL lock intent through these thin
        // wrappers over the existing _locked/CycleTarget/ClearLock internals, so there is one
        // owner of the manual lock + the per-frame AimPointOverride/LockedTarget writes (collapses
        // the old two-owner bug where the HUD wrote aim fields the indicator overwrote next frame).
        // These change NO behavior on their own — they just expose the existing lock as a clean API.

        /// <summary>True when a soft lock-on is engaged (the player/engage held a specific foe), false in
        /// free-look / auto-nearest. Mirror of "_locked != null engaged via the lock API".</summary>
        public bool LockEngaged { get; private set; }

        /// <summary>The locked enemy when <see cref="LockEngaged"/>, else null. Reads CurrentTarget so a
        /// dropped lock (target died / left range) reports null on the next LateUpdate.</summary>
        public IDamageable LockedEnemyTarget => LockEngaged ? CurrentTarget : null;

        /// <summary>
        /// Engage the soft lock-on onto a specific target (or the nearest candidate when null).
        /// Idempotent: engaging while already locked just re-points. Sets the same _locked the manual
        /// Tab/tap lock uses, so the reticle reds + abilities aim at it through the existing per-frame
        /// writes. No camera/facing change (slices 2-3).
        /// </summary>
        public void EngageLock(IDamageable target = null)
        {
            if (target == null)
            {
                // Pick the nearest hostile from a fresh scan (CurrentTarget may be stale this frame).
                RebuildCandidates();
                target = _candidates.Count > 0 ? _candidates[0] : (_locked ?? CurrentTarget);
            }
            if (target == null || !target.IsAlive) return;   // nothing to lock — stay auto-nearest
            _locked = target;
            LockEngaged = true;
            CurrentTarget = target;   // reflect immediately so reticle/HUD don't lag a frame
            var mb = target as MonoBehaviour;
            string nm = mb != null ? mb.gameObject.name.Replace("(Clone)", "").Trim() : "target";
            DeNelle.Core.Diagnostics.FlowTrace.Step("BattleArena", "LOCKON engage target='" + nm + "'.");
        }

        /// <summary>
        /// Release the soft lock-on back to auto-nearest (free-look). Reuses ClearLock internals (drops
        /// _locked + the aim override + the pinned bar) and leaves the reticle in auto/gold; the next
        /// LateUpdate re-acquires the nearest hostile from scratch.
        /// </summary>
        public void ReleaseLock()
        {
            LockEngaged = false;
            ClearLock();   // drops _locked, aim override, prev-target bar; reticle reverts to auto/gold
            DeNelle.Core.Diagnostics.FlowTrace.Step("BattleArena", "LOCKON release -> free-look.");
        }

        /// <summary>
        /// Switch the locked target in the existing nearest-first cycle order (dir reserved for a future
        /// reverse step; current cycle is forward). Engages the lock if it wasn't already.
        /// </summary>
        public void CycleLock(int dir)
        {
            CycleTarget();          // reuse the existing nearest-first cycle ordering
            LockEngaged = _locked != null;
            var t = _locked;
            var mb = t as MonoBehaviour;
            string nm = mb != null ? mb.gameObject.name.Replace("(Clone)", "").Trim() : "none";
            DeNelle.Core.Diagnostics.FlowTrace.Step("BattleArena", "LOCKON switch -> '" + nm + "'.");
        }

        /// <summary>
        /// Clear any MANUAL target lock (revert to auto-nearest) and drop the current target +
        /// its aim override. Called by BattleArena.Resolve on a loss so the hero doesn't return
        /// to the open world still locked onto a dead/stale foe (which would keep aiming abilities
        /// at nothing). Safe to call any time; the next LateUpdate re-acquires from scratch.
        /// </summary>
        public void ClearLock()
        {
            _locked = null;
            LockEngaged = false;   // WO-512: any clear path (Resolve loss, tap-empty) also drops the lock-on flag
            CurrentTarget = null;
            if (_abilities == null) _abilities = GetComponent<HeroAbilities>();
            if (_abilities != null) { _abilities.AimPointOverride = null; _abilities.LockedTarget = null; }
            // Release the previous target's pinned HP bar so it isn't left revealed.
            SetBarTargeted(_prevTarget, false);
            _prevTarget = null;
            SetVisible(false);
        }

        private Transform _reticle;
        private Material _reticleMat;
        private Camera _cam;
        private HeroAbilities _abilities;
        private float _nextScan;

        private IDamageable _locked;   // manual lock (null = auto-nearest)
        private IDamageable _prevTarget;  // DEF-206: last frame's CurrentTarget, to flip HP bars on change
        private readonly List<IDamageable> _candidates = new List<IDamageable>();
        private readonly List<Enemy> _enemyBuf = new List<Enemy>(64);   // TargetManager scratch

        // 2026-06-02 targeting fix: the scan used mask ~0 into a 64-slot buffer, but the
        // village has ~2,900 colliders — within 32 m the buffer FILLED with walls/ground
        // and the enemy collider was crowded OUT (OverlapSphere result order is arbitrary),
        // so the reticle never acquired even with a foe right there. Attacks still landed in
        // waves because HeroAbilities sweeps the ENEMY layer only (no overflow). Mask to the
        // Enemy layer here too + a roomier buffer so only enemy colliders come back.
        private static readonly Collider[] _hits = new Collider[128];
        private static Texture2D _ringTex;
        private int _enemyMask;

        private void Awake()
        {
            BuildReticle();
            _abilities = GetComponent<HeroAbilities>();
            _enemyMask = LayerMask.GetMask("Enemy");
            if (_enemyMask == 0) _enemyMask = ~0;   // layer undefined → fall back to all

            // WO-449: ACTIVATE the LoS gate out-of-the-box. The castle wall geometry is built onto
            // the dedicated "Structure" layer (CastleWallsFromRecipe + CastleHubBuilder.BuildInnerWallRing),
            // so a linecast masked to it is blocked by walls but NOT by the hero/ground (Default) or
            // enemies (Enemy) — those stay off the mask so the hero/target never self-block. We only
            // seed the default when unset in the inspector; if "Structure" doesn't exist GetMask
            // returns 0 and HasLoS's degrade rule (value == 0 → LoS clear) keeps targeting safe.
            if (_losMask.value == 0) _losMask = LayerMask.GetMask("Structure");
        }

        private void OnDestroy()
        {
            if (_reticle != null) Destroy(_reticle.gameObject);
            // Don't leave a stale aim override on the abilities component.
            if (_abilities != null) { _abilities.AimPointOverride = null; _abilities.LockedTarget = null; }
            // DEF-206: release the last target's HP-bar flag so it isn't pinned on.
            SetBarTargeted(_prevTarget, false);
            _prevTarget = null;
        }

        private void Update()
        {
            // WO-497: TAP (mobile) / LEFT-tap-on-enemy direct lock takes priority — a tap ON an
            // enemy collider locks THAT enemy directly (bypasses the AUTO forward-arc/LoS gates,
            // like a manual cycle); a tap on EMPTY clears back to auto-nearest.
            if (TapOrClickThisFrame(out Vector2 screenPos))
            {
                if (TryLockAtScreenPoint(screenPos)) return;   // hit an enemy → direct lock done
                // tap on empty space → clear manual lock (revert to auto-nearest)
                _locked = null;
                LockEngaged = false;   // WO-512: keep the lock-on flag honest on a tap-to-clear
                return;
            }

            // WO-497: RIGHT-CLICK (desktop) cycles/switches the locked target, additive to the
            // existing Tab/right-shoulder cycle (CyclePressed). WO-512: a manual cycle engages lock.
            if (CyclePressed()) { CycleTarget(); LockEngaged = _locked != null; }
        }

        private void LateUpdate()
        {
            if (Time.time >= _nextScan)
            {
                _nextScan = Time.time + _scanInterval;
                RebuildCandidates();
            }

            // Drop a manual lock that died or wandered out of range.
            if (_locked != null && (!_locked.IsAlive || !_candidates.Contains(_locked)))
                _locked = null;

            CurrentTarget = _locked ?? NearestCandidate();

            // AUTO-LOCK DRIVES AIM: feed the CURRENT target (auto-nearest OR manual lock)
            // to the ability aim so what the reticle shows is exactly what your spells hit.
            // (Previously only a manual Tab-lock overrode aim; auto-nearest fell back to
            // HeroAbilities' own pick, which could disagree with the reticle.)
            if (_abilities == null) _abilities = GetComponent<HeroAbilities>();
            if (_abilities != null)
            {
                _abilities.AimPointOverride = CurrentTarget != null ? (Vector3?)CurrentTarget.WorldPosition : null;
                // Hand the ability the EXACT locked foe so single-target hits damage what
                // the ring shows (not whatever an OverlapSphere happens to find).
                _abilities.LockedTarget = CurrentTarget;
            }

            // DEF-206: keep the CURRENT target's floating HP bar revealed (the
            // "engage to reveal" rule). When the target changes, release the old
            // one (it lingers a few seconds then fades) and reveal the new one.
            if (!ReferenceEquals(CurrentTarget, _prevTarget))
            {
                SetBarTargeted(_prevTarget, false);
                SetBarTargeted(CurrentTarget, true);
                _prevTarget = CurrentTarget;
            }

            if (CurrentTarget == null || !CurrentTarget.IsAlive)
            {
                SetVisible(false);
                return;
            }

            SetVisible(true);
            if (_cam == null || !_cam.isActiveAndEnabled) _cam = ResolveCamera();
            if (_reticleMat != null)
            {
                Color want = _locked != null ? _lockTint : _autoTint;
                if (_reticleMat.HasProperty("_BaseColor")) _reticleMat.SetColor("_BaseColor", want);
                _reticleMat.color = want;
            }

            // The reticle quad is a STANDALONE scene object (not parented to the hero), so a
            // scene change that CARRIES the hero (HeroControlEnsurer DDOL across a Single-load
            // seam, e.g. OuterWorld->Village2) destroys the quad while THIS component survives on
            // the carried hero — leaving _reticle a destroyed reference. Setting .position on it
            // then NRE-spams every frame once a target is in range. Rebuild on demand.
            if (_reticle == null) BuildReticle();
            if (_reticle == null) { SetVisible(false); return; }

            Vector3 p = CurrentTarget.WorldPosition + Vector3.up * _headHeight;
            _reticle.position = p;
            if (_cam != null)
            {
                // FULL billboard (face the camera on all axes). A yaw-only quad reads
                // edge-on — i.e. INVISIBLE — from the close 3D third-person cam looking
                // down. The quad's visible face is -Z, so point +Z away from the camera.
                Vector3 away = p - _cam.transform.position;
                if (away.sqrMagnitude > 0.0001f)
                    _reticle.rotation = Quaternion.LookRotation(away.normalized, Vector3.up);
            }
        }

        // Robust camera lookup: Camera.main only finds a "MainCamera"-tagged camera, which
        // isn't guaranteed — fall back to the SmartMobileCamera rig, then any active camera,
        // so the reticle billboard is never left un-oriented (edge-on quad = invisible).
        private static Camera ResolveCamera()
        {
            var c = Camera.main;
            if (c != null) return c;
            var smc = SmartMobileCamera.Instance;
            if (smc != null) { var cc = smc.GetComponent<Camera>(); if (cc != null) return cc; }
            return Object.FindFirstObjectByType<Camera>();
        }

        // ── Targeting ─────────────────────────────────────────────────────────

        private bool CyclePressed()
        {
            // Mobile-first: the keyboard Tab target-cycle is REMOVED. Target cycling is
            // reached by the gamepad right shoulder (below); on touch the reticle auto-
            // tracks the nearest hostile.
            var gp = Gamepad.current;
            if (gp != null && gp.rightShoulder.wasPressedThisFrame) return true;

            // WO-497: RIGHT-CLICK (desktop) ALSO cycles/switches the locked target — additive
            // to the gamepad shoulder. (A right-click does not carry a useful "what was under
            // the cursor" intent here; it just advances the cycle, matching Tab/shoulder.)
            var mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.wasPressedThisFrame) return true;
            return false;
        }

        // WO-497: was there a TAP (touch) or LEFT mouse click this frame? Returns the screen
        // point so the caller can raycast it into the world for a direct enemy lock. Touch wins
        // over mouse when both report (a touch device that also exposes a synthetic mouse).
        private static bool TapOrClickThisFrame(out Vector2 screenPos)
        {
            screenPos = default;
            var ts = Touchscreen.current;
            if (ts != null && ts.primaryTouch.press.wasPressedThisFrame)
            {
                screenPos = ts.primaryTouch.position.ReadValue();
                return true;
            }
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                screenPos = mouse.position.ReadValue();
                return true;
            }
            return false;
        }

        // WO-497: raycast a screen point into the world and, if it strikes an alive Hostile
        // IDamageable, set it as the MANUAL lock directly (bypassing the AUTO forward-arc/LoS
        // gates, like a Tab/shoulder cycle). Returns true when an enemy was locked. The raycast
        // uses the Enemy layer mask so terrain/walls between the camera and the foe don't eat the
        // pick; the ray length is bounded by the acquire range (no sniping off-screen mobs).
        private bool TryLockAtScreenPoint(Vector2 screenPos)
        {
            if (_cam == null || !_cam.isActiveAndEnabled) _cam = ResolveCamera();
            if (_cam == null) return false;

            Ray ray = _cam.ScreenPointToRay(screenPos);
            // Pick on the Enemy layer only — a wide pick range so a tap anywhere on the foe's
            // body locks it; bound it generously past the acquire range for camera distance.
            float maxDist = _acquireRange * 2f + 50f;
            if (!Physics.Raycast(ray, out RaycastHit hit, maxDist, _enemyMask, QueryTriggerInteraction.Collide))
                return false;

            var d = hit.collider != null ? hit.collider.GetComponentInParent<IDamageable>() : null;
            if (d == null || !d.IsAlive || d.Faction != CombatFaction.Hostile) return false;

            _locked = d;   // direct manual lock — bypasses AUTO arc/LoS, like Tab
            LockEngaged = true;   // WO-512: a direct tap-lock engages the lock-on flag
            return true;
        }

        private void CycleTarget()
        {
            // WO-449: RebuildCandidates() now applies the additive LoS gate, so the cycle
            // list only contains hostiles with a clear line-of-sight — a manual Tab/shoulder
            // cycle can no longer lock a target through a wall either.
            RebuildCandidates();
            if (_candidates.Count == 0) { _locked = null; return; }
            int idx = _locked != null ? _candidates.IndexOf(_locked) : -1;
            idx = (idx + 1) % _candidates.Count;
            _locked = _candidates[idx];
        }

        private void RebuildCandidates()
        {
            _candidates.Clear();

            // UNION of TWO sources so a nearby enemy is found whether or not it is in the
            // registry (2026-06-02: a mob that spawned BEFORE an editor hot-reload never
            // ran the new OnEnable→Register, so it's missing from the registry; the old
            // code early-returned on the registry and skipped the sweep that would catch
            // it). Both are cheap with the Enemy-layer mask, so always run both + dedup.

            // Source 1 — the cross-scene registry (overflow-proof, catches registered mobs).
            var tm = TargetManager.Instance;
            if (tm != null)
            {
                tm.CollectInRange(transform.position, _acquireRange, _enemyBuf);
                for (int i = 0; i < _enemyBuf.Count; i++)
                {
                    var e = _enemyBuf[i];
                    if (e == null) continue;
                    // Robust lookup (expert 2026-06-02): root first, then parent chain.
                    var d = e.GetComponent<IDamageable>();
                    if (d == null) d = e.GetComponentInParent<IDamageable>();
                    if (d == null || !d.IsAlive || d.Faction != CombatFaction.Hostile) continue;
                    if (!HasLoS(d)) continue;   // WO-449: additive LoS gate — no targeting through walls
                    if (!_candidates.Contains(d)) _candidates.Add(d);
                }
            }

            // Source 2 — Enemy-layer physics sweep (catches anything not registered, plus
            // non-Enemy hostiles). Few enemy colliders in range, so the 128 buffer is safe.
            int n = Physics.OverlapSphereNonAlloc(
                transform.position, _acquireRange, _hits, _enemyMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var c = _hits[i];
                if (c == null) continue;
                var d = c.GetComponentInParent<IDamageable>();
                if (d == null || !d.IsAlive || d.Faction != CombatFaction.Hostile) continue;
                if (!HasLoS(d)) continue;   // WO-449: additive LoS gate — no targeting through walls
                if (!_candidates.Contains(d)) _candidates.Add(d);
            }

            // Stable nearest-first order so Tab cycles outward predictably.
            Vector3 me = transform.position;
            _candidates.Sort((a, b) =>
                (a.WorldPosition - me).sqrMagnitude.CompareTo((b.WorldPosition - me).sqrMagnitude));
        }

        // DEF-269: the AUTO target is the nearest candidate the hero is FACING. _candidates
        // is sorted nearest-first, so walk it and return the first one inside the forward arc.
        // Returns null when every hostile in range is behind the hero — so running away ends
        // the engagement instead of letting the hero spam attacks at a target at their back.
        // (Manual Tab-locks are applied in Update/LateUpdate before this is ever consulted.)
        private IDamageable NearestCandidate()
        {
            Vector3 me = transform.position;
            Vector3 fwd = transform.forward;
            fwd.y = 0f;
            // Degenerate forward (hero hasn't oriented yet) → don't gate, fall back to nearest.
            bool gate = fwd.sqrMagnitude > 0.0001f;
            if (gate) fwd.Normalize();

            for (int i = 0; i < _candidates.Count; i++)
            {
                var cand = _candidates[i];
                if (cand == null || !cand.IsAlive) continue;
                if (!gate) return cand;   // can't determine facing → nearest wins
                Vector3 to = cand.WorldPosition - me;
                to.y = 0f;
                if (to.sqrMagnitude < 0.0001f) return cand;   // on top of the hero → in range
                if (Vector3.Dot(fwd, to.normalized) >= _facingDot) return cand;
            }
            return null;
        }

        // WO-449: true when the hero has a clear line-of-sight to the target (no wall/
        // structure between them) — so the hero can't lock + cast THROUGH a wall. We
        // linecast from an eye point on the hero to a TORSO point on the target (not feet)
        // so the ground/terrain doesn't false-block a target on a slope or a curb.
        // DEGRADE: an unset mask (value == 0) means "no blockers configured" → treat LoS as
        // always clear (fall back to range + arc only) so a misconfig never blanks targeting.
        private bool HasLoS(IDamageable target)
        {
            if (target == null) return false;
            if (_losMask.value == 0) return true;   // degrade: LoS gate disabled
            Vector3 eye = transform.position + Vector3.up * 1.4f;
            Vector3 torso = target.WorldPosition + Vector3.up * 1.0f;
            // Clear when the linecast hits NOTHING between eye and torso.
            return !Physics.Linecast(eye, torso, _losMask, QueryTriggerInteraction.Ignore);
        }

        private void SetVisible(bool on)
        {
            if (_reticle != null && _reticle.gameObject.activeSelf != on)
                _reticle.gameObject.SetActive(on);
        }

        // DEF-206: flag the target's floating HP bar as the player's current target
        // so it stays revealed while locked (and lingers when released). The
        // IDamageable impl (EnemyDamageable) is a MonoBehaviour on the Enemy root,
        // which is where FloatingHealthBar lives — resolve it via .gameObject.
        private static void SetBarTargeted(IDamageable target, bool targeted)
        {
            var mb = target as MonoBehaviour;
            if (mb == null) return;
            FloatingHealthBar.SetTargetedOn(mb.gameObject, targeted);
        }

        // ── Visual build (no art asset; runtime ring texture + transparent quad) ──

        private void BuildReticle()
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "HeroTargetReticle";
            var col = quad.GetComponent<Collider>();
            if (col != null) Destroy(col);
            quad.transform.localScale = new Vector3(_size, _size, _size);

            var mr = quad.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                                ?? Shader.Find("Sprites/Default")
                                ?? Shader.Find("Unlit/Transparent");
                if (shader != null)
                {
                    var mat = new Material(shader);
                    Texture2D ring = RingTexture();
                    if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", ring);
                    if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", ring);
                    mat.mainTexture = ring;
                    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", _autoTint);
                    mat.color = _autoTint;

                    // Transparent blend — mirrors PetDeployer.BuildSpriteBillboard (WebGL-safe).
                    if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
                    if (mat.HasProperty("_Blend"))   mat.SetFloat("_Blend", 0f);
                    if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    if (mat.HasProperty("_ZWrite"))   mat.SetInt("_ZWrite", 0);
                    if (mat.HasProperty("_Cull"))     mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    mat.EnableKeyword("_ALPHABLEND_ON");
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    mr.sharedMaterial = mat;
                    _reticleMat = mr.material;   // instance we recolour per lock state
                }
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }

            _reticle = quad.transform;
            _reticle.gameObject.SetActive(false);
        }

        /// <summary>A cached 64×64 soft ring (target-bracket) texture, drawn once.</summary>
        private static Texture2D RingTexture()
        {
            if (_ringTex != null) return _ringTex;

            const int S = 64;
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float c = (S - 1) * 0.5f;
            // Small "target" reticle: a THIN outer ring + a solid centre pip — reads as a
            // crosshair/target that pinpoints the foe, not a big filled halo.
            const float ringOuter = 30f, ringInner = 27f;  // thin rim band
            const float dotRadius = 6f;                     // centre pip
            var clear = new Color(1f, 1f, 1f, 0f);
            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    float dx = x - c, dy = y - c;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float ring = Mathf.InverseLerp(ringOuter, ringOuter - 2f, r) * Mathf.InverseLerp(ringInner, ringInner + 2f, r);
                    float dot  = Mathf.InverseLerp(dotRadius, dotRadius - 2f, r);
                    float a = Mathf.Clamp01(Mathf.Max(ring, dot));
                    t.SetPixel(x, y, a > 0f ? new Color(1f, 1f, 1f, a) : clear);
                }
            }
            t.Apply();
            _ringTex = t;
            return t;
        }
    }
}
