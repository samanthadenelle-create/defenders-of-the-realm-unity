// =============================================================================
// HudCommands — the Core command sink between the HUD kit and Village handlers.
// (HUD_OBSIDIAN_ARCHITECTURE_2026-07-03 A4 — P23 HUDKIT.)
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.HUD
//
// DIRECTION OF FLOW: Village REGISTERS handlers (Village -> Core is legal);
// the HUD kit FIRES them (HUD -> Core is legal). Neither assembly ever sees
// the other — the same seam pattern as CoreServices.Hud, but for the battle
// commands the old BattleHud9Zone wired inline (it was DeNelle.Village so it
// could call PlayerAttackController/BattleArena directly; the kit cannot).
//
// Handlers are plain delegate slots, re-registered per scene by the Village
// bridges (HudKitCommandBridge / BattleArenaHud). Firing an empty slot is a
// traced no-op — never a throw, never a blank screen.
// =============================================================================

using System;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.HUD
{
    /// <summary>Village-registered command handlers the HUD kit fires (see header).</summary>
    public static class HudCommands
    {
        private static Action _attack;
        private static Action _flee;
        private static Action<string> _cycleSelect;
        private static Action _potion;
        private static Action _talk;

        // ── registration (Village side) ──────────────────────────────────────

        /// <summary>Basic-attack swing (PlayerAttackController registers).</summary>
        public static void RegisterAttack(Action handler) { _attack = handler; }

        /// <summary>Flee the current battle (BattleArenaHud forwards BattleArena's handler;
        /// null on battle teardown).</summary>
        public static void RegisterFlee(Action handler) { _flee = handler; }

        /// <summary>True while a flee handler is live (a fleeable battle is running).</summary>
        public static bool HasFlee => _flee != null;

        /// <summary>Select a cycle target by the TargetRecord id (HudKitCommandBridge registers).</summary>
        public static void RegisterCycleSelect(Action<string> handler) { _cycleSelect = handler; }

        /// <summary>Use the assigned potion/consumable (Village potion system registers when built).</summary>
        public static void RegisterPotion(Action handler) { _potion = handler; }

        /// <summary>True while a potion handler is live (the potion slot earns its place).</summary>
        public static bool HasPotion => _potion != null;

        /// <summary>Talk to the in-range NPC (TalkHudBridge registers — replaces the stale
        /// reflection subscription onto the per-scene HUD's TalkRequested event).</summary>
        public static void RegisterTalk(Action handler) { _talk = handler; }

        // ── firing (HUD kit side) — traced, never-throw ──────────────────────

        /// <summary>Fire the basic-attack handler.</summary>
        public static void Attack() => Fire(_attack, "attack");

        /// <summary>Fire the flee handler.</summary>
        public static void Flee() => Fire(_flee, "flee");

        /// <summary>Fire the potion handler.</summary>
        public static void Potion() => Fire(_potion, "potion");

        /// <summary>Fire the talk handler.</summary>
        public static void Talk() => Fire(_talk, "talk");

        /// <summary>Fire the cycle-select handler for a target id.</summary>
        public static void CycleSelect(string targetId)
        {
            if (_cycleSelect == null)
            {
                FlowTrace.Warn("HudKit", "command 'cycleSelect' fired with NO registered handler");
                return;
            }
            Guard.Try("HudKit", "command cycleSelect", () => { _cycleSelect(targetId); return true; }, false);
        }

        private static void Fire(Action handler, string name)
        {
            if (handler == null)
            {
                FlowTrace.Warn("HudKit", "command '" + name + "' fired with NO registered handler");
                return;
            }
            FlowTrace.Step("HudKit", "command '" + name + "' fired");
            Guard.Try("HudKit", "command " + name, () => { handler(); return true; }, false);
        }
    }
}
