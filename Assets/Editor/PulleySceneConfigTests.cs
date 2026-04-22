using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

public class PulleySceneConfigTests
{
    private const string PulleyControllerMarker = "m_EditorClassIdentifier: Assembly-CSharp::PulleySystemController";
    private static readonly Regex RopeDampingRegex = new(@"^\s*ropeDamping:\s*([0-9.]+)", RegexOptions.Compiled);

    [TestCase("level2.unity", 1.2f)]
    [TestCase("tutorial2.unity", 1.2f)]
    public void PulleyScenes_UseTunedRopeDamping(string sceneFileName, float expectedRopeDamping)
    {
        string scenePath = Path.Combine(Application.dataPath, "Scenes", sceneFileName);

        bool insidePulleyController = false;

        foreach (string line in File.ReadLines(scenePath))
        {
            if (line.Contains(PulleyControllerMarker))
            {
                insidePulleyController = true;
                continue;
            }

            if (!insidePulleyController)
            {
                continue;
            }

            Match match = RopeDampingRegex.Match(line);
            if (!match.Success)
            {
                continue;
            }

            float ropeDamping = float.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            Assert.That(ropeDamping, Is.EqualTo(expectedRopeDamping).Within(0.0001f));
            return;
        }

        Assert.Fail($"Could not find ropeDamping in {sceneFileName}.");
    }
}
