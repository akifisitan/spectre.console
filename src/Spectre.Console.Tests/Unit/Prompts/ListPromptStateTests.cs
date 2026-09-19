namespace Spectre.Console.Tests.Unit;

public sealed class ListPromptStateTests
{
    private ListPromptState<string> CreateListPromptState(int count, int pageSize, bool shouldWrap, bool searchEnabled, Func<string, string, bool>? searchFilter = null, bool filterOnSearch = false, int initialIndex = 0)
        => new(
            Enumerable.Range(0, count).Select(i => new ListPromptItem<string>(i.ToString())).ToList(),
            text => text,
            pageSize, shouldWrap, SelectionMode.Independent, true, searchEnabled, searchFilter, filterOnSearch, initialIndex);

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Should_Have_Specified_Start_Index(int index)
    {
        // Given
        var state = CreateListPromptState(100, 10, false, false, initialIndex: index);

        // When
        /* noop */

        // Then
        state.Index.ShouldBe(index);
    }

    [Theory]
    [InlineData(ConsoleKey.UpArrow)]
    public void Should_Decrease_Index(ConsoleKey key)
    {
        // Given
        var state = CreateListPromptState(100, 10, false, false, null, false);
        state.Update(ConsoleKey.End.ToConsoleKeyInfo());
        var index = state.Index;

        // When
        state.Update(key.ToConsoleKeyInfo());

        // Then
        state.Index.ShouldBe(index - 1);
    }

