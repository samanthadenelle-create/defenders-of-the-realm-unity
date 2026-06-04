// =============================================================================
// PatriciaLightMode — the real-time "Defend the Tower" breach shooter (WO-47).
// -----------------------------------------------------------------------------
// PHASE 1 vertical slice (WO-47 part B). When the player picks "Defend the Tower"
// at a Heart breach (BreachChoiceOverlay), this self-managed MonoBehaviour:
//   • repositions Camera.main to a tower-defense view looking at the Heart, and
//     restores it on exit;
//   • builds a CODE HUD (Tower Integrity bar + status label + title) on a borrowed
//     PanelSettings UIDocument (WO-47 constraint 1: no UXML);
//   • runs a SHORT assault (~1 wave, ~8 enemies) spawning real Enemy instances via
//     WaveManager.SpawnEnemyForExternalMode from the LEFT and RIGHT of the Heart;
//   • auto-fires the hero at the nearest hostile IDamageable on a cooldown — via
//     HeroAbilities.TryCast when a hero rig is present, else a direct OverlapSphere
//     → TakeDamage + DamageAttribution.Record (mirrors Pet.Attack);
//   • drains Tower Integrity (HeartController.Hp) when an enemy reaches the Heart;
//   • WIN when all spawned enemies are dead → grants a Wisdom bonus and returns to
//     the village; LOSE when integrity hits 0 → returns to the village.
//
// PHASES 2-3 (portrait lanes, full 5-wave + boss, pets attack/repair, special
// abilities, juice/VFX) are deliberately NOT built here — see WORK_ORDER_47.
//
// HARD CONSTRAINTS honoured: code-built HUD only; lives in DeNelle.Village (no new
// asmdef); no .unity scene edited (everything is runtime); enemy damage routes
// through IDamageable.TakeDamage + DamageAttribution.Record so kills feed XP.
// =============================================================================

