using System;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Physically correct pulley system (Atwood machine).
/// Both platforms are Kinematic — the script positions them directly each FixedUpdate.
/// Rope length is conserved: leftSegment + rightSegment = constant.
/// Acceleration = (m_heavy - m_light) * g / (m_heavy + m_light).
/// </summary>
public class PulleySystemController : MonoBehaviour
{
    [Header("References")]
    public Rigidbody2D leftPlatform;
    public Rigidbody2D rightPlatform;
    public Transform leftAnchor;
    public Transform rightAnchor;

    [Header("Rope")]
    public bool autoInitializeFromCurrentPose = true;
    [Tooltip("Total rope length (auto-calculated if autoInitialize is on).")]
    public float totalRopeLength = 12f;
    public float minSegmentLength = 0.5f;
    public float maxSegmentLength = 20f;

    [Header("Pulley Physics")]
    public float gravity = 9.81f;
    [Tooltip("Mass of each empty platform (no load).")]
    public float platformMass = 1f;
    [Range(0f, 5f)]
    [Tooltip("Velocity damping to prevent perpetual oscillation.")]
    public float ropeDamping = 0.3f;
    [Tooltip("How fast detected mass converges (units/s). Prevents jitter from contact flickering.")]
    public float massSmoothingSpeed = 15f;

    [Header("Blocking")]
    [Tooltip("Prevent platforms from moving through static/kinematic colliders.")]
    public bool enableGroundBlocking = true;
    public LayerMask groundLayers = ~0;

    [Header("Auto Wiring")]
    public bool autoFindByName = true;
    public string leftAnchorName = "LeftAnchor";
    public string rightAnchorName = "RightAnchor";
    public string leftPlatformName = "LeftPlatform";
    public string rightPlatformName = "RightPlatform";

    [Header("Debug (runtime)")]
    public float debugLeftTotalMass;
    public float debugRightTotalMass;
    public float debugAcceleration;
    public float debugRopeVelocity;
    public float debugLeftRopeLength;
    public float debugRightRopeLength;

    // State
    private float ropeVelocity;       // positive = right rope gets longer (right goes down)
    private float rightRopeLength;
    private float availableRope;      // totalRopeLength - topSpan
    private float smoothedLeftMass;
    private float smoothedRightMass;
    private bool initialized;

    // Offset from Rigidbody2D center to the rope attach point (top of collider).
    // Needed when the collider is on a child object far from the Rigidbody2D.
    private float leftAttachOffsetY;
    private float rightAttachOffsetY;
    private Collider2D[] leftSolidColliders = Array.Empty<Collider2D>();
    private Collider2D[] rightSolidColliders = Array.Empty<Collider2D>();

    // Buffers
    private readonly Collider2D[] contactBuffer = new Collider2D[32];
    private readonly HashSet<Rigidbody2D> uniqueBodies = new HashSet<Rigidbody2D>();
    private readonly RaycastHit2D[] castBuffer = new RaycastHit2D[8];

    private void Awake()
    {
        TryResolveReferences();
        leftPlatform = ResolvePlatformBody(leftPlatform);
        rightPlatform = ResolvePlatformBody(rightPlatform);
        if (leftPlatform == null || rightPlatform == null ||
            leftAnchor == null || rightAnchor == null)
        {
            Debug.LogError("PulleySystemController: missing references.", this);
            enabled = false;
            return;
        }

        // Switch to Kinematic so we drive position directly
        leftPlatform.bodyType = RigidbodyType2D.Kinematic;
        rightPlatform.bodyType = RigidbodyType2D.Kinematic;
        leftPlatform.useFullKinematicContacts = true;
        rightPlatform.useFullKinematicContacts = true;

        // DistanceJoint2D is no longer needed
        DestroyJointIfPresent(leftPlatform);
        DestroyJointIfPresent(rightPlatform);

        // Ensure platform colliders are not triggers (needed for contact detection)
        EnsureNotTrigger(leftPlatform);
        EnsureNotTrigger(rightPlatform);

        // Calculate offset from Rigidbody2D center to the top of the actual
        // platform collider (the rope attaches to the top, not the RB center).
        leftAttachOffsetY = GetAttachOffsetY(leftPlatform);
        rightAttachOffsetY = GetAttachOffsetY(rightPlatform);
        leftSolidColliders = GetSolidColliders(leftPlatform);
        rightSolidColliders = GetSolidColliders(rightPlatform);

        InitializeRope();
    }

