using UnityEngine;

public class DoorOpen : MonoBehaviour
{
    public GameObject leftDoor;
    public GameObject rightDoor;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 检查是否碰到的是玩家（检测父物体是否有 PlayerController）
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
