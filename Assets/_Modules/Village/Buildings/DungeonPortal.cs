// =============================================================================
// DungeonPortal — proximity entrance to Dungeon_HealersCottage from the village.
// -----------------------------------------------------------------------------
// Owner ask 2026-05-20: "make sure dungeon is connected and playtest what you
// can". Village had no in-world hook into the dungeon scene; only the DevPanel
// "Jump → Dungeon" debug button could route there. This adds a real portal:
// a glowing stone arch placed near the village edge, with a Press-F prompt
// that calls SceneRouter.GoDungeon("Dungeon_HealersCottage").
//
// Visual is a placeholder primitive arch (two cube uprights + a plank lintel +
// a translucent purple sheet that pulses) so it reads as a portal without
// needing a KayKit asset. The interaction logic is what matters.
// =============================================================================

using Cysharp.Threading.Tasks;
using DeNelle.Core;
using DeNelle.Core.World;
using UnityEngine;

namespace DeNelle.Village
{
    [DisallowMultipleComponent]
    public sealed class DungeonPortal : MonoBehaviour
    {
        // DEF-26 (2026-05-27): tightened from 5.5 → 3.0 m so the [F] prompt
        // only fires when the hero is at the portal arch entrance. 5.5 m was
        // activating from ~2 m before the portal disc edge, which looked like
        // the prompt was firing in the open field when the hero was still clearly
        // approaching. 3.0 m matches ~the disc radius (3.5 m) and the "~2–3 m
        // from the door" spec in DEF-26.
        private const float ActivateRadius = 3.0f;
        private const float PromptHeight = 4.4f;

        [SerializeField] private string _dungeonId = "Dungeon_HealersCottage";
        [SerializeField] private string _displayName = "Healer's Cottage";

        /// <summary>The scene this portal loads ("Dungeon_HealersCottage"). Read-only —
        /// <see cref="Configure"/> stays the only writer. Exposed for RealmPinProducers
        /// (WO-829 §3), which pins the live portals on the map/minimap rather than keeping
        /// a second list of where the dungeons are.</summary>
        public string DungeonId => _dungeonId;

        /// <summary>Player-facing portal name ("Healer's Cottage"), falling back to the id
        /// so a pin is never labelled with an empty string (colourblind law: the WORD is
        /// what carries a pin's meaning, so it may not be blank).</summary>
        public string DisplayName => string.IsNullOrEmpty(_displayName) ? _dungeonId : _displayName;

        public void Configure(string dungeonId, string displayName)
        {
            _dungeonId = dungeonId;
            _displayName = string.IsNullOrEmpty(displayName) ? dungeonId : displayName;
        }

        private Transform _hero;
        private GameObject _promptGo;
        private Renderer _shimmer;
        private float _t;
        private bool _loading;
        private PortalVFXController _portalVfx;

        // DEF-40: perf — throttle distance checks; cache in-range state so
        // GetKeyDown can still fire every frame without regression.
        private bool _heroFound;
        private bool _isInRange;
        private float _nextProximityCheck;
        private const float CheckInterval = 0.15f;

        // WO-1114 — the remotely-flippable door state, re-read on the EXISTING 0.15 s
        // proximity tick (no second timer). Two properties fall out of reading it HERE,
        // at the door, rather than caching it once at spawn:
        //   • a status flip lands within a cache period with NO rebuild, and
        //   • a hero already inside a dungeon scene has no DungeonPortal in scope, so a
        //     mid-run flip can never eject an active delve.
        // ⛔ GROUND STATE IS CLOSED (owner ruling 2026-08-26, WO-1223: "not acesable if
        // not in table, if in table and works then yes"). This field used to seed
        // OpenDefault, so for the frames between Awake and the first Update resolve the
        // door read ENTERABLE even with no table behind it. Seeded ClosedDefault now:
        // the door opens only once DungeonStatusCatalog.For has said so out loud.
        private DungeonDoorInfo _door = DungeonDoorInfo.ClosedDefault;
        private DungeonDoorState _lastLoggedDoorState = DungeonDoorState.Sealed;

        private void Start()
        {
            ResolveHero();
            // DEF-100: the controller is self-sufficient (builds its own glow/light/
            // vortex in code) — ensure one exists even if the portal wasn't authored
            // with it, so the interior glow always renders + reacts to the hero.
            _portalVfx = GetComponent<PortalVFXController>();
            if (_portalVfx == null) _portalVfx = gameObject.AddComponent<PortalVFXController>();
        }

        private void ResolveHero()
        {
            if (_heroFound) return;
            var hero = UnityEngine.Object.FindAnyObjectByType<HeroLocomotion>();
            if (hero != null) { _hero = hero.transform; _heroFound = true; }
        }

