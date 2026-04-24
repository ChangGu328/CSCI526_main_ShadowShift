using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class CountdownAnchor : MonoBehaviour
{
    [Header("Source")]
    [SerializeField] private RoutedPortal routedPortal;
    [Tooltip("-1 = automatically display the highest-priority route that is currently counting down.")]
    [SerializeField] private int routeIndex = -1;

    [Header("Renderers")]
    [SerializeField] private SpriteRenderer clockRenderer;
    [SerializeField] private SpriteRenderer tensRenderer;
    [SerializeField] private SpriteRenderer onesRenderer;

    [Header("Clock Art")]
    [SerializeField] private Sprite[] clockSpinFrames = new Sprite[4];
    [SerializeField, Min(0.01f)] private float clockAnimationFps = 8f;

    [Header("Number Art")]
    [SerializeField] private Sprite[] numberSprites = new Sprite[10];

#if UNITY_EDITOR
    private const string NumbersAssetPath = "Assets/Art/Countdown/Numbers/lcd_numbers_gb_6x13_invert_grey.png";
    private static readonly string[] ClockAssetPaths =
    {
        "Assets/Art/Countdown/Clock/clock0.png",
        "Assets/Art/Countdown/Clock/clock1.png",
        "Assets/Art/Countdown/Clock/clock2.png",
        "Assets/Art/Countdown/Clock/clock3.png",
    };
#endif

    private void Reset()
    {
        AutoAssignSceneRefs();
#if UNITY_EDITOR
        AutoAssignArtRefs();
#endif
        RefreshVisuals();
    }

    private void Awake()
    {
        AutoAssignSceneRefs();
        RefreshVisuals();
    }

    private void LateUpdate()
    {
        RefreshVisuals();
    }

    private void OnValidate()
    {
        AutoAssignSceneRefs();
#if UNITY_EDITOR
        AutoAssignArtRefs();
#endif
        RefreshVisuals();
    }

    private void AutoAssignSceneRefs()
    {
        if (routedPortal == null)
            routedPortal = GetComponentInParent<RoutedPortal>();

        if (clockRenderer == null)
            clockRenderer = FindChildRenderer("Clock");

        if (tensRenderer == null)
            tensRenderer = FindChildRenderer("Tens");

        if (onesRenderer == null)
            onesRenderer = FindChildRenderer("Ones");

        EnsureArraySizes();
    }

    private SpriteRenderer FindChildRenderer(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponent<SpriteRenderer>() : null;
    }

    private void EnsureArraySizes()
    {
        if (clockSpinFrames == null || clockSpinFrames.Length != 4)
            Array.Resize(ref clockSpinFrames, 4);

        if (numberSprites == null || numberSprites.Length != 10)
            Array.Resize(ref numberSprites, 10);
    }

#if UNITY_EDITOR
    private void AutoAssignArtRefs()
    {
        EnsureArraySizes();

        for (int i = 0; i < clockSpinFrames.Length; i++)
        {
            if (clockSpinFrames[i] == null)
                clockSpinFrames[i] = AssetDatabase.LoadAssetAtPath<Sprite>(ClockAssetPaths[i]);
        }

        bool needsAnyDigit = false;
        for (int i = 0; i < numberSprites.Length; i++)
        {
            if (numberSprites[i] == null)
            {
                needsAnyDigit = true;
                break;
            }
        }

        if (!needsAnyDigit)
            return;

        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(NumbersAssetPath);
        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is not Sprite sprite)
                continue;

            if (TryGetDigitFromName(sprite.name, out int digit))
                numberSprites[digit] = sprite;
        }
    }

    private static bool TryGetDigitFromName(string spriteName, out int digit)
    {
        digit = -1;

        int underscore = spriteName.LastIndexOf('_');
        if (underscore < 0 || underscore >= spriteName.Length - 1)
            return false;

        return int.TryParse(spriteName[(underscore + 1)..], out digit) && digit is >= 0 and <= 9;
    }
