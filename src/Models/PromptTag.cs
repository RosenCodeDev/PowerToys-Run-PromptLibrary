using System.Text.Json.Serialization;

namespace Community.PowerToys.Run.Plugin.PromptLibrary.Models;

/// <summary>
/// Represents a single tag from the prompt library export (user.prompt.tag.json).
/// </summary>
public class PromptTag
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
}