        private void Update()
        {
            // Shimmer pulse — gated to in-range so distant portals don't burn
            // a SetColor + material property lookup every frame.
            if (_shimmer != null && _isInRange)
            {
                _t += Time.deltaTime;
                float pulse = 0.55f + Mathf.Sin(_t * 2.0f) * 0.18f;
                if (_shimmer.sharedMaterial != null && _shimmer.sharedMaterial.HasProperty("_BaseColor"))
                {
                    var c = _shimmer.sharedMaterial.GetColor("_BaseColor");
                    c.a = pulse;
                    _shimmer.sharedMaterial.SetColor("_BaseColor", c);
                }
            }

            if (!_heroFound) { ResolveHero(); return; }
            if (_loading) return;

            // The cached hero ref can go null if the village rebuilds/replaces the
            // hero rig after we first found it. Re-resolve rather than dereferencing
            // a destroyed Transform — otherwise the line below NREs EVERY FRAME and
            // the exception/stack-trace spam tanks the framerate (reads to the player
            // as "frozen, can't move"). DEF-40 regression: the _heroFound cache
            // removed the per-frame re-resolve that used to mask this.
            if (_hero == null) { _heroFound = false; return; }

            // Build mode: authoring, not interacting — release the button, hide the
            // bubble, skip the [F] press. Restored automatically on build exit.
            if (MobileInteractButton.Suppressed)
            {
                MobileInteractButton.Release(this);
                if (_promptGo != null) HidePrompt();
                return;
            }

            // Throttled proximity check (0.15 s) — prompt show/hide.
            if (Time.time >= _nextProximityCheck)
            {
                _nextProximityCheck = Time.time + CheckInterval;

                // WO-1114: re-read the door on the tick that is already running.
                _door = DungeonStatusCatalog.For(_dungeonId);
                if (_door.State != _lastLoggedDoorState)
                {
                    _lastLoggedDoorState = _door.State;
                    DeNelle.Core.Diagnostics.FlowTrace.Step(DungeonStatusCatalog.Sys,
                        $"door state for id='{_dungeonId}' is now {_door.State} " +
                        $"(provenance={DungeonStatusCatalog.Provenance}).");
                    // A door that closed while the prompt was up must not keep offering entry.
                    if (_isInRange) { HidePrompt(); ShowPrompt(); }
                }

                float distSqr = (_hero.position - transform.position).sqrMagnitude;
                bool nowInRange = distSqr <= ActivateRadius * ActivateRadius;
                if (nowInRange != _isInRange)
                {
                    _isInRange = nowInRange;
                    if (_isInRange) ShowPrompt();
                    else           HidePrompt();
                }
            }

            // DEF-203: register the shared on-screen Interact button while in range so
            // touch/mobile (no keyboard) can enter too. Desktop F + walk-in unchanged.
            //
            // WO-1114 — THE GATE. A closed door NEVER hands EnterDungeon to the button, so
            // no scene load is ever started and there is categorically no load-then-eject
            // (which is indistinguishable from a crash). The sealed door IS the content:
            // the button reads the authored headline and opens the prose dialogue.
            // Since WO-777 removed the walk-in auto-route (OnTriggerEnter arms VFX only)
            // and this button is the SOLE entry path, this one branch closes the door.
            if (_isInRange)
            {
                if (_door.IsOpen)
                    MobileInteractButton.Request(this, "Enter: " + _displayName, EnterDungeon);
                else
                    MobileInteractButton.Request(this, DoorHeadline(), ShowSealedDoor);
            }
            else
                MobileInteractButton.Release(this);

            // DEF-217: the shared button is the single canonical prompt — drop the
            // redundant world-space bubble while it is showing.
            if (_promptGo != null && MobileInteractButton.IsActive) HidePrompt();

            // Mobile-first: entering fires ONLY through the shared on-screen Interact
            // button (requested above). WO-777: the walk-into-trigger auto-route was
            // removed (accidental-delve footgun); OnTriggerEnter now only arms VFX.
        }

        private void OnDisable()
        {
            MobileInteractButton.Release(this);
        }

