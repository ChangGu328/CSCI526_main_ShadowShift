using UnityEngine;

public class WallTrigger : MonoBehaviour
{
    private PlayerController playerController;
    private PlayerMove playerMove;

    void OnTriggerEnter2D(Collider2D other)
    {
        PlayerMove pm = other.GetComponent<PlayerMove>();
        if (pm != null)
        {
            playerMove = pm;
            playerMove.canMove = false;
            playerMove.Stop();

            playerController = other.transform.parent.GetComponent<PlayerController>();
            if (playerController != null)
                playerController.wallBlocking = true;
        }
    }

    void Update()
    {
        if (playerController != null && playerController.wallBlocking == false && playerMove != null)
        {
            playerMove.canMove = true;
            transform.parent.gameObject.SetActive(false);
            playerMove = null;
            playerController = null;
        }
    }
}