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

    // 开局保护：防止出生点 / 初始化瞬间误触发导致第一下被 cooldown 挡
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

    // ✅ 用 PlayerMove 来识别“可传送的角色”（主体/分身）
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
        // ✅ 开局保护期
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

        // ✅ 只认 PlayerMove（主体/分身），避免子物体/检测器误触发
        if (!TryGetActor(other, out var actorRB, out var pm))
        {
            if (debugLog) Debug.Log($"[Portal] not actor. other={other.name}", this);
            return;
        }

        // ✅ per-actor cooldown（主体/分身各自冷却，不再用每个门的 lastTeleportTime）
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

        // ✅ 只有“真正准备传送”时才写入 cooldown 时间戳
        pm.lastPortalTime = Time.time;

        // 计算出口方向（既用于速度也用于出生点方向）
        Transform exitT = exitPortal.transform;
        ExitDirection targetDirection = exitPortal.exitDirection;
        Vector2 exitDir = targetDirection switch
        {
            ExitDirection.Right => (Vector2)exitT.right,
            ExitDirection.Left  => -(Vector2)exitT.right,
            ExitDirection.Up    => (Vector2)exitT.up,
            _                   => (Vector2)exitT.right
        };

        // 计算出口位置（沿出口门本地坐标偏移）
        // 当出口方向是 Left/Right 时，自动把 x 偏移朝向与 exitDirection 一致的那一侧
        Vector2 localOffset = pair.GetExitOffsetLocal(this);
        float sideOffset = localOffset.x;
        if (targetDirection == ExitDirection.Right) sideOffset = Mathf.Abs(localOffset.x);
        if (targetDirection == ExitDirection.Left)  sideOffset = -Mathf.Abs(localOffset.x);

        Vector2 worldOffset = (Vector2)exitT.right * sideOffset + (Vector2)exitT.up * localOffset.y;
        Vector2 exitPos = (Vector2)exitT.position + worldOffset;

        // 先保存入门速度（很重要）
        Vector2 vIn = actorRB.linearVelocity;

        if (debugLog) Debug.Log($"[Portal] TELEPORT {pm.name} -> {exitPortal.name} pos={exitPos}", this);
        if (debugLog) Debug.Log($"BEFORE v={vIn}", this);

        // 传送位置
        actorRB.position = exitPos;

        // 如果你勾了 zeroVelocity，那肯定没惯性
        if (pair.zeroVelocity)
        {
            actorRB.linearVelocity = Vector2.zero;
            if (debugLog) Debug.Log($"AFTER  v={actorRB.linearVelocity} (zeroVelocity ON)", this);
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

            actorRB.linearVelocity = vOut;
        }
        else
        {
            // 不转向：直接保留入门速度（默认惯性）
            actorRB.linearVelocity = vIn;
        }

        if (debugLog) Debug.Log($"AFTER  v={actorRB.linearVelocity}", this);
    }
}
