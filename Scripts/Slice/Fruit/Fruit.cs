using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Zerah.Slicer
{
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(RawImage))]
    public class Fruit : MonoBehaviour
    {
        public static readonly List<Fruit> Alive = new();

        private RectTransform _rt;
        private RawImage _img;
        private RectTransform _playArea;

        private Vector2 _velocity;
        private float _gravity;
        private int _typeIndex;

        public RectTransform RectT => _rt;

        public float HitRadiusPx => _rt != null ? _rt.rect.width * 0.45f : 40f;

        static RectTransform s_playArea;
        static RectTransform s_fxContainer;
        static Sprite[] s_crushSprites;
        static Sprite[] s_leftHalves;
        static Sprite[] s_rightHalves;

        static float s_pieceUpSpeed = 400f;
        static float s_pieceSideSpeed = 250f;
        static float s_pieceGravity = -2000f;
        static float s_pieceLife = 1.3f;

        public static void ConfigureFx(
            RectTransform playArea,
            RectTransform fxContainer,
            Sprite[] crush,
            Sprite[] left,
            Sprite[] right,
            float pieceGravity = -2000f,
            float pieceLife = 1.3f)
        {
            s_playArea = playArea;
            s_fxContainer = fxContainer != null ? fxContainer : playArea;
            s_crushSprites = crush;
            s_leftHalves = left;
            s_rightHalves = right;
            s_pieceGravity = pieceGravity;
            s_pieceLife = pieceLife;
        }

        public void Init(
            int typeIndex,
            Sprite sprite,
            RectTransform playArea,
            Vector2 startPos,
            Vector2 startVelocity,
            float gravity,
            float scale = 1f)
        {
            if (_rt == null) _rt = GetComponent<RectTransform>();
            if (_img == null) _img = GetComponent<RawImage>();

            _typeIndex = typeIndex;
            _playArea = playArea;
            _velocity = startVelocity;
            _gravity = gravity;

            if (sprite != null)
            {
                _img.texture = sprite.texture;
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

        private void OnEnable()
        {
            if (!Alive.Contains(this)) Alive.Add(this);
        }

        private void OnDisable()
        {
            Alive.Remove(this);
        }

        private void OnDestroy()
        {
            Alive.Remove(this);
        }

        private void Update()
        {
            if (_playArea == null) return;

            float dt = Time.deltaTime;
            _velocity.y += _gravity * dt;

            var pos = _rt.anchoredPosition;
            pos += _velocity * dt;
            _rt.anchoredPosition = pos;

            float halfH = _playArea.rect.height * 0.5f;
            if (pos.y < -halfH - 200f)
            {
                Destroy(gameObject);
            }
        }

        public void Slice()
        {

            SpawnPieces();
            SpawnCrushFx();

            ScoreManager_Fruit.I?.Add(1);

            Destroy(gameObject);
        }

        void SpawnCrushFx()
        {
            if (s_fxContainer == null || s_crushSprites == null) return;
            if (_typeIndex < 0 || _typeIndex >= s_crushSprites.Length) return;

            var sprite = s_crushSprites[_typeIndex];
            if (sprite == null) return;

            var go = new GameObject("FruitCrush", typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            var img = go.GetComponent<Image>();

            go.transform.SetParent(s_fxContainer, false);
            img.sprite = sprite;
            img.raycastTarget = false;

            var r = sprite.rect;
            rt.sizeDelta = new Vector2(r.width, r.height) * 0.9f;

            Vector2 pos = _rt.anchoredPosition;
            if (_rt.parent != s_fxContainer)
            {
                Vector3 world = _rt.TransformPoint(_rt.rect.center);
                Vector3 local = s_fxContainer.InverseTransformPoint(world);
                pos = new Vector2(local.x, local.y);
            }
            rt.anchoredPosition = pos;

            Object.Destroy(go, 0.6f);
        }

        void SpawnPieces()
        {
            if (s_playArea == null) return;
            if (s_leftHalves == null || s_rightHalves == null) return;
            if (_typeIndex < 0 || _typeIndex >= s_leftHalves.Length) return;
            if (_typeIndex < 0 || _typeIndex >= s_rightHalves.Length) return;

            Sprite left = s_leftHalves[_typeIndex];
            Sprite right = s_rightHalves[_typeIndex];

            if (left == null || right == null) return;

            Vector2 center = _rt.anchoredPosition;

            float offsetX = _rt.rect.width * 0.2f;

            CreatePiece(
                sprite: left,
                startPos: center + new Vector2(-offsetX, 0),
                startVel: new Vector2(-s_pieceSideSpeed, s_pieceUpSpeed)
            );

            CreatePiece(
                sprite: right,
                startPos: center + new Vector2(+offsetX, 0),
                startVel: new Vector2(+s_pieceSideSpeed, s_pieceUpSpeed)
            );
        }

        void CreatePiece(Sprite sprite, Vector2 startPos, Vector2 startVel)
        {
            var go = new GameObject("FruitPiece", typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            var img = go.GetComponent<Image>();

            go.transform.SetParent(s_playArea, false);
            img.sprite = sprite;
            img.raycastTarget = false;

            var r = sprite.rect;
            rt.sizeDelta = new Vector2(r.width, r.height);

            rt.anchoredPosition = startPos;

            var mover = go.AddComponent<FruitPiece>();
            mover.Init(startVel, s_pieceGravity, s_pieceLife);
        }
    }
}