        /// <summary>
        /// WO-777: the trigger no longer AUTO-ROUTES on walk-in. Walking into the
        /// portal used to call EnterDungeon() immediately (an accidental-delve
        /// footgun with no confirm — owner ask 2026-05-20 originally wired it that
        /// way). Now the trigger only ARMS the portal VFX (interior glow reacts to
        /// the approaching hero); the SOLE entry path is the shared Interact button
        /// requested in Update() (MobileInteractButton.Request → EnterDungeon), so
        /// entering is always an explicit tap/[F], never a walk-by.
        /// </summary>
        private void OnTriggerEnter(Collider other)
        {
            if (other == null) return;
            // Only the hero arms the portal — pets are kinematic and would otherwise
            // trigger the approach VFX while orbiting.
            var hero = other.GetComponentInParent<HeroLocomotion>();
            if (hero == null) return;
            Debug.Log("[DungeonPortal] Trigger entered by hero — arming portal (entry is via the Interact button, no walk-by).");
            _portalVfx?.OnHeroApproach();
        }

        public void BindShimmer(Renderer r) => _shimmer = r;

        private void ShowPrompt()
        {
            // WO-1114: a closed door says its authored line instead of offering entry.
            // The bubble is deliberately NOT rewritten in TMP here — it is legacy TextMesh,
            // which is outside the UiObsidianConformance StrongSmells regex. Rewriting it
            // would hard-fail [ui-obsidian]. The prose dialogue is the Obsidian surface.
            string label = _door.IsOpen
                ? "〔 Tap / F 〕 " + _displayName
                : DoorHeadline();

            _promptGo = BuildBubble(
                label,
                PromptHeight,
                new Color(0.10f, 0.04f, 0.20f, 0.96f),
                new Color(0.78f, 0.55f, 1f, 1f));
        }

        /// <summary>
        /// The one-line prose for the closed door: the payload's authored headline when it
        /// has one, else the per-status default from canon-strings.json. Copy resolution has
        /// exactly ONE owner (DungeonSealedDoorPanel) — never type a player sentence here.
        /// </summary>
        private string DoorHeadline()
        {
            string s = DungeonSealedDoorPanel.DoorHeadline(_door);
            return string.IsNullOrWhiteSpace(s) ? _displayName : s;
        }

        /// <summary>
        /// Open the sealed-door prose. If the dialogue cannot open the door STILL does not
        /// open — the gate lives here, never in the UI.
        /// </summary>
        private void ShowSealedDoor()
        {
            HidePrompt();
            DungeonSealedDoorPanel.Show(_door, _displayName);
        }

        private void HidePrompt()
        {
            if (_promptGo != null) UnityEngine.Object.Destroy(_promptGo);
            _promptGo = null;
        }

        private void EnterDungeon()
        {
            if (_loading) return;

            // WO-1114 BACKSTOP. The registration branch above is the player experience;
            // THIS is the invariant. It runs before the scene-name resolution and long
            // before SceneRouter.GoDungeonScene, so a closed dungeon never starts a load.
            // Note the _loading reset - the same dead-latch discipline as the
            // CanStreamedLevelBeLoaded guard below: a later flip back to open must leave
            // the portal live, not permanently dead.
            if (!DungeonStatusCatalog.IsOpen(_dungeonId))
            {
                DeNelle.Core.Diagnostics.FlowTrace.Warn(DungeonStatusCatalog.Sys,
                    $"EnterDungeon blocked at the door: id='{_dungeonId}' state={_door.State} " +
                    $"(provenance={DungeonStatusCatalog.Provenance}). No scene load attempted.");
                _loading = false;
                _door = DungeonStatusCatalog.For(_dungeonId);
                ShowSealedDoor();
                return;
            }

            // Resolve the target scene name. Legacy portals pass a bare dungeon id
            // ("HealersCottage") and the scene is "Dungeon_" + id. Composed dungeons
            // (GraphDungeonComposer, e.g. "dg_starter_loop") ship the FULL scene name
            // as the id, so prefer the id verbatim when it is already loadable; only
            // then fall back to the legacy "Dungeon_" prefix form.
            string sceneName = _dungeonId;
            bool loadable = Application.CanStreamedLevelBeLoaded(sceneName);
            if (!loadable)
            {
                string prefixed = "Dungeon_" + _dungeonId;
                if (Application.CanStreamedLevelBeLoaded(prefixed))
                {
                    sceneName = prefixed;
                    loadable = true;
                }
            }

            // DEAD-LATCH FIX: SceneManager.LoadScene does NOT throw on an unregistered
            // scene — it logs + no-ops, so the try/catch never fires, _loading would
            // latch true, and every later tap would be dead. Guard with
            // CanStreamedLevelBeLoaded: self-report via FlowTrace.Fail and stay live
            // (reset _loading) so the portal is never permanently dead on device.
            if (!loadable)
            {
                DeNelle.Core.Diagnostics.FlowTrace.Fail("DungeonPortal",
                    $"scene not loadable id='{_dungeonId}' (tried '{_dungeonId}' and 'Dungeon_{_dungeonId}') " +
                    "- not in Build Settings; portal stays live (no dead-latch).");
                _loading = false;
                return;
            }

            _loading = true;
            HidePrompt();
            Debug.Log("[DungeonPortal] Entering dungeon scene: " + sceneName);
            // F8 2026-07-30 (portal round-trip): route through the SAME SceneRouter fade path
            // the rest of the game uses. The 2026-05-20 "fader nulls on unload" freeze is
            // obsolete — the fader is the DDOL ScreenFader (SceneRouter.Fader), proven
            // in-session (dev-overlay GoDungeon + ExitToVillage both fade-load). This adds the
            // save flush (SaveBeforeSceneChange) + fade + [Flow:SceneRouter] trace the raw
            // sync LoadScene silently skipped. sceneName is passed VERBATIM (already resolved
            // above incl. the 'Dungeon_' prefix fallback) — NOT via GoDungeon, which would
            // re-prefix and break the composed 'dg_starter_loop' id.
            try
            {
                _portalVfx?.OnHeroEnter();
                // WO-1112: route through GoDungeonScene, NOT the bare LoadSceneWithFade. It is
                // the same fade/save/gate path (it calls LoadSceneWithFade), plus the one thing
                // this call site was missing: for a COMPOSED (dg_*) destination it arms the
                // WO-1109 hero carry as the pre-load hook, so the dungeon hero is the real town
                // hero WITH its abilities instead of the baker's bare rig (which carries no
                // HeroAbilities, so Q/W/E/R were dead in every composed dungeon, silently).
                // Hand-built Dungeon_* destinations are passed through uncarried — see
                // GoDungeonScene's remarks for why widening that gate would break them.
                // sceneName is still passed VERBATIM (already resolved above incl. the
                // "Dungeon_" prefix fallback) — NOT via GoDungeon(dungeonId), which re-prefixes.
                SceneRouter.GoDungeonScene(sceneName);
            }
            catch (System.Exception ex)
            {
                DeNelle.Core.Diagnostics.FlowTrace.Fail("DungeonPortal", "GoDungeonScene threw: " + ex);
                _loading = false;
            }
        }

