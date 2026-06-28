using System.Text.Json.Serialization;

namespace Community.PowerToys.Run.Plugin.PromptLibrary.Models;

/// <summary>
/// Represents a single prompt from the prompt library export (user.prompt.json).
/// </summary>
public class PromptItem
{
    [JsonPropertyName("act")]
    public string Act { get; set; } = string.Empty;

    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = string.Empty;

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;
}
