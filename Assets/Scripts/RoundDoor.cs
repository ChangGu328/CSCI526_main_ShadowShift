using UnityEngine;

public class RoundDoor : MonoBehaviour
{
    public Switch sw;          
    public GameObject inner;   

    void Update()
    {
        if (sw == null || inner == null) return;

        
        inner.SetActive(!sw.isOn);
    }
}