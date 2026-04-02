using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class ControlsReference : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public GameObject controlsPanel;

    private void Start()
    {
        if (controlsPanel != null)
            controlsPanel.SetActive(false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (controlsPanel != null)
            controlsPanel.SetActive(true);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (controlsPanel != null)
            controlsPanel.SetActive(false);
    }
}