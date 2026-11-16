using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
[ExecuteAlways]
public class SegmentationCompositeBinder : MonoBehaviour
{
    public enum FillMode { Stretch, Contain, Cover }

    [Header("References")]
    [Tooltip("MaskOverlayAnnotation 컴포넌트를 드래그")]
    public Component maskAnnotation;

    [Tooltip("비우면 Annotatable Screen RawImage.texture(카메라)를 사용")]
    public Texture cameraTexture;

    [Header("Backgrounds (Desert → Jungle → Mountain → Sea → Space)")]
    public Texture[] backgrounds;
    [SerializeField] private int _bgIndex = 0;

    [Range(0f, 1f)]
    public float threshold = 0.5f;

    [Range(0f, 0.5f)]
    public float feather = 0.05f;

    [Header("Fill Settings")]
    public FillMode fill = FillMode.Cover;
    [Tooltip("Cover/Contain일 때 부모 Rect 밖을 잘라내도록 RectMask2D 자동 추가")]
    public bool ensureRectMaskOnParent = true;

    [Header("Fallback Settings")]
    [Tooltip("카메라 텍스처가 일정 시간 이상 들어오지 않으면 Segmentation 끄고 배경만 보여줌")]
    public bool enableFallbackOnCameraFailure = true;

    [Tooltip("Mediapipe Solution 루트 오브젝트 (실패 시 비활성화하고 싶으면 드래그)")]
    public GameObject solutionRoot;

    [Tooltip("마지막으로 카메라 텍스처가 유효했던 후, 이 시간이 지나면 폴백 발동 (초)")]
    public float cameraTimeoutSeconds = 3f;

    private RawImage _img;
    private Material _mat;
    private RectTransform _rt;

    private bool _fallbackMode = false;
    private float _lastValidCameraTime = -999f;
    private Texture _lastCameraTex = null;

    void Awake()
    {
        _img = GetComponent<RawImage>();
        _rt = _img.rectTransform;

        if (_img.material != null)
        {
            _mat = Application.isPlaying
                ? Instantiate(_img.material)
                : new Material(_img.material);

            _img.material = _mat;
        }
        else
        {
            _mat = _img.material;
        }

        _lastValidCameraTime = -999f;
        ApplyLayout();
        ApplyStaticParams();
    }

    void OnEnable()
    {
        if (_img == null) Awake();
        ApplyLayout();
        ApplyStaticParams();
    }

    void Start()
    {
        ApplyLayout();
        ApplyStaticParams();
    }

    void OnValidate()
    {
        if (_img == null) _img = GetComponent<RawImage>();
        if (_rt == null && _img != null) _rt = _img.rectTransform;
        ApplyLayout();
        ApplyStaticParams();
    }

    void OnRectTransformDimensionsChange()
    {
        ApplyLayout();
    }

    void Update()
    {
        if (_img == null)
            return;

        if (_fallbackMode)
        {
            ApplyFallbackBackground();
            return;
        }

        if (_mat == null)
        {

            ActivateFallback("Material missing");
            return;
        }

        Texture camTex = cameraTexture != null ? cameraTexture : _img.texture;

        if (camTex != null)
        {
            _lastCameraTex = camTex;
            _lastValidCameraTime = Application.isPlaying ? Time.unscaledTime : 0f;
            _mat.SetTexture("_MainTex", camTex);
        }
        else
        {

            if (_lastCameraTex != null)
            {
                _mat.SetTexture("_MainTex", _lastCameraTex);
            }
            else
            {
                _mat.SetTexture("_MainTex", (Texture)null);
            }
        }

        Texture maskTex = GetMaskTexture(maskAnnotation);
        if (maskTex != null)
            _mat.SetTexture("_MaskTex", maskTex);

        Texture bgTex = null;
        if (backgrounds != null && backgrounds.Length > 0)
        {
            _bgIndex = Mathf.Clamp(_bgIndex, 0, backgrounds.Length - 1);
            bgTex = backgrounds[_bgIndex];
        }

        if (bgTex != null)
            _mat.SetTexture("_BgTex", bgTex);

        if (enableFallbackOnCameraFailure && Application.isPlaying)
        {
            bool hasValidCam = (camTex != null) || (_lastCameraTex != null);
            if (!hasValidCam)
            {
                float now = Time.unscaledTime;
                if (_lastValidCameraTime < 0f)
                {

                    _lastValidCameraTime = now;
                }

                if (now - _lastValidCameraTime > cameraTimeoutSeconds)
                {
                    ActivateFallback("Camera texture timeout");
                }
            }
        }
    }

