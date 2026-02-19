// PulleySystemController.cs
using UnityEngine;

// - Keeps rope-length conservation mapping always available (passive follow).
// - Applies mass bias gradually; preview fraction used while mass settles.
// - Detects when a platform is touching the ground (via provided collider & groundLayer).
//   * If a platform is grounded, that side is clamped and its velocity zeroed to avoid jitter.
//   * The other side's target is computed respecting the grounded constraint.
// - On significant mass changes, enters cooldown and zeros velocities to allow physics to settle.
// - Expects BoxWeightDetector or similar to expose CurrentMass/TotalMass; GetDetectorMass handles it.
[RequireComponent(typeof(Collider2D))]
public class PulleySystemController : MonoBehaviour
{
    [Header("References")]
    public Rigidbody2D leftPlatform;
    public Rigidbody2D rightPlatform;
    // detectors
    public MonoBehaviour leftDetector;
    public MonoBehaviour rightDetector;

    // colliders for ground detection (assign the platform's main collider)
    [Tooltip("Collider2D used to test ground contact for left platform (assign the platform's collider).")]
    public Collider2D leftPlatformCollider;
    [Tooltip("Collider2D used to test ground contact for right platform (assign the platform's collider).")]
    public Collider2D rightPlatformCollider;

    [Header("Ground")]
    [Tooltip("Layer mask considered ground for platform contact checks.")]
    public LayerMask groundLayer;

    [Header("Pulley geometry")]
    public float ratio = 1f;

    [Header("Dynamics")]
    public float smoothSpeed = 6f;

    [Header("Mass influence")]
    public float massInfluence = 0.05f;

    [Header("Mass deadzone")]
    public float massDeadzone = 0.02f;

    [Header("Anti-oscillation")]
    [Range(0.01f, 0.9f)]
    public float massAlpha = 0.18f;
    public float massChangeThreshold = 0.02f;
    public float stableDuration = 0.12f;
    public float movementCooldownExtra = 0.08f;
    public float minMoveDistance = 0.005f;

    [Header("Preview while settling")]
    [Range(0f, 1f)]
    public float previewFraction = 0.25f;

    [Header("Follow / cooldown")]
    [Tooltip("If external displacement of left platform > this, allow passive follow during cooldown.")]
    public float externalFollowThreshold = 0.02f;

    [Header("Extra cooldown on mass removal")]
    [Tooltip("Multiplier for cooldown when mass decreases quickly (e.g., box removed)")]
    public float removalCooldownMultiplier = 2f;

    [Header("Limits")]
    public float leftMinY = -10f;
    public float leftMaxY = 10f;
    public float rightMinY = -10f;
    public float rightMaxY = 10f;

    // internals
    private float smoothedMassDiff = 0f;
    private float smoothedMassOffset = 0f;
    private float massStableTimer = 0f;
    private float lastObservedMassDiff = 0f;
    private float movementCooldownTimer = 0f;

    private float leftStartY;
    private float rightStartY;

    void Start()
    {
        if (leftPlatform == null || rightPlatform == null)
        {
            Debug.LogError("PulleySystemController requires leftPlatform and rightPlatform references.");
            enabled = false;
            return;
        }

        // If colliders not assigned, try to auto-find
        if (leftPlatformCollider == null && leftPlatform != null)
            leftPlatformCollider = leftPlatform.GetComponent<Collider2D>();
        if (rightPlatformCollider == null && rightPlatform != null)
            rightPlatformCollider = rightPlatform.GetComponent<Collider2D>();

        leftStartY = leftPlatform.position.y;
        rightStartY = rightPlatform.position.y;

        float lm = GetDetectorMass(leftDetector, leftPlatform);
        float rm = GetDetectorMass(rightDetector, rightPlatform);
        lastObservedMassDiff = lm - rm;
        smoothedMassDiff = lastObservedMassDiff;
        smoothedMassOffset = lastObservedMassDiff * massInfluence;
    }