using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DeNelle.Core;
using DeNelle.Core.Combat;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.Village
{
    /// <summary>
    /// The Phase-1 Defend-the-Tower shooter: a short in-village assault on the
    /// Heart with a code HUD, an auto-firing hero, a Tower Integrity bar, and a
    /// win/lose payout that returns to the village. Self-managed — created and
    /// owned by its own GameObject via <see cref="Begin"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PatriciaLightMode : MonoBehaviour
    {
        // ── Phase-1 tuning (kept conservative; full ramp is Phase 3) ──────────
        private const int   EnemyCount        = 8;       // ~1 short wave
        private const float SpawnInterval     = 0.9f;    // seconds between spawns
        private const float SpawnSideOffset   = 14f;     // metres left/right of the Heart
        private const float SpawnDepthJitter  = 3.5f;    // ± spread along the approach
        private const float HeartHitDamage    = 12f;     // integrity lost per enemy that reaches the Heart
        private const float HeroFireCooldown  = 0.45f;   // spam-friendly auto-fire cadence
        private const float HeroFireRange     = 80f;     // OverlapSphere reach for the direct-fire fallback
        private const float HeroFireDamage    = 16f;     // direct-fire fallback damage per shot
        private const int   WisdomReward      = 3;        // Phase-1 win bonus
        private const string EnemyDefId       = "hollow-walker"; // the canonical wave-1 enemy

        // Tower-defense camera framing (relative to the Heart).
        private static readonly Vector3 CamOffset = new Vector3(0f, 16f, -20f);

        // ── State ─────────────────────────────────────────────────────────────
        private WaveManager _waves;
        private HeartController _heart;
        private int _waveId;

        private EnemyCatalog _enemyCatalog;
        private EnemyDef _enemyDef;

        private readonly List<Enemy> _liveEnemies = new List<Enemy>();
        private int _spawnedTotal;
        private int _killedTotal;
        private bool _running;
        private bool _resolved;
        private float _fireCooldown;
        private int _idCounter;

        // Auto-fire: prefer the hero's own kit; fall back to a direct sweep.
        private HeroAbilities _hero;
        private Transform _heroTransform;
        private readonly Collider[] _overlap = new Collider[64];

        // Camera cache (restored on exit even though we reload the scene — keeps
        // the village visually intact if a later phase runs without a reload).
        private Camera _cam;
        private Vector3 _camPosCache;
        private Quaternion _camRotCache;
        private bool _camCached;

        // ── HUD ───────────────────────────────────────────────────────────────
        private UIDocument _hudDoc;
        private VisualElement _integrityFill;   // height-driven vertical bar
        private Label _integrityLabel;
        private Label _statusLabel;

        /// <summary>
        /// Starts the Phase-1 Defend-the-Tower assault in the current (village)
        /// scene. Creates its own GameObject so it survives the caller. No-op if a
        /// mode is already running.
        /// </summary>
        /// <param name="waves">The village WaveManager (enemy prefab + catalog + spawn path).</param>
        /// <param name="heart">The Heart — the tower whose integrity is defended.</param>
        /// <param name="waveId">The breached wave ordinal (status display + ids).</param>
        public static PatriciaLightMode Begin(WaveManager waves, HeartController heart, int waveId)
        {
            if (FindAnyObjectByType<PatriciaLightMode>() != null)
            {
                Debug.LogWarning("[PatriciaLight] A Defend-the-Tower mode is already running — ignoring Begin().");
                return null;
            }

            var go = new GameObject("PatriciaLightMode");
            var mode = go.AddComponent<PatriciaLightMode>();
            mode._waves = waves;
            mode._heart = heart != null ? heart : (waves != null ? waves.Heart : FindAnyObjectByType<HeartController>());
            mode._waveId = waveId;
            mode.Run().Forget();
            return mode;
        }

        // =====================================================================
        //  Lifecycle
        // =====================================================================

        private async UniTask Run()
        {
            if (_heart == null)
            {
                Debug.LogError("[PatriciaLight] No Heart — cannot defend the tower. Returning to village.");
                ReturnToVillage();
                return;
            }

            // Resolve the enemy def from the SAME catalog the wave loop uses.
            if (_waves != null) _enemyCatalog = await _waves.GetEnemyCatalogAsync();
            if (_enemyCatalog == null) _enemyCatalog = await WaveDataLoader.LoadEnemiesAsync();
            _enemyDef = _enemyCatalog != null ? _enemyCatalog.Find(EnemyDefId) : null;
            if (_enemyDef == null && _enemyCatalog != null && _enemyCatalog.Enemies != null
                && _enemyCatalog.Enemies.Count > 0)
            {
                _enemyDef = _enemyCatalog.Enemies[0];   // any enemy beats stalling the breach
            }
            if (_enemyDef == null)
            {
                Debug.LogError("[PatriciaLight] No enemy def available — returning to village.");
                ReturnToVillage();
                return;
            }

            ResolveHero();
            FrameCamera();
            BuildHud();

            _heart.SetState(HeartState.Critical);
            SetStatus($"Wave {_waveId} — Defend the Tower!");
            RefreshIntegrity();

            _running = true;
            SpawnAssault().Forget();
        }

        private void Update()
        {
            if (!_running) return;

            // Prune dead / destroyed enemies and tally kills.
            for (int i = _liveEnemies.Count - 1; i >= 0; i--)
            {
                Enemy e = _liveEnemies[i];
                if (e == null || e.IsDead)
                {
                    _liveEnemies.RemoveAt(i);
                    _killedTotal++;
                }
            }

            TickHeroFire();

            // LOSE — tower integrity drained.
            if (_heart != null && _heart.Hp <= 0f)
            {
                Lose();
                return;
            }

            // WIN — every spawned enemy is dead and no more are coming.
            if (_spawnedTotal >= EnemyCount && _liveEnemies.Count == 0)
            {
                Win();
            }
        }

        // =====================================================================
        //  Assault — spawn left/right of the Heart via the wave loop's path
        // =====================================================================

        private async UniTask SpawnAssault()
        {
            Vector3 heartPos = _heart.transform.position;
            Transform heartT = _heart.transform;

            for (int i = 0; i < EnemyCount; i++)
            {
                if (!_running) return;

                // Alternate LEFT / RIGHT of the Heart, with a little depth jitter.
                float side = (i % 2 == 0) ? -1f : 1f;
                float depth = Random.Range(-SpawnDepthJitter, SpawnDepthJitter);
                Vector3 spawnPos = heartPos
                    + new Vector3(side * SpawnSideOffset, 0f, depth);

                string id = $"pl-w{_waveId}-{_enemyDef.Id}-{_idCounter++}";
                Enemy enemy = _waves != null
                    ? _waves.SpawnEnemyForExternalMode(_enemyDef, spawnPos, heartT, id)
                    : null;

                if (enemy != null)
                {
                    enemy.Died += HandleEnemyDied;
                    enemy.ReachedHeart += HandleEnemyReachedHeart;
                    _liveEnemies.Add(enemy);
                    _spawnedTotal++;
                }
                else
                {
                    // Spawn failed (no WaveManager) — still count it so the win
                    // condition can resolve rather than waiting forever.
                    _spawnedTotal++;
                }

                SetStatus($"Wave {_waveId} — {_spawnedTotal}/{EnemyCount} incoming");

                if (i < EnemyCount - 1)
                    await UniTask.Delay(System.TimeSpan.FromSeconds(SpawnInterval));
            }
        }

        private void HandleEnemyDied(Enemy enemy)
        {
            if (enemy != null)
            {
                enemy.Died -= HandleEnemyDied;
                enemy.ReachedHeart -= HandleEnemyReachedHeart;
            }
            _liveEnemies.Remove(enemy);
        }

        /// <summary>
        /// An enemy reached the Heart — drain tower integrity and despawn it so it
        /// does not keep ticking damage (Phase 1: one hit per arrival).
        /// </summary>
        private void HandleEnemyReachedHeart(Enemy enemy)
        {
            if (!_running || _heart == null) return;

            _heart.SetHp(_heart.Hp - HeartHitDamage);
            RefreshIntegrity();

            if (enemy != null)
            {
                enemy.Died -= HandleEnemyDied;
                enemy.ReachedHeart -= HandleEnemyReachedHeart;
                _liveEnemies.Remove(enemy);
                // Force-remove (no kill XP — it breached, it wasn't slain).
                enemy.Kill();
            }
        }

        // =====================================================================
        //  Hero auto-fire — TryCast when a hero is present, else a direct sweep
        // =====================================================================

        private void ResolveHero()
        {
            _hero = FindAnyObjectByType<HeroAbilities>();
            _heroTransform = _hero != null ? _hero.transform : null;
            if (_heroTransform == null)
            {
                // No hero rig in the scene — fire from the Heart itself so the
                // tower still defends (Phase 1 keeps the breach winnable).
                _heroTransform = _heart != null ? _heart.transform : transform;
            }
        }

        private void TickHeroFire()
        {
            _fireCooldown -= Time.deltaTime;
            if (_fireCooldown > 0f) return;
            _fireCooldown = HeroFireCooldown;

            // Prefer the hero's own primary slot — it animates, scales with the
            // talent tree + level multiplier, and routes damage + attribution
            // itself (HeroAbilities.ResolveEffect). TryCast returns false when on
            // cooldown / out of mana; then fall through to the direct sweep so the
            // tower keeps firing on the spammy cadence.
            if (_hero != null && _hero.TryCast(AbilitySlot.Q)) return;

            DirectFireNearest();
        }

        /// <summary>
        /// Fallback shot: nearest hostile IDamageable via OverlapSphere, damaged +
        /// attributed exactly like Pet.Attack so the kill feeds the XP system.
        /// </summary>
        private void DirectFireNearest()
        {
            Vector3 origin = _heroTransform != null ? _heroTransform.position : transform.position;
            int count = Physics.OverlapSphereNonAlloc(
                origin, HeroFireRange, _overlap, ~0, QueryTriggerInteraction.Collide);

            IDamageable best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var col = _overlap[i];
                if (col == null) continue;
                var dmg = col.GetComponentInParent<IDamageable>();
                if (dmg == null || !dmg.IsAlive || dmg.Faction != CombatFaction.Hostile) continue;
                float sqr = (dmg.WorldPosition - origin).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = dmg; }
            }

            if (best == null) return;
            best.TakeDamage(HeroFireDamage, DamageElement.None);
            DamageAttribution.Record(best, HeroProgression.Id, HeroFireDamage);
        }

        // =====================================================================
        //  Camera
        // =====================================================================

        private void FrameCamera()
        {
            _cam = Camera.main;
            if (_cam == null) return;

            _camPosCache = _cam.transform.position;
            _camRotCache = _cam.transform.rotation;
            _camCached = true;

            Vector3 heartPos = _heart.transform.position;
            _cam.transform.position = heartPos + CamOffset;
            _cam.transform.LookAt(heartPos + Vector3.up * 4f);
        }

        private void RestoreCamera()
        {
            if (_camCached && _cam != null)
                _cam.transform.SetPositionAndRotation(_camPosCache, _camRotCache);
        }

        // =====================================================================
        //  HUD — code-built, borrowed PanelSettings (WO-47 constraint 1)
        // =====================================================================

        private void BuildHud()
        {
            PanelSettings ps = null;
            float topSort = float.MinValue;
            foreach (var d in FindObjectsByType<UIDocument>(FindObjectsInactive.Include))
            {
                if (d == null || d.panelSettings == null) continue;
                if (d.sortingOrder >= topSort) { topSort = d.sortingOrder; ps = d.panelSettings; }
            }
            if (ps == null)
            {
                Debug.LogWarning("[PatriciaLight] No PanelSettings to host the HUD — running without it.");
                return;
            }

            var go = new GameObject("PatriciaLightHud");
            go.transform.SetParent(transform, false);
            go.SetActive(false);
            _hudDoc = go.AddComponent<UIDocument>();
            _hudDoc.panelSettings = ps;
            _hudDoc.sortingOrder = topSort + 60;
            go.SetActive(true);

            VisualElement root = _hudDoc.rootVisualElement;
            if (root == null) return;
            root.Clear();
            root.pickingMode = PickingMode.Ignore;

            // Title — top-centre.
            var title = new Label("DEFEND THE TOWER");
            title.style.position = Position.Absolute;
            title.style.top = 16f; title.style.left = 0f; title.style.right = 0f;
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            title.style.color = new StyleColor(new Color(0.97f, 0.92f, 0.74f, 1f));
            title.style.fontSize = 22f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            root.Add(title);

            // Status line under the title.
            _statusLabel = new Label(string.Empty);
            _statusLabel.style.position = Position.Absolute;
            _statusLabel.style.top = 46f; _statusLabel.style.left = 0f; _statusLabel.style.right = 0f;
            _statusLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _statusLabel.style.color = new StyleColor(Color.white);
            _statusLabel.style.fontSize = 14f;
            root.Add(_statusLabel);

            // Vertical Tower Integrity bar — left edge, mid-height.
            var barFrame = new VisualElement();
            barFrame.style.position = Position.Absolute;
            barFrame.style.left = 18f; barFrame.style.top = 90f;
            barFrame.style.width = 30f; barFrame.style.height = 240f;
            barFrame.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.55f));
            barFrame.style.justifyContent = Justify.FlexEnd;   // fill grows from the bottom
            barFrame.style.borderTopLeftRadius = 6f; barFrame.style.borderTopRightRadius = 6f;
            barFrame.style.borderBottomLeftRadius = 6f; barFrame.style.borderBottomRightRadius = 6f;
            root.Add(barFrame);

            _integrityFill = new VisualElement();
            _integrityFill.style.width = Length.Percent(100f);
            _integrityFill.style.height = Length.Percent(100f);
            _integrityFill.style.backgroundColor = new StyleColor(new Color(0.30f, 0.85f, 0.40f, 1f));
            _integrityFill.style.borderBottomLeftRadius = 6f; _integrityFill.style.borderBottomRightRadius = 6f;
            barFrame.Add(_integrityFill);

            _integrityLabel = new Label("100");
            _integrityLabel.style.position = Position.Absolute;
            _integrityLabel.style.left = 14f; _integrityLabel.style.top = 334f;
            _integrityLabel.style.width = 60f;
            _integrityLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _integrityLabel.style.color = new StyleColor(new Color(0.85f, 0.90f, 0.85f, 1f));
            _integrityLabel.style.fontSize = 13f;
            _integrityLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            root.Add(_integrityLabel);

            var caption = new Label("INTEGRITY");
            caption.style.position = Position.Absolute;
            caption.style.left = 6f; caption.style.top = 352f;
            caption.style.width = 76f;
            caption.style.unityTextAlign = TextAnchor.MiddleCenter;
            caption.style.color = new StyleColor(new Color(0.70f, 0.75f, 0.72f, 1f));
            caption.style.fontSize = 9f;
            root.Add(caption);
        }

        private void RefreshIntegrity()
        {
            if (_heart == null) return;
            float frac = Mathf.Clamp01(_heart.Hp / 100f);
            if (_integrityFill != null)
            {
                _integrityFill.style.height = Length.Percent(frac * 100f);
                // Green → amber → red as the tower weakens.
                Color c = frac > 0.5f
                    ? Color.Lerp(new Color(0.95f, 0.75f, 0.20f), new Color(0.30f, 0.85f, 0.40f), (frac - 0.5f) / 0.5f)
                    : Color.Lerp(new Color(0.86f, 0.27f, 0.27f), new Color(0.95f, 0.75f, 0.20f), frac / 0.5f);
                _integrityFill.style.backgroundColor = new StyleColor(c);
            }
            if (_integrityLabel != null)
                _integrityLabel.text = Mathf.RoundToInt(_heart.Hp).ToString();
        }

        private void SetStatus(string text)
        {
            if (_statusLabel != null) _statusLabel.text = text;
        }

        // =====================================================================
        //  Win / lose
        // =====================================================================

        private void Win()
        {
            if (_resolved) return;
            _resolved = true;
            _running = false;

            // Wisdom bonus (the future XP/quest payout layers on in Phase 3).
            var wisdom = DeNelle.Village.Talents.WisdomCurrencyService.Instance;
            if (wisdom != null) wisdom.Grant(WisdomReward);

            SetStatus($"Tower held! +{WisdomReward} Wisdom");
            Debug.Log($"[PatriciaLight] WIN — {_killedTotal} enemies repelled, +{WisdomReward} Wisdom. Returning to village.");
            FinishAndReturn().Forget();
        }

        private void Lose()
        {
            if (_resolved) return;
            _resolved = true;
            _running = false;

            SetStatus("The tower has fallen…");
            Debug.Log("[PatriciaLight] LOSE — tower integrity reached 0. Returning to village.");
            FinishAndReturn().Forget();
        }

        private async UniTask FinishAndReturn()
        {
            // Brief beat so the win/lose banner reads before the reload.
            await UniTask.Delay(System.TimeSpan.FromSeconds(1.4f));
            ReturnToVillage();
        }

        private void ReturnToVillage()
        {
            Cleanup();
            // Reload the village — a fresh WaveManager.Start() re-runs the loop,
            // exactly like the ATB return path. SceneRouter.GoVillage is the same
            // entry the ATB battle uses to come home.
            SceneRouter.GoVillage();
        }

        private void Cleanup()
        {
            // Drop event subs + any enemies we still own.
            foreach (Enemy e in _liveEnemies)
            {
                if (e == null) continue;
                e.Died -= HandleEnemyDied;
                e.ReachedHeart -= HandleEnemyReachedHeart;
                e.Kill();
            }
            _liveEnemies.Clear();

            RestoreCamera();

            if (_hudDoc != null) Destroy(_hudDoc.gameObject);
        }

        private void OnDestroy()
        {
            // Defensive: if we were torn down without a clean finish, restore the
            // camera so the village isn't left framed on the tower.
            RestoreCamera();
        }
    }
}
