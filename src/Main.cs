using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Community.PowerToys.Run.Plugin.PromptLibrary.Models;
using Community.PowerToys.Run.Plugin.PromptLibrary.Services;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Wox.Plugin;
using Wox.Plugin.Logger;

namespace Community.PowerToys.Run.Plugin.PromptLibrary;

/// <summary>
/// PromptLibrary - PowerToys Run plugin.
/// Searches, previews, and copies prompts from prompt library JSON exports.
/// </summary>
public class Main : IPlugin, IContextMenu, ISettingProvider, IReloadable, IDisposable
{
    // Plugin metadata
    public string Name => "PromptLibrary";
    public string Description => "Search and copy prompts from your prompt library.";
    public static string PluginID => "610E1DFF57844F0A978512C381ABE794";

    // Settings keys
    private const string SettingMaxResults = "MaxResults";
    private const string SettingAutoReload = "AutoReload";
    private const string SettingCustomDataPath = "CustomDataPath";
    private const string SettingIconStyle = "IconStyle";
    private const string SettingBackShortcut = "BackShortcut";
    private const string SettingForwardShortcut = "ForwardShortcut";
    private const string SettingResetShortcuts = "ResetShortcuts";
    private const string SettingLibraryFilesCommand = "LibraryFilesCommand";

    // Icon style values
    private const int IconStyleThinking = 0; // chat-centered-dots (default)
    private const int IconStyleMinimal = 1;  // chat-centered

    // Settings values
    private int _maxResults = 20;
    private bool _autoReload = true;
    private string _customDataPath = string.Empty;
    private int _iconStyle = IconStyleThinking;
    private string _backShortcutText = ShortcutConfiguration.DefaultBackText;
    private string _forwardShortcutText = ShortcutConfiguration.DefaultForwardText;
    private string _resetShortcutText = ShortcutConfiguration.DefaultResetText;
    private string _libraryFilesCommand = LibraryFileAccess.DefaultCommand;
    private ShortcutConfiguration _shortcuts = ShortcutConfiguration.Default;

    // Services
    private readonly PromptDataService _dataService = new();
    private readonly NavigationHistory _navigationHistory = new();
    private readonly object _navigationLock = new();
    private PluginInitContext? _context;
    private string _pluginDir = string.Empty;
    private string _iconPath = string.Empty;
    private Theme _currentTheme = Theme.Light;
    private bool _dataLoaded;
    private string _activeActionKeyword = string.Empty;
    private string _knownActionKeyword = string.Empty;

    // Scoped PowerToys Run input handling
    private bool _inputListenerAttached;
    private Window? _hostWindow;
    private TextChangedEventHandler? _hostTextChangedHandler;

    // File watcher for auto-reload
    private FileSystemWatcher? _fileWatcher;
    private Timer? _debounceTimer;
    private readonly object _debounceLock = new();

    // -- IPlugin --

    public void Init(PluginInitContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        
        // Resolve plugin directory from metadata
        _pluginDir = context.CurrentPluginMetadata.PluginDirectory;
        _knownActionKeyword = context.CurrentPluginMetadata.ActionKeyword?.Trim() ?? string.Empty;

        // Set icon path based on theme
        UpdateIconPath(Theme.Light);

        Log.Info($"PromptLibrary: Initializing from {_pluginDir}", typeof(Main));

        // Load data
        _dataLoaded = LoadPromptData();

        // PowerToys does not forward Alt+Arrow accelerators to plugins, so listen inside
        // the host WPF process and strictly scope handling to an active PromptLibrary query.
        ScheduleAttachInputListener();

        // Start file watcher if auto-reload is enabled
        if (_autoReload)
        {
            StartFileWatcher();
        }
    }