        // ── Reuses BuildingInteractable's bubble look for visual consistency. ──
        private GameObject BuildBubble(string text, float localY, Color bgColor, Color outlineColor)
        {
            var go = new GameObject("PortalPrompt");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * localY;

            float charsApprox = Mathf.Max(text.Length, 8);
            float w = Mathf.Clamp(charsApprox * 0.10f + 0.4f, 1.0f, 3.4f);
            float h = 0.38f;

            var outline = GameObject.CreatePrimitive(PrimitiveType.Quad);
            outline.name = "Outline";
            DestroyImmediate(outline.GetComponent<Collider>());
            outline.transform.SetParent(go.transform, false);
            outline.transform.localPosition = new Vector3(0f, 0f, 0.012f);
            outline.transform.localScale = new Vector3(w + 0.06f, h + 0.06f, 1f);
            ApplyRounded(outline.GetComponent<Renderer>(), outlineColor, (w + 0.06f) / (h + 0.06f));

            var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            bg.name = "Bg";
            DestroyImmediate(bg.GetComponent<Collider>());
            bg.transform.SetParent(go.transform, false);
            bg.transform.localPosition = new Vector3(0f, 0f, 0.006f);
            bg.transform.localScale = new Vector3(w, h, 1f);
            ApplyRounded(bg.GetComponent<Renderer>(), bgColor, w / h);

            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(go.transform, false);
            txtGo.transform.localPosition = Vector3.zero;
            txtGo.transform.localScale = Vector3.one * 0.06f;
            var tm = txtGo.AddComponent<TextMesh>();
            tm.text = text;
            tm.fontSize = 96;
            tm.characterSize = 0.30f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = new Color(0.97f, 0.95f, 0.88f);

            var billboard = go.AddComponent<PromptBillboard>();
            billboard.Camera = Camera.main;
            return go;
        }

        private static void ApplyRounded(Renderer renderer, Color colour, float aspect)
        {
            if (renderer == null) return;
            Shader rounded = Shader.Find("DeNelle/UI/RoundedChatBubble")
                             ?? Shader.Find("Universal Render Pipeline/Unlit")
                             ?? Shader.Find("Unlit/Color");
            if (rounded == null) return;
            var mat = new Material(rounded);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", colour);
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color", colour);
            if (mat.HasProperty("_Radius")) mat.SetFloat("_Radius", 0.30f);
            if (mat.HasProperty("_Aspect")) mat.SetFloat("_Aspect", Mathf.Max(0.5f, aspect));
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            renderer.sharedMaterial = mat;
        }
    }
}
