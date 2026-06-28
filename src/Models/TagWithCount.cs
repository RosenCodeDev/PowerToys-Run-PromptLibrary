namespace Community.PowerToys.Run.Plugin.PromptLibrary.Models;

/// <summary>
/// Bundles a tag with the number of prompts that reference it.
/// </summary>
public class TagWithCount
{
    public required PromptTag Tag { get; init; }
    public required int Count { get; init; }
}
