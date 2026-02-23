using UnityEngine;

public class DoorOpen : MonoBehaviour
{
    public GameObject leftDoor;
    public GameObject rightDoor;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Check if the object encountered is a player (check if the parent object has a PlayerController).
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

            if (leftDoor != null)
                leftDoor.SetActive(true);

            if (rightDoor != null)
                rightDoor.SetActive(true);
        }
    }
}
