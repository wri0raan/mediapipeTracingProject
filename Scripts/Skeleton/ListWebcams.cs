using UnityEngine;

public class ListWebcams : MonoBehaviour
{
    void Start()
    {
        foreach (var d in WebCamTexture.devices)
            Debug.Log($"[Cam] {d.name}");

    }

    void Update() {  }
}