    private void FixedUpdate()
    {
        if (!initialized) return;
        float dt = Time.fixedDeltaTime;
        if (dt <= 0f) return;

        // ── 1. Mass detection with smoothing ──
        float rawLeft = platformMass + GetContactMass(leftPlatform);
        float rawRight = platformMass + GetContactMass(rightPlatform);
        smoothedLeftMass = Mathf.MoveTowards(smoothedLeftMass, rawLeft, massSmoothingSpeed * dt);
        smoothedRightMass = Mathf.MoveTowards(smoothedRightMass, rawRight, massSmoothingSpeed * dt);
        float totalMass = smoothedLeftMass + smoothedRightMass;

        // ── 2. Atwood machine acceleration ──
        // a = (m_right - m_left) * g / (m_right + m_left)
        float accel = 0f;
        if (totalMass > 0.001f)
        {
            accel = (smoothedRightMass - smoothedLeftMass) * gravity / totalMass;
        }

        // ── 3. Integrate velocity with damping ──
        ropeVelocity += accel * dt;
        ropeVelocity *= Mathf.Max(0f, 1f - ropeDamping * dt);

        // ── 4. Integrate position ──
        float desiredDelta = ropeVelocity * dt;
        float newRight = Mathf.Clamp(
            rightRopeLength + desiredDelta,
            minSegmentLength,
            availableRope - minSegmentLength);

        // Ground blocking
        if (enableGroundBlocking)
        {
            newRight = ClampByGround(newRight);
        }

        // If clamped by any limit, kill velocity
        if (Mathf.Abs(newRight - (rightRopeLength + desiredDelta)) > 0.0001f)
        {
            ropeVelocity = 0f;
        }

        rightRopeLength = newRight;
        float leftRopeLength = availableRope - rightRopeLength;

        // ── 5. Move platforms (only Y axis — preserve original X) ──
        // attachY = anchor.y - ropeLength  →  rb.y = attachY - offsetY
        leftPlatform.MovePosition(new Vector2(
            leftPlatform.position.x,
            leftAnchor.position.y - leftRopeLength - leftAttachOffsetY));
        rightPlatform.MovePosition(new Vector2(
            rightPlatform.position.x,
            rightAnchor.position.y - rightRopeLength - rightAttachOffsetY));

        // ── Debug ──
        debugLeftTotalMass = smoothedLeftMass;
        debugRightTotalMass = smoothedRightMass;
        debugAcceleration = accel;
        debugRopeVelocity = ropeVelocity;
        debugLeftRopeLength = leftRopeLength;
        debugRightRopeLength = rightRopeLength;
    }

    // ─────────────────── Initialization ───────────────────

    private void InitializeRope()
    {
        float topSpan = Vector2.Distance(leftAnchor.position, rightAnchor.position);
        // Rope length is measured from anchor to the TOP of the platform collider,
        // not to the Rigidbody2D center (which may be far from the actual platform).
        float leftAttachY = leftPlatform.position.y + leftAttachOffsetY;
        float rightAttachY = rightPlatform.position.y + rightAttachOffsetY;
        float currentLeft = Mathf.Abs(leftAnchor.position.y - leftAttachY);
        float currentRight = Mathf.Abs(rightAnchor.position.y - rightAttachY);

        if (autoInitializeFromCurrentPose)
        {
            totalRopeLength = currentLeft + currentRight + topSpan;
        }

        availableRope = Mathf.Max(minSegmentLength * 2f, totalRopeLength - topSpan);
        rightRopeLength = Mathf.Clamp(currentRight, minSegmentLength, availableRope - minSegmentLength);
        smoothedLeftMass = platformMass;
        smoothedRightMass = platformMass;
        ropeVelocity = 0f;
        initialized = true;
    }

    // ─────────────────── Mass Detection ───────────────────