    public List<Result> Query(Query query)
    {
        var searchQuery = (query.Search ?? string.Empty).Trim();
        ObserveNavigationQuery(searchQuery, query.ActionKeyword);

        if (LibraryFileAccess.IsMatch(searchQuery, _libraryFilesCommand))
        {
            return CreateLibraryFileResults();
        }

        if (!_dataLoaded)
        {
            return new List<Result>
            {
                CreateErrorResult(
                    "Prompt data not found",
                    "Add or repair user.prompt.json, or use the library files command.")
            };
        }

        // Special keyword: show all available tags
        if (searchQuery.Equals("tag", StringComparison.OrdinalIgnoreCase))
        {
            var tags = _dataService.GetTagsWithCount().Take(_maxResults).ToList();

            if (tags.Count == 0)
            {
                return new List<Result>
                {
                    CreateErrorResult("No tags found", "No tags are associated with any prompts in your library.")
                };
            }

            return tags.Select(tw => CreateTagResult(tw, query.ActionKeyword)).ToList();
        }

        var resolvedPrompts = _dataService.GetResolvedPrompts();
        if (resolvedPrompts.Count == 0)
        {
            return new List<Result>
            {
                CreateErrorResult(
                    "No prompts found",
                    $"Add prompts to user.prompt.json or type {_libraryFilesCommand} to open the library files.")
            };
        }

        var scored = PromptLibrarySearchService.Search(resolvedPrompts, searchQuery, _maxResults);

        return scored.Select(sp => CreatePromptResult(sp)).ToList();
    }

    // -- IContextMenu --

    public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
    {
        if (selectedResult.ContextData is not PromptContextData data)
        {
            return new List<ContextMenuResult>();
        }

        var results = new List<ContextMenuResult>
        {
            new ContextMenuResult
            {
                PluginName = Name,
                Title = "Copy prompt (Ctrl+C)",
                Glyph = "\xE8C8", // Copy icon
                FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                AcceleratorKey = System.Windows.Input.Key.C,
                AcceleratorModifiers = System.Windows.Input.ModifierKeys.Control,
                Action = _ =>
                {
                    ClipboardHelper.CopyToClipboard($"{data.Title}\n\n{data.PromptBody}");
                    return true;
                },
            },
            new ContextMenuResult
            {
                PluginName = Name,
                Title = "Copy prompt title (Ctrl+T)",
                Glyph = "\xE8C8",
                FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                AcceleratorKey = System.Windows.Input.Key.T,
                AcceleratorModifiers = System.Windows.Input.ModifierKeys.Control,
                Action = _ =>
                {
                    ClipboardHelper.CopyToClipboard(data.Title);
                    return true;
                },
            },
            new ContextMenuResult
            {
                PluginName = Name,
                Title = "Open data folder (Ctrl+O)",
                Glyph = "\xE838", // Folder icon
                FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                AcceleratorKey = System.Windows.Input.Key.O,
                AcceleratorModifiers = System.Windows.Input.ModifierKeys.Control,
                Action = _ =>
                {
                    OpenDataFolder();
                    return true;
                },
            },
        };

        return results;
    }

    // -- ISettingProvider --

