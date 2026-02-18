using UnityEngine;

public class GatedPortal : Portal
{
    protected override void OnTriggerEnter2D(Collider2D other)
    {
        // 门没开就不让传
        if (pair != null && !pair.IsGateOpen())
        {
            if (debugLog) Debug.Log("[GatedPortal] Gate closed.", this);
            return;
        }

        base.OnTriggerEnter2D(other);
    }
}