    [Theory]
    [InlineData(ConsoleKey.DownArrow, true)]
    [InlineData(ConsoleKey.DownArrow, false)]
    public void Should_Increase_Index(ConsoleKey key, bool wrap)
    {
        // Given
        var state = CreateListPromptState(100, 10, wrap, false, null, false);
        var index = state.Index;

        // When
        state.Update(key.ToConsoleKeyInfo());

        // Then
        state.Index.ShouldBe(index + 1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Should_Go_To_End(bool wrap)
    {
        // Given
        var state = CreateListPromptState(100, 10, wrap, false, null, false);

        // When
        state.Update(ConsoleKey.End.ToConsoleKeyInfo());

        // Then
        state.Index.ShouldBe(99);
    }

    [Theory]
    [InlineData(ConsoleKey.DownArrow)]
    public void Should_Clamp_Index_If_No_Wrap(ConsoleKey key)
    {
        // Given
        var state = CreateListPromptState(100, 10, false, false, null, false);
        state.Update(ConsoleKey.End.ToConsoleKeyInfo());

        // When
        state.Update(key.ToConsoleKeyInfo());

        // Then
        state.Index.ShouldBe(99);
    }

    [Theory]
    [InlineData(ConsoleKey.DownArrow)]
    public void Should_Wrap_Index_If_Wrap(ConsoleKey key)
    {
        // Given
        var state = CreateListPromptState(100, 10, true, false, null, false);
        state.Update(ConsoleKey.End.ToConsoleKeyInfo());

        // When
        state.Update(key.ToConsoleKeyInfo());

        // Then
        state.Index.ShouldBe(0);
    }

    [Theory]
    [InlineData(ConsoleKey.UpArrow)]
    public void Should_Wrap_Index_If_Wrap_And_Down(ConsoleKey key)
    {
        // Given
        var state = CreateListPromptState(100, 10, true, false, null, false);

        // When
        state.Update(key.ToConsoleKeyInfo());

        // Then
        state.Index.ShouldBe(99);
    }

    [Fact]
    public void Should_Wrap_Index_If_Wrap_And_Page_Up()
    {
        // Given
        var state = CreateListPromptState(10, 100, true, false, null, false);

        // When
        state.Update(ConsoleKey.PageUp.ToConsoleKeyInfo());

        // Then
        state.Index.ShouldBe(0);
    }

    [Theory]
    [InlineData(ConsoleKey.UpArrow)]
    public void Should_Wrap_Index_If_Wrap_And_Offset_And_Page_Down(ConsoleKey key)
    {
        // Given
        var state = CreateListPromptState(10, 100, true, false, null, false);
        state.Update(ConsoleKey.End.ToConsoleKeyInfo());
        state.Update(key.ToConsoleKeyInfo());

        // When
        state.Update(ConsoleKey.PageDown.ToConsoleKeyInfo());

        // Then
        state.Index.ShouldBe(8);
    }

    [Theory]
    [InlineData(ConsoleKey.PageUp, false)]
    [InlineData(ConsoleKey.PageUp, true)]
    [InlineData(ConsoleKey.PageDown, false)]
    [InlineData(ConsoleKey.PageDown, true)]
    public void Should_Allow_Paging_And_Clearing_A_Search_With_No_Matches(ConsoleKey key, bool wrap)
    {
        // Given
        var state = CreateListPromptState(10, 3, wrap, true, filterOnSearch: true);
        state.Update(ConsoleKey.Z.ToConsoleKeyInfo());

        // When
        state.Update(new ConsoleKeyInfo('\0', key, false, false, false));

        // Then
        state.VisibleItems.ShouldBeEmpty();
        state.Current.ShouldBeNull();
        state.Index.ShouldBe(0);

        state.Update(ConsoleKey.Backspace.ToConsoleKeyInfo());
        state.VisibleItems.Count.ShouldBe(10);
        state.Current.ShouldNotBeNull().Data.ShouldBe("0");
    }

    [Theory]
    [InlineData(ConsoleKey.PageUp, false)]
    [InlineData(ConsoleKey.PageUp, true)]
    [InlineData(ConsoleKey.PageDown, false)]
    [InlineData(ConsoleKey.PageDown, true)]
    public void Should_Allow_Paging_When_Only_A_Group_Matches(ConsoleKey key, bool wrap)
    {
        // Given
        var root = new ListPromptItem<string>("Z group");
        root.AddChild("Child");
        var state = new ListPromptState<string>(root.Traverse(true).ToList(), text => text,
            3, wrap, SelectionMode.Leaf, true, true, null, true);
        state.Update(ConsoleKey.Z.ToConsoleKeyInfo());

        // When
        state.Update(new ConsoleKeyInfo('\0', key, false, false, false));

        // Then
        state.VisibleItems.ShouldBe([root]);
        state.Current.ShouldBeNull();

        state.Update(ConsoleKey.Backspace.ToConsoleKeyInfo());
        state.Current.ShouldNotBeNull().Data.ShouldBe("Child");
    }

    [Theory]
    [InlineData(ConsoleKey.PageUp, true, "Z last")]
    [InlineData(ConsoleKey.PageDown, true, "Z first")]
    [InlineData(ConsoleKey.PageDown, false, "Z last")]
    public void Should_Page_Past_Trailing_Unselectable_Groups(ConsoleKey key, bool wrap, string expected)
    {
        // Given
        var first = new ListPromptItem<string>("Z first");
        var last = new ListPromptItem<string>("Z last");
        var group = new ListPromptItem<string>("Z group");
        group.AddChild("Hidden child");
        var items = new[] { first, last }.Concat(group.Traverse(true)).ToList();
        var state = new ListPromptState<string>(items, text => text,
            5, wrap, SelectionMode.Leaf, true, true, null, true);
        state.Update(ConsoleKey.Z.ToConsoleKeyInfo());
        if (key == ConsoleKey.PageUp)
        {
            state.Update(new ConsoleKeyInfo('\0', ConsoleKey.End, false, false, false));
        }

        // When
        state.Update(new ConsoleKeyInfo('\0', key, false, false, false));

        // Then
        state.Current.ShouldNotBeNull().Data.ShouldBe(expected);
    }

    [Fact]
    public void Should_Wrap_Page_Up_When_Page_Size_Exceeds_The_Filtered_List()
    {
        // Given
        var state = CreateListPromptState(14, 5, true, true, filterOnSearch: true);
        state.Update(ConsoleKey.D3.ToConsoleKeyInfo());

        // When
        state.Update(new ConsoleKeyInfo('\0', ConsoleKey.PageUp, false, false, false));

        // Then
        state.Current.ShouldNotBeNull().Data.ShouldBe("13");
        state.Index.ShouldBe(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Should_Keep_All_Ancestors_Of_A_Nested_Match(bool customFilter)
    {
        // Given
        var root = new ListPromptItem<string>("Root");
        var branch = root.AddChild("Branch");
        branch.AddChild("Nested").AddChild("Z match");
        branch.AddChild("Other leaf");
        root.AddChild("Other branch").AddChild("Unrelated leaf");
        var state = new ListPromptState<string>(root.Traverse(true).ToList(), text => text,
            3, false, SelectionMode.Leaf, true, true,
            customFilter ? (text, search) => text.StartsWith(search, StringComparison.OrdinalIgnoreCase) : null, true);

        // When
        state.Update(ConsoleKey.Z.ToConsoleKeyInfo());

        // Then
        state.VisibleItems.Select(item => item.Data).ShouldBe(["Root", "Branch", "Nested", "Z match"]);
        state.Current.ShouldNotBeNull().Data.ShouldBe("Z match");
        state.Index.ShouldBe(3);

        state.Update(ConsoleKey.Backspace.ToConsoleKeyInfo());
        state.VisibleItems.ShouldBe(state.Items);
    }

    [Theory]
    [InlineData(ConsoleKey.J, false)]
    [InlineData(ConsoleKey.J, true)]
    [InlineData(ConsoleKey.K, false)]
    [InlineData(ConsoleKey.K, true)]
    public void Should_Not_Navigate_With_Letter_Keys(ConsoleKey key, bool wrap)
    {
        // Given
        var state = CreateListPromptState(100, 10, wrap, false, initialIndex: 50);

        // When
        state.Update(key.ToConsoleKeyInfo());

        // Then
        state.Index.ShouldBe(50);
    }

    [Fact]
    public void Should_Jump_To_First_Matching_Item_When_Searching()
    {
        // Given
        var state = CreateListPromptState(10, 100, true, true, null, false);

        // When
        state.Update(ConsoleKey.D3.ToConsoleKeyInfo());

        // Then
        state.Index.ShouldBe(3);
    }

    [Fact]
    public void Should_Filter_Items_On_Search()
    {
        // Given
        var state = CreateListPromptState(14, 100, true, true, null, true);

        // When
        state.Update(ConsoleKey.D3.ToConsoleKeyInfo());

        // Then
        state.VisibleItems.Select(x => x.Data).ShouldBe(["3", "13"]);
    }

    [Fact]
    public void Should_Jump_Back_To_First_Item_When_Clearing_Search_Term()
    {
        // Given
        var state = CreateListPromptState(10, 100, true, true, null, false);

        // When
        state.Update(ConsoleKey.D3.ToConsoleKeyInfo());
        state.Update(ConsoleKey.Backspace.ToConsoleKeyInfo());

        // Then
        state.Index.ShouldBe(0);
    }
}