    void FixedUpdate()
    {
        // compute left displacement from baseline (for rope mapping)
        float leftDelta = leftPlatform.position.y - leftStartY;
        float baseTargetRightY = rightStartY - leftDelta * ratio;

        // decrement cooldown
        if (movementCooldownTimer > 0f)
            movementCooldownTimer -= Time.fixedDeltaTime;

        // read masses
        float leftMass = GetDetectorMass(leftDetector, leftPlatform);
        float rightMass = GetDetectorMass(rightDetector, rightPlatform);
        float massDiff = leftMass - rightMass;

        // smoothing
        smoothedMassDiff = Mathf.Lerp(smoothedMassDiff, massDiff, massAlpha);

        // detect significant mass change
        bool significantChange = Mathf.Abs(massDiff - lastObservedMassDiff) > massChangeThreshold;
        if (significantChange)
        {
            // start cooldown; if mass decreased (object removed), extend cooldown
            bool massDecreased = (massDiff < lastObservedMassDiff);
            float extra = movementCooldownExtra * (massDecreased ? removalCooldownMultiplier : 1f);
            movementCooldownTimer = stableDuration + extra;

            massStableTimer = 0f;
            lastObservedMassDiff = massDiff;

            // zero velocities immediately
            ZeroVelocities();
            // allow passive follow to still operate below
        }
        else
        {
            massStableTimer += Time.fixedDeltaTime;
        }

        // ground detection
        bool leftGrounded = IsColliderTouchingGround(leftPlatformCollider);
        bool rightGrounded = IsColliderTouchingGround(rightPlatformCollider);

        // deadzone: if masses nearly equal -> snap to baseline and zero velocities
        if (Mathf.Abs(smoothedMassDiff) <= massDeadzone)
        {
            SnapToBaselineAndZero();
            return;
        }

        bool massIsStable = massStableTimer >= stableDuration;
        bool cooldownActive = movementCooldownTimer > 0f;
        bool allowPassiveFollow = Mathf.Abs(leftDelta) > externalFollowThreshold;
        bool allowActiveMove = (!cooldownActive) && massIsStable;

        // compute mass offset smoothing
        float instantMassOffset = massDiff * massInfluence;
        smoothedMassOffset = Mathf.Lerp(smoothedMassOffset, instantMassOffset, massAlpha);

        // applied offset: full when stable, preview otherwise
        float appliedOffset = (massIsStable && !cooldownActive) ? smoothedMassOffset : smoothedMassOffset * previewFraction;

        // compute targetRightY (passive follow + applied offset as allowed)
        float targetRightY = baseTargetRightY + (allowActiveMove ? smoothedMassOffset : appliedOffset);
        targetRightY = Mathf.Clamp(targetRightY, rightMinY, rightMaxY);

        // If one side is grounded, enforce clamping:
        if (leftGrounded)
        {
            // left is on ground: clamp left to the ground baseline (don't let it go below leftStartY)
            float leftGroundY = Mathf.Max(leftPlatform.position.y, leftStartY); // typically leftStartY is ground height
            leftPlatform.MovePosition(new Vector2(leftPlatform.position.x, leftGroundY));
            leftPlatform.linearVelocity = Vector2.zero;
            // recompute right target using leftGroundY as actual left position
            float leftDeltaFromGround = leftGroundY - leftStartY;
            float baseFromGroundRightY = rightStartY - leftDeltaFromGround * ratio;
            targetRightY = baseFromGroundRightY + (allowActiveMove ? smoothedMassOffset : appliedOffset);
            targetRightY = Mathf.Clamp(targetRightY, rightMinY, rightMaxY);
        }
        else if (rightGrounded)
        {
            // right is on ground: right must be clamped, compute left from it
            float rightGroundY = Mathf.Max(rightPlatform.position.y, rightStartY);
            rightPlatform.MovePosition(new Vector2(rightPlatform.position.x, rightGroundY));
            rightPlatform.linearVelocity = Vector2.zero;
            // recompute left target using rightGroundY
            float rightDeltaFromGround = rightGroundY - rightStartY;
            float desiredLeftYFromGround = leftStartY - rightDeltaFromGround / ratio;
            desiredLeftYFromGround = Mathf.Clamp(desiredLeftYFromGround, leftMinY, leftMaxY);
            // if right grounded, snap left accordingly (respecting movement rules)
            if (cooldownActive && !allowPassiveFollow)
            {
                leftPlatform.MovePosition(new Vector2(leftPlatform.position.x, leftPlatform.position.y)); // keep
                leftPlatform.linearVelocity = Vector2.zero;
            }
            else
            {
                leftPlatform.MovePosition(new Vector2(leftPlatform.position.x, desiredLeftYFromGround));
            }
            return; // right grounded handled; avoid further move below
        }

        // compute left desired from rope constraint (rightStartY used)
        float rightDeltaNew = targetRightY - rightStartY;
        float desiredLeftY = leftStartY - rightDeltaNew / ratio;
        desiredLeftY = Mathf.Clamp(desiredLeftY, leftMinY, leftMaxY);

        // avoid micro-drift
        float rightMoveDelta = Mathf.Abs(targetRightY - rightPlatform.position.y);
        float leftMoveDelta = Mathf.Abs(desiredLeftY - leftPlatform.position.y);
        if (rightMoveDelta < minMoveDistance && leftMoveDelta < minMoveDistance)
        {
            // small movement -> snap & zero velocity
            leftPlatform.MovePosition(new Vector2(leftPlatform.position.x, leftPlatform.position.y));
            rightPlatform.MovePosition(new Vector2(rightPlatform.position.x, rightPlatform.position.y));
            ZeroVelocities();
            return;
        }

        // If cooldown is active and passive follow not allowed, keep snapped baseline
        if (cooldownActive && !allowPassiveFollow && !allowActiveMove)
        {
            SnapToBaselineAndZero();
            return;
        }

        // perform smooth movement
        Vector2 leftDesiredPos = new Vector2(leftPlatform.position.x, desiredLeftY);
        Vector2 rightDesiredPos = new Vector2(rightPlatform.position.x, targetRightY);
        float t = Time.fixedDeltaTime * smoothSpeed;
        Vector2 leftNew = Vector2.Lerp(leftPlatform.position, leftDesiredPos, t);
        Vector2 rightNew = Vector2.Lerp(rightPlatform.position, rightDesiredPos, t);

        // Move positions, but if either side is about to penetrate ground, clamp
        if (!leftGrounded)
            leftPlatform.MovePosition(leftNew);
        if (!rightGrounded)
            rightPlatform.MovePosition(rightNew);

        // small safety cooldown after significant move
        if (Mathf.Abs(rightNew.y - rightPlatform.position.y) > minMoveDistance * 0.5f)
            movementCooldownTimer = movementCooldownExtra;
    }

