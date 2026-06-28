using System.IO;
using System.Windows.Input;
using Community.PowerToys.Run.Plugin.PromptLibrary.Services;

var tests = new (string Name, Action Run)[]
{
    ("Phrase Back and Forward", TestPhraseBackAndForward),
    ("Tag two-level traversal", TestTagTraversal),
    ("Typing clears Forward", TestTypingClearsForward),
    ("Reset clears all history", TestReset),
    ("Root operations are no-ops", TestRootNoOps),
    ("Default and custom shortcuts parse", TestShortcutParsing),
    ("Invalid and duplicate shortcuts are rejected", TestInvalidShortcuts),
    ("Custom action keywords are scoped exactly", TestQueryScope),
    ("Library files command validates and matches", TestLibraryFilesCommand),
    ("Library files menu is fixed and ordered", TestLibraryFilesMenu),
    ("Minimal and extended prompt JSON parses", TestPromptJsonParsing),
    ("Tag JSON supports short IDs and optional metadata", TestTagJsonParsing),
    ("Malformed records are skipped safely", TestMalformedRecords),
    ("Invalid top-level JSON is rejected", TestInvalidDocuments),
    ("Repository prompt library parses", TestRepositoryLibrary),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS  {test.Name}");
    }
    catch (Exception ex)
    {
        failures.Add($"{test.Name}: {ex.Message}");
        Console.WriteLine($"FAIL  {test.Name}: {ex.Message}");
    }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine($"{failures.Count} logic test(s) failed.");
    return 1;
}

Console.WriteLine($"All {tests.Length} PromptLibrary logic tests passed.");
return 0;

static void TestPhraseBackAndForward()
{
    var history = new NavigationHistory();
    history.Start("design");

    Assert(history.TryBack(out var query), "Back should clear a first-level phrase.");
    Equal(string.Empty, query);
    Equal(1, history.ForwardCount);

    Assert(history.TryForward(out query), "Forward should restore the phrase.");
    Equal("design", query);
    Equal(0, history.ForwardCount);
}

static void TestTagTraversal()
{
    var history = new NavigationHistory();
    history.Start("tag");
    history.NavigateTo("Consulting");

    Assert(history.TryBack(out var query), "Back should return to the tag list.");
    Equal("tag", query);

    Assert(history.TryBack(out query), "A second Back should return to the root.");
    Equal(string.Empty, query);

    Assert(history.TryForward(out query), "Forward should restore the tag list.");
    Equal("tag", query);

    Assert(history.TryForward(out query), "A second Forward should restore the chosen tag.");
    Equal("Consulting", query);
}

static void TestTypingClearsForward()
{
    var history = new NavigationHistory();
    history.Start("tag");
    history.NavigateTo("Consulting");
    Assert(history.TryBack(out _), "Test setup should create Forward history.");

    history.ObserveUserQuery("design");
    Equal(0, history.ForwardCount);
    Assert(!history.TryForward(out _), "Forward must be unavailable after direct editing.");
}

static void TestReset()
{
    var history = new NavigationHistory();
    history.Start("tag");
    history.NavigateTo("Consulting");
    Assert(history.TryBack(out _), "Test setup should create Forward history.");

    history.Reset();
    Equal(string.Empty, history.Current);
    Equal(0, history.BackCount);
    Equal(0, history.ForwardCount);
    Assert(!history.TryForward(out _), "Reset must discard Forward history.");
}

static void TestRootNoOps()
{
    var history = new NavigationHistory();
    history.Start(string.Empty);

    Assert(!history.TryBack(out var back), "Back at the root should be a no-op.");
    Equal(string.Empty, back);
    Assert(!history.TryForward(out var forward), "Forward without history should be a no-op.");
    Equal(string.Empty, forward);
}

