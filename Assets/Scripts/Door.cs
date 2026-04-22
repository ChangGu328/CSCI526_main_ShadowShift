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
    private Vector2 doorCenter;
    private Vector2 doorSize;
    private bool boundsInitialized = false;
    private bool doorsOpen = false;
    private Animator animator;

    void Start()
    {
        CacheDoorBounds();
        animator = GetComponent<Animator>();
        Debug.Log("animator=" + animator);
        doorsOpen = false;
        Debug.Log("Start: doorsOpen=" + doorsOpen + " sw.isOn=" + sw.isOn);
        if (animator != null)
            animator.SetBool("IsOpen", false);
    }

    void Update()
    {
        Debug.Log("sw=" + sw + " | sw.isOn=" + (sw != null ? sw.isOn.ToString() : "NULL") + " | doorsOpen=" + doorsOpen);

        if (sw.isOn)
        {
            if (!doorsOpen)
            {
                leftDoor.SetActive(false);
                rightDoor.SetActive(false);
                doorsOpen = true;
                if (animator != null)
                {
                    Debug.Log("Door opening! sw.isOn = " + sw.isOn);
                    animator.SetBool("IsOpen", true);
                }
                else
                {
                    Debug.Log("animator is NULL!");
                }
            }
        }
        else
        {
            if (doorsOpen)
            {
                if (IsPlayerInDoorArea())
                {
                    PushPlayerOut();
                }
                else
                {
                    leftDoor.SetActive(true);
                    rightDoor.SetActive(true);
                    doorsOpen = false;
                    if (animator != null)
                        animator.SetBool("IsOpen", false);
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
                if (!initialized) { bounds = col.bounds; initialized = true; }
                else bounds.Encapsulate(col.bounds);
            }
        }
        if (rightDoor != null)
        {
            foreach (var col in rightDoor.GetComponentsInChildren<Collider2D>())
            {
                if (!initialized) { bounds = col.bounds; initialized = true; }
                else bounds.Encapsulate(col.bounds);
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
            if (results[i].GetComponentInParent<PlayerController>() != null) return true;
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