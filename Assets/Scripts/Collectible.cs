using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider2D))]
public class Collectible : MonoBehaviour
{
    
    private void Start()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
            col.isTrigger = true;
        
        // Register to manager
        CollectibleManager.Instance?.RegisterCollectible(this);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Check if collided with player
        if (other.CompareTag("Player") || 
            other.GetComponentInParent<PlayerController>() != null)
        {
            Collect(other.gameObject);
        }
    }
    
    // Called when this collectible is collected.
    // Notifies manager and triggers local events.
    public void Collect(GameObject collector)
    {
        CollectibleManager.Instance?.NotifyCollected(this, collector);
        
        Destroy(gameObject);
    }
    
}