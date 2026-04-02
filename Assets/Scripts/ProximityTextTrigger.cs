using UnityEngine;
using TMPro;

public class ProximityTextTrigger : MonoBehaviour
{
    [Header("TMP")]
    public TMP_Text targetText;
    
    [Header("Distance")]
    public float triggerRadius = 3f;

    private Transform playerBody;
    private Transform playerSoul;
    private PlayerController playerController;

    private void Start()
    {
        if (targetText != null)
            targetText.gameObject.SetActive(false);

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            playerController = player.GetComponent<PlayerController>();
            playerBody = player.transform.Find("Player_Body");
            playerSoul = player.transform.Find("Player_Soul");
        }
    }

    private void Update()
    {
        if (targetText == null || playerController == null) return;

        Transform activeTransform = playerController.currentState == PLAYERSTATE.BODY
            ? playerBody 
            : playerSoul;

        if (activeTransform == null) return;

        float dist = Vector2.Distance(transform.position, activeTransform.position);
        targetText.gameObject.SetActive(dist <= triggerRadius);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, triggerRadius);
    }
}