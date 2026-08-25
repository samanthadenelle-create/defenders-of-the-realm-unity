// =============================================================================
// BankOverflowToastPresenter -- the PLAYER-FACING half of the bank-cap warn.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.UI
//
// WHY THIS FILE EXISTS AT ALL
//   Owner ruling 2026-08-04 (WO-901 Sec.5): overflow at the town bank cap is CLAMP AND
//   WARN -- the surplus is LOST and the player is TOLD. That makes the warn load-bearing:
//   it is the only thing standing between the player and silently vaporised resources.
//   TownBankCapacity.ClampGrant already emits an UNTHROTTLED [Flow:Bank] Warn on every
//   clamped grant (the captured-data half, Sec.12). This is the on-screen half.
//
// PRESENTATION IS A SEPARATE LAYER (ARCHITECTURE_PRINCIPLES Sec.2)
//   EconomyService / the income paths never build UI and never know a toast exists. They
//   raise TownBankCapacity.Overflowed; this observer renders it through the ONE established
//   toast seam (ElarionUiKit.ShowToast). No new reflection bridge, no static_gate allowlist
//   entry, no HudKitController coupling -- Core observing a Core event.
//
// COPY LAW (WO-901 Sec.4)
//   This surface owns the words "Storage" / "Bank" / current-max -- it is the WALLET.
//   It must NEVER say "collectors N/M full" (that is the pending pools, WO-900).
//   ASCII only, state text-encoded, never colour alone (the owner is red/green colourblind):
//   the tone accent is decoration, the sentence carries the whole message.
//
// THROTTLING
//   Per RESOURCE, on storage-caps.json overflowWarnCooldownSeconds, so a hot income loop
//   (per-kill trickle, offline catch-up) cannot spam the screen. The FlowTrace warn is
//   deliberately NOT throttled -- the break-log must carry every event even when the
//   screen shows one.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.Economy;

namespace DeNelle.Core.UI
{
    /// <summary>Renders <see cref="TownBankCapacity.Overflowed"/> as the one transient toast.</summary>
    public static class BankOverflowToastPresenter
    {
        private static readonly Dictionary<BankResource, float> _lastShownAt = new Dictionary<BankResource, float>();
        private static bool _attached;

        /// <summary>Self-attaches once per play session (and re-attaches after a domain reload).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Attach()
        {
            if (_attached) return;
            _attached = true;
            _lastShownAt.Clear();
            TownBankCapacity.Overflowed += OnOverflow;
            FlowTrace.Step("Bank", "BankOverflowToastPresenter attached -- clamped grants will surface on screen.");
        }

        /// <summary>Detach (tests / teardown). Idempotent.</summary>
        public static void Detach()
        {
            if (!_attached) return;
            TownBankCapacity.Overflowed -= OnOverflow;
            _attached = false;
        }

        private static void OnOverflow(BankOverflowStatus s)
        {
            if (s.Lost <= 0) return;

            float now = Time.unscaledTime;
            if (_lastShownAt.TryGetValue(s.Resource, out float last))
            {
                float cooldown = StorageCapsCatalog.OverflowWarnCooldownSeconds;
                if (now - last < cooldown) return;   // screen-only throttle; the Flow warn already fired
            }
            _lastShownAt[s.Resource] = now;

            // WO-1191 -- TWO SITUATIONS, TWO SENTENCES. `FOUNDATIONAL_RULINGS.md` section 7 governs;
            // cite it, never restate it.
            //
            //   s.OverCap == false : the ordinary full-or-nearly-full bank. Something the player
            //                        earned did not fit and is gone. Existing words, unchanged.
            //
            //   s.OverCap == true  : the balance is ALREADY above capacity. That state is created on
            //                        purpose, and after a player has PAID to reach it the old
            //                        sentence -- "storage FULL, N lost, build a bigger container" --
            //                        reads as a penalty for the purchase. Nothing of theirs is being
            //                        taken: every unit above the cap is still in the wallet and still
            //                        spendable, the earned faucet is merely paused, and it restarts by
            //                        itself. So the copy states the amount, states that it is theirs,
            //                        and names the ONE condition that resumes income -- and it is NOT
            //                        a Danger toast, because nothing went wrong.
            //
            // Both lines are ASCII, state text-encoded in words, never carried by colour alone (the
            // owner is red/green colourblind) -- the tone accent is decoration in both branches.
            //
            // *** THE EXACT PLAYER WORDING BELOW IS A PROPOSAL AWAITING THE OWNER (WO-1191). ***
            // The BEHAVIOUR -- two branches, no loss language above the cap, the resume condition
            // named with the measured number -- is the part this file is asserting.
            if (s.OverCap)
            {
                string msg = $"{s.ResourceName} is above storage - {s.Current} of {s.Max}. All of it is yours to spend. "
                           + $"Harvests and rewards add no {s.ResourceName.ToLowerInvariant()} until you are back under {s.Max}.";
                ElarionUiKit.ShowToast(msg, ElarionUiKit.ToastTone.Info, 4.5f);
                return;
            }

            // ASCII, text-encoded, names the resource AND the amount lost AND the fix.
            string full = $"{s.ResourceName} storage FULL - {s.Lost} lost. Build or upgrade a {s.ContainerName}, or spend {s.ResourceName.ToLowerInvariant()}.";
            ElarionUiKit.ShowToast(full, ElarionUiKit.ToastTone.Danger, 3.2f);
        }
    }
}
