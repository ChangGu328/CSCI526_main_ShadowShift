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
    

    public bool hideSoul = true;

    private bool isCoolingDown;

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

        StartCoroutine(ExtinguishRoutine(player));
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