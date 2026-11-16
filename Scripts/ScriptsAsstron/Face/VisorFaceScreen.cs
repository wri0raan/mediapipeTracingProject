using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class VisorFaceScreen : MonoBehaviour
{
    [Header("Source (Mediapipe imageSource)")]
    public ImageSourceTextureExposer source;

    [Header("Material & Texture Slot")]
    public string textureProperty = "_BaseMap";

    [Header("Mirror & Crop (UV 0..1)")]
    public bool mirrorX = true;
    [Range(0, 1)] public float cropX = 0.25f;
    [Range(0, 1)] public float cropY = 0.25f;
    [Range(0.05f, 1)] public float cropW = 0.5f;
    [Range(0.05f, 1)] public float cropH = 0.5f;

    [Header("Round Mask (shader optional)")]
    public Vector2 maskCenter = new Vector2(0.5f, 0.5f);
    public Vector2 maskRadii = new Vector2(0.45f, 0.45f);
    [Range(0, 0.2f)] public float maskFeather = 0.05f;

    [Header("Debug")]
    public bool showDebug = false;

    Material mat;

    void Start()
    {

        mat = GetComponent<MeshRenderer>().material;
        if (!mat)
            Debug.LogError("[VisorFaceScreen] Material not found on MeshRenderer.");
    }

    void LateUpdate()
    {
        if (!source || !mat || source.SourceTexture == null) return;

        if (mat.GetTexture(textureProperty) != source.SourceTexture)
            mat.SetTexture(textureProperty, source.SourceTexture);

        if (mat.HasProperty("_CropX"))
        {
            mat.SetFloat("_CropX", Mathf.Clamp01(cropX));
            mat.SetFloat("_CropY", Mathf.Clamp01(cropY));
            mat.SetFloat("_CropW", Mathf.Clamp01(cropW));
            mat.SetFloat("_CropH", Mathf.Clamp01(cropH));
            mat.SetFloat("_MirrorX", mirrorX ? 1f : 0f);
        }
        else
        {

            if (showDebug)
                Debug.LogWarning("[VisorFaceScreen] Shader does not have _CropX/_CropY/_CropW/_CropH properties.");
        }

        if (mat.HasProperty("_MaskCenter")) mat.SetVector("_MaskCenter", maskCenter);
        if (mat.HasProperty("_MaskRadii")) mat.SetVector("_MaskRadii", maskRadii);
        if (mat.HasProperty("_Feather")) mat.SetFloat("_Feather", maskFeather);

        if (showDebug)
        {
            Debug.Log($"[VisorFaceScreen] Crop=({cropX:F2},{cropY:F2},{cropW:F2},{cropH:F2}) Mirror={mirrorX}");
        }

        transform.localRotation = Quaternion.identity;
    }
}