static void TestShortcutParsing()
{
    Assert(
        ShortcutConfiguration.TryCreate(
            ShortcutConfiguration.DefaultBackText,
            ShortcutConfiguration.DefaultForwardText,
            ShortcutConfiguration.DefaultResetText,
            out var defaults,
            out var error),
        error);

    Equal(new KeyboardShortcut(Key.Left, ModifierKeys.Alt), defaults.Back);
    Equal(new KeyboardShortcut(Key.Right, ModifierKeys.Alt), defaults.Forward);
    Equal(2, defaults.Reset.Count);

    Assert(
        ShortcutConfiguration.TryCreate(
            "control+leftarrow",
            "CTRL+RightArrow",
            "escape; Shift+F12",
            out var custom,
            out error),
        error);

    Equal(new KeyboardShortcut(Key.Left, ModifierKeys.Control), custom.Back);
    Equal(new KeyboardShortcut(Key.Escape, ModifierKeys.None), custom.Reset[0]);
}

static void TestInvalidShortcuts()
{
    Assert(
        !ShortcutConfiguration.TryCreate(
            "Alt+Left",
            "Alt+Left",
            "Alt+Up",
            out _,
            out _),
        "Duplicate assignments should be rejected.");

    Assert(
        !ShortcutConfiguration.TryCreate(
            "Alt+NotAKey",
            "Alt+Right",
            "Alt+Up",
            out _,
            out _),
        "Unknown keys should be rejected.");
}

static void TestQueryScope()
{
    Assert(PromptQueryScope.IsExplicitQuery("/p", "/p"), "The action keyword alone is in scope.");
    Assert(PromptQueryScope.IsExplicitQuery("/P design", "/p"), "Action keyword matching is case-insensitive.");
    Assert(!PromptQueryScope.IsExplicitQuery("/prompt design", "/p"), "Prefix-only matches must be rejected.");
    Assert(!PromptQueryScope.IsExplicitQuery("design", string.Empty), "Global queries must be rejected.");
    Equal("/custom", PromptQueryScope.BuildQuery("/custom", string.Empty));
    Equal("/custom design", PromptQueryScope.BuildQuery("/custom", "design"));
    Assert(
        PromptQueryScope.TryGetSearchText("/custom   design", "/custom", out var restored),
        "A restored explicit query should be recognized.");
    Equal("design", restored);
}

static void TestLibraryFilesCommand()
{
    Assert(
        LibraryFileAccess.TryNormalizeCommand(" + ", out var command, out var error),
        error);
    Equal("+", command);
    Assert(LibraryFileAccess.IsMatch("+", command), "Default command should match.");
    Assert(LibraryFileAccess.IsMatch(" + ", command), "Command matching should trim input.");

    Assert(
        LibraryFileAccess.TryNormalizeCommand("EDIT", out command, out error),
        error);
    Assert(LibraryFileAccess.IsMatch("edit", command), "Matching should ignore case.");

    Assert(
        !LibraryFileAccess.TryNormalizeCommand(" ", out _, out _),
        "Blank commands should be rejected.");
    Assert(
        !LibraryFileAccess.TryNormalizeCommand("TAG", out _, out _),
        "The tag command should remain reserved.");
}

static void TestLibraryFilesMenu()
{
    Equal(3, LibraryFileAccess.MenuItems.Count);
    Equal("Edit prompts JSON", LibraryFileAccess.MenuItems[0].Title);
    Equal("Edit tags JSON", LibraryFileAccess.MenuItems[1].Title);
    Equal("Open data folder", LibraryFileAccess.MenuItems[2].Title);

    var root = Path.Combine("C:", "Prompts");
    Equal(
        Path.Combine(root, "user.prompt.json"),
        LibraryFileAccess.GetPath(root, LibraryFileTarget.Prompts));
    Equal(
        Path.Combine(root, "user.prompt.tag.json"),
        LibraryFileAccess.GetPath(root, LibraryFileTarget.Tags));
    Equal(root, LibraryFileAccess.GetPath(root, LibraryFileTarget.DataFolder));
}

