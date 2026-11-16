using UnityEngine;
using UnityEngine.UI;

namespace Mediapipe.Unity
{

    [RequireComponent(typeof(UnityEngine.UI.Button))]
    [RequireComponent(typeof(UnityEngine.UI.Image))]
    public class TrackingFlipToggleButton : MonoBehaviour
    {
        [Header("Sprites")]
        [SerializeField] private Sprite normalSprite;
        [SerializeField] private Sprite flippedSprite;

        private UnityEngine.UI.Button _button;
        private UnityEngine.UI.Image _image;

        private void Awake()
        {
            _button = GetComponent<UnityEngine.UI.Button>();
            _image = GetComponent<UnityEngine.UI.Image>();

            _button.onClick.AddListener(OnClick);
            RefreshVisual();
        }

        private void OnEnable()
        {
            RefreshVisual();
        }

        private void OnClick()
        {

            GlobalFlipConfig.flipTrackingHorizontally = !GlobalFlipConfig.flipTrackingHorizontally;

            Debug.Log($"[FlipButton] flipTracking = {GlobalFlipConfig.flipTrackingHorizontally}");

            RefreshVisual();
        }

        private void RefreshVisual()
        {
            if (_image == null) return;

            if (GlobalFlipConfig.flipTrackingHorizontally)
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
}
