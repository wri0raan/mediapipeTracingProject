using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class FixedInternalResolution : MonoBehaviour
{
    [Header("Links")]
    public Camera gameCamera;
    public RawImage backbufferView;

    [Header("Internal Resolution (px)")]
    public int width = 720;
    public int height = 1280;

    [Header("Clear Color")]
    public Color letterboxColor = new Color(0.06f, 0.06f, 0.06f, 1f);

    RenderTexture rt;
    Vector2Int lastScreen;

    void OnEnable() { RebuildRT(); ApplyFit(); }
    void OnDisable() { ReleaseRT(); }

    void Update()
    {
        if (rt == null || rt.width != width || rt.height != height)
            RebuildRT();

        var cur = new Vector2Int(Screen.width, Screen.height);
        if (cur != lastScreen)
            ApplyFit();
    }

    void ApplyFit()
    {
        lastScreen = new Vector2Int(Screen.width, Screen.height);
        if (!backbufferView) return;

        var screen = (Vector2)lastScreen;
        var target = new Vector2(width, height);
        float s = Mathf.Min(screen.x / target.x, screen.y / target.y);
        var sz = target * s;

        var rtRect = backbufferView.rectTransform;

        rtRect.anchorMin = new Vector2(0.5f, 0.5f);
        rtRect.anchorMax = new Vector2(0.5f, 0.5f);
        rtRect.pivot = new Vector2(0.5f, 0.5f);
        rtRect.localScale = Vector3.one;

        rtRect.sizeDelta = sz;
        rtRect.anchoredPosition = Vector2.zero;

        backbufferView.color = Color.white;
        if (Camera.main) Camera.main.backgroundColor = letterboxColor;
    }

    void RebuildRT()
    {
        ReleaseRT();
        rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
        {
            useMipMap = false,
            autoGenerateMips = false,
            antiAliasing = 1
        };
        rt.Create();

        if (gameCamera) gameCamera.targetTexture = rt;
        if (backbufferView) backbufferView.texture = rt;
    }

    void ReleaseRT()
    {
        if (gameCamera) gameCamera.targetTexture = null;
        if (backbufferView) backbufferView.texture = null;
        if (rt) { rt.Release(); DestroyImmediate(rt); rt = null; }
    }
}
