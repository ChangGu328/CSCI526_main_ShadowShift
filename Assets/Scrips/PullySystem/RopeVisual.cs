// RopeVisualOrthogonal.cs  (fix: use pully visual top; enhanced gizmos + ray debug)
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(LineRenderer))]
public class RopeVisual : MonoBehaviour
{
    [Header("References (Required)")]
    public Transform leftAttach;
    public Transform rightAttach;
    public Transform leftAnchor;
    public Transform rightAnchor;
    public Transform pully;              // optional visual pulley

    [Header("Visual")]
    public float lineWidth = 0.02f;
    public Material lineMaterial;

    [Header("Top Clearance")]
    [Tooltip("Minimum offset above attach points for horizontal segment.")]
    public float topOffset = 0f;

    [Header("Obstacle Avoidance")]
    public bool avoidObstacles = true;
    public LayerMask obstacleMask = ~0;
    public float raiseStep = 0.2f;
    public int maxRaiseAttempts = 10;

    [Header("Pulley Exit")]
    [Tooltip("When set, force the top-right horizontal start to be at the visual right edge of the pulley plus this offset.")]
    public float pullyExitOffset = 0.05f;
    public bool forceExitOnRight = true;

    [Header("Debug")]
    public bool debugDrawPoints = true;
    public float debugPointSize = 0.06f;
    public bool debugDrawRaycasts = true;
    public bool debugDrawPulleyRefs = true;

    private LineRenderer lr;
    private Vector3[] debugPoints = new Vector3[6];

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
        if (lr == null) lr = gameObject.AddComponent<LineRenderer>();

        if (lineMaterial != null) lr.material = lineMaterial;

        lr.useWorldSpace = true;
        lr.numCapVertices = 0;
        lr.positionCount = 6;
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

    private Vector3 GetAnchorWorldCenter(Transform anchor)
    {
        if (anchor == null) return Vector3.zero;
        var sr = anchor.GetComponent<SpriteRenderer>();
        if (sr != null) return sr.bounds.center;
        var mr = anchor.GetComponent<Renderer>();
        if (mr != null) return mr.bounds.center;
        return anchor.position;
    }

    private float GetRendererHalfWidth(Transform t)
    {
        if (t == null) return 0f;
        var sr = t.GetComponent<SpriteRenderer>();
        if (sr != null) return sr.bounds.extents.x;
        var mr = t.GetComponent<Renderer>();
        if (mr != null) return mr.bounds.extents.x;
        return 0f;
    }

    private float GetRendererHalfHeight(Transform t)
    {
        if (t == null) return 0f;
        var sr = t.GetComponent<SpriteRenderer>();
        if (sr != null) return sr.bounds.extents.y;
        var mr = t.GetComponent<Renderer>();
        if (mr != null) return mr.bounds.extents.y;
        return 0f;
    }

    void Update()
    {
        if (leftAttach == null || rightAttach == null || leftAnchor == null || rightAnchor == null)
            return;

        // compute anchor visual centers
        Vector3 leftAnchorCenter = GetAnchorWorldCenter(leftAnchor);
        Vector3 rightAnchorCenter = GetAnchorWorldCenter(rightAnchor);

        // Start topY as max anchor center y + offset
        float topY = Mathf.Max(leftAnchorCenter.y, rightAnchorCenter.y) + topOffset;
        Debug.Log(topY);
        
        // If pully exists, ensure topY is at least at the pully's visual TOP edge (not pully.position.y)
        if (pully != null)
        {
            Vector3 pullyCenter = GetAnchorWorldCenter(pully);
            float pullyHalfHeight = GetRendererHalfHeight(pully);
            float pullyTopY = pullyCenter.y + pullyHalfHeight;
            topY = Mathf.Max(topY, pullyTopY);
            
        }

        // anchor X positions (visual center X)
        float topLeftAnchorX = leftAnchorCenter.x;
        float topRightAnchorX = rightAnchorCenter.x;

        // If we want rope to visibly exit the pully to the right, compute pully right edge
        if (pully != null && forceExitOnRight)
        {
            Vector3 pullyCenter = GetAnchorWorldCenter(pully);
            float pullyHalfWidth = GetRendererHalfWidth(pully);
            float pullyRightEdgeX = pullyCenter.x + pullyHalfWidth + pullyExitOffset;
            topRightAnchorX = pullyRightEdgeX;
        }

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
        float topZ = (leftAnchorCenter.z + rightAnchorCenter.z) * 0.5f;

        Vector3 p0 = leftAttach.position;
        Vector3 p1 = new Vector3(attachLeftTopX, topY, p0.z);
        Vector3 p1a = new Vector3(topLeftAnchorX, topY, topZ);
        Vector3 p2a = new Vector3(topRightAnchorX, topY, topZ);
        Vector3 p2 = new Vector3(attachRightTopX, topY, rightAttach.position.z);
        Vector3 p3 = rightAttach.position;

        debugPoints[0] = p0;
        debugPoints[1] = p1;
        debugPoints[2] = p1a;
        debugPoints[3] = p2a;
        debugPoints[4] = p2;
        debugPoints[5] = p3;

        for (int i = 0; i < 6; i++)
        {
            lr.SetPosition(i, debugPoints[i]);
        }
    }

