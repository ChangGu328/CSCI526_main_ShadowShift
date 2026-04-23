using UnityEngine;

public class PortalEntranceVisual : MonoBehaviour
{
    [Header("Portal Renderers")]
    public SpriteRenderer portalRed;
    public SpriteRenderer portalBlue;

    [Header("Routed Portal")]
    public RoutedPortal routedPortal;

    [Header("Route Index")]
    public int redRouteIndex = 1;
    public int blueRouteIndex = 0;

    [Header("Settings")]
    public float pingPongSpeed = 1f;

    void Update()
    {
        bool redOpen  = routedPortal.routes[redRouteIndex].IsOpenNow();
        bool blueOpen = routedPortal.routes[blueRouteIndex].IsOpenNow();

        var redRoute  = routedPortal.routes[redRouteIndex];
        var blueRoute = routedPortal.routes[blueRouteIndex];

        bool redHalf  = (redRoute.bodyPlate.IsPressed || redRoute.soulPlate.IsPressed) && !redOpen;
        bool blueHalf = (blueRoute.bodyPlate.IsPressed || blueRoute.soulPlate.IsPressed) && !blueOpen;

        if (redOpen)
            SetColor(1f, 0f, 1f);
        else if (blueOpen)
            SetColor(0f, 1f, 1f);
        else if (redHalf)
            SetColor(0.8f, 0.2f, 0.6f);
        else if (blueHalf)
            SetColor(0.2f, 0.8f, 0.6f);
        else
        {
            float t = Mathf.PingPong(Time.time * pingPongSpeed, 1f);
            SetColor(t, 1f - t, 0.3f);
        }
    }

    void SetColor(float redAlpha, float blueAlpha, float brightness = 1f)
    {
        portalRed.color  = new Color(brightness, brightness, brightness, redAlpha);
        portalBlue.color = new Color(brightness, brightness, brightness, blueAlpha);
    }
}