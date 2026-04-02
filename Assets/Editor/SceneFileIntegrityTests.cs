using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

public class SceneFileIntegrityTests
{
    private static readonly Regex ObjectHeaderRegex = new(@"^--- !u!(\d+) &(-?\d+)", RegexOptions.Compiled);
    private static readonly Regex GameObjectReferenceRegex = new(@"^\s*m_GameObject: \{fileID: (-?\d+)\}", RegexOptions.Compiled);

    [Test]
    public void Level1Scene_HasUniqueObjectIds_AndResolvableGameObjectReferences()
    {
        string scenePath = Path.Combine(Application.dataPath, "Scenes", "level1.unity");
        string[] lines = File.ReadAllLines(scenePath);

        var objectIds = new Dictionary<long, int>();
        var gameObjectIds = new HashSet<long>();
        var gameObjectReferences = new List<(int lineNumber, long ownerId, long referencedId)>();

        long currentObjectId = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            Match headerMatch = ObjectHeaderRegex.Match(line);

            if (headerMatch.Success)
            {
                int classId = int.Parse(headerMatch.Groups[1].Value);
                currentObjectId = long.Parse(headerMatch.Groups[2].Value);

                objectIds.TryGetValue(currentObjectId, out int count);
                objectIds[currentObjectId] = count + 1;

                if (classId == 1)
                {
                    gameObjectIds.Add(currentObjectId);
                }

                continue;
            }

            Match gameObjectReferenceMatch = GameObjectReferenceRegex.Match(line);
            if (gameObjectReferenceMatch.Success)
            {
                long referencedId = long.Parse(gameObjectReferenceMatch.Groups[1].Value);
                if (referencedId != 0)
                {
                    gameObjectReferences.Add((i + 1, currentObjectId, referencedId));
                }
            }
        }

        var duplicateIds = objectIds
            .Where(entry => entry.Value > 1)
            .OrderBy(entry => entry.Key)
            .Select(entry => $"{entry.Key} ({entry.Value}x)")
            .ToArray();

        var missingGameObjectReferences = gameObjectReferences
            .Where(reference => !gameObjectIds.Contains(reference.referencedId))
            .Select(reference => $"line {reference.lineNumber}: object {reference.ownerId} -> missing GameObject {reference.referencedId}")
            .ToArray();

        CollectionAssert.IsEmpty(
            duplicateIds,
            "Scene contains duplicate local object IDs: " + string.Join(", ", duplicateIds));

        CollectionAssert.IsEmpty(
            missingGameObjectReferences,
            "Scene contains components pointing at missing GameObjects:\n" + string.Join("\n", missingGameObjectReferences));
    }
}