    void OnDrawGizmos()
    {
        if (!debugDrawPoints) return;
        if (debugPoints == null || debugPoints.Length != 6) return;

        // Draw the 6 points
        Gizmos.color = Color.red;
        for (int i = 0; i < debugPoints.Length; i++)
        {
            Gizmos.DrawSphere(debugPoints[i], debugPointSize);
        }

        // Connect them
        Gizmos.color = Color.yellow;
        for (int i = 0; i < debugPoints.Length - 1; i++)
        {
            Gizmos.DrawLine(debugPoints[i], debugPoints[i + 1]);
        }

        // Draw labels P0..P5 in Editor
#if UNITY_EDITOR
        for (int i = 0; i < debugPoints.Length; i++)
        {
            Handles.Label(debugPoints[i] + Vector3.up * (debugPointSize * 0.6f), "P" + i);
        }
#endif

        // Draw pulley reference lines: center and top edge
        if (debugDrawPulleyRefs && pully != null)
        {
            Vector3 pullyCenter = GetAnchorWorldCenter(pully);
            float halfW = GetRendererHalfWidth(pully);
            float halfH = GetRendererHalfHeight(pully);
            Vector3 topEdge = new Vector3(pullyCenter.x, pullyCenter.y + halfH, pullyCenter.z);
            Vector3 rightEdge = new Vector3(pullyCenter.x + halfW, pullyCenter.y, pullyCenter.z);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(pullyCenter, debugPointSize * 0.6f);
            Gizmos.DrawLine(pullyCenter, topEdge);
            Gizmos.DrawLine(pullyCenter, rightEdge);
            // mark top and right edge small spheres
            Gizmos.DrawSphere(topEdge, debugPointSize * 0.4f);
            Gizmos.DrawSphere(rightEdge, debugPointSize * 0.4f);

#if UNITY_EDITOR
            Handles.Label(topEdge + Vector3.up * 0.05f, "PulleyTopY: " + topEdge.y.ToString("F2"));
            Handles.Label(rightEdge + Vector3.right * 0.05f, "PulleyRightX: " + rightEdge.x.ToString("F2"));
#endif
        }

        // Optionally draw the raycast lines that were tested (approx)
        if (debugDrawRaycasts)
        {
            Gizmos.color = Color.magenta;
            // main horizontal
            Gizmos.DrawLine(debugPoints[2], debugPoints[3]);
            // left sub
            Gizmos.DrawLine(new Vector3(debugPoints[1].x, debugPoints[1].y, debugPoints[1].z),
                            new Vector3(debugPoints[2].x, debugPoints[2].y, debugPoints[2].z));
            // right sub
            Gizmos.DrawLine(new Vector3(debugPoints[3].x, debugPoints[3].y, debugPoints[3].z),
                            new Vector3(debugPoints[4].x, debugPoints[4].y, debugPoints[4].z));
        }
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