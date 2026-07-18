// =============================================================================
// ClanChatVM — the PURE ViewModel behind ClanChatPanel (strict-MVVM Silo E).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.HUD   Namespace: DeNelle.HUD
//
// ALL clan/chat state + projection that used to live in the ClanChatPanel VIEW
// now lives here (mirrors the gold-standard BuildingUpgradeVM):
//   * implements IPanelViewModel (Title / Changed / Close / Dispose).
//   * NO UnityEngine UI types — unit-testable without a scene.
//   * projects ClanService (InClan / Current / Messages / AccountId) + the
//     ChatPhraseCatalog into UI-free rows (Messages -> MessageRow, phrases ->
//     ChipRow with category dividers + never-blank fallback).
//   * the 4 writes are commands: LeaveClan, CreateClan(name,tag),
//     SendPhrase(id), SendCustom(text). The View owns only the input widgets.
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.Services;
using DeNelle.Core.UI.Mvvm;

namespace DeNelle.HUD
{
    /// <summary>
    /// Pure ViewModel for the clan team-chat panel. Exposes the in-clan flag, the
    /// clan header line, the projected message + phrase-chip lists, and the 4 write
    /// commands. Raises <see cref="Changed"/> on every ClanService mutation.
    /// </summary>
    public sealed class ClanChatVM : IPanelViewModel, IDisposable
    {
        // ── Seam over ClanService + ChatPhraseCatalog (fake in tests; singleton live). ──
        public interface ISource
        {
            event Action Changed;                                 // ClanService.Changed
            bool InClan { get; }                                  // ClanService.InClan
            string ClanName { get; }                              // ClanService.Current?.Name
            string ClanTag { get; }                               // ClanService.Current?.Tag
            string AccountId { get; }                             // ClanService.AccountId
            IReadOnlyList<ChatMessage> Messages { get; }          // ClanService.Messages
            IReadOnlyList<ChatPhraseDef> Phrases { get; }         // ChatPhraseCatalog.Phrases
            string CategoryLabel(string key);                     // ChatPhraseCatalog.Categories lookup
            void LeaveClan();                                     // ClanService.LeaveClan()
            void CreateClan(string name, string tag);            // ClanService.CreateClan(name,tag)
            void AddTemplatedMessage(string phraseId);           // ClanService.AddTemplatedMessage(id)
            void AddCustomMessage(string text);                  // ClanService.AddCustomMessage(text)
        }

        /// <summary>One projected chat row (meta line + body). Hint = the italic empty-state row.</summary>
        public readonly struct MessageRow
        {
            public readonly string Meta;
            public readonly string Body;
            public readonly bool IsHint;
            public MessageRow(string meta, string body, bool isHint)
            { Meta = meta; Body = body; IsHint = isHint; }
        }

        /// <summary>One phrase-rail entry: either a category DIVIDER (label) or a tappable CHIP.</summary>
        public readonly struct ChipRow
        {
            public readonly bool IsDivider;
            public readonly string Label;      // divider label OR chip display text
            public readonly string PhraseId;   // set only on a chip
            public readonly bool IsFallback;   // the "no quick phrases" italic note
            public ChipRow(bool isDivider, string label, string phraseId, bool isFallback)
            { IsDivider = isDivider; Label = label; PhraseId = phraseId; IsFallback = isFallback; }
        }

        /// <summary>Custom free-text cap (mirrors ClanService.CustomTextMaxChars) so the View's
        /// input field never names ClanService.</summary>
        public int CustomTextMaxChars => ClanService.CustomTextMaxChars;

        private readonly ISource _source;
        private readonly Action _onClose;
        private readonly Action _changedHandler;
        private bool _disposed;

        private readonly List<MessageRow> _messages = new List<MessageRow>();
        private readonly List<ChipRow> _chips = new List<ChipRow>();

        public static ClanChatVM CreateDefault(Action onClose)
            => new ClanChatVM(new ServiceSource(), onClose);

        public ClanChatVM(ISource source, Action onClose)
        {
            _source = source;
            _onClose = onClose;
            if (_source != null)
            {
                _changedHandler = Rebuild;
                _source.Changed += _changedHandler;
            }
            Rebuild();
        }

