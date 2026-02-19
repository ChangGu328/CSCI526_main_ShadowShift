using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Two fixed-pulley controller driven by DistanceJoint2D.
/// Keeps rope-length conservation: leftSegment + rightSegment + topSpan = totalRopeLength.
/// Load difference (right - left) drives how rope length is redistributed.
/// </summary>
public class PulleySystemController : MonoBehaviour
{
    [Header("References")]
    public Rigidbody2D leftPlatform;
    public Rigidbody2D rightPlatform;
    public Transform leftAnchor;
    public Transform rightAnchor;

    [Header("Distance joints (auto-created if missing)")]
    public DistanceJoint2D leftJoint;
    public DistanceJoint2D rightJoint;

    [Header("Load sensors (optional explicit refs)")]
    public MonoBehaviour leftDetector;
    public MonoBehaviour rightDetector;

    [Header("Attach points (platform local space)")]
    public Vector2 leftLocalAttach = new Vector2(0f, 0.5f);
    public Vector2 rightLocalAttach = new Vector2(0f, 0.5f);

    [Header("Rope")]
    [Tooltip("Total rope length in world units. If autoInitializeFromCurrentPose is enabled, this is recalculated in Awake.")]
    public float totalRopeLength = 12f;
    [Tooltip("Extra rope slack added on auto initialization.")]
    public float ropeSlack = 0f;
    [Tooltip("Per-segment clamp.")]
    public float minSegmentLength = 0.1f;
    [Tooltip("Per-segment clamp.")]
    public float maxSegmentLength = 30f;
    [Range(0f, 1f)]
    [Tooltip("1 = solve immediately, lower = softer correction.")]
    public float solveLerp = 1f;
    public bool autoInitializeFromCurrentPose = true;

    [Header("Load To Motion")]
    [Tooltip("Ignore tiny mass difference to avoid jitter.")]
    public float massDeadzone = 0.05f;
    [Tooltip("How many rope units per second a 1.0 mass difference produces.")]
    public float lengthChangePerMassPerSecond = 0.35f;
    [Tooltip("Hard speed clamp for rope-length redistribution.")]
    public float maxLengthChangePerSecond = 2f;
    [Range(0f, 1f)]
    [Tooltip("Mass smoothing factor.")]
    public float massSmoothing = 0.2f;
    [Tooltip("Include platform rigidbody mass when computing each side load.")]
    public bool includePlatformMass = false;

    [Header("Auto wiring")]
    public bool autoFindAnchorsByName = true;
    public bool autoFindPlatformsByName = true;
    public bool autoFindDetectorsInChildren = true;
    public string leftAnchorName = "LeftAnchor";
    public string rightAnchorName = "RightAnchor";
    public string leftPlatformName = "LeftPlatform";
    public string rightPlatformName = "RightPlatform";
    public bool autoCreateDistanceJoints = true;
    public bool autoConfigureJointSettings = true;

    [Header("Debug (runtime)")]
    public float debugLeftLoad;
    public float debugRightLoad;
    public float debugMassDiff;
    public float debugLeftDistance;
    public float debugRightDistance;
    public float debugLeftDetectorLoad;
    public float debugRightDetectorLoad;
    public float debugLeftContactLoad;
    public float debugRightContactLoad;

    private float smoothedMassDiff;
    private bool initialized;
    private readonly Collider2D[] contactBuffer = new Collider2D[32];
    private readonly HashSet<Rigidbody2D> uniqueContactBodies = new HashSet<Rigidbody2D>();

    private void Awake()
    {
        TryResolveReferences();
        EnsureJoints();
        if (!HasRequiredReferences())
        {
            Debug.LogError("PulleySystemController is missing required references.", this);
            enabled = false;
            return;
        }

        ConfigureJoint(leftJoint, leftAnchor, leftLocalAttach);
        ConfigureJoint(rightJoint, rightAnchor, rightLocalAttach);
        InitializeRopeFromCurrentPose();

        float ignoreA;
        float ignoreB;
        float initialLeftLoad = GetPlatformLoad(leftPlatform, leftDetector, out ignoreA, out ignoreB);
        float initialRightLoad = GetPlatformLoad(rightPlatform, rightDetector, out ignoreA, out ignoreB);
        smoothedMassDiff = initialRightLoad - initialLeftLoad;
    }

