using System.Collections;
using UnityEngine;


// Handles soul collision with light.
// Adds delay before switching back to body and includes cooldown
// to prevent repeated triggering.
public class LightExtinguish : MonoBehaviour
{
    [Header("Timing")] [Tooltip("Delay before switching control back to body.")]
    public float extinguishDelay = 0.3f;

    [Tooltip("Cooldown time before this light can trigger again.")]
    public float cooldownTime = 0.5f;

    [Header("Light Blocking")]
    [Tooltip("Tag used by platforms that block this light from top to bottom.")]
    public string lightBlockerTag = "LightBlocker";

    [Tooltip("Only consider blockers in this mask. Leave as Everything if unsure.")]
    public LayerMask blockerMask = Physics2D.DefaultRaycastLayers;

    [Header("Beam Visual")]
    [Tooltip("Minimum beam length to avoid disappearing when blocker is very close to the lamp.")]
    public float minBeamLength = 0.05f;

    [Tooltip("Thickness used by downward blocker cast. Keep small, but > 0.")]
    public float blockerCastThickness = 0.02f;

    public bool hideSoul = true;

    private bool isCoolingDown;
    private BoxCollider2D beamCollider;
    private float beamTopLocalY;
    private float initialScaleY;
    private float initialTopAnchorLocalY;
    private float initialBeamLengthWorld;
    private bool hasBeamSetup;

    private void Awake()
    {
        CacheBeamSetup();
        UpdateBeamCutoff();
    }

    private void LateUpdate()
    {
        if (!hasBeamSetup)
            return;

        UpdateBeamCutoff();
    }

    private void Reset()
    {
        var col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isCoolingDown)
            return;

        var player = other.GetComponentInParent<PlayerController>();
        if (player == null)
            return;

        if (player.soul == null)
            return;

        if (other.gameObject != player.soul)
            return;

        if (!CanLightReachSoul(player, other))
            return;

        StartCoroutine(ExtinguishRoutine(player));
    }

    private bool CanLightReachSoul(PlayerController player, Collider2D soulCollider)
    {
        if (soulCollider == null)
            return false;

        Vector2 lightPos = GetLightOrigin();
        Vector2 soulPos = soulCollider.bounds.center;

        // This light is assumed to shine downward.
        if (soulPos.y > lightPos.y)
            return false;

        var hits = Physics2D.LinecastAll(lightPos, soulPos, blockerMask);
        foreach (var hit in hits)
        {
            var col = hit.collider;
            if (col == null)
                continue;

            if (col.isTrigger)
                continue;

            if (col == soulCollider)
                continue;

            if (player != null && col.transform.IsChildOf(player.transform))
                continue;

            if (col.CompareTag(lightBlockerTag))
                return false;
        }

        return true;
    }

    private void CacheBeamSetup()
    {
        beamCollider = GetComponent<BoxCollider2D>();
        if (beamCollider == null)
        {
            hasBeamSetup = false;
            return;
        }

        initialScaleY = transform.localScale.y;
        if (Mathf.Approximately(initialScaleY, 0f))
        {
            hasBeamSetup = false;
            return;
        }

        beamTopLocalY = beamCollider.offset.y + beamCollider.size.y * 0.5f;
        initialTopAnchorLocalY = transform.localPosition.y + beamTopLocalY * initialScaleY;
        initialBeamLengthWorld = Mathf.Abs(transform.lossyScale.y) * beamCollider.size.y;
        hasBeamSetup = initialBeamLengthWorld > 0.0001f;
    }

    private Vector2 GetLightOrigin()
    {
        if (hasBeamSetup)
            return transform.TransformPoint(new Vector3(0f, beamTopLocalY, 0f));

        return transform.position;
    }

    private void UpdateBeamCutoff()
    {
        float targetLengthWorld = initialBeamLengthWorld;
        Vector2 origin = GetLightOrigin();
        Physics2D.SyncTransforms();
        float beamWidthWorld = Mathf.Abs(transform.lossyScale.x) * beamCollider.size.x;
        Vector2 castSize = new Vector2(Mathf.Max(beamWidthWorld, 0.01f), Mathf.Max(blockerCastThickness, 0.001f));
        var hits = Physics2D.BoxCastAll(origin, castSize, 0f, Vector2.down, initialBeamLengthWorld, blockerMask);

        for (int i = 0; i < hits.Length; i++)
        {
            var col = hits[i].collider;
            if (col == null)
                continue;

            if (col.isTrigger)
                continue;

            if (col.transform == transform || col.transform.IsChildOf(transform))
                continue;

            if (!col.CompareTag(lightBlockerTag))
                continue;

            targetLengthWorld = Mathf.Min(targetLengthWorld, hits[i].distance);
        }

        targetLengthWorld = Mathf.Clamp(targetLengthWorld, minBeamLength, initialBeamLengthWorld);
        float ratio = targetLengthWorld / initialBeamLengthWorld;
        float newScaleY = initialScaleY * ratio;

        Vector3 localScale = transform.localScale;
        localScale.y = newScaleY;
        transform.localScale = localScale;

        Vector3 localPos = transform.localPosition;
        localPos.y = initialTopAnchorLocalY - beamTopLocalY * newScaleY;
        transform.localPosition = localPos;
    }

    private IEnumerator ExtinguishRoutine(PlayerController player)
    {
        isCoolingDown = true;

        var soulObj = player.soul;
        var bodyObj = player.body;

        // Stop soul movement immediately
        if (soulObj != null)
        {
            var soulMove = soulObj.GetComponent<PlayerMove>();
            if (soulMove != null)
            {
                try
                {
                    soulMove.Stop();
                }
                catch
                {
                }

                soulMove.enabled = false;
            }

            var soulRb = soulObj.GetComponent<Rigidbody2D>();
            if (soulRb != null)
                soulRb.linearVelocity = Vector2.zero;
        }

        // Small delay before switching (visual smoothing)
        yield return new WaitForSeconds(extinguishDelay);

        // Hide or destroy soul
        if (soulObj != null)
        {
            if (hideSoul)
                soulObj.SetActive(false);
            else
                Destroy(soulObj);
        }

        // Reactivate body control
        if (bodyObj != null)
        {
            bodyObj.SetActive(true);

            var bodyMove = bodyObj.GetComponent<PlayerMove>();
            if (bodyMove != null)
                bodyMove.enabled = true;
        }

        player.currentState = PLAYERSTATE.BODY;

        // Cooldown period
        yield return new WaitForSeconds(cooldownTime);
        isCoolingDown = false;
    }
}
