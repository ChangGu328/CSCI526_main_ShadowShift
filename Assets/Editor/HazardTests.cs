using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class HazardTests
{
    private readonly List<GameObject> createdObjects = new();
    private float originalTimeScale;

    [SetUp]
    public void SetUp()
    {
        originalTimeScale = Time.timeScale;
        Time.timeScale = 1f;
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
            {
                Object.DestroyImmediate(createdObjects[i]);
            }
        }

        createdObjects.Clear();
        Time.timeScale = originalTimeScale;
    }

    [Test]
    public void OnTriggerEnter2D_ActivatesSceneDeathHint_WhenHintUiIsUnset()
    {
        GameObject hudCanvas = CreateGameObject("Canvas_HUD");
        GameObject deathHint = CreateGameObject("Text (Died)");
        deathHint.transform.SetParent(hudCanvas.transform, false);
        deathHint.SetActive(false);

        Hazard hazard = CreateGameObject("Hazard").AddComponent<Hazard>();
        BoxCollider2D playerCollider = CreatePlayerBodyCollider();

        InvokeTriggerEnter(hazard, playerCollider);

        Assert.That(deathHint.activeSelf, Is.True);
        Assert.That(Time.timeScale, Is.Zero);
    }

    private GameObject CreateGameObject(string name)
    {
        GameObject gameObject = new GameObject(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private BoxCollider2D CreatePlayerBodyCollider()
    {
        GameObject player = CreateGameObject("Player");
        player.layer = LayerMask.NameToLayer("Player_Body");
        return player.AddComponent<BoxCollider2D>();
    }

    private static void InvokeTriggerEnter(Hazard hazard, Collider2D collider)
    {
        MethodInfo triggerMethod = typeof(Hazard).GetMethod(
            "OnTriggerEnter2D",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(triggerMethod);
        triggerMethod.Invoke(hazard, new object[] { collider });
    }
}
