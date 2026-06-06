// =============================================================================
// StructureAttackAlert — "this structure is UNDER ATTACK right now" indicator.
// -----------------------------------------------------------------------------
// Owner 2026-05-27: a structure being attacked only showed the amber "repairable"
// marker (RepairHighlight) — "a rotating yellow square doesn't scream we're being
// attacked." This adds a dedicated, urgent cue (owner picked "alert icon + hit-
// flash"):
//   • a red flash of light at the structure on EACH hit (it visibly flashes red),
//   • a billboarded red "!" icon that bobs above the structure while it's under
//     attack, auto-hiding ~2.5 s after the last hit.
//
// Detection is a per-frame HP poll (Building.Hp dropped since last frame = a hit) —
// no coupling to a specific damage event. The flash is a transient Light, NOT a
// material tint, so it never fights TowerDamageVisuals' emissive damage states.
//
// Auto-attached to every Building by the installer below (runtime, no scene edit,
// mirrors the project's other bootstraps). DeNelle.Village only.
// =============================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>Per-structure "under attack" cue: red hit-flash + a bobbing red "!" icon.</summary>
    [DisallowMultipleComponent]
    public sealed class StructureAttackAlert : MonoBehaviour
    {
        private const float AttackWindow = 2.5f;   // icon stays this long after the last hit
        private const float FlashPeak    = 4.0f;   // flash-light intensity on a hit
        private const float FlashFade    = 6.0f;   // intensity units/sec decay

        private static readonly Color AlertRed = new Color(0.95f, 0.16f, 0.12f);

        private Building  _building;
        private Transform _icon;            // billboarded parent (card + "!")
        private Light     _flash;
        private Camera    _cam;

        private float _prevHp;
        private float _lastHitTime = -999f;
        private float _flashIntensity;
        private float _bobPhase;
        private float _topY = 3f;           // world height to float the icon at

        private void Awake()
        {
            _building = GetComponent<Building>();
        }

        private void Start()
        {
            if (_building == null) { enabled = false; return; }
            _prevHp = _building.Hp;
            _topY   = ComputeTopWorldY() + 1.2f;
            BuildFlashLight();
            BuildIcon();
            SetIconActive(false);
        }

        private void Update()
        {
            if (_building == null) return;

            // ── Hit detection — HP fell since last frame ──────────────────────
            float hp = _building.Hp;
            if (hp < _prevHp - 0.01f) OnHit();
            _prevHp = hp;

            // ── Flash light decay ─────────────────────────────────────────────
            if (_flashIntensity > 0f)
            {
                _flashIntensity = Mathf.Max(0f, _flashIntensity - FlashFade * Time.deltaTime);
                if (_flash != null)
                {
                    _flash.intensity = _flashIntensity;
                    if (_flash.enabled != _flashIntensity > 0.01f)
                        _flash.enabled = _flashIntensity > 0.01f;
                }
            }

            // ── Icon — visible only while recently hit ────────────────────────
            bool underAttack = Time.time - _lastHitTime < AttackWindow;
            SetIconActive(underAttack);
            if (underAttack && _icon != null)
            {
                _bobPhase += Time.deltaTime * 4.5f;
                float bob   = Mathf.Sin(_bobPhase) * 0.18f;
                float pulse = 1f + Mathf.Sin(_bobPhase * 1.3f) * 0.12f;

                _icon.position   = transform.position + new Vector3(0f, _topY + bob, 0f);
                _icon.localScale = Vector3.one * pulse;

                // Billboard — face the camera (resolve lazily; Camera.main can be late).
                if (_cam == null) _cam = Camera.main;
                if (_cam != null)
                    _icon.rotation = Quaternion.LookRotation(_icon.position - _cam.transform.position);
            }
        }

        // Global cooldown so a swarm hitting MANY structures blasts ONE warning horn,
        // not a wall of overlapping horns — reads as an alarm, not spam.
        private static float s_lastHornTime = -999f;
        private const float HornCooldown = 8f;

        private void OnHit()
        {
            _lastHitTime    = Time.time;
            _flashIntensity = FlashPeak;
            if (_flash != null) { _flash.enabled = true; _flash.intensity = FlashPeak; }

            // "Structure under attack" warning — the lookout horn, globally debounced.
            if (Time.time - s_lastHornTime >= HornCooldown)
            {
                s_lastHornTime = Time.time;
                GameSfx.PlayLookoutHorn();
            }
        }

        // ── Build the transient red flash light ───────────────────────────────

        private void BuildFlashLight()
        {
            var go = new GameObject("AttackFlashLight");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, Mathf.Max(1f, _topY * 0.4f), 0f);
            _flash = go.AddComponent<Light>();
            _flash.type      = LightType.Point;
            _flash.color     = AlertRed;
            _flash.range     = Mathf.Max(6f, _topY * 2.5f);
            _flash.intensity = 0f;
            _flash.enabled   = false;
            _flash.shadows   = LightShadows.None;
        }

        // ── Build the billboarded red "!" icon ────────────────────────────────

        private void BuildIcon()
        {
            var root = new GameObject("AttackAlertIcon");
            _icon = root.transform;
            _icon.SetParent(transform, false);

            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit")
                           ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");

            // Red card backing (always visible — the alert even if the glyph font is missing).
            var card = GameObject.CreatePrimitive(PrimitiveType.Quad);
            card.name = "Card";
            var cardCol = card.GetComponent<Collider>();
            if (cardCol != null) Destroy(cardCol);
            card.transform.SetParent(_icon, false);
            card.transform.localScale = new Vector3(0.7f, 0.7f, 1f);
            var cardR = card.GetComponent<MeshRenderer>();
            cardR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            cardR.receiveShadows = false;
            if (unlit != null)
            {
                var m = new Material(unlit);
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", AlertRed);
                else m.color = AlertRed;
                cardR.sharedMaterial = m;
            }

            // "!" glyph via TextMesh (builtin font, with fallbacks). If the font
            // can't resolve the red card alone still reads as an alert.
            var textGo = new GameObject("Bang");
            textGo.transform.SetParent(_icon, false);
            textGo.transform.localPosition = new Vector3(0f, 0f, -0.02f); // in front of the card
            textGo.transform.localScale = Vector3.one * 0.16f;
            var tm = textGo.AddComponent<TextMesh>();
            tm.text = "!";
            tm.fontSize = 64;
            tm.fontStyle = FontStyle.Bold;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = Color.white;
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                tm.font = font;
                var mr = textGo.GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = font.material;
            }
        }

        private void SetIconActive(bool on)
        {
            if (_icon != null && _icon.gameObject.activeSelf != on)
                _icon.gameObject.SetActive(on);
        }

        private float ComputeTopWorldY()
        {
            var rends = GetComponentsInChildren<Renderer>();
            float top = transform.position.y;
            bool any = false;
            foreach (var r in rends)
            {
                if (r == null) continue;
                top = any ? Mathf.Max(top, r.bounds.max.y) : r.bounds.max.y;
                any = true;
            }
            return any ? top : transform.position.y + 2.5f;
        }
    }

    /// <summary>
    /// Runtime installer: attaches a <see cref="StructureAttackAlert"/> to every
    /// Building in the Village (and rescans periodically so runtime-built structures
    /// get one too). Self-bootstrapping DDOL — no scene authoring.
    /// </summary>
    public static class StructureAttackAlertInstaller
    {
        private const string TargetScene = "Village2";
        private static bool s_hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (s_hooked) return;
            s_hooked = true;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (SceneManager.GetActiveScene().name == TargetScene) StartWatch();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == TargetScene) StartWatch();
        }

        private static void StartWatch()
        {
            var host = new GameObject("StructureAttackAlertInstaller");
            Object.DontDestroyOnLoad(host);
            host.AddComponent<Runner>().StartCoroutine(ScanLoop());
        }

        private static IEnumerator ScanLoop()
        {
            // A few seconds of rescans covers both baked + runtime-built structures,
            // then a slow heartbeat for anything spawned later (towers built mid-game).
            for (int i = 0; ; i++)
            {
                AttachToAll();
                yield return new WaitForSeconds(i < 6 ? 1f : 4f);
            }
        }

        private static void AttachToAll()
        {
            if (SceneManager.GetActiveScene().name != TargetScene) return;
            var buildings = Object.FindObjectsByType<Building>(FindObjectsSortMode.None);
            foreach (var b in buildings)
            {
                if (b != null && b.GetComponent<StructureAttackAlert>() == null)
                    b.gameObject.AddComponent<StructureAttackAlert>();
            }
        }

        /// <summary>Tiny MonoBehaviour host so the static installer can run a coroutine.</summary>
        private sealed class Runner : MonoBehaviour { }
    }
}
