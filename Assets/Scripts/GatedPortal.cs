using UnityEngine;

public class GatedPortal : Portal
{
    protected override void OnTriggerEnter2D(Collider2D other)
    {
        // They won't let anyone in if the door isn't open.
        if (pair != null && !pair.IsGateOpen())
        {
            if (debugLog) Debug.Log("[GatedPortal] Gate closed.", this);
            return;
        }

        base.OnTriggerEnter2D(other);
    }
}