    private float GetContactMass(Rigidbody2D platform)
    {
        if (platform == null) return 0f;

        int count = platform.GetContacts(contactBuffer);
        uniqueBodies.Clear();
        float total = 0f;

        for (int i = 0; i < count; i++)
        {
            Collider2D col = contactBuffer[i];
            if (col == null) continue;

            Rigidbody2D rb = col.attachedRigidbody;
            if (rb == null || rb == platform) continue;
            if (rb.bodyType != RigidbodyType2D.Dynamic) continue;
            // Only count bodies resting ON TOP of the platform
            if (rb.position.y <= platform.position.y) continue;
            if (!uniqueBodies.Add(rb)) continue;

            total += rb.mass;
        }

        return total;
    }

    // ─────────────────── Ground Blocking ───────────────────

    private float ClampByGround(float proposedRight)
    {
        // Two passes so either side clamping can propagate through rope constraint.
        float rightLength = proposedRight;
        for (int i = 0; i < 2; i++)
        {
            rightLength = ClampSegmentByCollisions(
                rightPlatform,
                rightSolidColliders,
                rightAnchor,
                rightAttachOffsetY,
                rightLength);

            float leftLength = availableRope - rightLength;
            leftLength = ClampSegmentByCollisions(
                leftPlatform,
                leftSolidColliders,
                leftAnchor,
                leftAttachOffsetY,
                leftLength);

            rightLength = availableRope - leftLength;
        }

        return Mathf.Clamp(rightLength, minSegmentLength, availableRope - minSegmentLength);
    }

    private float ClampSegmentByCollisions(
        Rigidbody2D platform,
        Collider2D[] platformColliders,
        Transform anchor,
        float attachOffsetY,
        float proposedLength)
    {
        if (platform == null) return proposedLength;

        float targetY = anchor.position.y - proposedLength - attachOffsetY;
        float currentY = platform.position.y;

        // Moving down
        if (targetY < currentY)
        {
            float desired = currentY - targetY;
            float allowed = GetMaxTravel(platform, platformColliders, Vector2.down, desired);
            if (allowed < desired)
            {
                float clampedY = currentY - allowed;
                return anchor.position.y - clampedY - attachOffsetY;
            }
        }
        // Moving up
        else if (targetY > currentY)
        {
            float desired = targetY - currentY;
            float allowed = GetMaxTravel(platform, platformColliders, Vector2.up, desired);
            if (allowed < desired)
            {
                float clampedY = currentY + allowed;
                return anchor.position.y - clampedY - attachOffsetY;
            }
        }

        return proposedLength;
    }

    private float GetMaxTravel(
        Rigidbody2D platform,
        Collider2D[] platformColliders,
        Vector2 direction,
        float desired)
    {
        if (platform == null || desired <= 0f) return 0f;
        if (platformColliders == null || platformColliders.Length == 0) return desired;

        ContactFilter2D filter = new ContactFilter2D();
        filter.useLayerMask = true;
        filter.layerMask = groundLayers;
        filter.useTriggers = false;

        float max = desired;
        for (int c = 0; c < platformColliders.Length; c++)
        {
            Collider2D col = platformColliders[c];
            if (col == null || !col.enabled || col.isTrigger) continue;

            int hits = col.Cast(direction, filter, castBuffer, desired);
            for (int i = 0; i < hits; i++)
            {
                Collider2D hitCol = castBuffer[i].collider;
                if (hitCol == null) continue;

                Rigidbody2D hitRb = hitCol.attachedRigidbody;
                // Skip self and the other platform
                if (hitRb == platform || hitRb == leftPlatform || hitRb == rightPlatform) continue;
                // Skip dynamic bodies (boxes, player) — only block on static/kinematic colliders
                if (hitRb != null && hitRb.bodyType == RigidbodyType2D.Dynamic) continue;

                max = Mathf.Min(max, Mathf.Max(0f, castBuffer[i].distance - 0.01f));
            }
        }

        return max;
    }

    // ─────────────────── Auto Wiring ───────────────────

