// =============================================================================
// BattleVfx — SNES-retro 2D presentation layer for the ATB battle (WO-170)
// -----------------------------------------------------------------------------
// PRESENTATION ONLY. This class never touches the engine, the RNG, or combat
// resolution. It is a passive *view* that replays what already happened:
//
//   • It reads the append-only BattleState.Log (the engine's authoritative record
//     of every strike / heal / death) and DIFFS it on each turn-resolved event,
//     replaying a flat, punchy 2D effect per new entry — hit flash + recoil,
//     floating retro damage / heal numbers, a per-element burst flipbook on the
//     target, a defeat fade, and a screen flash / shake on big or critical hits.
//   • It drives a per-card idle bob and an attack/cast lunge pose, all off the
//     existing ATBRuntimeState UnityEvents (OnActionSubmitted / OnTurnResolved /
//     OnOutcome) — it never polls.
//
// DATA-DRIVEN, NO HARD-CODED ELEMENT BRANCHES: every effect is looked up from a
// VfxCatalog keyed by an effect id (element token + "heal"). Adding an element =
// adding a catalog row; the replay logic does not switch on specific elements.
// The "flipbooks" here are deliberately code-built flat-color placeholders (a
// tinted burst that scales + fades) so the SYSTEM ships without blocking on art;
// swap a row's frames/sprite for final art later without touching this logic.
//
// All animation runs on the UI Toolkit scheduler (VisualElement.schedule) — no
// MonoBehaviour, no coroutine. The effect elements live on the HUD's
// input-transparent VfxLayer, so nothing here can ever eat a gameplay tap.
//
// Determinism note: this reads the log read-only and resolves a unit's element
// from the live (already-resolved) snapshot. It performs NO Rng draws and mutates
// no engine state, so the EditMode golden vectors are untouched by design.
// =============================================================================

