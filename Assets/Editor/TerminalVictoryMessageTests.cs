using NUnit.Framework;

public class TerminalVictoryMessageTests
{
    [Test]
    public void Build_ReturnsDefaultPrompt_WhenSummaryDisabled()
    {
        Assert.AreEqual(
            "You win!\n\nPress L to enter level choice.",
            TerminalVictoryMessageFormatter.Build(
                showCollectibleSummary: false,
                collectiblesAreOptional: true,
                collectedCount: 0,
                totalCount: 0));
    }

    [Test]
    public void Build_IncludesOptionalCollectibleProgress_WhenSummaryEnabled()
    {
        string message = TerminalVictoryMessageFormatter.Build(
            showCollectibleSummary: true,
            collectiblesAreOptional: true,
            collectedCount: 2,
            totalCount: 3);

        StringAssert.Contains("Stars collected: 2/3", message);
        StringAssert.Contains("optional", message.ToLowerInvariant());
        StringAssert.Contains("Press L", message);
    }

    [Test]
    public void Build_CallsOutFullCollection_WhenAllStarsCollected()
    {
        string message = TerminalVictoryMessageFormatter.Build(
            showCollectibleSummary: true,
            collectiblesAreOptional: true,
            collectedCount: 3,
            totalCount: 3);

        StringAssert.Contains("Stars collected: 3/3", message);
        StringAssert.Contains("All stars collected", message);
    }
}
