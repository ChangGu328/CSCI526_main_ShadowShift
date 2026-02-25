using UnityEngine;

public class GatedPortalPair : PortalPair
{
    [Header("Gate (Both must be pressed)")]
    public ExclusivePlate bodyPlate;
    public ExclusivePlate soulPlate;
    public bool gateRequired = true;

    [Header("Hold Open (seconds)")]
    public float holdOpenSeconds = 2f;   // The door remains open for N seconds (adjustable).
    public bool refreshWhilePressed = true; // Does the timer keep refreshing while the two people are constantly stepping on it

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

    // Override the parent class method
    public override bool IsGateOpen()
    {
        // The timer > 0 keeps the gate open
        return openTimer > 0f;
    }

    private void TickGateTimer()
    {
        bool bothPressedNow = IsBothPressed();

        // Trigger opening: both pressed once -> directly pull the timer to full
        if (bothPressedNow)
        {
            if (refreshWhilePressed)
            {
                openTimer = holdOpenSeconds;
            }
            else
            {
                // Don't refresh: only trigger once "both pressed"
                // Use a small trick: only re-open when timer <= 0
                if (openTimer <= 0f)
                    openTimer = holdOpenSeconds;
            }
        }
        else
        {
            // Not both pressed: count down if the timer is running
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