    public void NextBackground()
    {
        if (backgrounds == null || backgrounds.Length == 0)
            return;

        _bgIndex = (_bgIndex + 1) % backgrounds.Length;

        if (_fallbackMode)
        {

            ApplyFallbackBackground();
        }
        else
        {
            if (_mat != null && backgrounds[_bgIndex] != null)
                _mat.SetTexture("_BgTex", backgrounds[_bgIndex]);
        }
    }

    void ActivateFallback(string reason)
    {
        if (_fallbackMode) return;

        _fallbackMode = true;
        Debug.LogWarning($"[SegmentationCompositeBinder] Fallback mode activated: {reason}");

        if (solutionRoot != null)
            solutionRoot.SetActive(false);

        if (maskAnnotation != null)
        {
            var go = maskAnnotation.gameObject;
            if (go != null) go.SetActive(false);
        }

        _img.material = null;
        ApplyFallbackBackground();
    }

    void ApplyFallbackBackground()
    {
        if (_img == null) return;
        if (backgrounds == null || backgrounds.Length == 0) return;

        _bgIndex = Mathf.Clamp(_bgIndex, 0, backgrounds.Length - 1);
        _img.texture = backgrounds[_bgIndex];
    }

    void ApplyStaticParams()
    {
        if (_mat == null) return;
        _mat.SetFloat("_Threshold", threshold);
        _mat.SetFloat("_Feather", feather);
    }

    void ApplyLayout()
    {
        if (_img == null) return;
        if (_rt == null) _rt = _img.rectTransform;

        var parent = _rt.parent as RectTransform;
        if (parent == null) return;

        if (ensureRectMaskOnParent && (fill == FillMode.Cover || fill == FillMode.Contain))
        {
            if (parent.GetComponent<RectMask2D>() == null)
                parent.gameObject.AddComponent<RectMask2D>();
        }

        float pw = parent.rect.width;
        float ph = parent.rect.height;
        if (pw <= 0f || ph <= 0f) return;

        Texture srcTex = null;
        if (backgrounds != null && backgrounds.Length > 0 &&
            _bgIndex >= 0 && _bgIndex < backgrounds.Length)
        {
            srcTex = backgrounds[_bgIndex];
        }
        if (srcTex == null)
        {
            srcTex = _img != null ? _img.texture : null;
        }

        Vector2 srcSize = srcTex != null
            ? new Vector2(srcTex.width, srcTex.height)
            : new Vector2(1920f, 1080f);

        float srcRatio = srcSize.x / srcSize.y;
        float parentRatio = pw / ph;

        Vector2 size;

        switch (fill)
        {
            case FillMode.Stretch:
                size = new Vector2(pw, ph);
                break;

            case FillMode.Contain:
            case FillMode.Cover:
                bool cover = (fill == FillMode.Cover);
                bool useWidth = cover
                    ? (srcRatio <= parentRatio)
                    : (srcRatio >= parentRatio);

                if (useWidth)
                {
                    float w = pw;
                    float h = w / srcRatio;
                    size = new Vector2(w, h);
                }
                else
                {
                    float h = ph;
                    float w = h * srcRatio;
                    size = new Vector2(w, h);
                }
                break;

            default:
                size = new Vector2(pw, ph);
                break;
        }

        _rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
        _rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
        _rt.anchoredPosition = Vector2.zero;
    }

    static Texture GetMaskTexture(Component anno)
    {
        if (anno == null) return null;
        var t = anno.GetType();

        foreach (var pn in new[] { "MaskTexture", "maskTexture" })
        {
            var p = t.GetProperty(pn, BindingFlags.Public | BindingFlags.Instance);
            if (p != null && typeof(Texture).IsAssignableFrom(p.PropertyType))
            {
                var val = p.GetValue(anno) as Texture;
                if (val != null) return val;
            }
        }

        foreach (var fn in new[] { "MaskTexture", "maskTexture", "_maskTexture" })
        {
            var f = t.GetField(fn, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (f != null && typeof(Texture).IsAssignableFrom(f.FieldType))
            {
                var val = f.GetValue(anno) as Texture;
                if (val != null) return val;
            }
        }

        var ri = anno.GetComponent<RawImage>();
        if (ri && ri.material)
        {
            var m = ri.material;
            if (m.HasProperty("_MaskTex")) return m.GetTexture("_MaskTex");
            if (m.HasProperty("_MainTex")) return m.GetTexture("_MainTex");
        }

        var rend = anno.GetComponent<Renderer>();
        if (rend && rend.sharedMaterial)
        {
            var m = rend.sharedMaterial;
            if (m.HasProperty("_MaskTex")) return m.GetTexture("_MaskTex");
            if (m.HasProperty("_MainTex")) return m.GetTexture("_MainTex");
        }

        return null;
    }
}
