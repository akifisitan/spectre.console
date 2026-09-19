namespace Spectre.Console;

internal sealed class ListPromptState<T>
    where T : notnull
{
    private readonly Func<T, string> _converter;

    public int Index => _selectableItems.Count == 0 ? 0 : _selectableItems[_selectableIndex].Index;
    public int PageSize { get; }
    public bool WrapAround { get; }
    public SelectionMode Mode { get; }
    public bool SkipUnselectableItems { get; private set; }
    public bool SearchEnabled { get; }
    public bool IsCancelled { get; private set; }
    public IReadOnlyList<ListPromptItem<T>> Items { get; }
    public ListPromptItem<T>? Current => _selectableItems.Count == 0 ? null : _selectableItems[_selectableIndex].Item;
    public string SearchText { get; private set; }
    private readonly Func<T, string, bool> _searchFilter;
    public List<ListPromptItem<T>> VisibleItems { get; private set; }
    public bool FilterOnSearch { get; private set; }
    private List<SelectableItem> _selectableItems;
    private int _selectableIndex;
    public string? InvokedCustomHotkeyRegistrationKey { get; set; }

    public ListPromptState(
        IReadOnlyList<ListPromptItem<T>> items,
        Func<T, string> converter,
        int pageSize, bool wrapAround,
        SelectionMode mode,
        bool skipUnselectableItems,
        bool searchEnabled,
        Func<T, string, bool>? searchFilter,
        bool filterOnSearch,
        int initialIndex = 0)
    {
        _converter = converter ?? throw new ArgumentNullException(nameof(converter));
        Items = items;
        PageSize = pageSize;
        WrapAround = wrapAround;
        Mode = mode;
        SkipUnselectableItems = skipUnselectableItems;
        SearchEnabled = searchEnabled;
        SearchText = string.Empty;
        _searchFilter = searchFilter ?? DefaultSearchFilter;
        FilterOnSearch = filterOnSearch;
        VisibleItems = Items.ToList();
        _selectableItems = GetSelectableItems();
        _selectableIndex = Math.Max(0, _selectableItems.FindIndex(x => x.Index == initialIndex));
    }

    public bool Update(ConsoleKeyInfo keyInfo)
    {
        if (SearchEnabled)
        {
            if (!char.IsControl(keyInfo.KeyChar) && keyInfo.Modifiers != ConsoleModifiers.Alt)
            {
                SearchText += keyInfo.KeyChar;
                if (FilterOnSearch)
                {
                    VisibleItems = FilterItemsBySearch();
                    _selectableItems = GetSelectableItems();
                    _selectableIndex = 0;
                }
                else
                {
                    var item = _selectableItems
                        .FirstOrDefault(x => MatchesSearch(x.Item));
                    if (item != null)
                    {
                        _selectableIndex = _selectableItems.IndexOf(item);
                    }
                }

                return true;
            }

            if (keyInfo.Key == ConsoleKey.Backspace)
            {
                if (SearchText.Length > 0)
                {
                    SearchText = keyInfo.Modifiers != ConsoleModifiers.Control ? SearchText[..^1] : "";
                    if (FilterOnSearch)
                    {
                        VisibleItems = FilterItemsBySearch();
                        _selectableItems = GetSelectableItems();
                        _selectableIndex = 0;
                    }
                    else
                    {
                        var item = _selectableItems
                            .FirstOrDefault(x => MatchesSearch(x.Item));
                        if (item != null)
                        {
                            _selectableIndex = _selectableItems.IndexOf(item);
                        }
                    }

                    return true;
                }
            }
        }

        switch (keyInfo.Key)
        {
            case ConsoleKey.UpArrow:
                if (_selectableIndex > 0)
                {
                    _selectableIndex--;
                }
                else if (WrapAround)
                {
                    _selectableIndex = _selectableItems.Count - 1;
                }

                return true;

            case ConsoleKey.DownArrow:
                if (_selectableIndex < _selectableItems.Count - 1)
                {
                    _selectableIndex++;
                }
                else if (WrapAround)
                {
                    _selectableIndex = 0;
                }

                return true;

            case ConsoleKey.Home:
                _selectableIndex = 0;
                return true;

            case ConsoleKey.End:
                _selectableIndex = _selectableItems.Count - 1;
                return true;

            case ConsoleKey.PageUp:
                MovePage(-PageSize);
                return true;

            case ConsoleKey.PageDown:
                MovePage(PageSize);
                return true;
        }

        return false;
    }

    internal void Cancel()
    {
        IsCancelled = true;
    }

    private void MovePage(int offset)
    {
        if (_selectableItems.Count == 0)
        {
            return;
        }

        var targetIndex = (long)Index + offset;
        if (WrapAround)
        {
            targetIndex = ((targetIndex % VisibleItems.Count) + VisibleItems.Count) % VisibleItems.Count;
        }
        else
        {
            targetIndex = Math.Max(0, Math.Min(targetIndex, VisibleItems.Count - 1));
        }

        var selectableIndex = _selectableItems.FindIndex(item => item.Index >= targetIndex);
        if (selectableIndex >= 0)
        {
            _selectableIndex = selectableIndex;
        }
        else
        {
            // The target lies after the last selectable item, among filtered groups.
            _selectableIndex = WrapAround && offset > 0 ? 0 : _selectableItems.Count - 1;
        }
    }

    private bool DefaultSearchFilter(T data, string search)
    {
        return _converter.Invoke(data).Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private List<SelectableItem> GetSelectableItems()
    {
        var selectableItems = VisibleItems
            .Select((item, filteredIndex) => new SelectableItem(item, filteredIndex));

        if (SkipUnselectableItems && Mode == SelectionMode.Leaf)
        {
            selectableItems = selectableItems.Where(x => !x.Item.IsGroup);
        }

        return selectableItems.ToList();
    }

    private List<ListPromptItem<T>> FilterItemsBySearch()
    {
        var matches = new HashSet<ListPromptItem<T>>();
        foreach (var item in Items.Where(MatchesSearch))
        {
            // Keep the complete path to a match without including unrelated children.
            var current = item;
            while (current != null && matches.Add(current))
            {
                current = current.Parent;
            }
        }

        return Items.Where(matches.Contains).ToList();
    }

    private bool MatchesSearch(ListPromptItem<T> item) => _searchFilter(item.Data, SearchText);

    private class SelectableItem(ListPromptItem<T> item, int index)
    {
        public ListPromptItem<T> Item { get; } = item;
        public int Index { get; } = index;
    }
}