    public IEnumerable<PluginAdditionalOption> AdditionalOptions => new List<PluginAdditionalOption>
    {
        new PluginAdditionalOption
        {
            Key = SettingBackShortcut,
            DisplayLabel = "Back shortcut",
            DisplayDescription = "Go to the previous PromptLibrary level. Example: Alt+Left.",
            PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
            TextValue = _backShortcutText,
            PlaceholderText = ShortcutConfiguration.DefaultBackText,
        },
        new PluginAdditionalOption
        {
            Key = SettingForwardShortcut,
            DisplayLabel = "Forward shortcut",
            DisplayDescription = "Go forward after using Back. Example: Alt+Right.",
            PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
            TextValue = _forwardShortcutText,
            PlaceholderText = ShortcutConfiguration.DefaultForwardText,
        },
        new PluginAdditionalOption
        {
            Key = SettingResetShortcuts,
            DisplayLabel = "Reset shortcuts",
            DisplayDescription = "Return to the empty PromptLibrary root. Separate alternatives with semicolons. Escape is supported but overrides Run's close shortcut while PromptLibrary is active.",
            PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
            TextValue = _resetShortcutText,
            PlaceholderText = ShortcutConfiguration.DefaultResetText,
        },
        new PluginAdditionalOption
        {
            Key = SettingLibraryFilesCommand,
            DisplayLabel = "Library files command",
            DisplayDescription = "Command after the action keyword that opens prompt and tag file actions. Example: + creates /p +.",
            PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
            TextValue = _libraryFilesCommand,
            PlaceholderText = LibraryFileAccess.DefaultCommand,
        },
        new PluginAdditionalOption
        {
            Key = SettingIconStyle,
            DisplayLabel = "Plugin icon style",
            DisplayDescription = "Choose the icon shown on search results in the Run launcher.",
            PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Combobox,
            ComboBoxItems = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("Thinking", IconStyleThinking.ToString()),
                new KeyValuePair<string, string>("Minimal", IconStyleMinimal.ToString()),
            },
            ComboBoxValue = _iconStyle,
        },
        new PluginAdditionalOption
        {
            Key = SettingMaxResults,
            DisplayLabel = "Maximum results",
            DisplayDescription = "Maximum number of prompt results to show (1-50).",
            PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Numberbox,
            NumberValue = _maxResults,
            NumberBoxMin = 1,
            NumberBoxMax = 50,
        },
        new PluginAdditionalOption
        {
            Key = SettingAutoReload,
            DisplayLabel = "Auto-reload on file changes",
            DisplayDescription = "Automatically reload prompts when JSON files are modified.",
            Value = _autoReload,
        },
        new PluginAdditionalOption
        {
            Key = SettingCustomDataPath,
            DisplayLabel = "Custom data folder path",
            DisplayDescription = "Leave empty to use the default Data folder inside the plugin directory.",
            PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
            TextValue = _customDataPath,
        },
    };

    public Control CreateSettingPanel()
    {
        throw new NotImplementedException();
    }

    public void UpdateSettings(PowerLauncherPluginSettings settings)
    {
        if (settings?.AdditionalOptions == null)
        {
            return;
        }

        var oldAutoReload = _autoReload;
        var oldCustomDataPath = _customDataPath;
        var candidateBackShortcut = _backShortcutText;
        var candidateForwardShortcut = _forwardShortcutText;
        var candidateResetShortcuts = _resetShortcutText;
        var candidateLibraryFilesCommand = _libraryFilesCommand;

        foreach (var option in settings.AdditionalOptions)
        {
            switch (option.Key)
            {
                case SettingBackShortcut:
                    candidateBackShortcut = option.TextValue?.Trim() ?? string.Empty;
                    break;
                case SettingForwardShortcut:
                    candidateForwardShortcut = option.TextValue?.Trim() ?? string.Empty;
                    break;
                case SettingResetShortcuts:
                    candidateResetShortcuts = option.TextValue?.Trim() ?? string.Empty;
                    break;
                case SettingLibraryFilesCommand:
                    candidateLibraryFilesCommand = option.TextValue?.Trim() ?? string.Empty;
                    break;
                case SettingIconStyle:
                    _iconStyle = option.ComboBoxValue;
                    // Re-apply icon path with updated style, preserving current theme
                    UpdateIconPath(_currentTheme);
                    break;
                case SettingMaxResults:
                    _maxResults = Math.Clamp((int)option.NumberValue, 1, 50);
                    break;
                case SettingAutoReload:
                    _autoReload = option.Value;
                    break;
                case SettingCustomDataPath:
                    _customDataPath = option.TextValue?.Trim() ?? string.Empty;
                    break;
            }
        }

        if (ShortcutConfiguration.TryCreate(
            candidateBackShortcut,
            candidateForwardShortcut,
            candidateResetShortcuts,
            out var shortcuts,
            out var shortcutError))
        {
            lock (_navigationLock)
            {
                _shortcuts = shortcuts;
                _backShortcutText = candidateBackShortcut;
                _forwardShortcutText = candidateForwardShortcut;
                _resetShortcutText = candidateResetShortcuts;
            }
        }
        else
        {
            Log.Warn(
                $"PromptLibrary: Invalid navigation shortcut settings; retaining the previous valid configuration. {shortcutError}",
                typeof(Main));
        }

        if (LibraryFileAccess.TryNormalizeCommand(
            candidateLibraryFilesCommand,
            out var libraryFilesCommand,
            out var libraryFilesCommandError))
        {
            _libraryFilesCommand = libraryFilesCommand;
        }
        else
        {
            Log.Warn(
                $"PromptLibrary: Invalid library files command; retaining the previous valid value. {libraryFilesCommandError}",
                typeof(Main));
        }

        // If auto-reload setting changed, start/stop watcher
        if (_autoReload != oldAutoReload)
        {
            if (_autoReload)
            {
                StartFileWatcher();
            }
            else
            {
                StopFileWatcher();
            }
        }

        // If data path changed, reload data and restart watcher
        if (_customDataPath != oldCustomDataPath)
        {
            _dataLoaded = LoadPromptData();
            if (_autoReload)
            {
                StopFileWatcher();
                StartFileWatcher();
            }
        }
    }

    // -- IReloadable --

    public void ReloadData()
    {
        Log.Info("PromptLibrary: ReloadData called", typeof(Main));
        ReloadPromptDataPreservingLastSnapshot();
    }

    // -- Theme handling --

    private void UpdateIconPath(Theme theme)
    {
        _currentTheme = theme;
        var isLight = theme == Theme.Light || theme == Theme.HighContrastWhite;
        var themeSuffix = isLight ? "light" : "dark";
        var iconPrefix = _iconStyle == IconStyleMinimal ? "prompt-minimal" : "prompt";
        _iconPath = $"Images/{iconPrefix}.{themeSuffix}.png";
    }

    // -- Private helpers --

    private string GetDataDir()
    {
        if (!string.IsNullOrWhiteSpace(_customDataPath) && Directory.Exists(_customDataPath))
        {
            return _customDataPath;
        }

        return Path.Combine(_pluginDir, "Data");
    }

    private bool LoadPromptData()
    {
        var dataDir = GetDataDir();
        Log.Info($"PromptLibrary: Loading data from {dataDir}", typeof(Main));

        if (!Directory.Exists(dataDir))
        {
            Log.Error($"PromptLibrary: Data directory does not exist: {dataDir}", typeof(Main));
            return false;
        }

        return _dataService.LoadData(dataDir);
    }

    private void ReloadPromptDataPreservingLastSnapshot()
    {
        if (LoadPromptData())
        {
            _dataLoaded = true;
        }
        else if (_dataLoaded)
        {
            Log.Warn(
                "PromptLibrary: Reload failed; continuing to use the last valid library snapshot.",
                typeof(Main));
        }
    }

    private List<Result> CreateLibraryFileResults()
    {
        var dataDir = GetDataDir();
        return LibraryFileAccess.MenuItems
            .Select(item =>
            {
                var path = LibraryFileAccess.GetPath(dataDir, item.Target);
                var exists = item.Target == LibraryFileTarget.DataFolder
                    ? Directory.Exists(path)
                    : File.Exists(path);

                return new Result
                {
                    Title = item.Title,
                    SubTitle = exists
                        ? path
                        : $"Not found — open the data folder to repair: {path}",
                    IcoPath = _iconPath,
                    Score = item.Score,
                    Action = _ =>
                    {
                        EndNavigationSession();
                        if (item.Target == LibraryFileTarget.DataFolder)
                        {
                            OpenDataFolder();
                        }
                        else
                        {
                            OpenLibraryFile(path);
                        }

                        return true;
                    },
                };
            })
            .ToList();
    }

    private void OpenLibraryFile(string path)
    {
        if (!File.Exists(path))
        {
            Log.Warn(
                $"PromptLibrary: Library file not found; opening data folder instead: {path}",
                typeof(Main));
            OpenDataFolder();
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
            });
        }
        catch (Exception associationException)
        {
            Log.Warn(
                $"PromptLibrary: Default JSON editor failed; falling back to Notepad. {associationException.Message}",
                typeof(Main));

            try
            {
                var notepad = new ProcessStartInfo
                {
                    FileName = "notepad.exe",
                    UseShellExecute = true,
                };
                notepad.ArgumentList.Add(path);
                Process.Start(notepad);
            }
            catch (Exception notepadException)
            {
                Log.Error(
                    $"PromptLibrary: Failed to open library file: {notepadException.Message}",
                    typeof(Main));
                OpenDataFolder();
            }
        }
    }

    private void OpenDataFolder()
    {
        var dataDir = GetDataDir();
        var target = Directory.Exists(dataDir) ? dataDir : _pluginDir;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = target,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Log.Error(
                $"PromptLibrary: Failed to open data folder: {ex.Message}",
                typeof(Main));
        }
    }

    private Result CreatePromptResult(ScoredPrompt sp)
    {
        var rp = sp.Prompt;
        var prompt = rp.Source;

        // Build subtitle: tag names + truncated body preview
        var subtitle = BuildSubtitle(rp.TagDisplay, prompt.Prompt);

        return new Result
        {
            Title = prompt.Act,
            SubTitle = subtitle,
            IcoPath = _iconPath,
            ToolTipData = new ToolTipData(prompt.Act, TruncateText(prompt.Prompt, 500)),
            Score = sp.Score,
            ContextData = new PromptContextData
            {
                Title = prompt.Act,
                PromptBody = prompt.Prompt,
            },
            Action = _ =>
            {
                // Selecting a prompt ends this navigation session because Run closes.
                EndNavigationSession();
                var copied = ClipboardHelper.CopyToClipboard(prompt.Prompt);
                if (!copied)
                {
                    Log.Error("PromptLibrary: Failed to copy prompt to clipboard", typeof(Main));
                }
                return true;
            },
        };
    }

    private Result CreateTagResult(TagWithCount tw, string actionKeyword)
    {
        var promptWord = tw.Count == 1 ? "prompt" : "prompts";
        var subtitle = $"Tag \u2014 {tw.Count} {promptWord}";

        return new Result
        {
            Title = tw.Tag.Name,
            SubTitle = subtitle,
            IcoPath = _iconPath,
            ToolTipData = new ToolTipData(tw.Tag.Name, subtitle),
            Score = tw.Count,
            ContextData = null, // Explicitly null: prevents the "Copy prompt" context menu from appearing
            Action = _ =>
            {
                // Use the user's configured ActionKeyword (not hardcoded "p:") so custom keywords work
                NavigateToTag(tw.Tag.Name, actionKeyword);
                var newQuery = string.IsNullOrEmpty(actionKeyword)
                    ? tw.Tag.Name
                    : $"{actionKeyword} {tw.Tag.Name}";
                _context?.API.ChangeQuery(newQuery, true);
                return false; // Keep the window open so results update immediately
            },
        };
    }

    private Result CreateErrorResult(string title, string subtitle)
    {
        return new Result
        {
            Title = title,
            SubTitle = subtitle,
            IcoPath = _iconPath,
            Action = _ =>
            {
                // Open data folder on click for convenience
                var dataDir = GetDataDir();
                try
                {
                    if (Directory.Exists(dataDir))
                    {
                        Process.Start(new ProcessStartInfo { FileName = dataDir, UseShellExecute = true });
                    }
                    else
                    {
                        // Open plugin directory instead
                        Process.Start(new ProcessStartInfo { FileName = _pluginDir, UseShellExecute = true });
                    }
                }
                catch { }
                return true;
            },
        };
    }

    private static string BuildSubtitle(string tagDisplay, string promptBody)
    {
        var preview = TruncateText(promptBody, 80);

        if (string.IsNullOrEmpty(tagDisplay))
        {
            return preview;
        }

        return $"{tagDisplay} — {preview}";
    }

    private static string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        // Clean up newlines for display
        var clean = text.Replace("\r\n", " ").Replace("\n", " ").Replace("\\n", " ").Trim();

        if (clean.Length <= maxLength)
        {
            return clean;
        }

        return clean[..maxLength] + "…";
    }

    // -- Explorer-style navigation --

    private enum NavigationCommand
    {
        Back,
        Forward,
        Reset,
    }

    private void ObserveNavigationQuery(string searchText, string? actionKeyword)
    {
        ScheduleAttachInputListener();

        var normalizedKeyword = actionKeyword?.Trim() ?? string.Empty;
        lock (_navigationLock)
        {
            // Navigation shortcuts are intentionally disabled for global queries. Without
            // an action keyword, a plugin cannot safely know that the user is interacting
            // with one of its results rather than another global result.
            if (string.IsNullOrEmpty(normalizedKeyword))
            {
                _navigationHistory.End();
                _activeActionKeyword = string.Empty;
                return;
            }

            _knownActionKeyword = normalizedKeyword;
            if (!_navigationHistory.IsActive ||
                !string.Equals(_activeActionKeyword, normalizedKeyword, StringComparison.Ordinal))
            {
                _activeActionKeyword = normalizedKeyword;
                _navigationHistory.Start(searchText);
                return;
            }

            _navigationHistory.ObserveUserQuery(searchText);
        }
    }

    private void NavigateToTag(string tagName, string? actionKeyword)
    {
        var normalizedKeyword = actionKeyword?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(normalizedKeyword))
        {
            return;
        }

        lock (_navigationLock)
        {
            if (!_navigationHistory.IsActive ||
                !string.Equals(_activeActionKeyword, normalizedKeyword, StringComparison.Ordinal))
            {
                _activeActionKeyword = normalizedKeyword;
                _navigationHistory.Start("tag");
            }

            _navigationHistory.NavigateTo(tagName);
        }
    }

    private void EndNavigationSession()
    {
        lock (_navigationLock)
        {
            _navigationHistory.End();
            _activeActionKeyword = string.Empty;
        }
    }

    private void ScheduleAttachInputListener()
    {
        var application = Application.Current;
        if (application?.Dispatcher == null || application.Dispatcher.HasShutdownStarted)
        {
            return;
        }

        if (application.Dispatcher.CheckAccess())
        {
            AttachInputListener();
        }
        else
        {
            _ = application.Dispatcher.BeginInvoke(AttachInputListener);
        }
    }

    private void AttachInputListener()
    {
        if (!_inputListenerAttached)
        {
            InputManager.Current.PreProcessInput += OnPreProcessInput;
            _inputListenerAttached = true;
        }

        var currentWindow = Application.Current?.MainWindow;
        if (currentWindow == null || ReferenceEquals(currentWindow, _hostWindow))
        {
            return;
        }

        DetachHostWindowEvents();
        _hostWindow = currentWindow;
        _hostWindow.IsVisibleChanged += OnHostWindowVisibilityChanged;
        _hostTextChangedHandler = OnHostTextChanged;
        _hostWindow.AddHandler(TextBox.TextChangedEvent, _hostTextChangedHandler, true);
    }

    private void OnPreProcessInput(object sender, PreProcessInputEventArgs e)
    {
        if (e.StagingItem.Input is not KeyEventArgs keyEvent ||
            (keyEvent.RoutedEvent != Keyboard.PreviewKeyDownEvent &&
             keyEvent.RoutedEvent != Keyboard.KeyDownEvent) ||
            keyEvent.IsRepeat)
        {
            return;
        }

        var key = keyEvent.Key == Key.System ? keyEvent.SystemKey : keyEvent.Key;
        var pressed = new KeyboardShortcut(key, Keyboard.Modifiers);
        NavigationCommand? command = null;

        lock (_navigationLock)
        {
            if (pressed == _shortcuts.Back)
            {
                command = NavigationCommand.Back;
            }
            else if (pressed == _shortcuts.Forward)
            {
                command = NavigationCommand.Forward;
            }
            else if (_shortcuts.Reset.Contains(pressed))
            {
                command = NavigationCommand.Reset;
            }
        }

        if (!command.HasValue || !IsExplicitPromptLibraryActive())
        {
            return;
        }

        HandleNavigationCommand(command.Value);

        // Mark and cancel the staged key event before PowerToys Run handles its own
        // Left/Right navigation or Escape binding.
        keyEvent.Handled = true;
        e.Cancel();
    }

    private bool IsExplicitPromptLibraryActive()
    {
        var window = _hostWindow ?? Application.Current?.MainWindow;
        if (window == null || !window.IsVisible || !window.IsActive)
        {
            return false;
        }

        string actionKeyword;
        lock (_navigationLock)
        {
            if (!_navigationHistory.IsActive || string.IsNullOrEmpty(_activeActionKeyword))
            {
                return false;
            }

            actionKeyword = _activeActionKeyword;
        }

        return Keyboard.FocusedElement is TextBox textBox &&
            PromptQueryScope.IsExplicitQuery(textBox.Text, actionKeyword);
    }

    private void HandleNavigationCommand(NavigationCommand command)
    {
        string actionKeyword;
        string targetSearch;
        bool queryChanged;

        lock (_navigationLock)
        {
            actionKeyword = _activeActionKeyword;
            targetSearch = _navigationHistory.Current;
            queryChanged = command switch
            {
                NavigationCommand.Back => _navigationHistory.TryBack(out targetSearch),
                NavigationCommand.Forward => _navigationHistory.TryForward(out targetSearch),
                NavigationCommand.Reset => ResetNavigation(out targetSearch),
                _ => false,
            };
        }

        // Back at the root and Forward without history are consumed but intentionally
        // leave the query unchanged.
        if (queryChanged && !string.IsNullOrEmpty(actionKeyword))
        {
            _context?.API.ChangeQuery(PromptQueryScope.BuildQuery(actionKeyword, targetSearch), true);
        }
    }

    private bool ResetNavigation(out string searchText)
    {
        var changed = !string.IsNullOrEmpty(_navigationHistory.Current) ||
            _navigationHistory.BackCount > 0 ||
            _navigationHistory.ForwardCount > 0;
        _navigationHistory.Reset();
        searchText = string.Empty;
        return changed;
    }

    private void OnHostTextChanged(object sender, TextChangedEventArgs e)
    {
        if (e.OriginalSource is not TextBox textBox)
        {
            return;
        }

        string actionKeyword;
        lock (_navigationLock)
        {
            if (!_navigationHistory.IsActive)
            {
                return;
            }

            actionKeyword = _activeActionKeyword;
        }

        if (!PromptQueryScope.IsExplicitQuery(textBox.Text, actionKeyword))
        {
            EndNavigationSession();
        }
    }

    private void OnHostWindowVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is false)
        {
            EndNavigationSession();
        }
        else
        {
            _ = Application.Current.Dispatcher.BeginInvoke(TryStartRestoredNavigationSession);
        }
    }

    private void TryStartRestoredNavigationSession()
    {
        if (_hostWindow == null || !_hostWindow.IsVisible ||
            Keyboard.FocusedElement is not TextBox textBox)
        {
            return;
        }

        string knownActionKeyword;
        lock (_navigationLock)
        {
            if (_navigationHistory.IsActive)
            {
                return;
            }

            knownActionKeyword = _knownActionKeyword;
        }

        if (!PromptQueryScope.TryGetSearchText(
            textBox.Text,
            knownActionKeyword,
            out var searchText))
        {
            return;
        }

        lock (_navigationLock)
        {
            if (!_navigationHistory.IsActive)
            {
                _activeActionKeyword = knownActionKeyword;
                _navigationHistory.Start(searchText);
            }
        }
    }

    private void DetachInputListener()
    {
        if (_inputListenerAttached)
        {
            InputManager.Current.PreProcessInput -= OnPreProcessInput;
            _inputListenerAttached = false;
        }

        DetachHostWindowEvents();
    }

    private void DetachHostWindowEvents()
    {
        if (_hostWindow != null)
        {
            _hostWindow.IsVisibleChanged -= OnHostWindowVisibilityChanged;
            if (_hostTextChangedHandler != null)
            {
                _hostWindow.RemoveHandler(TextBox.TextChangedEvent, _hostTextChangedHandler);
            }
        }

        _hostTextChangedHandler = null;
        _hostWindow = null;
    }

    // -- File watching --

    private void StartFileWatcher()
    {
        StopFileWatcher();

        var dataDir = GetDataDir();
        if (!Directory.Exists(dataDir))
        {
            Log.Warn($"PromptLibrary: Cannot start file watcher — data dir does not exist: {dataDir}", typeof(Main));
            return;
        }

        try
        {
            _fileWatcher = new FileSystemWatcher(dataDir, "*.json")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                EnableRaisingEvents = true,
            };

            _fileWatcher.Changed += OnDataFileChanged;
            _fileWatcher.Created += OnDataFileChanged;
            _fileWatcher.Renamed += (_, e) => OnDataFileChanged(null, e);

            Log.Info($"PromptLibrary: File watcher started on {dataDir}", typeof(Main));
        }
        catch (Exception ex)
        {
            Log.Error($"PromptLibrary: Failed to start file watcher: {ex.Message}", typeof(Main));
        }
    }

    private void StopFileWatcher()
    {
        if (_fileWatcher != null)
        {
            _fileWatcher.EnableRaisingEvents = false;
            _fileWatcher.Dispose();
            _fileWatcher = null;
        }

        lock (_debounceLock)
        {
            _debounceTimer?.Dispose();
            _debounceTimer = null;
        }
    }

    private void OnDataFileChanged(object? sender, FileSystemEventArgs e)
    {
        // Debounce: wait 500ms after last change before reloading
        // This avoids reloading multiple times when a file is being written
        lock (_debounceLock)
        {
            _debounceTimer?.Dispose();
            _debounceTimer = new Timer(_ =>
            {
                Log.Info($"PromptLibrary: Data file changed ({e.Name}), reloading...", typeof(Main));
                ReloadPromptDataPreservingLastSnapshot();
            }, null, 500, Timeout.Infinite);
        }
    }

    // -- IDisposable --

    public void Dispose()
    {
        EndNavigationSession();

        var application = Application.Current;
        if (application?.Dispatcher != null && !application.Dispatcher.HasShutdownStarted)
        {
            if (application.Dispatcher.CheckAccess())
            {
                DetachInputListener();
            }
            else
            {
                application.Dispatcher.Invoke(DetachInputListener);
            }
        }

        StopFileWatcher();
        _dataService.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Context data passed from Query results to context menu actions.
/// </summary>
internal class PromptContextData
{
    public required string Title { get; init; }
    public required string PromptBody { get; init; }
}
