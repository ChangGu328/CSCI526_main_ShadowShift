// RopeVisualOrthogonal.cs  (robust anchor-center handling)
using UnityEngine;

/// <summary>
/// Orthogonal rope that respects anchor visual center (Sprite bounds) even when attach.x != anchor.x.
/// Draws a 6-point polyline:
/// leftAttach -> up to (leftAttach.x, topY) -> shift to (leftAnchorCenter.x, topY)
/// -> across to (rightAnchorCenter.x, topY) -> shift to (rightAttach.x, topY) -> down to rightAttach.
/// Requires leftAttach, rightAttach, leftAnchor, rightAnchor assigned in Inspector.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class RopeVisual : MonoBehaviour
{
    [Header("References (Required)")]
    public Transform leftAttach;
    public Transform rightAttach;
    public Transform leftAnchor;
    public Transform rightAnchor;
    public Transform pully;

    [Header("Visual")]
    public float lineWidth = 0.02f;
    public Material lineMaterial;

    [Header("Top Clearance")]
    [Tooltip("Minimum offset above attach points for horizontal segment.")]
    public float topOffset = 1.0f;

    [Header("Obstacle Avoidance")]
    public bool avoidObstacles = true;
    public LayerMask obstacleMask = ~0;
    public float raiseStep = 0.2f;
    public int maxRaiseAttempts = 10;

    private LineRenderer lr;

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
        if (lr == null) lr = gameObject.AddComponent<LineRenderer>();

        if (lineMaterial != null) lr.material = lineMaterial;
        
        lr.useWorldSpace = true;
        lr.numCapVertices = 0;
        lr.positionCount = 6; // six points for improved anchor alignment
        SetLineWidth(lineWidth);
    }

    void OnValidate()
    {
        if (lr == null) lr = GetComponent<LineRenderer>();
        if (lr != null)
        {
            SetLineWidth(lineWidth);
            lr.positionCount = 6;
        }
    }

    private void SetLineWidth(float w)
    {
        if (lr == null) return;
        lr.startWidth = w;
        lr.endWidth = w;
    }

    // Helper: get the visual center (world) of an anchor.
    // If a SpriteRenderer exists, use its bounds.center (visual center).
    // Otherwise, fallback to transform.position.
    private Vector3 GetAnchorWorldCenter(Transform anchor)
    {
        if (anchor == null) return Vector3.zero;
        var sr = anchor.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            return sr.bounds.center;
        }

        // also support other renderers (MeshRenderer)
        var mr = anchor.GetComponent<Renderer>();
        if (mr != null)
        {
            return mr.bounds.center;
        }

        return anchor.position;
    }

    void Update()
    {
        if (leftAttach == null || rightAttach == null || leftAnchor == null || rightAnchor == null)
            return;

        // compute anchor visual centers
        Vector3 leftAnchorCenter = GetAnchorWorldCenter(leftAnchor);
        Vector3 rightAnchorCenter = GetAnchorWorldCenter(rightAnchor);
        
        float topY = Mathf.Max(leftAnchorCenter.y, rightAnchorCenter.y) + topOffset;
        if (pully != null)
        {
            topY = Mathf.Max(topY, pully.position.y);
        }

        // anchor X positions (visual center X)
        float topLeftAnchorX = leftAnchorCenter.x;
        float topRightAnchorX = rightAnchorCenter.x;

        // attach top X (vertical from attach meets topY)
        float attachLeftTopX = leftAttach.position.x;
        float attachRightTopX = rightAttach.position.x;

        // obstacle avoidance: check left-sub, main, right-sub horizontal spans
        if (avoidObstacles)
        {
            int attempts = 0;
            while (attempts < maxRaiseAttempts)
            {
                bool blocked = false;

                // main top span (anchor center to anchor center)
                Vector2 mainStart = new Vector2(topLeftAnchorX, topY);
                Vector2 mainEnd = new Vector2(topRightAnchorX, topY);
                float mainDist = Vector2.Distance(mainStart, mainEnd);
                if (mainDist > 0.0001f)
                {
                    RaycastHit2D hitMain = Physics2D.Raycast(mainStart, (mainEnd - mainStart).normalized, mainDist, obstacleMask);
                    if (hitMain.collider != null) blocked = true;
                }

                // left sub-segment from attachTop to leftAnchorCenter top
                Vector2 leftSubStart = new Vector2(attachLeftTopX, topY);
                Vector2 leftSubEnd = new Vector2(topLeftAnchorX, topY);
                float leftSubDist = Mathf.Abs(leftSubEnd.x - leftSubStart.x);
                if (!blocked && leftSubDist > 0.0001f)
                {
                    RaycastHit2D hitLeft = Physics2D.Raycast(leftSubStart, (leftSubEnd - leftSubStart).normalized, leftSubDist, obstacleMask);
                    if (hitLeft.collider != null) blocked = true;
                }

                // right sub-segment from rightAnchorCenter top to attachTop
                Vector2 rightSubStart = new Vector2(topRightAnchorX, topY);
                Vector2 rightSubEnd = new Vector2(attachRightTopX, topY);
                float rightSubDist = Mathf.Abs(rightSubEnd.x - rightSubStart.x);
                if (!blocked && rightSubDist > 0.0001f)
                {
                    RaycastHit2D hitRight = Physics2D.Raycast(rightSubStart, (rightSubEnd - rightSubStart).normalized, rightSubDist, obstacleMask);
                    if (hitRight.collider != null) blocked = true;
                }

                if (!blocked) break;

                topY += raiseStep;
                attempts++;
            }
        }

        // Build 6 points using anchor visual centers for the top horizontal between anchors.
        // Use anchorCenter.z for the top points z to avoid parallax with pulley sprite.
        float topZ = (leftAnchorCenter.z + rightAnchorCenter.z) * 0.5f;

        Vector3 p0 = leftAttach.position;
        Vector3 p1 = new Vector3(attachLeftTopX, topY, p0.z);
        Vector3 p1a = new Vector3(topLeftAnchorX, topY, topZ);
        Vector3 p2a = new Vector3(topRightAnchorX, topY, topZ);
        Vector3 p2 = new Vector3(attachRightTopX, topY, rightAttach.position.z);
        Vector3 p3 = rightAttach.position;

        lr.SetPosition(0, p0);
        lr.SetPosition(1, p1);
        lr.SetPosition(2, p1a);
        lr.SetPosition(3, p2a);
        lr.SetPosition(4, p2);
        lr.SetPosition(5, p3);
    }

    /// <summary>
    /// Runtime API to change width.
    /// </summary>
    public void SetWidth(float w)
    {
        lineWidth = w;
        SetLineWidth(w);
    }
}
