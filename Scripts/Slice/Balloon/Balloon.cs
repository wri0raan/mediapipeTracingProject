using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Zerah.Slicer
{
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(RawImage))]
    public class Balloon : MonoBehaviour
    {

        public static readonly List<Balloon> Alive = new();

        private RectTransform _rt;
        private RawImage _img;
        private RectTransform _playArea;
        private float _speed;

        public RectTransform RectT => _rt;
        public float HitRadiusPx => _rt != null ? _rt.rect.width * 0.45f : 40f;

        static RectTransform s_boomTemplate;
        static RectTransform s_boomContainer;
        static float s_boomLife = 0.45f;

        public static void ConfigureBoom(RectTransform template, RectTransform container, float life)
        {
            s_boomTemplate = template;
            s_boomContainer = container;
            s_boomLife = Mathf.Max(0.05f, life);
            if (s_boomTemplate != null) s_boomTemplate.gameObject.SetActive(false);
        }

        public void Init(Sprite sprite, float speed, RectTransform playArea, Vector2 startPos, float scale = 1f)
        {
            if (_rt == null) _rt = GetComponent<RectTransform>();
            if (_img == null) _img = GetComponent<RawImage>();

            _playArea = playArea;
            _speed = speed;

            _img.texture = sprite != null ? sprite.texture : null;
            if (sprite != null)
            {
                var r = sprite.rect;
                _rt.sizeDelta = new Vector2(r.width, r.height) * scale;
            }

            _rt.anchoredPosition = startPos;
            gameObject.SetActive(true);
        }

        private void Awake()
        {
            _rt = GetComponent<RectTransform>();
            _img = GetComponent<RawImage>();
        }

        private void OnEnable() { if (!Alive.Contains(this)) Alive.Add(this); }
        private void OnDisable() { Alive.Remove(this); }
        private void OnDestroy() { Alive.Remove(this); }

        private void Update()
        {
            if (_playArea == null) return;

            var pos = _rt.anchoredPosition;
            pos.y += _speed * Time.deltaTime;
            _rt.anchoredPosition = pos;

            float halfH = _playArea.rect.height * 0.5f;
            float myHalfH = _rt.rect.height * 0.5f;
            if (pos.y > halfH + myHalfH + 50f)
                Pop();
        }

        public void Pop()
        {
            SpawnBoomFx();
            Zerah.Slicer.ScoreManager.I?.Add(1);
            Destroy(gameObject);
        }

        void SpawnBoomFx()
        {
            if (s_boomTemplate == null || s_boomContainer == null) return;

            var go = Instantiate(s_boomTemplate.gameObject, s_boomContainer);
            var rt = go.GetComponent<RectTransform>();
            var img = go.GetComponent<Image>();
            if (img != null)
            {
                var c = img.color; c.a = 1f; img.color = c;
                img.raycastTarget = false;
            }

            Vector2 pos = _rt.anchoredPosition;
            if (_rt.parent != s_boomContainer)
            {
                Vector3 world = _rt.TransformPoint(_rt.rect.center);
                Vector3 local = s_boomContainer.InverseTransformPoint(world);
                pos = new Vector2(local.x, local.y);
            }
            rt.anchoredPosition = pos;

            rt.sizeDelta = _rt.rect.size * 0.95f;
            go.SetActive(true);
            Destroy(go, s_boomLife);

            StartCoroutine(FadeAndRemove(img, go, s_boomLife));
        }

        IEnumerator FadeAndRemove(Image img, GameObject go, float life)
        {
            float t = 0f;
            while (t < life)
            {
                t += Time.unscaledDeltaTime;
                if (img != null)
                {
                    float u = t / life;

                    img.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 1.15f, u);
                    var col = img.color; col.a = 1f - u; img.color = col;
                }
                yield return null;
            }
            if (go != null) Destroy(go);
        }

    }
}
