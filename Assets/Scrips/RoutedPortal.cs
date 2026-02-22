using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Collider2D))]
public class RoutedPortal : MonoBehaviour
{
    [System.Serializable]
    public class Route
    {
        [Header("Where to exit")]
        public Transform exit;                 // 出口门/出口点

        [Header("Gate condition (optional)")]
        public ExclusivePlate bodyPlate;       // 主体按钮（可共用）
        public ExclusivePlate soulPlate;       // 影子按钮（每条路线可不同）
        public bool gateRequired = true;

        [Header("Hold open (seconds)")]
        public float holdOpenSeconds = 3f;     // 满足条件后保持N秒
        public bool refreshWhilePressed = true;// 持续站着就续命

        [Header("Per-route momentum bonus (optional)")]
        public bool useVerticalBonus = false;
        public float verticalMomentumBonus = 0f;
        public float verticalSpeedThreshold = 0.05f;

        [Header("Door control (optional)")]
        public Switch[] doorSwitches;          // 可同时控制多个门开关（入口/出口）

        [HideInInspector] public float openUntilTime = -999f;

        public bool PlatesPressedNow()
        {
            if (!gateRequired) return true;
            if (bodyPlate == null || soulPlate == null) return false;
            return bodyPlate.IsPressed && soulPlate.IsPressed;
        }

        public void TickTimer()
        {
            if (!gateRequired)
            {
                openUntilTime = float.PositiveInfinity;
                return;
            }

            bool pressed = PlatesPressedNow();
            if (pressed)
            {
                if (refreshWhilePressed || Time.time > openUntilTime)
                    openUntilTime = Time.time + holdOpenSeconds;
            }
        }

        public bool IsOpenNow()
        {
            if (!gateRequired) return true;
            return Time.time <= openUntilTime;
        }

    }

    [Header("Routes (top = higher priority)")]
    public Route[] routes;

    [Header("Teleport")]
    public float cooldown = 0.2f;
    public bool zeroVelocity = false;

    [Header("Momentum")]
    public bool redirectMomentum = true;
    public float momentumMultiplier = 1f;
    public float maxExitSpeed = 25f;
    public ExitDirection exitDirection = ExitDirection.Right;
    public enum ExitDirection { Right, Up }

    [Header("Exit offset (LOCAL to exit transform)")]
    public Vector2 exitOffsetLocal = new Vector2(1f, 0f);

    [Header("Debug")]
    public bool debugLog = true;

    private float lastTeleportTime = -999f;

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void Update()
    {
        if (routes == null) return;
        for (int i = 0; i < routes.Length; i++)
        {
            if (routes[i] == null) continue;
            routes[i].TickTimer();
        }

        SyncAggregatedDoorStates();
    }

    private void SyncAggregatedDoorStates()
    {
        // 同一扇门可被多条 route 控制：只要任一路线打开，就保持打开。
        var aggregatedStates = new Dictionary<Switch, bool>();

        for (int i = 0; i < routes.Length; i++)
        {
            var route = routes[i];
            if (route == null) continue;

            bool isOpen = route.IsOpenNow();

            if (route.doorSwitches == null) continue;
            for (int j = 0; j < route.doorSwitches.Length; j++)
            {
                var sw = route.doorSwitches[j];
                if (sw == null) continue;

                if (aggregatedStates.TryGetValue(sw, out bool current))
                    aggregatedStates[sw] = current || isOpen;
                else
                    aggregatedStates.Add(sw, isOpen);
            }
        }

        foreach (var kv in aggregatedStates)
            kv.Key.isOn = kv.Value;
    }

    private bool TryGetPlayer(Collider2D other, out Rigidbody2D rb, out PlayerController pc)
    {
        rb = other.attachedRigidbody;
        if (rb == null) rb = other.GetComponentInParent<Rigidbody2D>();

        pc = other.GetComponentInParent<PlayerController>();
        if (pc == null && rb != null) pc = rb.GetComponentInParent<PlayerController>();

        return (rb != null && pc != null);
    }

    private Route PickRoute()
    {
        if (routes == null) return null;

        // 优先选择“当前打开”的路线（数组越靠前优先级越高）
        for (int i = 0; i < routes.Length; i++)
        {
            var r = routes[i];
            if (r == null || r.exit == null) continue;
            if (r.IsOpenNow()) return r;
        }

        // 如果你想：没任何路线开就不传送 => return null
        return null;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (Time.time - lastTeleportTime < cooldown) return;

        if (!TryGetPlayer(other, out var playerRB, out var pc))
            return;

        Route route = PickRoute();
        if (route == null)
        {
            if (debugLog) Debug.Log("[RoutedPortal] No route open -> blocked", this);
            return;
        }
        Transform exitT = route.exit;

        lastTeleportTime = Time.time;

        Vector2 worldOffset = (Vector2)exitT.right * exitOffsetLocal.x + (Vector2)exitT.up * exitOffsetLocal.y;
        Vector2 exitPos = (Vector2)exitT.position + worldOffset;

        Vector2 vIn = playerRB.linearVelocity;

        if (debugLog) Debug.Log($"[RoutedPortal] TELEPORT -> {exitT.name} pos={exitPos}", this);

        playerRB.position = exitPos;

        if (zeroVelocity)
        {
            playerRB.linearVelocity = Vector2.zero;
            return;
        }

        if (redirectMomentum)
        {
            float speed = vIn.magnitude * momentumMultiplier;
            if (route.useVerticalBonus && Mathf.Abs(vIn.y) > route.verticalSpeedThreshold)
            {
                speed += route.verticalMomentumBonus;
            }

            Vector2 dir = (exitDirection == ExitDirection.Right)
                ? (Vector2)exitT.right
                : (Vector2)exitT.up;

            Vector2 vOut = dir.normalized * speed;
            if (vOut.magnitude > maxExitSpeed) vOut = vOut.normalized * maxExitSpeed;

            playerRB.linearVelocity = vOut;
        }
        else
        {
            playerRB.linearVelocity = vIn;
        }
    }
}
