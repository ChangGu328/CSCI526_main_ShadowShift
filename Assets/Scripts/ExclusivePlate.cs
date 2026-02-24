using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class ExclusivePlate : MonoBehaviour
{
    public enum Required { Body, Soul }

    public Required required = Required.Body;

    public string bodyLayerName = "Player_Body";
    public string soulLayerName = "Player_Soul";

    public bool IsPressed { get; private set; }

    private int count = 0;

    private Vector2 originLocalScale;
    private Vector3 originPosition;

    private void Start()
    {
        originLocalScale = transform.localScale;
        originPosition = transform.position;
    }

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private bool Match(Collider2D other)
    {
        int bodyLayer = LayerMask.NameToLayer(bodyLayerName);
        int soulLayer = LayerMask.NameToLayer(soulLayerName);

        if (required == Required.Body) return other.gameObject.layer == bodyLayer;
        if (required == Required.Soul) return other.gameObject.layer == soulLayer;

        return false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!Match(other)) return;

        count++;
        IsPressed = count > 0;
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!Match(other)) return;

        count = Mathf.Max(0, count - 1);
        IsPressed = count > 0;
    }

    private void Update()
    {
        if (IsPressed)
            ApplyScale(0.3f);
        else
            ApplyScale(1f);
    }

    void ApplyScale(float yScale)
    {
        transform.localScale = new Vector2(originLocalScale.x, originLocalScale.y * yScale);

        float deltaY = (originLocalScale.y - transform.localScale.y) * 0.5f;
        transform.position = originPosition - new Vector3(0, deltaY, 0);
    }
}
