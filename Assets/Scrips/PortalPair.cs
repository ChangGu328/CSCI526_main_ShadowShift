using UnityEngine;

public class PortalPair : MonoBehaviour
{
    [Header("Drag two portals here")]
    public Portal portalA;
    public Portal portalB;

    [Header("Optional Gate Switch")]
    public Switch gateSwitch;

    [Header("Teleport")]
    public float cooldown = 0.2f;
    public bool zeroVelocity = false;

    [Header("Exit offsets (LOCAL to exit portal)")]
    public Vector2 exitOffsetAtoB = new Vector2(1f, 0f);
    public Vector2 exitOffsetBtoA = new Vector2(1f, 0f);

    private void Awake()
    {
        if (portalA != null) portalA.SetPair(this, true);
        if (portalB != null) portalB.SetPair(this, false);
    }

    public Portal GetOther(Portal current)
    {
        if (current == portalA) return portalB;
        if (current == portalB) return portalA;
        return null;
    }

    // Portal.cs 调用拿“相对出口门方向”的 offset
    public Vector2 GetExitOffsetLocal(Portal from)
    {
        if (from == portalA) return exitOffsetAtoB;
        if (from == portalB) return exitOffsetBtoA;
        return Vector2.zero;
    }

    // ⭐⭐ 新增：默认永远开门（旧关卡不受影响）
    public virtual bool IsGateOpen()
    {
        // If no switch is linked, keep backward-compatible behavior (always open).
        if (gateSwitch == null) return true;
        return gateSwitch.isOn;
    }
}
