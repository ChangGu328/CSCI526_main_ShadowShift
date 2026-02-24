using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Portal : MonoBehaviour
{
    [Header("Debug")]
    public bool debugLog = true;

    [Header("Momentum (override pair settings if you want)")]
    public bool redirectMomentum = true;      // Turn the learning speed into the exit direction
    public float momentumMultiplier = 1f;     // The speed at the exit will be "entry speed * momentumMultiplier"
    public float maxExitSpeed = 25f;          // Cap the exit speed to prevent flying off
    public ExitDirection exitDirection = ExitDirection.Right;

    public enum ExitDirection { Right, Left, Up }

    protected PortalPair pair;

    // Start-of-game protection: Prevents accidental triggering at the spawn point/initialization, which could cause the initial cooldown to be blocked.
    [Header("Safety")]
    public float ignoreAtLevelStartSeconds = 0.2f;

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    public void SetPair(PortalPair p, bool thisIsA)
    {
        pair = p;
    }

    // Use PlayerMove to identify "teleportable characters" (main character/clone).
    private bool TryGetActor(Collider2D other, out Rigidbody2D rb, out PlayerMove pm)
    {
        pm = other.GetComponentInParent<PlayerMove>();
        if (pm == null)
        {
            rb = null;
            return false;
        }

        rb = other.attachedRigidbody;
        if (rb == null) rb = pm.GetComponent<Rigidbody2D>();
        if (rb == null) rb = pm.GetComponentInParent<Rigidbody2D>();

        return rb != null;
    }

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        // Initial protection period
        if (Time.timeSinceLevelLoad < ignoreAtLevelStartSeconds)
            return;

        if (debugLog) Debug.Log($"[Portal] ENTER portal={name} by {other.name} root={other.transform.root.name}", this);

        if (pair == null)
        {
            if (debugLog) Debug.Log("[Portal] pair == null (没绑定 PortalPair?)", this);
            return;
        }

        if (!pair.IsGateOpen())
        {
            if (debugLog) Debug.Log("[Portal] gate closed (switch is off).", this);
            return;
        }

        // Only recognize PlayerMove (main body/clone) to avoid accidental triggering of child objects/detectors.
        if (!TryGetActor(other, out var actorRB, out var pm))
        {
            if (debugLog) Debug.Log($"[Portal] not actor. other={other.name}", this);
            return;
        }

        // Per-actor cooldown (Main body/clones cool down independently, no longer using lastTeleportTime for each door)
        if (Time.time - pm.lastPortalTime < pair.cooldown)
        {
            if (debugLog) Debug.Log($"[Portal] cooldown blocked actor={pm.name} dt={(Time.time - pm.lastPortalTime):F3}", this);
            return;
        }

        var exitPortal = pair.GetOther(this);
        if (exitPortal == null)
        {
            if (debugLog) Debug.Log("[Portal] exitPortal == null (pair里没配好A/B?)", this);
            return;
        }

        // A cooldown timestamp is only written when the device is "truly ready to be transferred".
        pm.lastPortalTime = Time.time;

        // Calculate the exit direction (used for both velocity and spawn point direction).
        Transform exitT = exitPortal.transform;
        ExitDirection targetDirection = exitPortal.exitDirection;
        Vector2 exitDir = targetDirection switch
        {
            ExitDirection.Right => (Vector2)exitT.right,
            ExitDirection.Left  => -(Vector2)exitT.right,
            ExitDirection.Up    => (Vector2)exitT.up,
            _                   => (Vector2)exitT.right
        };

        // Calculate the exit position (offset along the exit portal's local coordinates).
        // When the exit direction is Left/Right, automatically adjust the x offset to align with the exitDirection.
        Vector2 localOffset = pair.GetExitOffsetLocal(this);
        float sideOffset = localOffset.x;
        if (targetDirection == ExitDirection.Right) sideOffset = Mathf.Abs(localOffset.x);
        if (targetDirection == ExitDirection.Left)  sideOffset = -Mathf.Abs(localOffset.x);

        Vector2 worldOffset = (Vector2)exitT.right * sideOffset + (Vector2)exitT.up * localOffset.y;
        Vector2 exitPos = (Vector2)exitT.position + worldOffset;

        // First, save your learning speed
        Vector2 vIn = actorRB.linearVelocity;

        if (debugLog) Debug.Log($"[Portal] TELEPORT {pm.name} -> {exitPortal.name} pos={exitPos}", this);
        if (debugLog) Debug.Log($"BEFORE v={vIn}", this);

        // Teleportation location
        actorRB.position = exitPos;

        // If you check zeroVelocity, there will be no inertia
        if (pair.zeroVelocity)
        {
            actorRB.linearVelocity = Vector2.zero;
            if (debugLog) Debug.Log($"AFTER  v={actorRB.linearVelocity} (zeroVelocity ON)", this);
            return;
        }

        // Momentum redirection: push the "entry speed magnitude" along the exit direction
        if (redirectMomentum)
        {
            float speed = vIn.magnitude * momentumMultiplier;
            Vector2 vOut = exitDir.normalized * speed;

            // Limit speed
            if (vOut.magnitude > maxExitSpeed)
                vOut = vOut.normalized * maxExitSpeed;

            actorRB.linearVelocity = vOut;
        }
        else
        {
            // No redirection: directly retain the entry speed (default inertia)
            actorRB.linearVelocity = vIn;
        }

        if (debugLog) Debug.Log($"AFTER  v={actorRB.linearVelocity}", this);
    }
}
