using System;
using System.Collections.Generic;

namespace DeNelle.Core.UI
{
    /// <summary>
    /// Pure navigation history for player-facing workspaces. The root is always retained;
    /// Back never means Close. The host decides when Close exits the workspace.
    /// </summary>
    public sealed class NavigationStack<T>
    {
        private readonly List<T> _pages = new List<T>();

        public event Action Changed;

        public int Count => _pages.Count;
        public bool CanGoBack => _pages.Count > 1;
        public T Current => _pages.Count == 0 ? default : _pages[_pages.Count - 1];

        public void OpenRoot(T page)
        {
            _pages.Clear();
            _pages.Add(page);
            Changed?.Invoke();
        }

        public void Push(T page)
        {
            if (_pages.Count == 0)
                throw new InvalidOperationException("OpenRoot must be called before Push.");
            _pages.Add(page);
            Changed?.Invoke();
        }

        public bool Back()
        {
            if (!CanGoBack) return false;
            _pages.RemoveAt(_pages.Count - 1);
            Changed?.Invoke();
            return true;
        }

        public void Replace(T page)
        {
            if (_pages.Count == 0) _pages.Add(page);
            else _pages[_pages.Count - 1] = page;
            Changed?.Invoke();
        }

        public void Clear()
        {
            if (_pages.Count == 0) return;
            _pages.Clear();
            Changed?.Invoke();
        }
    }
}
