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
        bool filterOnSearch)
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
        _selectableIndex = 0;
    }

    public bool Update(ConsoleKeyInfo keyInfo)
    {
        if (SearchEnabled)
        {
            if (!char.IsControl(keyInfo.KeyChar))
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
                var pageUpIndex = Index - PageSize;
                if (WrapAround)
                {
                    pageUpIndex = (pageUpIndex + VisibleItems.Count) % VisibleItems.Count;
                }
                else
                {
                    pageUpIndex = Math.Max(pageUpIndex, 0);
                }

                _selectableIndex = _selectableItems.IndexOf(_selectableItems.First(x => x.Index >= pageUpIndex));
                return true;

            case ConsoleKey.PageDown:
                var pageDownIndex = Index + PageSize;
                if (WrapAround)
                {
                    pageDownIndex %= VisibleItems.Count;
                }
                else
                {
                    pageDownIndex = Math.Min(pageDownIndex, VisibleItems.Count - 1);
                }

                _selectableIndex = _selectableItems.IndexOf(_selectableItems.First(x => x.Index >= pageDownIndex));
                return true;
        }

        return false;
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
        return Items
            .Where(x => MatchesSearch(x) || x.Children.Any(MatchesSearch))
            .ToList();
    }

    private bool MatchesSearch(ListPromptItem<T> item) => _searchFilter(item.Data, SearchText);

    private class SelectableItem(ListPromptItem<T> item, int index)
    {
        public ListPromptItem<T> Item { get; } = item;
        public int Index { get; } = index;
    }
}