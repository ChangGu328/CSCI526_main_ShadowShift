// BoxWeightDetector.cs
using System.Collections.Generic;
using UnityEngine;

// BoxWeightDetector:
// - Tracks rigidbodies that are currently overlapping the detector (using a HashSet to avoid double-counting).
// - Updates CurrentMass as the sum of tracked rigidbody masses.
// - Exposes TotalMass = platformMass + CurrentMass for convenience.
[RequireComponent(typeof(Collider2D))]
public class BoxWeightDetector : MonoBehaviour
{
    [Tooltip("Mass threshold to consider the platform loaded.")]
    public float massThreshold = 0.1f;

    // Use a HashSet to track unique rigidbodies overlapping the detector
    private readonly HashSet<Rigidbody2D> tracked = new HashSet<Rigidbody2D>();

    // cached current mass sum of tracked rigidbodies (not including platform)
    private float currentMass = 0f;

    // Platform's own rigidbody (optional)
    private Rigidbody2D platformRb;

    private void Awake()
    {
        // try to find a Rigidbody2D on the same GameObject (platform) - may be null if detector is child
        platformRb = GetComponent<Rigidbody2D>();
    }

    // Sum of masses of objects currently on the detector (excludes platform self).
    public float CurrentMass => currentMass;

    // Total mass including the platform's own Rigidbody2D.mass (if present).
    public float TotalMass => (platformRb != null ? platformRb.mass : 0f) + currentMass;

    public bool IsLoaded => CurrentMass >= massThreshold;

    private void AddBody(Rigidbody2D rb)
    {
        if (rb == null) return;
        if (tracked.Add(rb))
        {
            currentMass += Mathf.Max(0f, rb.mass);
        }
    }

    private void RemoveBody(Rigidbody2D rb)
    {
        if (rb == null) return;
        if (tracked.Remove(rb))
        {
            currentMass -= Mathf.Max(0f, rb.mass);
            if (currentMass < 0f) currentMass = 0f;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // attach to the rigidbody of the other collider
        Rigidbody2D rb = other.attachedRigidbody;
        AddBody(rb);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        Rigidbody2D rb = other.attachedRigidbody;
        RemoveBody(rb);
    }
    
}