    // zero velocities cleanly
    private void ZeroVelocities()
    {
        if (leftPlatform != null)
        {
            leftPlatform.linearVelocity = Vector2.zero;
            leftPlatform.angularVelocity = 0f;
        }
        if (rightPlatform != null)
        {
            rightPlatform.linearVelocity = Vector2.zero;
            rightPlatform.angularVelocity = 0f;
        }
    }

    // snap both to baseline and zero velocities
    private void SnapToBaselineAndZero()
    {
        leftPlatform.MovePosition(new Vector2(leftPlatform.position.x, leftStartY));
        rightPlatform.MovePosition(new Vector2(rightPlatform.position.x, rightStartY));
        ZeroVelocities();
        leftStartY = leftPlatform.position.y;
        rightStartY = rightPlatform.position.y;
        smoothedMassOffset = 0f;
    }

    // test whether a platform collider is touching ground layer (uses Collider2D.IsTouchingLayers)
    private bool IsColliderTouchingGround(Collider2D col)
    {
        if (col == null) return false;
        return col.IsTouchingLayers(groundLayer);
    }
    
    
    // GetDetectorMass: combine platform mass and detector tracked mass (prefers TotalMass if detector provides it).
    private float GetDetectorMass(MonoBehaviour detector, Rigidbody2D platformRb)
    {
        float platformMass = (platformRb != null) ? platformRb.mass : 0f;
        float detectorMass = 0f;

        if (detector != null)
        {
            var boxDet = detector as BoxWeightDetector;
            if (boxDet != null)
            {
                // prefer TotalMass if available
                try
                {
                    detectorMass = Mathf.Max(0f, boxDet.TotalMass - platformMass);
                }
                catch
                {
                    detectorMass = boxDet.CurrentMass;
                }
            }
            else
            {
                var propT = detector.GetType().GetProperty("TotalMass");
                if (propT != null)
                {
                    object v = propT.GetValue(detector, null);
                    if (v is float f) detectorMass = Mathf.Max(0f, f - platformMass);
                    else if (v is double d) detectorMass = Mathf.Max(0f, (float)d - platformMass);
                }
                else
                {
                    var propC = detector.GetType().GetProperty("CurrentMass");
                    if (propC != null)
                    {
                        object v2 = propC.GetValue(detector, null);
                        if (v2 is float f2) detectorMass = f2;
                        else if (v2 is double d2) detectorMass = (float)d2;
                    }
                }
            }
        }

        return Mathf.Max(0f, platformMass + detectorMass);
    }
    
}