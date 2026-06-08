// =============================================================================
// PartyHudBridge — shows the joined story companions in the HUD party stack.
// -----------------------------------------------------------------------------
// The hero is party slot 0 (pushed by HeroAbilitiesHudBridge.SetHeroHp). This
// bridge fills slots 1..3 with the live StoryCompanion followers, so the party
// frames grow as companions join (hero + first companion at start = 2, then 3,
// then 4 — matching the companion-roster join order).
//
// Self-bootstrapping (RuntimeInitializeOnLoad, DDOL) — no scene edit needed,
// same pattern as PetHarvestBootstrap. Resolves the HUD through CoreServices.Hud
// (Village → Core is allowed) and calls the HUD's SetPartyMember /
// SetPartyMemberVisible by reflection (Village → HUD is asmdef-isolated, same
// reflection seam as the other *HudBridge components).
//
// Companions currently have no health (they are immortal support units — see
// StoryCompanion), so the HP bar is a PLACEHOLDER full bar. When a companion
// health system lands, push real values here instead of the placeholder.
// =============================================================================

using System.Reflection;
using DeNelle.Core;
using DeNelle.Core.State;
using UnityEngine;

namespace DeNelle.Village
{
    [DisallowMultipleComponent]
    public sealed class PartyHudBridge : MonoBehaviour
    {
        private const int CompanionSlots = 3;     // HUD slots 1..3 (slot 0 is the hero)
        private const float Placeholder = 100f;   // companions have no HP yet — show a full bar
        private const float RefreshInterval = 0.5f;

        private MethodInfo _setPartyMember;     // SetPartyMember(int slot, string name, float current, float max)
        private MethodInfo _setPartyVisible;    // SetPartyMemberVisible(int slot, bool visible)
        private object _hud;                    // the resolved VillageHudController instance
        private readonly object[] _memberArgs = new object[4];
        private readonly object[] _visibleArgs = new object[2];

        private float _timer;
        private readonly StoryCompanion[] _scratch = new StoryCompanion[CompanionSlots];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("PartyHudBridge");
            go.AddComponent<PartyHudBridge>();
            DontDestroyOnLoad(go);
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = RefreshInterval;

            if (!ResolveHud()) return;

            // WO-301 — the PERSISTED ROSTER is the source of truth for how many party
            // frames show. PartySize companions (slots 1..PartySize) are visible; the
            // rest are hidden. The live StoryCompanion still supplies the per-frame
            // name/HP below, but a frame only appears when the roster says that member
            // has joined — so the UI tracks the roster, not just whatever happens to be
            // spawned. No service ⇒ fall back to "show whatever is spawned" (rosterCount
            // = CompanionSlots) so non-persisted dev scenes still render.
            var svc = GameStateService.Instance;
            int rosterCount = (svc != null && svc.State != null) ? svc.State.PartySize : CompanionSlots;

            // Gather companions, stable slot order (lowest instance id = joined first).
            var all = FindObjectsByType<StoryCompanion>(FindObjectsSortMode.None);
            int n = 0;
            for (int i = 0; i < _scratch.Length; i++) _scratch[i] = null;
            for (int i = 0; i < all.Length && n < CompanionSlots; i++)
            {
                // simple insertion sort by instanceID into the small scratch buffer
                var c = all[i];
                int pos = n;
                while (pos > 0 && _scratch[pos - 1].GetInstanceID() > c.GetInstanceID())
                {
                    _scratch[pos] = _scratch[pos - 1];
                    pos--;
                }
                _scratch[pos] = c;
                n++;
            }

            for (int slot = 0; slot < CompanionSlots; slot++)
            {
                int hudSlot = slot + 1; // 0 is the hero
                var c = _scratch[slot];
                // Roster gate (WO-301): only show a frame for a slot the roster covers.
                if (c != null && slot < rosterCount)
                {
                    // Real HP now that StoryCompanion is mortal (was a placeholder full
                    // bar). MaxHp guards against a 0 (pre-Start) read so the bar never
                    // shows empty for a just-spawned companion.
                    float max = c.MaxHp > 0f ? c.MaxHp : Placeholder;
                    float cur = Mathf.Clamp(c.Hp, 0f, max);
                    _memberArgs[0] = hudSlot;
                    _memberArgs[1] = c.DisplayName;
                    _memberArgs[2] = cur;
                    _memberArgs[3] = max;
                    _setPartyMember?.Invoke(_hud, _memberArgs);
                }
                else
                {
                    _visibleArgs[0] = hudSlot;
                    _visibleArgs[1] = false;
                    _setPartyVisible?.Invoke(_hud, _visibleArgs);
                }
            }
        }

        // Resolve the HUD instance + its party methods once it is registered.
        private bool ResolveHud()
        {
            var hud = CoreServices.Hud as object;
            if (hud == null) { _hud = null; return false; }
            if (!ReferenceEquals(hud, _hud))
            {
                _hud = hud;
                var t = hud.GetType();
                _setPartyMember = t.GetMethod("SetPartyMember",
                    BindingFlags.Public | BindingFlags.Instance, null,
                    new[] { typeof(int), typeof(string), typeof(float), typeof(float) }, null);
                _setPartyVisible = t.GetMethod("SetPartyMemberVisible",
                    BindingFlags.Public | BindingFlags.Instance, null,
                    new[] { typeof(int), typeof(bool) }, null);
                if (_setPartyMember == null)
                    Debug.LogWarning("[PartyHudBridge] HUD has no SetPartyMember(int,string,float,float) — " +
                                     "companion frames will not show.");
            }
            return _setPartyMember != null;
        }
    }
}
