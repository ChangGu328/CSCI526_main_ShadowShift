using UnityEngine;

public class Door : MonoBehaviour
{
    public Switch sw;
    public GameObject leftDoor;
    public GameObject rightDoor;

    [Header("Push Settings")]
    [Tooltip("The horizontal force applied to the player when they are pushed out of the door area.")]
    public float pushForce = 12f;
    [Tooltip("The vertical force applied to the player when they are pushed out of the door area.")]
    public float pushUpForce = 3f;

    // Buffer Gate Region
    private Vector2 doorCenter;
    private Vector2 doorSize;
    private bool boundsInitialized = false;

    private bool doorsOpen = false;
    private bool waitingForPlayerToLeave = false;

    void Start()
    {
        CacheDoorBounds();
        // Initial State: The door is closed.
        doorsOpen = false;
    }

    void Update()
    {
        if (sw.isOn)
        {
            // Turn on switch Open door
            if (!doorsOpen)
            {
                leftDoor.SetActive(false);
                rightDoor.SetActive(false);
                doorsOpen = true;
                waitingForPlayerToLeave = false;
            }
        }
        else
        {
            // Switch Off
            if (doorsOpen)
            {
                // Detect whether the player is inside the door.
                if (IsPlayerInDoorArea())
                {
                    // Player is inside the door  Push the player; the door remains open.
                    PushPlayerOut();
                    waitingForPlayerToLeave = true;
                    // Do not close the door; wait until the next frame to check again.
                }
                else
                {
                    // The player is not inside the door (or has already been pushed out) Close the door.
                    leftDoor.SetActive(true);
                    rightDoor.SetActive(true);
                    doorsOpen = false;
                    waitingForPlayerToLeave = false;
                }
            }
        }
    }

    private void CacheDoorBounds()
    {
        Bounds bounds = new Bounds();
        bool initialized = false;

        if (leftDoor != null)
        {
            foreach (var col in leftDoor.GetComponentsInChildren<Collider2D>())
            {
                if (!initialized)
                {
                    bounds = col.bounds;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(col.bounds);
                }
            }
        }

        if (rightDoor != null)
        {
            foreach (var col in rightDoor.GetComponentsInChildren<Collider2D>())
            {
                if (!initialized)
                {
                    bounds = col.bounds;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(col.bounds);
                }
            }
        }

        if (initialized)
        {
            doorCenter = new Vector2(bounds.center.x, bounds.center.y);
            doorSize = new Vector2(bounds.size.x + 0.1f, bounds.size.y + 0.1f);
            boundsInitialized = true;
        }
    }

    private bool IsPlayerInDoorArea()
    {
        if (!boundsInitialized) return false;

        Collider2D[] results = Physics2D.OverlapBoxAll(doorCenter, doorSize, 0f);

        for (int i = 0; i < results.Length; i++)
        {
            if (results[i] == null) continue;
            if (results[i].GetComponentInParent<PlayerController>() != null)
                return true;
        }

        return false;
    }

    private void PushPlayerOut()
    {
        if (!boundsInitialized) return;

        Collider2D[] results = Physics2D.OverlapBoxAll(doorCenter, doorSize, 0f);

        for (int i = 0; i < results.Length; i++)
        {
            if (results[i] == null) continue;

            PlayerController player = results[i].GetComponentInParent<PlayerController>();
            if (player == null) continue;

            Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
            if (rb == null) rb = player.GetComponentInChildren<Rigidbody2D>();
            if (rb == null) continue;

            float facingDir = player.transform.localScale.x >= 0 ? 1f : -1f;

            rb.linearVelocity = new Vector2(facingDir * pushForce, pushUpForce);
        }
    }
}