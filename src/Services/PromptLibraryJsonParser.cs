using System.Text.Json;
using Community.PowerToys.Run.Plugin.PromptLibrary.Models;

namespace Community.PowerToys.Run.Plugin.PromptLibrary.Services;

public sealed class JsonLibraryParseResult<T>
{
    private JsonLibraryParseResult(
        bool success,
        List<T> items,
        List<string> warnings,
        string error)
    {
        Success = success;
        Items = items;
        Warnings = warnings;
        Error = error;
    }

    public bool Success { get; }

    public List<T> Items { get; }

    public IReadOnlyList<string> Warnings { get; }

    public string Error { get; }

    public static JsonLibraryParseResult<T> Succeeded(List<T> items, List<string> warnings)
    {
        return new JsonLibraryParseResult<T>(true, items, warnings, string.Empty);
    }

    public static JsonLibraryParseResult<T> Failed(string error)
    {
        return new JsonLibraryParseResult<T>(false, new List<T>(), new List<string>(), error);
    }
}

/// <summary>
/// Tolerant, record-by-record parsing for hand-written and exported prompt libraries.
/// Unknown metadata is intentionally ignored.
/// </summary>
public static class PromptLibraryJsonParser
{
    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

    public static JsonLibraryParseResult<PromptItem> ParsePrompts(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json, DocumentOptions);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return JsonLibraryParseResult<PromptItem>.Failed(
                    "user.prompt.json must contain a top-level JSON array.");
            }

            var prompts = new List<PromptItem>();
            var warnings = new List<string>();
            var index = 0;

            foreach (var element in document.RootElement.EnumerateArray())
            {
                index++;
                if (element.ValueKind != JsonValueKind.Object)
                {
                    warnings.Add($"Prompt #{index} was skipped because it is not a JSON object.");
                    continue;
                }

                if (!TryGetRequiredString(element, "act", trimValue: true, out var act))
                {
                    warnings.Add($"Prompt #{index} was skipped because act must be a non-empty string.");
                    continue;
                }

                if (!TryGetRequiredString(element, "prompt", trimValue: false, out var body))
                {
                    warnings.Add($"Prompt #{index} was skipped because prompt must be a non-empty string.");
                    continue;
                }

                var tags = ReadStringArray(element, "tags", index, warnings);
                var id = ReadOptionalString(element, "id", index, "Prompt", warnings);

                prompts.Add(new PromptItem
                {
                    Act = act,
                    Prompt = body,
                    Tags = tags,
                    Id = id,
                });
            }

            return JsonLibraryParseResult<PromptItem>.Succeeded(prompts, warnings);
        }
        catch (JsonException ex)
        {
            return JsonLibraryParseResult<PromptItem>.Failed(
                $"user.prompt.json is not valid JSON: {ex.Message}");
        }
    }

    public static JsonLibraryParseResult<PromptTag> ParseTags(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json, DocumentOptions);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return JsonLibraryParseResult<PromptTag>.Failed(
                    "user.prompt.tag.json must contain a top-level JSON array.");
            }

            var tags = new List<PromptTag>();
            var warnings = new List<string>();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var index = 0;

            foreach (var element in document.RootElement.EnumerateArray())
            {
                index++;
                if (element.ValueKind != JsonValueKind.Object)
                {
                    warnings.Add($"Tag #{index} was skipped because it is not a JSON object.");
                    continue;
                }

                if (!TryGetRequiredString(element, "id", trimValue: true, out var id))
                {
                    warnings.Add($"Tag #{index} was skipped because id must be a non-empty string.");
                    continue;
                }

                if (!TryGetRequiredString(element, "name", trimValue: true, out var name))
                {
                    warnings.Add($"Tag #{index} was skipped because name must be a non-empty string.");
                    continue;
                }

                if (!ids.Add(id))
                {
                    warnings.Add($"Tag #{index} was skipped because id '{id}' is duplicated.");
                    continue;
                }

                tags.Add(new PromptTag
                {
                    Id = id,
                    Name = name,
                    Description = ReadOptionalString(
                        element,
                        "description",
                        index,
                        "Tag",
                        warnings),
                });
            }

            return JsonLibraryParseResult<PromptTag>.Succeeded(tags, warnings);
        }
        catch (JsonException ex)
        {
            return JsonLibraryParseResult<PromptTag>.Failed(
                $"user.prompt.tag.json is not valid JSON: {ex.Message}");
        }
    }

    private static bool TryGetRequiredString(
        JsonElement element,
        string propertyName,
        bool trimValue,
        out string value)
    {
        value = string.Empty;
        if (!TryGetProperty(element, propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var rawValue = property.GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return false;
        }

        value = trimValue ? rawValue.Trim() : rawValue;
        return true;
    }

    private static string ReadOptionalString(
        JsonElement element,
        string propertyName,
        int index,
        string recordType,
        List<string> warnings)
    {
        if (!TryGetProperty(element, propertyName, out var property) ||
            property.ValueKind == JsonValueKind.Null)
        {
            return string.Empty;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            warnings.Add(
                $"{recordType} #{index} has a non-string {propertyName}; the optional value was ignored.");
            return string.Empty;
        }

        return property.GetString()?.Trim() ?? string.Empty;
    }

    private static List<string> ReadStringArray(
        JsonElement element,
        string propertyName,
        int index,
        List<string> warnings)
    {
        if (!TryGetProperty(element, propertyName, out var property) ||
            property.ValueKind == JsonValueKind.Null)
        {
            return new List<string>();
        }

        if (property.ValueKind != JsonValueKind.Array)
        {
            warnings.Add(
                $"Prompt #{index} has a non-array {propertyName}; the optional value was ignored.");
            return new List<string>();
        }

        var values = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                warnings.Add($"Prompt #{index} contains a non-string tag reference; it was ignored.");
                continue;
            }

            var value = item.GetString()?.Trim() ?? string.Empty;
            if (value.Length > 0 && seen.Add(value))
            {
                values.Add(value);
            }
        }

        return values;
    }

    private static bool TryGetProperty(
        JsonElement element,
        string propertyName,
        out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
