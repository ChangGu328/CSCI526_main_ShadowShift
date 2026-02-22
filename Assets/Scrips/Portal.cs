using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Portal : MonoBehaviour
{
    [Header("Debug")]
    public bool debugLog = true;

    [Header("Momentum (override pair settings if you want)")]
    public bool redirectMomentum = true;      // 把入门速度“转向”为出门方向
    public float momentumMultiplier = 1f;     // 想更猛就调大，比如 1.2 / 1.5
    public float maxExitSpeed = 25f;          // 限速，防止飞太远
    public ExitDirection exitDirection = ExitDirection.Right;

    public enum ExitDirection { Right, Left, Up }

    protected PortalPair pair;
    private float lastTeleportTime = -999f;

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    public void SetPair(PortalPair p, bool thisIsA)
    {
        pair = p;
    }

    private bool TryGetPlayer(Collider2D other, out Rigidbody2D rb, out PlayerController pc)
    {
        rb = other.attachedRigidbody;
        if (rb == null) rb = other.GetComponentInParent<Rigidbody2D>();

        pc = other.GetComponentInParent<PlayerController>();
        if (pc == null && rb != null) pc = rb.GetComponentInParent<PlayerController>();

        return (rb != null && pc != null);
    }

    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        if (debugLog) Debug.Log($"[Portal] ENTER by {other.name} root={other.transform.root.name}", this);

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

        if (!TryGetPlayer(other, out var playerRB, out var pc))
        {
            if (debugLog) Debug.Log($"[Portal] not player. other={other.name}, attachedRB={(other.attachedRigidbody ? other.attachedRigidbody.name : "null")}", this);
            return;
        }

        if (Time.time - lastTeleportTime < pair.cooldown)
        {
            if (debugLog) Debug.Log("[Portal] cooldown blocked", this);
            return;
        }

        var exitPortal = pair.GetOther(this);
        if (exitPortal == null)
        {
            if (debugLog) Debug.Log("[Portal] exitPortal == null (pair里没配好A/B?)", this);
            return;
        }

        // 入口&出口都上冷却，防止来回传
        lastTeleportTime = Time.time;
        exitPortal.lastTeleportTime = Time.time;

        // 计算出口方向（既用于速度也用于出生点方向）
        Transform exitT = exitPortal.transform;
        ExitDirection targetDirection = exitPortal.exitDirection;
        Vector2 exitDir = targetDirection switch
        {
            ExitDirection.Right => (Vector2)exitT.right,
            ExitDirection.Left => -(Vector2)exitT.right,
            ExitDirection.Up => (Vector2)exitT.up,
            _ => (Vector2)exitT.right
        };

        // 计算出口位置（沿出口门本地坐标偏移）
        // 当出口方向是 Left/Right 时，自动把 x 偏移朝向与 exitDirection 一致的那一侧
        Vector2 localOffset = pair.GetExitOffsetLocal(this);
        float sideOffset = localOffset.x;
        if (targetDirection == ExitDirection.Right) sideOffset = Mathf.Abs(localOffset.x);
        if (targetDirection == ExitDirection.Left) sideOffset = -Mathf.Abs(localOffset.x);

        Vector2 worldOffset = (Vector2)exitT.right * sideOffset + (Vector2)exitT.up * localOffset.y;
        Vector2 exitPos = (Vector2)exitT.position + worldOffset;

        // 先保存入门速度（很重要）
        Vector2 vIn = playerRB.linearVelocity;

        if (debugLog) Debug.Log($"[Portal] TELEPORT {playerRB.name} -> {exitPortal.name} pos={exitPos}", this);
        if (debugLog) Debug.Log($"BEFORE v={vIn}", this);

        // 传送位置
        playerRB.position = exitPos;

        // 如果你勾了 zeroVelocity，那肯定没惯性
        if (pair.zeroVelocity)
        {
            playerRB.linearVelocity = Vector2.zero;
            if (debugLog) Debug.Log($"AFTER  v={playerRB.linearVelocity} (zeroVelocity ON)", this);
            return;
        }

        // 动量转向：把“入门速度大小”沿出口方向推出去
        if (redirectMomentum)
        {
            float speed = vIn.magnitude * momentumMultiplier;

            Vector2 vOut = exitDir.normalized * speed;

            // 限速
            if (vOut.magnitude > maxExitSpeed)
                vOut = vOut.normalized * maxExitSpeed;

            playerRB.linearVelocity = vOut;
        }
        else
        {
            // 不转向：直接保留入门速度（默认惯性）
            playerRB.linearVelocity = vIn;
        }

        if (debugLog) Debug.Log($"AFTER  v={playerRB.linearVelocity}", this);
    }
}