using System.Collections.Generic;
using DeNelle.BattleATB.Engine;
using DeNelle.BattleATB.State;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.BattleATB
{
    /// <summary>
    /// Retro 2D battle VFX presenter. Bound once to a <see cref="BattleHud"/>; fed
    /// the live snapshot on every turn-resolved + the active unit on action-submit.
    /// </summary>
    public sealed class BattleVfx
    {
        // ── Retro palette (flat, bold, readable — FF/Chrono throwback) ──────────

        // DEF-270 (owner: "RED for damage, GREEN for heals"). Normal damage now reads
        // RED so a hit is unmistakable at a glance; a crit stays a hotter yellow-orange
        // so the two still read apart; heals stay green. Honest contrast over the old
        // white-cyan numerals, which didn't say "damage" at a glance.
        private static readonly Color NumNormal = new Color(1f, 0.30f, 0.26f, 1f);      // red
        private static readonly Color NumCrit = new Color(1f, 0.82f, 0.16f, 1f);        // hot yellow
        private static readonly Color NumHeal = new Color(0.45f, 0.95f, 0.50f, 1f);     // green
        private static readonly Color FlashWhite = new Color(1f, 1f, 1f, 0.85f);

        // ── Effect catalog — data-driven, keyed by effect id. ───────────────────
        // A "flipbook" placeholder is a flat tinted burst (color + glyph) that
        // pops, scales and fades. Final art swaps the visual without code changes.

        /// <summary>One catalog row = the look of an elemental / support effect.</summary>
        private sealed class VfxEntry
        {
            public Color Color;     // burst tint + number tint hint
            public string Glyph;    // a single retro glyph drawn in the burst
        }

        private readonly Dictionary<string, VfxEntry> _catalog = new Dictionary<string, VfxEntry>
        {
            // element id (matches ElementType.ToToken()) → burst look. Glyphs are
            // ASCII-safe so they render in the default font (placeholder "flipbook");
            // swap for real per-element sprite frames later without touching logic.
            { "physical", new VfxEntry { Color = new Color(0.95f, 0.92f, 0.80f), Glyph = "*" } },
            { "flame",    new VfxEntry { Color = new Color(1.00f, 0.45f, 0.18f), Glyph = "#" } },
            { "ice",      new VfxEntry { Color = new Color(0.55f, 0.85f, 1.00f), Glyph = "x" } },
            { "aether",   new VfxEntry { Color = new Color(0.78f, 0.55f, 1.00f), Glyph = "o" } },
            // support family
            { "heal",     new VfxEntry { Color = new Color(0.45f, 0.95f, 0.50f), Glyph = "+" } },
        };

        // Big-hit thresholds for the screen flash / shake flourishes.
        private const int BigHitDamage = 30;

        // ── Binding ─────────────────────────────────────────────────────────────

        private BattleHud _hud;
        private int _seenLogCount;                 // diff cursor into BattleState.Log
        private readonly HashSet<string> _idle = new HashSet<string>();   // cards with a running bob
        private readonly HashSet<string> _dead = new HashSet<string>();   // units already played-out

        /// <summary>Bind to a built HUD. Call once after <see cref="BattleHud.Build"/>.</summary>
        public void Bind(BattleHud hud)
        {
            _hud = hud;
            _seenLogCount = 0;
            _idle.Clear();
            _dead.Clear();
        }

        /// <summary>Reset the diff cursor + per-unit bookkeeping for a fresh battle.</summary>
        public void Reset()
        {
            _seenLogCount = 0;
            _idle.Clear();
            _dead.Clear();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Drive points (called by BattleController off the runtime-state events)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>OnActionSubmitted — play the acting unit's attack/cast lunge.</summary>
        public void OnActionSubmitted(BattleState state)
        {
            if (_hud == null || state == null) return;
            // The active unit is the one who just acted (engine sets ActiveUnitId to
            // the actor while resolving its turn). Lunge it forward + back.
            PlayCastPose(state.ActiveUnitId, state);
        }

        /// <summary>OnTurnResolved / OnBattleChanged — replay every new log entry as
        /// a retro effect, and keep idle bobs running on the living cards.</summary>
        public void OnTurnResolved(BattleState state)
        {
            if (_hud == null || state == null) return;
            EnsureIdleBobs(state);
            ReplayLog(state);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Log replay — the heart of the retro feedback
        // ─────────────────────────────────────────────────────────────────────

        private void ReplayLog(BattleState state)
        {
            List<BattleLogEntry> log = state.Log;
            if (log == null) return;

            // A brand-new battle (or a re-bind) resets the cursor below the count.
            if (_seenLogCount > log.Count) _seenLogCount = 0;

            for (int i = _seenLogCount; i < log.Count; i++)
            {
                BattleLogEntry e = log[i];
                if (e == null) continue;

                switch (e.Event)
                {
                    case BattleLogEvent.Attack:
                    case BattleLogEvent.Ability:
                        PlayStrike(state, e);
                        break;
                    case BattleLogEvent.Item:
                        PlayItem(state, e);
                        break;
                    case BattleLogEvent.StatusTick:
                        // Damage/heal-over-time tick: a small floating number, no flash.
                        if (e.Amount.HasValue && e.Amount.Value != 0)
                            FloatNumber(e.TargetId, e.Amount.Value, false, state);
                        break;
                    case BattleLogEvent.Death:
                        PlayDefeat(e.TargetId ?? e.SourceId);
                        break;
                    default:
                        break;
                }
            }
            _seenLogCount = log.Count;
        }

        /// <summary>An attack or offensive ability resolving on a target.</summary>
        private void PlayStrike(BattleState state, BattleLogEntry e)
        {
            // Heal-ability (Amount < 0) → green motes + heal number, no recoil.
            if (e.Amount.HasValue && e.Amount.Value < 0)
            {
                Burst(e.TargetId, "heal");
                FloatNumber(e.TargetId, e.Amount.Value, false, state);
                return;
            }

            int dmg = e.Amount.HasValue ? e.Amount.Value : 0;
            bool crit = e.Crit == true;

            // Elemental burst on the target, tinted by the SOURCE unit's element.
            Burst(e.TargetId, ElementId(state, e.SourceId));

            if (dmg > 0)
            {
                HitFlash(e.TargetId, crit);
                Recoil(e.TargetId);
                FloatNumber(e.TargetId, dmg, crit, state);

                // Throwback flourishes — screen flash on a crit, shake on a big hit.
                if (crit) ScreenFlash();
                if (crit || dmg >= BigHitDamage) ScreenShake();
            }
        }

        /// <summary>An item resolving on an ally (potion / mana / cleanse).</summary>
        private void PlayItem(BattleState state, BattleLogEntry e)
        {
            Burst(e.TargetId, "heal");
            if (e.Amount.HasValue && e.Amount.Value != 0)
                FloatNumber(e.TargetId, e.Amount.Value, false, state);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Effect primitives — all flat, snappy, code-built (placeholder art)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>White hit-flash overlay on a struck card, brighter on a crit.</summary>
        private void HitFlash(string unitId, bool crit)
        {
            if (!_hud.TryGetCardElement(unitId, out VisualElement card)) return;
            var flash = NewOverlay(card);
            flash.style.backgroundColor = new StyleColor(crit ? Color.white : FlashWhite);
            card.Add(flash);
            // Two-step fade: full → gone over ~140ms. Snappy, not smeary.
            flash.schedule.Execute(() => { if (flash != null) flash.style.opacity = 0f; }).StartingIn(40);
            flash.schedule.Execute(() => Remove(flash)).StartingIn(160);
        }

        /// <summary>Quick recoil nudge — the struck card jerks then settles.</summary>
        private void Recoil(string unitId)
        {
            if (!_hud.TryGetCardElement(unitId, out VisualElement card)) return;
            // Party cards (right) recoil right; enemy cards (left) recoil left.
            bool party = card.parent != null && IsPartySide(card);
            float dx = party ? 10f : -10f;
            card.style.translate = new StyleTranslate(new Translate(dx, 0, 0));
            card.schedule.Execute(() =>
            {
                if (card != null) card.style.translate = new StyleTranslate(new Translate(0, 0, 0));
            }).StartingIn(90);
        }

        /// <summary>A tinted elemental burst flipbook (placeholder: a glyph that pops,
        /// scales up and fades). Plays on the target card; AoE plays per-target as
        /// the engine logs one entry per hit, so every struck unit gets its own.</summary>
        private void Burst(string unitId, string effectId)
        {
            if (!_hud.TryGetCardElement(unitId, out VisualElement card)) return;
            VfxEntry entry = LookupEntry(effectId);

            var burst = new Label(entry.Glyph);
            burst.pickingMode = PickingMode.Ignore;
            burst.style.position = Position.Absolute;
            burst.style.left = 0; burst.style.right = 0;
            burst.style.top = 0; burst.style.bottom = 0;
            burst.style.unityTextAlign = TextAnchor.MiddleCenter;
            burst.style.fontSize = 30;
            burst.style.color = new StyleColor(entry.Color);
            burst.style.unityFontStyleAndWeight = FontStyle.Bold;
            burst.style.scale = new StyleScale(new Scale(new Vector3(0.6f, 0.6f, 1f)));
            card.Add(burst);

            // Pop: scale 0.6 → 1.4 then fade out. ~3 "frames" of feel.
            burst.schedule.Execute(() =>
            {
                if (burst == null) return;
                burst.style.scale = new StyleScale(new Scale(new Vector3(1.4f, 1.4f, 1f)));
            }).StartingIn(20);
            burst.schedule.Execute(() => { if (burst != null) burst.style.opacity = 0f; }).StartingIn(180);
            burst.schedule.Execute(() => Remove(burst)).StartingIn(360);
        }

        /// <summary>A floating retro damage / heal number that rises + fades.</summary>
        private void FloatNumber(string unitId, int amount, bool crit, BattleState state)
        {
            if (_hud.VfxLayer == null) return;
            if (!_hud.TryGetCardElement(unitId, out VisualElement card)) return;

            bool heal = amount < 0;
            int shown = Mathf.Abs(amount);

            var num = new Label((heal ? "+" : "") + shown.ToString());
            num.pickingMode = PickingMode.Ignore;
            num.style.position = Position.Absolute;
            num.style.unityTextAlign = TextAnchor.MiddleCenter;
            num.style.unityFontStyleAndWeight = FontStyle.Bold;
            // Retro chunky numerals — yellow crit, green heal, white-cyan normal.
            num.style.fontSize = crit ? 30 : 22;
            num.style.color = new StyleColor(heal ? NumHeal : (crit ? NumCrit : NumNormal));
            // Bitmap-ish hard edge: a black outline-ish shadow under the glyph.
            num.style.textShadow = new StyleTextShadow(new TextShadow
            {
                offset = new Vector2(1.5f, 1.5f),
                blurRadius = 0f,
                color = new Color(0f, 0f, 0f, 0.85f),
            });

            // Anchor over the card's centre in VfxLayer space (layer is full-screen,
            // so card.worldBound maps straight onto it).
            Rect wb = card.worldBound;
            float x = wb.center.x - 40f;
            float y = wb.yMin - 6f;
            num.style.left = x; num.style.top = y;
            num.style.width = 80;
            _hud.VfxLayer.Add(num);

            // Rise ~24px and fade over ~700ms. Crit rises a touch more for punch.
            float rise = crit ? 34f : 24f;
            num.schedule.Execute(() =>
            {
                if (num == null) return;
                num.style.top = y - rise;
            }).StartingIn(20);
            num.schedule.Execute(() => { if (num != null) num.style.opacity = 0f; }).StartingIn(420);
            num.schedule.Execute(() => Remove(num)).StartingIn(760);
        }

        /// <summary>Defeat — a quick fade + topple feel on the fallen card.</summary>
        private void PlayDefeat(string unitId)
        {
            if (string.IsNullOrEmpty(unitId) || _dead.Contains(unitId)) return;
            _dead.Add(unitId);
            _idle.Remove(unitId); // stop bobbing a corpse
            if (!_hud.TryGetCardElement(unitId, out VisualElement card)) return;

            // Topple: rotate + sink slightly. The HUD itself dims dead cards to 0.35
            // opacity on the next bind; this adds the moment-of-death motion.
            card.style.rotate = new StyleRotate(new Rotate(8f));
            card.style.translate = new StyleTranslate(new Translate(0, 6, 0));
            card.schedule.Execute(() =>
            {
                if (card != null) card.style.rotate = new StyleRotate(new Rotate(0f));
            }).StartingIn(420);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Idle bob + attack lunge (per-unit "sprite anim" feel)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Give every living card a gentle 2-frame breathing bob.</summary>
        private void EnsureIdleBobs(BattleState state)
        {
            foreach (BattleUnit u in state.Units)
            {
                if (u == null || !u.Alive) continue;
                if (_idle.Contains(u.Id)) continue;
                if (!_hud.TryGetCardElement(u.Id, out VisualElement card)) continue;
                _idle.Add(u.Id);
                StartBob(u.Id, card);
            }
        }

        private void StartBob(string unitId, VisualElement card)
        {
            // A slow up/down toggle — the classic 2-frame idle. ~900ms per frame.
            bool[] up = { false };
            card.schedule.Execute(() =>
            {
                if (card == null || !_idle.Contains(unitId)) return;
                up[0] = !up[0];
                // Don't fight an active recoil/cast translate: only nudge Y by a hair.
                card.style.translate = new StyleTranslate(new Translate(0, up[0] ? -2f : 0f, 0));
            }).Every(900);
        }

        /// <summary>Attack/cast lunge — step the actor toward the foe, then settle.</summary>
        private void PlayCastPose(string unitId, BattleState state)
        {
            if (string.IsNullOrEmpty(unitId)) return;
            if (!_hud.TryGetCardElement(unitId, out VisualElement card)) return;

            bool party = IsPartySide(card);
            // Party (right column) lunges LEFT toward enemies; enemies lunge RIGHT.
            float dx = party ? -16f : 16f;
            card.style.translate = new StyleTranslate(new Translate(dx, 0, 0));
            card.schedule.Execute(() =>
            {
                if (card != null) card.style.translate = new StyleTranslate(new Translate(0, 0, 0));
            }).StartingIn(180);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Screen flourishes — flash + shake (mobile-light, no particles)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>A brief full-screen white flash on a crit / big hit.</summary>
        private void ScreenFlash()
        {
            VisualElement layer = _hud.VfxLayer;
            if (layer == null) return;
            var flash = new VisualElement();
            flash.pickingMode = PickingMode.Ignore;
            flash.style.position = Position.Absolute;
            flash.style.left = 0; flash.style.right = 0; flash.style.top = 0; flash.style.bottom = 0;
            flash.style.backgroundColor = new StyleColor(new Color(1f, 1f, 1f, 0.5f));
            layer.Add(flash);
            flash.schedule.Execute(() => { if (flash != null) flash.style.opacity = 0f; }).StartingIn(30);
            flash.schedule.Execute(() => Remove(flash)).StartingIn(150);
        }

        /// <summary>A short, decaying shake of the whole HUD root on a heavy hit.</summary>
        private void ScreenShake()
        {
            VisualElement root = _hud.Root;
            if (root == null) return;
            int[] step = { 0 };
            float[] mag = { 8f };
            var item = root.schedule.Execute(() =>
            {
                if (root == null) return;
                step[0]++;
                float m = mag[0] * Mathf.Max(0f, 1f - step[0] / 6f);
                float ox = (step[0] % 2 == 0 ? 1f : -1f) * m;
                root.style.translate = new StyleTranslate(new Translate(ox, 0, 0));
            });
            item.Every(35);
            // Stop + recentre after ~6 ticks.
            root.schedule.Execute(() =>
            {
                item.Pause();
                if (root != null) root.style.translate = new StyleTranslate(new Translate(0, 0, 0));
            }).StartingIn(230);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>The effect id for a source unit's element ("flame"/"ice"/… or
        /// "physical"). Data lookup only — no per-element branch in callers.</summary>
        private static string ElementId(BattleState state, string sourceId)
        {
            BattleUnit u = string.IsNullOrEmpty(sourceId) ? null : BattleStateOps.GetUnit(state, sourceId);
            return u != null ? u.Element.ToToken() : "physical";
        }

        private VfxEntry LookupEntry(string effectId)
        {
            if (!string.IsNullOrEmpty(effectId) && _catalog.TryGetValue(effectId, out VfxEntry entry))
                return entry;
            return _catalog["physical"]; // safe default (always present)
        }

        /// <summary>True when a card lives in the party (right) column. Inferred from
        /// the parent column's alignItems (party = FlexEnd, enemies = FlexStart in
        /// BattleHud.MakeColumn) — a read-only view query, no engine coupling.</summary>
        private static bool IsPartySide(VisualElement card)
        {
            VisualElement col = card != null ? card.parent : null;
            if (col == null) return true;
            return col.resolvedStyle.alignItems == Align.FlexEnd;
        }

        private static VisualElement NewOverlay(VisualElement card)
        {
            var fx = new VisualElement();
            fx.pickingMode = PickingMode.Ignore;
            fx.style.position = Position.Absolute;
            fx.style.left = 0; fx.style.right = 0; fx.style.top = 0; fx.style.bottom = 0;
            fx.style.borderTopLeftRadius = 8; fx.style.borderTopRightRadius = 8;
            fx.style.borderBottomLeftRadius = 8; fx.style.borderBottomRightRadius = 8;
            return fx;
        }

        private static void Remove(VisualElement e)
        {
            if (e != null) e.RemoveFromHierarchy();
        }
    }
}
