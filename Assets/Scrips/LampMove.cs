using UnityEngine;

public class LampMove : MonoBehaviour
{
    [Header("Move Settings")]
    public float moveDistance = 1.5f;
    public float moveSpeed = 2f;

    private Vector3 startPosition;

    private void Start()
    {
        startPosition = transform.position;
    }

    private void Update()
    {
        // PingPong returns a value that loops between 0 and moveDistance.
        float xOffset = Mathf.PingPong(Time.time * moveSpeed, moveDistance);
        transform.position = startPosition + new Vector3(xOffset, 0f, 0f);
    }
}
