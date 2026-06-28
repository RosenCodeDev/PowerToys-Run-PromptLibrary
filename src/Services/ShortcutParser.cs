using System.Windows.Input;

namespace Community.PowerToys.Run.Plugin.PromptLibrary.Services;

/// <summary>
/// A normalized keyboard chord used by PromptLibrary navigation.
/// </summary>
public readonly record struct KeyboardShortcut(Key Key, ModifierKeys Modifiers);

/// <summary>
/// Immutable, validated shortcut assignments for navigation commands.
/// </summary>
public sealed class ShortcutConfiguration
{
    public const string DefaultBackText = "Alt+Left";
    public const string DefaultForwardText = "Alt+Right";
    public const string DefaultResetText = "Alt+Up; Alt+Down";

    private ShortcutConfiguration(
        KeyboardShortcut back,
        KeyboardShortcut forward,
        IReadOnlyList<KeyboardShortcut> reset)
    {
        Back = back;
        Forward = forward;
        Reset = reset;
    }

    public KeyboardShortcut Back { get; }

    public KeyboardShortcut Forward { get; }

    public IReadOnlyList<KeyboardShortcut> Reset { get; }

    public static ShortcutConfiguration Default { get; } = CreateDefault();

    public static bool TryCreate(
        string? backText,
        string? forwardText,
        string? resetText,
        out ShortcutConfiguration configuration,
        out string error)
    {
        configuration = Default;

        if (!ShortcutParser.TryParseSingle(backText, out var back, out error))
        {
            error = $"Back shortcut: {error}";
            return false;
        }

        if (!ShortcutParser.TryParseSingle(forwardText, out var forward, out error))
        {
            error = $"Forward shortcut: {error}";
            return false;
        }

        if (!ShortcutParser.TryParseList(resetText, out var reset, out error))
        {
            error = $"Reset shortcuts: {error}";
            return false;
        }

        var allShortcuts = new HashSet<KeyboardShortcut>();
        if (!allShortcuts.Add(back))
        {
            error = "Back shortcut is duplicated.";
            return false;
        }

        if (!allShortcuts.Add(forward))
        {
            error = "Back and Forward cannot use the same shortcut.";
            return false;
        }

        foreach (var shortcut in reset)
        {
            if (!allShortcuts.Add(shortcut))
            {
                error = "Every Back, Forward, and Reset shortcut must be unique.";
                return false;
            }
        }

        configuration = new ShortcutConfiguration(back, forward, reset);
        error = string.Empty;
        return true;
    }

    private static ShortcutConfiguration CreateDefault()
    {
        if (!ShortcutParser.TryParseSingle(DefaultBackText, out var back, out _) ||
            !ShortcutParser.TryParseSingle(DefaultForwardText, out var forward, out _) ||
            !ShortcutParser.TryParseList(DefaultResetText, out var reset, out _))
        {
            throw new InvalidOperationException("Default PromptLibrary shortcuts are invalid.");
        }

        return new ShortcutConfiguration(back, forward, reset);
    }
}

/// <summary>
/// Parses case-insensitive shortcut text such as Alt+Left or Ctrl+Shift+K.
/// </summary>
public static class ShortcutParser
{
    private static readonly Dictionary<string, ModifierKeys> ModifierAliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Alt"] = ModifierKeys.Alt,
            ["Ctrl"] = ModifierKeys.Control,
            ["Control"] = ModifierKeys.Control,
            ["Shift"] = ModifierKeys.Shift,
            ["Win"] = ModifierKeys.Windows,
            ["Windows"] = ModifierKeys.Windows,
        };

    private static readonly Dictionary<string, Key> KeyAliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Esc"] = Key.Escape,
            ["LeftArrow"] = Key.Left,
            ["RightArrow"] = Key.Right,
            ["UpArrow"] = Key.Up,
            ["DownArrow"] = Key.Down,
            ["PgUp"] = Key.PageUp,
            ["PgDn"] = Key.PageDown,
            ["Del"] = Key.Delete,
            ["Ins"] = Key.Insert,
            ["Return"] = Key.Enter,
        };

    public static bool TryParseSingle(
        string? text,
        out KeyboardShortcut shortcut,
        out string error)
    {
        shortcut = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            error = "Enter a shortcut.";
            return false;
        }

        var parts = text.Split(
            '+',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length == 0)
        {
            error = "Enter a shortcut.";
            return false;
        }

        var modifiers = ModifierKeys.None;
        Key? key = null;

        foreach (var part in parts)
        {
            if (ModifierAliases.TryGetValue(part, out var modifier))
            {
                if ((modifiers & modifier) != 0)
                {
                    error = $"Modifier '{part}' is repeated.";
                    return false;
                }

                modifiers |= modifier;
                continue;
            }

            if (key.HasValue)
            {
                error = "A shortcut must contain exactly one non-modifier key.";
                return false;
            }

            if (!TryParseKey(part, out var parsedKey))
            {
                error = $"Key '{part}' is not recognized.";
                return false;
            }

            key = parsedKey;
        }

        if (!key.HasValue)
        {
            error = "A shortcut must contain a non-modifier key.";
            return false;
        }

        shortcut = new KeyboardShortcut(key.Value, modifiers);
        error = string.Empty;
        return true;
    }

    public static bool TryParseList(
        string? text,
        out IReadOnlyList<KeyboardShortcut> shortcuts,
        out string error)
    {
        shortcuts = Array.Empty<KeyboardShortcut>();
        if (string.IsNullOrWhiteSpace(text))
        {
            error = "Enter at least one shortcut.";
            return false;
        }

        var entries = text.Split(
            ';',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var parsed = new List<KeyboardShortcut>(entries.Length);
        var unique = new HashSet<KeyboardShortcut>();

        foreach (var entry in entries)
        {
            if (!TryParseSingle(entry, out var shortcut, out error))
            {
                return false;
            }

            if (!unique.Add(shortcut))
            {
                error = $"Shortcut '{entry}' is repeated.";
                return false;
            }

            parsed.Add(shortcut);
        }

        shortcuts = parsed;
        error = string.Empty;
        return true;
    }

    private static bool TryParseKey(string text, out Key key)
    {
        if (KeyAliases.TryGetValue(text, out key))
        {
            return true;
        }

        if (!Enum.TryParse(text, true, out key))
        {
            return false;
        }

        return key is not Key.None
            and not Key.System
            and not Key.LeftAlt
            and not Key.RightAlt
            and not Key.LeftCtrl
            and not Key.RightCtrl
            and not Key.LeftShift
            and not Key.RightShift
            and not Key.LWin
            and not Key.RWin;
    }
}