    private void FixedUpdate()
    {
        if (!initialized)
        {
            return;
        }

        UpdateConnectedAnchors();

        float topSpan = GetTopSpan();
        float availableForVerticalSegments = Mathf.Max(minSegmentLength * 2f, totalRopeLength - topSpan);

        float leftDetectorLoad;
        float rightDetectorLoad;
        float leftContactLoad;
        float rightContactLoad;
        float leftLoad = GetPlatformLoad(leftPlatform, leftDetector, out leftDetectorLoad, out leftContactLoad);
        float rightLoad = GetPlatformLoad(rightPlatform, rightDetector, out rightDetectorLoad, out rightContactLoad);
        float rawMassDiff = rightLoad - leftLoad;

        smoothedMassDiff = Mathf.Lerp(smoothedMassDiff, rawMassDiff, massSmoothing);

        float changePerSecond = 0f;
        if (Mathf.Abs(smoothedMassDiff) > massDeadzone)
        {
            changePerSecond = Mathf.Clamp(
                smoothedMassDiff * lengthChangePerMassPerSecond,
                -maxLengthChangePerSecond,
                maxLengthChangePerSecond);
        }

        float rightDelta = changePerSecond * Time.fixedDeltaTime;
        ApplyConstrainedDelta(rightDelta, availableForVerticalSegments);

        debugLeftLoad = leftLoad;
        debugRightLoad = rightLoad;
        debugMassDiff = smoothedMassDiff;
        debugLeftDistance = leftJoint != null ? leftJoint.distance : 0f;
        debugRightDistance = rightJoint != null ? rightJoint.distance : 0f;
        debugLeftDetectorLoad = leftDetectorLoad;
        debugRightDetectorLoad = rightDetectorLoad;
        debugLeftContactLoad = leftContactLoad;
        debugRightContactLoad = rightContactLoad;
    }

    private void TryResolveReferences()
    {
        if (autoFindPlatformsByName)
        {
            if (leftPlatform == null)
            {
                Transform t = FindChildRecursive(transform, leftPlatformName);
                if (t != null) leftPlatform = t.GetComponent<Rigidbody2D>();
            }

            if (rightPlatform == null)
            {
                Transform t = FindChildRecursive(transform, rightPlatformName);
                if (t != null) rightPlatform = t.GetComponent<Rigidbody2D>();
            }
        }

        if (autoFindAnchorsByName)
        {
            if (leftAnchor == null)
            {
                Transform t = FindChildRecursive(transform, leftAnchorName);
                if (t != null) leftAnchor = t;
            }

            if (rightAnchor == null)
            {
                Transform t = FindChildRecursive(transform, rightAnchorName);
                if (t != null) rightAnchor = t;
            }
        }

        if (leftJoint == null && leftPlatform != null)
        {
            leftJoint = leftPlatform.GetComponent<DistanceJoint2D>();
        }

        if (rightJoint == null && rightPlatform != null)
        {
            rightJoint = rightPlatform.GetComponent<DistanceJoint2D>();
        }

        if (autoFindDetectorsInChildren)
        {
            if (leftDetector == null && leftPlatform != null)
            {
                leftDetector = leftPlatform.GetComponentInChildren<BoxWeightDetector>(true);
            }

            if (rightDetector == null && rightPlatform != null)
            {
                rightDetector = rightPlatform.GetComponentInChildren<BoxWeightDetector>(true);
            }
        }
    }

    private void EnsureJoints()
    {
        if (!autoCreateDistanceJoints)
        {
            return;
        }

        if (leftPlatform != null && leftJoint == null)
        {
            leftJoint = leftPlatform.gameObject.AddComponent<DistanceJoint2D>();
        }

        if (rightPlatform != null && rightJoint == null)
        {
            rightJoint = rightPlatform.gameObject.AddComponent<DistanceJoint2D>();
        }
    }

    private bool HasRequiredReferences()
    {
        return leftPlatform != null &&
               rightPlatform != null &&
               leftAnchor != null &&
               rightAnchor != null &&
               leftJoint != null &&
               rightJoint != null;
    }

    private void ConfigureJoint(DistanceJoint2D joint, Transform anchor, Vector2 localAttach)
    {
        if (joint == null || anchor == null)
        {
            return;
        }

        if (autoConfigureJointSettings)
        {
            joint.autoConfigureConnectedAnchor = false;
            joint.autoConfigureDistance = false;
            joint.enableCollision = false;
            joint.maxDistanceOnly = false;
        }

        joint.connectedBody = null;
        joint.anchor = localAttach;
        joint.connectedAnchor = anchor.position;
    }

    private void InitializeRopeFromCurrentPose()
    {
        float leftLength = GetSegmentLength(leftPlatform, leftAnchor, leftLocalAttach);
        float rightLength = GetSegmentLength(rightPlatform, rightAnchor, rightLocalAttach);
        float topSpan = GetTopSpan();

        if (autoInitializeFromCurrentPose)
        {
            totalRopeLength = leftLength + rightLength + topSpan + ropeSlack;
        }

        float availableForVerticalSegments = Mathf.Max(minSegmentLength * 2f, totalRopeLength - topSpan);
        float rightMinAllowed = Mathf.Max(minSegmentLength, availableForVerticalSegments - maxSegmentLength);
        float rightMaxAllowed = Mathf.Min(maxSegmentLength, availableForVerticalSegments - minSegmentLength);

        float rightDistance = Mathf.Clamp(rightLength, rightMinAllowed, rightMaxAllowed);
        float leftDistance = availableForVerticalSegments - rightDistance;

        leftJoint.distance = Mathf.Clamp(leftDistance, minSegmentLength, maxSegmentLength);
        rightJoint.distance = Mathf.Clamp(rightDistance, minSegmentLength, maxSegmentLength);
        initialized = true;
    }