        // ── IPanelViewModel ───────────────────────────────────────────────────
        public event Action Changed;
        public string Title => "Clan Chat";
        public void Close() => _onClose?.Invoke();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_source != null && _changedHandler != null) _source.Changed -= _changedHandler;
            Changed = null;
        }

        // ── Read-only data the View renders ─────────────────────────────────────

        /// <summary>True when the player is in a clan (drives status + which body zone shows).</summary>
        public bool InClan { get; private set; }

        /// <summary>Clan header line ("[TAG] Name") when in a clan, else "No clan yet".</summary>
        public string StatusLine { get; private set; }

        /// <summary>Header action button label ("Leave" in-clan, else "Create").</summary>
        public string ActionLabel => InClan ? "Leave" : "Create";

        /// <summary>Projected chat rows (oldest first); a single hint row when empty. Never null.</summary>
        public IReadOnlyList<MessageRow> Messages => _messages;

        /// <summary>Projected phrase rail (dividers + chips; a single fallback when none). Never null.</summary>
        public IReadOnlyList<ChipRow> Chips => _chips;

        // ── Commands ────────────────────────────────────────────────────────────

        /// <summary>Header button: leave the clan when in one (the not-in-clan state shows the create form).</summary>
        public void OnHeaderButton()
        {
            if (InClan) _source?.LeaveClan();
            // else: the create form is already visible — no-op (preserves View behavior).
        }

        /// <summary>Found a clan with the given name + tag (blank -> service defaults).</summary>
        public void CreateClan(string name, string tag) => _source?.CreateClan(name, tag);

        /// <summary>Post a templated phrase by id (no-op unless in a clan; service guards).</summary>
        public void SendPhrase(string phraseId)
        {
            if (_source == null || !InClan) return;
            _source.AddTemplatedMessage(phraseId);
        }

        /// <summary>Post a custom free-text message (no-op unless in a clan; service caps + guards).</summary>
        public void SendCustom(string text)
        {
            if (_source == null || !InClan) return;
            _source.AddCustomMessage(text);
        }

        // ── Projection (moved verbatim from the View) ───────────────────────────

        private void Rebuild()
        {
            InClan = _source != null && _source.InClan;

            if (InClan)
            {
                string tag = string.IsNullOrEmpty(_source.ClanTag) ? "" : "[" + _source.ClanTag + "] ";
                StatusLine = tag + (_source.ClanName ?? "");
                RebuildMessages();
                RebuildChips();
            }
            else
            {
                StatusLine = "No clan yet";
                _messages.Clear();
                _chips.Clear();
            }
            Raise();
        }

        private void RebuildMessages()
        {
            _messages.Clear();
            var msgs = _source.Messages;
            if (msgs == null || msgs.Count == 0)
            {
                _messages.Add(new MessageRow("", "Send a phrase below to start the chat.", true));
                return;
            }
            string me = _source.AccountId;
            foreach (var m in msgs)
            {
                if (m == null) continue;
                var meta = (m.SenderId == me ? "You" : (m.SenderName ?? "?"))
                           + (m.IsCustom ? " - custom" : "");
                _messages.Add(new MessageRow(meta, m.Text ?? "...", false));
            }
        }

        private void RebuildChips()
        {
            _chips.Clear();
            var phrases = _source.Phrases;
            if (phrases == null || phrases.Count == 0)
            {
                _chips.Add(new ChipRow(false, "No quick phrases - use Custom... below to chat.", null, true));
                return;
            }

            int chipCount = 0;
            string lastCategory = null;
            foreach (var p in phrases)
            {
                if (p == null) continue;
                if (p.Category != lastCategory)
                {
                    lastCategory = p.Category;
                    _chips.Add(new ChipRow(true, _source.CategoryLabel(p.Category), null, false));
                }
                var label = string.IsNullOrEmpty(p.Emoji) ? p.Text : (p.Emoji + " " + p.Text);
                _chips.Add(new ChipRow(false, label, p.Id, false));
                chipCount++;
            }
            if (chipCount == 0)
                _chips.Add(new ChipRow(false, "No quick phrases - use Custom... below to chat.", null, true));
        }

        private void Raise() { if (!_disposed) Changed?.Invoke(); }

        // ── Real seam: wraps ClanService + ChatPhraseCatalog (SOLE live resolution site). ──
        private sealed class ServiceSource : ISource
        {
            public event Action Changed
            {
                add    { if (ClanService.Instance != null) ClanService.Instance.Changed += value; }
                remove { if (ClanService.Instance != null) ClanService.Instance.Changed -= value; }
            }

            public bool InClan => ClanService.Instance != null && ClanService.Instance.InClan;
            public string ClanName => ClanService.Instance?.Current?.Name;
            public string ClanTag => ClanService.Instance?.Current?.Tag;
            public string AccountId => ClanService.Instance?.AccountId;

            public IReadOnlyList<ChatMessage> Messages
                => ClanService.Instance != null ? ClanService.Instance.Messages : System.Array.Empty<ChatMessage>();

            public IReadOnlyList<ChatPhraseDef> Phrases => ChatPhraseCatalog.Phrases;

            public string CategoryLabel(string key)
            {
                foreach (var c in ChatPhraseCatalog.Categories)
                    if (c != null && c.Key == key) return c.Label;
                return key ?? "Phrases";
            }

            public void LeaveClan() => ClanService.Instance?.LeaveClan();
            public void CreateClan(string name, string tag) => ClanService.Instance?.CreateClan(name, tag);
            public void AddTemplatedMessage(string phraseId) => ClanService.Instance?.AddTemplatedMessage(phraseId);
            public void AddCustomMessage(string text) => ClanService.Instance?.AddCustomMessage(text);
        }
    }
}
