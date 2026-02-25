using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Global manager that tracks all collectibles in the scene.
// Provides public interfaces to check completion status.
// Fires events when collectibles are collected or all collected.
public class CollectibleManager : MonoBehaviour
{
    private static CollectibleManager _instance;
    
    private static bool applicationQuitting = false;

    public static CollectibleManager Instance
    {
        get
        {
            if (_instance == null && !applicationQuitting)
            {
                _instance = FindObjectOfType<CollectibleManager>();

                if (_instance == null)
                {
                    GameObject go = new GameObject("CollectibleManager");
                    _instance = go.AddComponent<CollectibleManager>();
                }
            }

            return _instance;
        }
    }

    public static bool IsInitialized => _instance != null;

    // List of all registered collectibles
    private readonly List<Collectible> registered = new List<Collectible>();

    // Set of collected collectibles
    private readonly HashSet<Collectible> collectedSet = new HashSet<Collectible>();

    // Event fired when one collectible is collected
    public UnityEvent<Collectible, GameObject> OnCollected = new UnityEvent<Collectible, GameObject>();

    // Event fired when all collectibles are collected
    public UnityEvent OnAllCollected = new UnityEvent();

    private bool allCollectedFired = false;

    public int TotalCount => registered.Count;
    public int CollectedCount => collectedSet.Count;

    private void OnApplicationQuit()
    {
        applicationQuitting = true;
    }
    
    // Register a collectible to the manager.
    // Called automatically by Collectible.
    public void RegisterCollectible(Collectible c)
    {
        if (c == null) return;

        if (!registered.Contains(c))
        {
            registered.Add(c);
        }
    }
    
    
    // Called when a collectible is picked up.
    public void NotifyCollected(Collectible c, GameObject collector)
    {
        if (c == null) return;

        if (!collectedSet.Contains(c))
        {
            collectedSet.Add(c);
            OnCollected?.Invoke(c, collector);
        }

        CheckAllCollected();
    }
    
    // Checks if all collectibles are collected.
    // Fires OnAllCollected once.
    private void CheckAllCollected()
    {
        if (registered.Count == 0) return;

        if (!allCollectedFired && collectedSet.Count >= registered.Count)
        {
            allCollectedFired = true;
            OnAllCollected?.Invoke();
            Debug.Log("All collectibles collected.");
        }
    }
    
    // Public interface to check completion state.
    public bool IsAllCollected()
    {
        if (registered.Count == 0) return false;
        return collectedSet.Count >= registered.Count;
    }
    
    // Reset collection progress.
    // Useful for restarting level.
    public void ResetAll()
    {
        collectedSet.Clear();
        allCollectedFired = false;
    }
}