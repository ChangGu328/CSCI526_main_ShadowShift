using System.Reflection;
using NUnit.Framework;
using UnityEngine;

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

public class TerminalCompatibilityTests
{
    [Test]
    public void Terminal_ExposesIsGameOverCompatibilityProperty()
    {
        PropertyInfo property = typeof(Terminal).GetProperty("IsGameOver");

        Assert.NotNull(property);
        Assert.AreEqual(typeof(bool), property.PropertyType);
    }

    [Test]
    public void IsGameOver_MirrorsIsFinishedState()
    {
        GameObject gameObject = new GameObject("TerminalCompatibilityTests");

        try
        {
            Terminal terminal = gameObject.AddComponent<Terminal>();
            FieldInfo finishedBackingField = typeof(Terminal).GetField("<IsFinished>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            PropertyInfo isGameOverProperty = typeof(Terminal).GetProperty("IsGameOver");

            Assert.NotNull(finishedBackingField);
            Assert.NotNull(isGameOverProperty);

            finishedBackingField.SetValue(terminal, true);
            Assert.AreEqual(true, (bool)isGameOverProperty.GetValue(terminal));

            finishedBackingField.SetValue(terminal, false);
            Assert.AreEqual(false, (bool)isGameOverProperty.GetValue(terminal));
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }
}
