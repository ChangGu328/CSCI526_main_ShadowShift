using UnityEngine;

public class DoorOpen : MonoBehaviour
{
    public GameObject leftDoor;
    public GameObject rightDoor;

    private Collider2D doorZone;

    private void Start()
    {
        // Automatically retrieves the triggers on this object for use as door area detectors.
        doorZone = GetComponent<Collider2D>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player != null)
        {
            Debug.Log("[DoorOpen] Player Entered");
            if (leftDoor != null)
                leftDoor.SetActive(false);
            if (rightDoor != null)
                rightDoor.SetActive(false);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player != null)
        {
            Debug.Log("[DoorOpen] Player Exited");

            // Check if there are any players remaining in the door area before closing the door.
            if (!IsPlayerInDoorZone())
            {
                if (leftDoor != null)
                    leftDoor.SetActive(true);
                if (rightDoor != null)
                    rightDoor.SetActive(true);
            }
        }
    }

    private bool IsPlayerInDoorZone()
    {
        if (doorZone == null) return false;

        Collider2D[] results = new Collider2D[8];
        ContactFilter2D filter = new ContactFilter2D();
        filter.useTriggers = true;
        filter.NoFilter();

        int count = doorZone.Overlap(filter, results);
        for (int i = 0; i < count; i++)
        {
            if (results[i] == null) continue;
            if (results[i].GetComponentInParent<PlayerController>() != null)
                return true;
        }

        return false;
    }
}