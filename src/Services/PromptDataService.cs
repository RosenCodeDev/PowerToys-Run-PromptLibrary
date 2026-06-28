using System.IO;
using Community.PowerToys.Run.Plugin.PromptLibrary.Models;
using Wox.Plugin.Logger;

namespace Community.PowerToys.Run.Plugin.PromptLibrary.Services;

/// <summary>
/// Loads and manages prompt and tag data from JSON files.
/// Thread-safe via ReaderWriterLockSlim for concurrent Query access during reload.
/// </summary>
public class PromptDataService : IDisposable
{
    private readonly ReaderWriterLockSlim _lock = new();
    private Dictionary<string, PromptTag> _tagsById = new(StringComparer.OrdinalIgnoreCase);
    private List<ResolvedPrompt> _resolvedPrompts = new();

    /// <summary>
    /// A prompt with its tag names pre-resolved for search performance.
    /// </summary>
    public class ResolvedPrompt
    {
        public required PromptItem Source { get; init; }

        public required List<string> TagNames { get; init; }

        public required string TagDisplay { get; init; }

        public required string ActLower { get; init; }

        public required string PromptLower { get; init; }

        public required List<string> TagNamesLower { get; init; }

        public required string TagDescriptionsLower { get; init; }
    }

    /// <summary>
    /// Returns the current immutable-by-convention prompt snapshot.
    /// </summary>
    public List<ResolvedPrompt> GetResolvedPrompts()
    {
        _lock.EnterReadLock();
        try
        {
            return _resolvedPrompts;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Returns tags with prompt usage counts, sorted by count then name.
    /// </summary>
    public List<TagWithCount> GetTagsWithCount()
    {
        _lock.EnterReadLock();
        try
        {
            var countById = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var prompt in _resolvedPrompts)
            {
                foreach (var tagId in prompt.Source.Tags)
                {
                    countById[tagId] = countById.TryGetValue(tagId, out var count)
                        ? count + 1
                        : 1;
                }
            }

            var result = new List<TagWithCount>();
            foreach (var pair in countById)
            {
                if (pair.Value > 0 && _tagsById.TryGetValue(pair.Key, out var tag))
                {
                    result.Add(new TagWithCount { Tag = tag, Count = pair.Value });
                }
            }

            result.Sort((left, right) =>
            {
                var byCount = right.Count.CompareTo(left.Count);
                return byCount != 0
                    ? byCount
                    : string.Compare(
                        left.Tag.Name,
                        right.Tag.Name,
                        StringComparison.OrdinalIgnoreCase);
            });

            return result;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Parses both library files and swaps the active snapshot only after a successful load.
    /// </summary>
    public bool LoadData(string dataDir)
    {
        var promptPath = Path.Combine(dataDir, "user.prompt.json");
        var tagPath = Path.Combine(dataDir, "user.prompt.tag.json");

        try
        {
            var newTagsById = new Dictionary<string, PromptTag>(
                StringComparer.OrdinalIgnoreCase);

            if (File.Exists(tagPath))
            {
                var tagResult = PromptLibraryJsonParser.ParseTags(File.ReadAllText(tagPath));
                if (!tagResult.Success)
                {
                    Log.Error($"PromptLibrary: {tagResult.Error}", typeof(PromptDataService));
                    return false;
                }

                LogWarnings(tagResult.Warnings);
                foreach (var tag in tagResult.Items)
                {
                    newTagsById[tag.Id] = tag;
                }

                Log.Info(
                    $"PromptLibrary: Loaded {newTagsById.Count} tags",
                    typeof(PromptDataService));
            }
            else
            {
                Log.Warn(
                    "PromptLibrary: Tag file not found, continuing without tags",
                    typeof(PromptDataService));
            }

            if (!File.Exists(promptPath))
            {
                Log.Error(
                    $"PromptLibrary: Prompt file not found: {promptPath}",
                    typeof(PromptDataService));
                return false;
            }

            var promptResult = PromptLibraryJsonParser.ParsePrompts(
                File.ReadAllText(promptPath));
            if (!promptResult.Success)
            {
                Log.Error($"PromptLibrary: {promptResult.Error}", typeof(PromptDataService));
                return false;
            }

            LogWarnings(promptResult.Warnings);

            var unresolvedTagIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var newResolved = new List<ResolvedPrompt>(promptResult.Items.Count);
            foreach (var prompt in promptResult.Items)
            {
                var tagNames = new List<string>();
                var tagNamesLower = new List<string>();
                var tagDescriptions = new List<string>();

                foreach (var tagId in prompt.Tags)
                {
                    if (newTagsById.TryGetValue(tagId, out var tag))
                    {
                        tagNames.Add(tag.Name);
                        tagNamesLower.Add(tag.Name.ToLowerInvariant());
                        if (!string.IsNullOrWhiteSpace(tag.Description))
                        {
                            tagDescriptions.Add(tag.Description);
                        }
                    }
                    else
                    {
                        unresolvedTagIds.Add(tagId);
                    }
                }

                newResolved.Add(new ResolvedPrompt
                {
                    Source = prompt,
                    TagNames = tagNames,
                    TagDisplay = string.Join(", ", tagNames),
                    ActLower = prompt.Act.ToLowerInvariant(),
                    PromptLower = prompt.Prompt.ToLowerInvariant(),
                    TagNamesLower = tagNamesLower,
                    TagDescriptionsLower = string.Join(" ", tagDescriptions).ToLowerInvariant(),
                });
            }

            if (unresolvedTagIds.Count > 0)
            {
                Log.Warn(
                    $"PromptLibrary: Ignored {unresolvedTagIds.Count} unresolved tag ID(s)",
                    typeof(PromptDataService));
            }

            _lock.EnterWriteLock();
            try
            {
                _tagsById = newTagsById;
                _resolvedPrompts = newResolved;
            }
            finally
            {
                _lock.ExitWriteLock();
            }

            Log.Info(
                $"PromptLibrary: Loaded {newResolved.Count} prompts",
                typeof(PromptDataService));
            return true;
        }
        catch (IOException ex)
        {
            Log.Error(
                $"PromptLibrary: File read error: {ex.Message}",
                typeof(PromptDataService));
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            Log.Error(
                $"PromptLibrary: File access error: {ex.Message}",
                typeof(PromptDataService));
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(
                $"PromptLibrary: Unexpected error loading data: {ex.Message}",
                typeof(PromptDataService));
            return false;
        }
    }

    public string GetStatus()
    {
        _lock.EnterReadLock();
        try
        {
            return $"{_resolvedPrompts.Count} prompts, {_tagsById.Count} tags loaded";
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public void Dispose()
    {
        _lock.Dispose();
        GC.SuppressFinalize(this);
    }

    private static void LogWarnings(IReadOnlyList<string> warnings)
    {
        foreach (var warning in warnings)
        {
            Log.Warn($"PromptLibrary: {warning}", typeof(PromptDataService));
        }
    }
}
