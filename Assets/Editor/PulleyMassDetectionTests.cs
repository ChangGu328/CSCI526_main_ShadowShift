using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class PulleyMassDetectionTests
{
    private readonly List<GameObject> createdObjects = new();
    private SimulationMode2D originalSimulationMode;

    [SetUp]
    public void SetUp()
    {
        originalSimulationMode = Physics2D.simulationMode;
        Physics2D.simulationMode = SimulationMode2D.Script;
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
        Physics2D.simulationMode = originalSimulationMode;
    }

    [Test]
    public void GetContactMass_CountsBodiesStackedOnTopOfSupportedBody()
    {
        PulleySystemController controller = CreateController();
        Rigidbody2D lowerBox = CreateDynamicBox("LowerBox", new Vector2(-2f, 0.85f), 2f);
        Rigidbody2D upperBox = CreateDynamicBox("UpperBox", new Vector2(-2f, 1.9f), 3f);

        SimulatePhysics(90);

        float detectedMass = InvokeGetContactMass(controller, controller.leftPlatform);

        Assert.That(detectedMass, Is.EqualTo(lowerBox.mass + upperBox.mass).Within(0.05f));
    }

    private PulleySystemController CreateController()
    {
        Rigidbody2D leftPlatform = CreatePlatform("LeftPlatform", new Vector2(-2f, 0f));
        Rigidbody2D rightPlatform = CreatePlatform("RightPlatform", new Vector2(2f, 0f));
        Transform leftAnchor = CreateAnchor("LeftAnchor", new Vector2(-2f, 3f));
        Transform rightAnchor = CreateAnchor("RightAnchor", new Vector2(2f, 3f));

        GameObject controllerObject = CreateGameObject("PulleySystem");
        PulleySystemController controller = controllerObject.AddComponent<PulleySystemController>();
        controller.autoFindByName = false;
        controller.leftPlatform = leftPlatform;
        controller.rightPlatform = rightPlatform;
        controller.leftAnchor = leftAnchor;
        controller.rightAnchor = rightAnchor;
        controller.enableGroundBlocking = false;

        InvokePrivateMethod(controller, "Awake");
        controller.enabled = false;
        return controller;
    }

    private Rigidbody2D CreatePlatform(string name, Vector2 position)
    {
        GameObject platform = CreateGameObject(name);
        platform.transform.position = position;

        Rigidbody2D rb = platform.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        BoxCollider2D collider = platform.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(3f, 0.5f);

        return rb;
    }

    private Rigidbody2D CreateDynamicBox(string name, Vector2 position, float mass)
    {
        GameObject box = CreateGameObject(name);
        box.transform.position = position;

        Rigidbody2D rb = box.AddComponent<Rigidbody2D>();
        rb.mass = mass;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        BoxCollider2D collider = box.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;

        return rb;
    }

    private Transform CreateAnchor(string name, Vector2 position)
    {
        GameObject anchor = CreateGameObject(name);
        anchor.transform.position = position;
        return anchor.transform;
    }

    private GameObject CreateGameObject(string name)
    {
        GameObject gameObject = new GameObject(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private static void SimulatePhysics(int steps)
    {
        Physics2D.SyncTransforms();

        for (int i = 0; i < steps; i++)
        {
            Physics2D.Simulate(Time.fixedDeltaTime);
        }
    }

    private static float InvokeGetContactMass(PulleySystemController controller, Rigidbody2D platform)
    {
        MethodInfo method = typeof(PulleySystemController).GetMethod(
            "GetContactMass",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        return (float)method.Invoke(controller, new object[] { platform });
    }

    private static void InvokePrivateMethod(object target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        method.Invoke(target, null);
    }
}
