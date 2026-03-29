// RopeVisual.cs — Optimized: LateUpdate, cached raycasts, editor preview
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways] // The rope is visible even in Edit Mode.
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
    public float topOffset = 0f;

    [Header("Obstacle Avoidance")]
    public bool avoidObstacles = true;
    public LayerMask obstacleMask = ~0;
    public float raiseStep = 0.2f;
    public int maxRaiseAttempts = 10;
    [Tooltip("How often (in frames) to perform obstacle detection (higher values ​​save performance but result in slower response times).")]
    public int obstacleCheckInterval = 3;

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

    // Obstacle Detection Cache
    private float cachedObstacleTopY = 0f;
    private int frameCounter = 0;
    private bool hasCachedValue = false;

    void Awake()
    {
        InitLineRenderer();
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

    private void InitLineRenderer()
    {
        lr = GetComponent<LineRenderer>();
        if (lr == null) lr = gameObject.AddComponent<LineRenderer>();

        if (lineMaterial != null) lr.material = lineMaterial;

        lr.useWorldSpace = true;
        lr.numCapVertices = 0;
        lr.positionCount = 6;
        SetLineWidth(lineWidth);
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

    void LateUpdate()
    {
        // Ensure the LineRenderer is initialized in Edit Mode.
        if (lr == null) InitLineRenderer();

        if (leftAttach == null || rightAttach == null || leftAnchor == null || rightAnchor == null)
            return;

        // Calculate the Visual Center of the Anchor Point
        Vector3 leftAnchorCenter = GetAnchorWorldCenter(leftAnchor);
        Vector3 rightAnchorCenter = GetAnchorWorldCenter(rightAnchor);

        float topY = Mathf.Max(leftAnchorCenter.y, rightAnchorCenter.y) + topOffset;

        // Pulley Top Constraint
        if (pully != null)
        {
            Vector3 pullyCenter = GetAnchorWorldCenter(pully);
            float pullyHalfHeight = GetRendererHalfHeight(pully);
            float pullyTopY = pullyCenter.y + pullyHalfHeight;
            topY = Mathf.Max(topY, pullyTopY);
        }


        float topLeftAnchorX = leftAnchorCenter.x;
        float topRightAnchorX = rightAnchorCenter.x;

        // Pulley Exit (Right Side)
        if (pully != null && forceExitOnRight)
        {
            Vector3 pullyCenter = GetAnchorWorldCenter(pully);
            float pullyHalfWidth = GetRendererHalfWidth(pully);
            float pullyRightEdgeX = pullyCenter.x + pullyHalfWidth + pullyExitOffset;
            topRightAnchorX = pullyRightEdgeX;
        }

        float attachLeftTopX = leftAttach.position.x;
        float attachRightTopX = rightAttach.position.x;

        if (avoidObstacles)
        {
            frameCounter++;
            bool shouldCheck = !hasCachedValue || frameCounter >= obstacleCheckInterval;

            #if UNITY_EDITOR
            if (!Application.isPlaying) shouldCheck = true;
            #endif

            if (shouldCheck)
            {
                frameCounter = 0;
                cachedObstacleTopY = ComputeAvoidanceTopY(
                    topY, topLeftAnchorX, topRightAnchorX,
                    attachLeftTopX, attachRightTopX
                );
                hasCachedValue = true;
            }

            topY = Mathf.Max(topY, cachedObstacleTopY);
        }

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

    private float ComputeAvoidanceTopY(
        float baseTopY, float leftAnchorX, float rightAnchorX,
        float leftAttachX, float rightAttachX)
    {
        float topY = baseTopY;
        int attempts = 0;

        while (attempts < maxRaiseAttempts)
        {
            bool blocked = false;

            Vector2 mainStart = new Vector2(leftAnchorX, topY);
            Vector2 mainEnd = new Vector2(rightAnchorX, topY);
            float mainDist = Vector2.Distance(mainStart, mainEnd);
            if (mainDist > 0.0001f)
            {
                RaycastHit2D hit = Physics2D.Raycast(mainStart, (mainEnd - mainStart).normalized, mainDist, obstacleMask);
                if (hit.collider != null) blocked = true;
            }

            if (!blocked)
            {
                float leftSubDist = Mathf.Abs(leftAnchorX - leftAttachX);
                if (leftSubDist > 0.0001f)
                {
                    Vector2 start = new Vector2(leftAttachX, topY);
                    Vector2 end = new Vector2(leftAnchorX, topY);
                    RaycastHit2D hit = Physics2D.Raycast(start, (end - start).normalized, leftSubDist, obstacleMask);
                    if (hit.collider != null) blocked = true;
                }
            }

            if (!blocked)
            {
                float rightSubDist = Mathf.Abs(rightAttachX - rightAnchorX);
                if (rightSubDist > 0.0001f)
                {
                    Vector2 start = new Vector2(rightAnchorX, topY);
                    Vector2 end = new Vector2(rightAttachX, topY);
                    RaycastHit2D hit = Physics2D.Raycast(start, (end - start).normalized, rightSubDist, obstacleMask);
                    if (hit.collider != null) blocked = true;
                }
            }

            if (!blocked) break;

            topY += raiseStep;
            attempts++;
        }

        return topY;
    }

    void OnDrawGizmos()
    {
        if (!debugDrawPoints) return;
        if (debugPoints == null || debugPoints.Length != 6) return;

        Gizmos.color = Color.red;
        for (int i = 0; i < debugPoints.Length; i++)
        {
            Gizmos.DrawSphere(debugPoints[i], debugPointSize);
        }

        Gizmos.color = Color.yellow;
        for (int i = 0; i < debugPoints.Length - 1; i++)
        {
            Gizmos.DrawLine(debugPoints[i], debugPoints[i + 1]);
        }

#if UNITY_EDITOR
        for (int i = 0; i < debugPoints.Length; i++)
        {
            Handles.Label(debugPoints[i] + Vector3.up * (debugPointSize * 0.6f), "P" + i);
        }
#endif

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
            Gizmos.DrawSphere(topEdge, debugPointSize * 0.4f);
            Gizmos.DrawSphere(rightEdge, debugPointSize * 0.4f);

#if UNITY_EDITOR
            Handles.Label(topEdge + Vector3.up * 0.05f, "PulleyTopY: " + topEdge.y.ToString("F2"));
            Handles.Label(rightEdge + Vector3.right * 0.05f, "PulleyRightX: " + rightEdge.x.ToString("F2"));
#endif
        }

        if (debugDrawRaycasts)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(debugPoints[2], debugPoints[3]);
            Gizmos.DrawLine(debugPoints[1], debugPoints[2]);
            Gizmos.DrawLine(debugPoints[3], debugPoints[4]);
        }
    }

    public void SetWidth(float w)
    {
        lineWidth = w;
        SetLineWidth(w);
    }
}