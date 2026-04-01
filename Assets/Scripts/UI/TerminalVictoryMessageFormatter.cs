public static class TerminalVictoryMessageFormatter
{
    private const string DefaultPrompt = "You win!\n\nPress L to enter level choice.";

    public static string Build(
        bool showCollectibleSummary,
        bool collectiblesAreOptional,
        int collectedCount,
        int totalCount)
    {
        if (!showCollectibleSummary || totalCount <= 0)
        {
            return DefaultPrompt;
        }

        int safeCollectedCount = collectedCount;
        if (safeCollectedCount < 0)
        {
            safeCollectedCount = 0;
        }
        else if (safeCollectedCount > totalCount)
        {
            safeCollectedCount = totalCount;
        }

        string collectibleSummary = $"Stars collected: {safeCollectedCount}/{totalCount}";
        string collectibleStatus = safeCollectedCount >= totalCount
            ? "All stars collected."
            : collectiblesAreOptional
                ? "Stars are optional in this level."
                : "Collect all stars before leaving.";

        return $"You win!\n\n{collectibleSummary}\n{collectibleStatus}\n\nPress L to enter level choice.";
    }
}
