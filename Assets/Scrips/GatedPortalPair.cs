using UnityEngine;

public class GatedPortalPair : PortalPair
{
    [Header("Gate (Both must be pressed)")]
    public ExclusivePlate bodyPlate;
    public ExclusivePlate soulPlate;
    public bool gateRequired = true;

    [Header("Hold Open (seconds)")]
    public float holdOpenSeconds = 2f;   // ✅ 门开启后保持N秒（可调）
    public bool refreshWhilePressed = true; // ✅ 两人一直踩着时是否一直刷新计时

    [Header("Visual")]
    public bool controlPortalCollider = true;
    public bool dimWhenClosed = true;
    public float closedAlpha = 0.3f;

    private float openTimer = 0f;

    private void Update()
    {
        TickGateTimer();
        UpdateGateVisual();
    }

    // ⭐ override 父类方法（关键！）
    public override bool IsGateOpen()
    {
        // 计时器 > 0 就保持开启
        return openTimer > 0f;
    }

    private void TickGateTimer()
    {
        bool bothPressedNow = IsBothPressed();

        // 触发开启：两者踩对一次 -> 直接把计时器拉满
        if (bothPressedNow)
        {
            if (refreshWhilePressed)
            {
                openTimer = holdOpenSeconds;
            }
            else
            {
                // 不刷新：只在“刚刚同时踩到”的瞬间触发一次
                // 用一个小技巧：只有当 timer <= 0 才重新开
                if (openTimer <= 0f)
                    openTimer = holdOpenSeconds;
            }
        }
        else
        {
            // 没踩着就倒计时
            if (openTimer > 0f)
                openTimer -= Time.deltaTime;
        }

        if (openTimer < 0f) openTimer = 0f;
    }

    private bool IsBothPressed()
    {
        if (!gateRequired) return true;
        if (bodyPlate == null || soulPlate == null) return false;
        return bodyPlate.IsPressed && soulPlate.IsPressed;
    }

    private void UpdateGateVisual()
    {
        bool open = IsGateOpen();
        ApplyToPortal(portalA, open);
        ApplyToPortal(portalB, open);
    }

    private void ApplyToPortal(Portal portal, bool open)
    {
        if (portal == null) return;

        if (controlPortalCollider)
        {
            var col = portal.GetComponent<Collider2D>();
            if (col != null) col.enabled = open;
        }

        if (dimWhenClosed)
        {
            var sr = portal.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                var c = sr.color;
                c.a = open ? 1f : closedAlpha;
                sr.color = c;
            }
        }
    }
}
