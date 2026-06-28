namespace Community.PowerToys.Run.Plugin.PromptLibrary.Services;

/// <summary>
/// Maintains Explorer-style back and forward history for one visible PromptLibrary session.
/// Search text is stored without the PowerToys Run action keyword.
/// </summary>
public sealed class NavigationHistory
{
    private readonly Stack<string> _back = new();
    private readonly Stack<string> _forward = new();

    public bool IsActive { get; private set; }

    public string Current { get; private set; } = string.Empty;

    public int BackCount => _back.Count;

    public int ForwardCount => _forward.Count;

    public void Start(string searchText)
    {
        _back.Clear();
        _forward.Clear();
        Current = Normalize(searchText);
        IsActive = true;
    }

    public void End()
    {
        _back.Clear();
        _forward.Clear();
        Current = string.Empty;
        IsActive = false;
    }

    /// <summary>
    /// Updates the current first-level query after direct user editing.
    /// Editing creates a new path, so any forward history is discarded.
    /// </summary>
    public void ObserveUserQuery(string searchText)
    {
        var normalized = Normalize(searchText);
        if (!IsActive)
        {
            Start(normalized);
            return;
        }

        if (string.Equals(Current, normalized, StringComparison.Ordinal))
        {
            return;
        }

        Current = normalized;
        _forward.Clear();
    }

    /// <summary>
    /// Navigates into a child level, preserving the current query for Back.
    /// </summary>
    public void NavigateTo(string searchText)
    {
        var normalized = Normalize(searchText);
        if (!IsActive)
        {
            Start(normalized);
            return;
        }

        if (string.Equals(Current, normalized, StringComparison.Ordinal))
        {
            return;
        }

        if (!string.IsNullOrEmpty(Current))
        {
            _back.Push(Current);
        }

        Current = normalized;
        _forward.Clear();
    }

    public bool TryBack(out string searchText)
    {
        searchText = Current;
        if (!IsActive)
        {
            return false;
        }

        if (_back.Count > 0)
        {
            _forward.Push(Current);
            Current = _back.Pop();
            searchText = Current;
            return true;
        }

        if (!string.IsNullOrEmpty(Current))
        {
            _forward.Push(Current);
            Current = string.Empty;
            searchText = Current;
            return true;
        }

        return false;
    }

    public bool TryForward(out string searchText)
    {
        searchText = Current;
        if (!IsActive || _forward.Count == 0)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(Current))
        {
            _back.Push(Current);
        }

        Current = _forward.Pop();
        searchText = Current;
        return true;
    }

    public void Reset()
    {
        _back.Clear();
        _forward.Clear();
        Current = string.Empty;
    }

    private static string Normalize(string? searchText)
    {
        return searchText?.Trim() ?? string.Empty;
    }
}
