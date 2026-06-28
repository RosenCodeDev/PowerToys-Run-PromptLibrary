using Community.PowerToys.Run.Plugin.PromptLibrary.Models;
using static Community.PowerToys.Run.Plugin.PromptLibrary.Services.PromptDataService;

namespace Community.PowerToys.Run.Plugin.PromptLibrary.Services;

/// <summary>
/// Tokenized, scored search across prompt titles, tag names, body text, and tag descriptions.
/// </summary>
public static class PromptLibrarySearchService
{
    // Scoring weights by field
    private const int TitleExactWeight = 200;
    private const int TitlePrefixWeight = 150;
    private const int TitleContainsWeight = 100;
    private const int TagNameExactWeight = 100;
    private const int TagNameContainsWeight = 80;
    private const int BodyContainsWeight = 40;
    private const int TagDescriptionWeight = 20;

    /// <summary>
    /// Scores and returns matching prompts sorted by relevance (descending).
    /// </summary>
    /// <param name="resolvedPrompts">Pre-resolved prompts with lowercase fields</param>
    /// <param name="query">Raw user query string</param>
    /// <param name="maxResults">Maximum number of results to return</param>
    public static List<ScoredPrompt> Search(
        List<ResolvedPrompt> resolvedPrompts,
        string query,
        int maxResults)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            // No query — return all prompts up to maxResults, sorted alphabetically
            return resolvedPrompts
                .Take(maxResults)
                .Select(rp => new ScoredPrompt { Prompt = rp, Score = 0 })
                .ToList();
        }

        var tokens = query.ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (tokens.Length == 0)
        {
            return resolvedPrompts
                .Take(maxResults)
                .Select(rp => new ScoredPrompt { Prompt = rp, Score = 0 })
                .ToList();
        }

        var scored = new List<ScoredPrompt>();

        foreach (var rp in resolvedPrompts)
        {
            int totalScore = 0;
            bool allTokensMatched = true;

            foreach (var token in tokens)
            {
                int tokenScore = ScoreToken(rp, token);
                if (tokenScore == 0)
                {
                    allTokensMatched = false;
                    break;
                }

                totalScore += tokenScore;
            }

            if (allTokensMatched && totalScore > 0)
            {
                // Bonus for matching more tokens
                if (tokens.Length > 1)
                {
                    totalScore += tokens.Length * 10;
                }

                scored.Add(new ScoredPrompt { Prompt = rp, Score = totalScore });
            }
        }

        return scored
            .OrderByDescending(s => s.Score)
            .Take(maxResults)
            .ToList();
    }

    private static int ScoreToken(ResolvedPrompt rp, string token)
    {
        int score = 0;

        // Title matching (highest priority)
        if (rp.ActLower == token)
        {
            score += TitleExactWeight;
        }
        else if (rp.ActLower.StartsWith(token, StringComparison.Ordinal))
        {
            score += TitlePrefixWeight;
        }
        else if (rp.ActLower.Contains(token, StringComparison.Ordinal))
        {
            score += TitleContainsWeight;
        }

        // Tag name matching (pre-lowercased list)
        foreach (var tagLower in rp.TagNamesLower)
        {
            if (tagLower == token)
            {
                score += TagNameExactWeight;
            }
            else if (tagLower.Contains(token, StringComparison.Ordinal))
            {
                score += TagNameContainsWeight;
            }
        }

        // Body text matching (medium priority)
        if (rp.PromptLower.Contains(token, StringComparison.Ordinal))
        {
            score += BodyContainsWeight;
        }

        // Tag description matching (low priority)
        if (!string.IsNullOrEmpty(rp.TagDescriptionsLower) &&
            rp.TagDescriptionsLower.Contains(token, StringComparison.Ordinal))
        {
            score += TagDescriptionWeight;
        }

        return score;
    }
}

/// <summary>
/// A prompt with its computed search relevance score.
/// </summary>
public class ScoredPrompt
{
    public required ResolvedPrompt Prompt { get; init; }
    public required int Score { get; init; }
}
