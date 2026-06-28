namespace Community.PowerToys.Run.Plugin.PromptLibrary.Services;

/// <summary>
/// Helpers for recognizing and composing explicitly activated PromptLibrary queries.
/// </summary>
public static class PromptQueryScope
{
    public static bool IsExplicitQuery(string? fullQuery, string? actionKeyword)
    {
        if (string.IsNullOrEmpty(fullQuery) || string.IsNullOrEmpty(actionKeyword) ||
            !fullQuery.StartsWith(actionKeyword, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return fullQuery.Length == actionKeyword.Length ||
            char.IsWhiteSpace(fullQuery[actionKeyword.Length]);
    }

    public static string BuildQuery(string actionKeyword, string searchText)
    {
        return string.IsNullOrEmpty(searchText)
            ? actionKeyword
            : $"{actionKeyword} {searchText}";
    }

    public static bool TryGetSearchText(
        string? fullQuery,
        string? actionKeyword,
        out string searchText)
    {
        searchText = string.Empty;
        if (!IsExplicitQuery(fullQuery, actionKeyword))
        {
            return false;
        }

        searchText = fullQuery![actionKeyword!.Length..].TrimStart();
        return true;
    }
}
