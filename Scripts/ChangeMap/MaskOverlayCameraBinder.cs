using UnityEngine;

[RequireComponent(typeof(Renderer))]
public class MaskOverlayCameraBinder : MonoBehaviour
{
    [Tooltip("카메라 영상 텍스처(WebCamSource나 ImageSource의 출력 텍스처)")]
    public Texture cameraTexture;

    Material _mat;

    void Awake()
    {
        var r = GetComponent<Renderer>();
        _mat = r ? r.material : null;
    }

    void Update()
    {
        if (_mat == null) return;
        if (cameraTexture != null)
            _mat.SetTexture("_CamTex", cameraTexture);
    }
}