    private void ApplyConstrainedDelta(float rightDelta, float availableForVerticalSegments)
    {
        if (leftJoint == null || rightJoint == null)
        {
            return;
        }

        float rightMinAllowed = Mathf.Max(minSegmentLength, availableForVerticalSegments - maxSegmentLength);
        float rightMaxAllowed = Mathf.Min(maxSegmentLength, availableForVerticalSegments - minSegmentLength);
        if (rightMinAllowed > rightMaxAllowed)
        {
            float fallback = Mathf.Clamp(availableForVerticalSegments * 0.5f, minSegmentLength, maxSegmentLength);
            rightMinAllowed = fallback;
            rightMaxAllowed = fallback;
        }

        float targetRight = Mathf.Clamp(rightJoint.distance + rightDelta, rightMinAllowed, rightMaxAllowed);
        float targetLeft = Mathf.Clamp(availableForVerticalSegments - targetRight, minSegmentLength, maxSegmentLength);
        targetRight = Mathf.Clamp(availableForVerticalSegments - targetLeft, rightMinAllowed, rightMaxAllowed);

        leftJoint.distance = Mathf.Lerp(leftJoint.distance, targetLeft, solveLerp);
        rightJoint.distance = Mathf.Lerp(rightJoint.distance, targetRight, solveLerp);
    }

    private void UpdateConnectedAnchors()
    {
        if (leftJoint != null && leftAnchor != null)
        {
            leftJoint.connectedAnchor = leftAnchor.position;
        }

        if (rightJoint != null && rightAnchor != null)
        {
            rightJoint.connectedAnchor = rightAnchor.position;
        }
    }

    private float GetPlatformLoad(
        Rigidbody2D platformRb,
        MonoBehaviour detector,
        out float detectorOnlyLoad,
        out float contactOnlyLoad)
    {
        float platformMass = platformRb != null ? platformRb.mass : 0f;
        float detectorMass = 0f;

        if (detector is BoxWeightDetector boxDetector)
        {
            detectorMass = Mathf.Max(0f, boxDetector.CurrentMass);
        }
        else if (detector != null)
        {
            if (!TryReadMass(detector, "CurrentMass", out detectorMass))
            {
                float totalMassValue;
                if (TryReadMass(detector, "TotalMass", out totalMassValue))
                {
                    detectorMass = Mathf.Max(0f, totalMassValue - platformMass);
                }
            }
        }

        float contactMass = GetContactMass(platformRb);
        float sensedMass = Mathf.Max(detectorMass, contactMass);
        detectorOnlyLoad = detectorMass;
        contactOnlyLoad = contactMass;

        if (includePlatformMass)
        {
            return Mathf.Max(0f, platformMass + sensedMass);
        }

        return Mathf.Max(0f, sensedMass);
    }

    private float GetContactMass(Rigidbody2D platformRb)
    {
        if (platformRb == null)
        {
            return 0f;
        }

        int count = platformRb.GetContacts(contactBuffer);
        if (count <= 0)
        {
            return 0f;
        }

        uniqueContactBodies.Clear();
        float total = 0f;
        for (int i = 0; i < count; i++)
        {
            Collider2D c = contactBuffer[i];
            if (c == null)
            {
                continue;
            }

            Rigidbody2D otherRb = c.attachedRigidbody;
            if (otherRb == null || otherRb == platformRb)
            {
                continue;
            }

            if (!uniqueContactBodies.Add(otherRb))
            {
                continue;
            }

            total += Mathf.Max(0f, otherRb.mass);
        }

        return total;
    }

    private static bool TryReadMass(MonoBehaviour detector, string propertyName, out float value)
    {
        value = 0f;
        var prop = detector.GetType().GetProperty(propertyName);
        if (prop == null)
        {
            return false;
        }

        object v = prop.GetValue(detector, null);
        if (v is float f)
        {
            value = f;
            return true;
        }

        if (v is double d)
        {
            value = (float)d;
            return true;
        }

        return false;
    }

    private float GetTopSpan()
    {
        if (leftAnchor == null || rightAnchor == null)
        {
            return 0f;
        }

        return Vector2.Distance(leftAnchor.position, rightAnchor.position);
    }

    private static float GetSegmentLength(Rigidbody2D platform, Transform anchor, Vector2 localAttach)
    {
        if (platform == null || anchor == null)
        {
            return 0f;
        }

        Vector2 worldAttach = platform.transform.TransformPoint(localAttach);
        return Vector2.Distance(worldAttach, anchor.position);
    }

    private static Transform FindChildRecursive(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrEmpty(targetName))
        {
            return null;
        }

        if (root.name == targetName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), targetName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