static void TestPromptJsonParsing()
{
    var minimal = PromptLibraryJsonParser.ParsePrompts(
        """
        [
          {
            "act": "Rewrite Clearly",
            "prompt": "Rewrite this text clearly."
          }
        ]
        """);

    Assert(minimal.Success, minimal.Error);
    Equal(1, minimal.Items.Count);
    Equal(0, minimal.Items[0].Tags.Count);
    Equal(string.Empty, minimal.Items[0].Id);

    var extended = PromptLibraryJsonParser.ParsePrompts(
        """
        [
          {
            "ACT": "Tagged prompt",
            "prompt": "Line one\nLine two",
            "id": "short-prompt-id",
            "disabled": true,
            "tags": ["writing", "research", "WRITING"],
            "extra": { "portable": true }
          }
        ]
        """);

    Assert(extended.Success, extended.Error);
    Equal(1, extended.Items.Count);
    Equal(2, extended.Items[0].Tags.Count);
    Equal("short-prompt-id", extended.Items[0].Id);
    Equal("Line one\nLine two", extended.Items[0].Prompt);
}

static void TestTagJsonParsing()
{
    var result = PromptLibraryJsonParser.ParseTags(
        """
        [
          {
            "id": "writing",
            "name": "Writing",
            "description": "Writing and editing prompts",
            "color": {
              "metaColor": {
                "originalInput": "#2563eb"
              }
            }
          },
          {
            "id": "research",
            "name": "Research"
          }
        ]
        """);

    Assert(result.Success, result.Error);
    Equal(2, result.Items.Count);
    Equal("writing", result.Items[0].Id);
    Equal(string.Empty, result.Items[1].Description);
}

static void TestMalformedRecords()
{
    var prompts = PromptLibraryJsonParser.ParsePrompts(
        """
        [
          { "act": "Valid", "prompt": "Keep this.", "tags": ["writing", 42] },
          { "act": "", "prompt": "Missing title." },
          "not an object",
          { "act": "Also valid", "prompt": "Keep this too.", "tags": "writing" }
        ]
        """);

    Assert(prompts.Success, prompts.Error);
    Equal(2, prompts.Items.Count);
    Equal(1, prompts.Items[0].Tags.Count);
    Equal(0, prompts.Items[1].Tags.Count);
    Assert(prompts.Warnings.Count >= 3, "Malformed prompt records should produce warnings.");

    var tags = PromptLibraryJsonParser.ParseTags(
        """
        [
          { "id": "writing", "name": "Writing" },
          { "id": "WRITING", "name": "Duplicate" },
          { "id": "", "name": "Missing ID" },
          { "id": "missing-name" }
        ]
        """);

    Assert(tags.Success, tags.Error);
    Equal(1, tags.Items.Count);
    Equal(3, tags.Warnings.Count);
}

static void TestInvalidDocuments()
{
    var prompts = PromptLibraryJsonParser.ParsePrompts("""{ "act": "Not an array" }""");
    Assert(!prompts.Success, "Prompt objects must be contained in a top-level array.");

    var tags = PromptLibraryJsonParser.ParseTags("""[{"id": "broken" """);
    Assert(!tags.Success, "Malformed tag JSON should fail the document load.");

    var empty = PromptLibraryJsonParser.ParsePrompts("[]");
    Assert(empty.Success, empty.Error);
    Equal(0, empty.Items.Count);
}

static void TestRepositoryLibrary()
{
    var candidateDirectories = new[]
    {
        Path.Combine(Environment.CurrentDirectory, "noi_prompts"),
        Path.Combine(Environment.CurrentDirectory, "release-samples"),
    };

    var dataDirectory = candidateDirectories.FirstOrDefault(Directory.Exists);
    Assert(dataDirectory != null, "The repository prompt library directory was not found.");
    var existingDataDirectory = dataDirectory!;

    var prompts = PromptLibraryJsonParser.ParsePrompts(
        File.ReadAllText(Path.Combine(existingDataDirectory, "user.prompt.json")));
    var tags = PromptLibraryJsonParser.ParseTags(
        File.ReadAllText(Path.Combine(existingDataDirectory, "user.prompt.tag.json")));

    Assert(prompts.Success, prompts.Error);
    Assert(tags.Success, tags.Error);
    Assert(prompts.Items.Count > 0, "The repository should contain at least one prompt.");
    Assert(tags.Items.Count > 0, "The repository should contain at least one tag.");

    var tagIds = tags.Items.Select(tag => tag.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
    var unresolved = prompts.Items
        .SelectMany(prompt => prompt.Tags)
        .Where(tagId => !tagIds.Contains(tagId))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();
    Equal(0, unresolved.Count);
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
    }
}