    private void TryResolveReferences()
    {
        if (!autoFindByName) return;

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

    // ─────────────────── Utilities ───────────────────

    private static Transform FindChildRecursive(Transform root, string targetName)
    {
        if (root == null || string.IsNullOrEmpty(targetName)) return null;
        if (root.name == targetName) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), targetName);
            if (found != null) return found;
        }
        return null;
    }

    private static void DestroyJointIfPresent(Rigidbody2D platform)
    {
        if (platform == null) return;
        DistanceJoint2D joint = platform.GetComponent<DistanceJoint2D>();
        if (joint != null) Destroy(joint);
    }

    /// <summary>
    /// Returns the Y offset from the Rigidbody2D position to the top of
    /// the highest non-trigger collider (the rope attachment point).
    /// Can be negative when the collider is on a child far below the RB.
    /// </summary>
    private static float GetAttachOffsetY(Rigidbody2D platform)
    {
        if (platform == null) return 0f;

        Collider2D[] cols = platform.GetComponentsInChildren<Collider2D>();
        float bestTop = platform.position.y;
        bool found = false;

        foreach (var col in cols)
        {
            if (col.attachedRigidbody != platform) continue;
            if (col.isTrigger) continue;
            float top = col.bounds.max.y;
            if (!found || top > bestTop)
            {
                bestTop = top;
                found = true;
            }
        }

        return found ? bestTop - platform.position.y : 0f;
    }

    private static void EnsureNotTrigger(Rigidbody2D platform)
    {
        if (platform == null) return;
        foreach (var col in platform.GetComponentsInChildren<Collider2D>())
        {
            if (col.attachedRigidbody != platform) continue;
            // Only fix the main platform collider, leave functional triggers alone
            if (col.GetComponent<BoxWeightDetector>() != null) continue;
            if (col.GetComponent<Switch>() != null) continue;
            if (col.isTrigger)
            {
                col.isTrigger = false;
            }
        }
    }

    private static Rigidbody2D ResolvePlatformBody(Rigidbody2D current)
    {
        if (current == null) return null;
        if (GetPrimarySolidCollider(current) != null) return current;

        Rigidbody2D replacement = null;
        float bestArea = -1f;
        Rigidbody2D[] children = current.GetComponentsInChildren<Rigidbody2D>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Rigidbody2D candidate = children[i];
            if (candidate == null || candidate == current) continue;

            Collider2D candidateCol = GetPrimarySolidCollider(candidate);
            if (candidateCol == null) continue;

            Vector3 size = candidateCol.bounds.size;
            float area = Mathf.Abs(size.x * size.y);
            if (area > bestArea)
            {
                bestArea = area;
                replacement = candidate;
            }
        }

        if (replacement != null)
        {
            current.simulated = false;
            Debug.LogWarning(
                $"PulleySystemController: '{current.name}' had no solid collider on its own Rigidbody2D. " +
                $"Using child Rigidbody2D '{replacement.name}' as platform body.");
            return replacement;
        }

        return current;
    }

    private static Collider2D GetPrimarySolidCollider(Rigidbody2D rb)
    {
        if (rb == null) return null;

        Collider2D best = null;
        float bestArea = -1f;
        Collider2D[] cols = rb.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < cols.Length; i++)
        {
            Collider2D col = cols[i];
            if (col == null || !col.enabled) continue;
            if (col.isTrigger) continue;
            if (col.attachedRigidbody != rb) continue;

            Vector3 size = col.bounds.size;
            float area = Mathf.Abs(size.x * size.y);
            if (area > bestArea)
            {
                bestArea = area;
                best = col;
            }
        }

        return best;
    }

    private static Collider2D[] GetSolidColliders(Rigidbody2D rb)
    {
        if (rb == null) return Array.Empty<Collider2D>();

        Collider2D[] all = rb.GetComponentsInChildren<Collider2D>(true);
        List<Collider2D> solids = new List<Collider2D>(all.Length);
        for (int i = 0; i < all.Length; i++)
        {
            Collider2D col = all[i];
            if (col == null || !col.enabled) continue;
            if (col.isTrigger) continue;
            if (col.attachedRigidbody != rb) continue;
            solids.Add(col);
        }

        return solids.ToArray();
    }

}
