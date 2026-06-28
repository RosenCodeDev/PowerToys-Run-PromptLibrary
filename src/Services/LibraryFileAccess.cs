using System.IO;

namespace Community.PowerToys.Run.Plugin.PromptLibrary.Services;

public enum LibraryFileTarget
{
    Prompts,
    Tags,
    DataFolder,
}

public readonly record struct LibraryFileMenuItem(
    LibraryFileTarget Target,
    string Title,
    int Score);

/// <summary>
/// Validates the configurable library-files command and describes its fixed menu.
/// </summary>
public static class LibraryFileAccess
{
    public const string DefaultCommand = "+";

    public static IReadOnlyList<LibraryFileMenuItem> MenuItems { get; } =
    [
        new(LibraryFileTarget.Prompts, "Edit prompts JSON", 300),
        new(LibraryFileTarget.Tags, "Edit tags JSON", 200),
        new(LibraryFileTarget.DataFolder, "Open data folder", 100),
    ];

    public static bool TryNormalizeCommand(
        string? value,
        out string normalized,
        out string error)
    {
        normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            error = "The library files command cannot be blank.";
            return false;
        }

        if (normalized.Equals("tag", StringComparison.OrdinalIgnoreCase))
        {
            error = "The library files command cannot use the reserved tag command.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    public static bool IsMatch(string? searchText, string command)
    {
        return string.Equals(
            searchText?.Trim(),
            command,
            StringComparison.OrdinalIgnoreCase);
    }

    public static string GetPath(string dataDirectory, LibraryFileTarget target)
    {
        return target switch
        {
            LibraryFileTarget.Prompts => Path.Combine(dataDirectory, "user.prompt.json"),
            LibraryFileTarget.Tags => Path.Combine(dataDirectory, "user.prompt.tag.json"),
            LibraryFileTarget.DataFolder => dataDirectory,
            _ => throw new ArgumentOutOfRangeException(nameof(target)),
        };
    }
}
