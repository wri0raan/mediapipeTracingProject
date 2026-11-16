using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
[RequireComponent(typeof(Image))]
public class CameraViewFlipButton : MonoBehaviour
{
    [Header("Flip 대상 (카메라/배경이 붙어있는 오브젝트)")]
    [SerializeField] private Transform target;

    [Header("아이콘(선택)")]
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite flippedSprite;

    private UnityEngine.UI.Button _button;
    private UnityEngine.UI.Image _image;

    private Vector3 _originalScale;
    private bool _isFlipped = false;

    private void Awake()
    {
        _button = GetComponent<UnityEngine.UI.Button>();
        _image = GetComponent<UnityEngine.UI.Image>();

        if (target != null)
        {
            _originalScale = target.localScale;
        }
        else
        {
            Debug.LogWarning("[CameraViewFlipButton] target이 비어 있습니다. Inspector에서 Transform을 지정해주세요.");
        }

        _button.onClick.AddListener(OnClick);
        RefreshVisual();
    }

    private void OnEnable()
    {
        RefreshVisual();
    }

    private void OnClick()
    {
        _isFlipped = !_isFlipped;

        if (target != null)
        {
            var s = _originalScale;

            if (_isFlipped)
                s.x = -Mathf.Abs(s.x);
            else
                s.x = Mathf.Abs(s.x);

            target.localScale = s;
        }

        RefreshVisual();
    }

    private void RefreshVisual()
    {
        if (_image == null) return;

        if (_isFlipped)
        {
            if (flippedSprite != null)
                _image.sprite = flippedSprite;
        }
        else
        {
            if (normalSprite != null)
                _image.sprite = normalSprite;
        }
    }
}