#endif

    private void RefreshVisuals()
    {
        float remainingSeconds = GetDisplayedRemainingSeconds();
        bool countdownActive = remainingSeconds > 0f;

        if (!countdownActive)
        {
            SetRendererVisible(clockRenderer, false);
            SetRendererVisible(tensRenderer, false);
            SetRendererVisible(onesRenderer, false);
            return;
        }

        int shownValue = float.IsPositiveInfinity(remainingSeconds)
            ? 99
            : Mathf.Clamp(Mathf.CeilToInt(remainingSeconds), 0, 99);

        ApplyNumberDisplay(shownValue);
        ApplyClockDisplay();
    }

    private float GetDisplayedRemainingSeconds()
    {
        if (routedPortal == null || routedPortal.routes == null || routedPortal.routes.Length == 0)
            return 0f;

        if (routeIndex >= 0 && routeIndex < routedPortal.routes.Length)
            return GetRemainingSeconds(routedPortal.routes[routeIndex]);

        for (int i = 0; i < routedPortal.routes.Length; i++)
        {
            float remaining = GetRemainingSeconds(routedPortal.routes[i]);
            if (remaining > 0f)
                return remaining;
        }

        return 0f;
    }

    private float GetRemainingSeconds(RoutedPortal.Route route)
    {
        if (route == null || !route.gateRequired)
            return 0f;

        if (route.refreshWhilePressed && route.PlatesPressedNow())
            return Mathf.Max(0f, route.holdOpenSeconds);

        float remaining = route.openUntilTime - Time.time;
        if (remaining <= 0f && route.PlatesPressedNow())
            remaining = route.holdOpenSeconds;

        return Mathf.Max(0f, remaining);
    }

    private void ApplyNumberDisplay(int value)
    {
        value = Mathf.Clamp(value, 0, 99);
        bool showTens = value >= 10;

        UpdateNumberLayout(showTens);

        SetDigit(onesRenderer, value % 10, true);

        if (showTens)
            SetDigit(tensRenderer, value / 10, true);
        else if (tensRenderer != null)
            tensRenderer.enabled = false;
    }

    private void UpdateNumberLayout(bool showTens)
    {
        if (onesRenderer == null)
            return;

        Vector3 onesLocalPosition = onesRenderer.transform.localPosition;
        float centerX = clockRenderer != null ? clockRenderer.transform.localPosition.x : onesLocalPosition.x;

        if (!showTens)
        {
            onesRenderer.transform.localPosition = new Vector3(centerX, onesLocalPosition.y, onesLocalPosition.z);
            return;
        }

        if (tensRenderer == null)
            return;

        Vector3 tensLocalPosition = tensRenderer.transform.localPosition;
        float mirroredOnesX = centerX + (centerX - tensLocalPosition.x);
        onesRenderer.transform.localPosition = new Vector3(mirroredOnesX, onesLocalPosition.y, onesLocalPosition.z);
    }

    private void SetDigit(SpriteRenderer renderer, int digit, bool visible)
    {
        if (renderer == null)
            return;

        renderer.enabled = visible;

        if (!visible)
            return;

        if (digit < 0 || digit >= numberSprites.Length)
            return;

        Sprite sprite = numberSprites[digit];
        if (sprite != null)
            renderer.sprite = sprite;
    }

    private void SetRendererVisible(SpriteRenderer renderer, bool visible)
    {
        if (renderer != null)
            renderer.enabled = visible;
    }

    private void ApplyClockDisplay()
    {
        if (clockRenderer == null)
            return;

        int frameCount = 0;
        for (int i = 0; i < clockSpinFrames.Length; i++)
        {
            if (clockSpinFrames[i] != null)
                frameCount++;
        }

        if (frameCount == 0)
            return;

        clockRenderer.enabled = true;

        int frameIndex = Mathf.FloorToInt(Time.time * clockAnimationFps) % frameCount;
        clockRenderer.sprite = clockSpinFrames[frameIndex];
    }
}
