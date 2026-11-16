using UnityEngine;
using UnityEngine.UI;

namespace Zerah.Slicer
{
    public class BalloonSpawner : MonoBehaviour
    {
        [Header("Balloon Library (7 sprites)")]
        public Sprite[] balloonSprites;

        [Header("Template")]
        public Balloon balloonTemplate;

        [Header("Spawn Settings")]
        [Tooltip("풍선 생성 간격 (초)")]
        public Vector2 spawnIntervalRange = new Vector2(0.3f, 2.0f);
        [Tooltip("풍선 이동 속도 범위 (px/sec)")]
        public Vector2 speedRange = new Vector2(120f, 220f);
        [Tooltip("풍선 크기 랜덤 배율")]
        public Vector2 scaleRange = new Vector2(0.85f, 1.1f);

        [Header("Boom FX")]
        public RectTransform boomTemplate;
        public RectTransform boomContainer;
        public float boomLife = 0.45f;

        private RectTransform _rt;
        private float _timer;
        private float _nextSpawnTime;

        private void Awake()
        {
            _rt = GetComponent<RectTransform>();
            if (balloonTemplate != null)
                balloonTemplate.gameObject.SetActive(false);

            Balloon.ConfigureBoom(boomTemplate, boomContainer != null ? boomContainer : _rt, boomLife);

            _nextSpawnTime = Random.Range(spawnIntervalRange.x, spawnIntervalRange.y);
        }

        private void Update()
        {
            _timer += Time.deltaTime;

            if (_timer >= _nextSpawnTime)
            {
                _timer = 0f;

                SpawnOne();

                _nextSpawnTime = Random.Range(spawnIntervalRange.x, spawnIntervalRange.y);
            }
        }

        private void SpawnOne()
        {
            if (balloonSprites == null || balloonSprites.Length == 0 || balloonTemplate == null) return;

            Sprite sprite = balloonSprites[Random.Range(0, balloonSprites.Length)];

            float halfW = _rt.rect.width * 0.5f;
            float x = Random.Range(-halfW * 0.9f, halfW * 0.9f);

            float halfH = _rt.rect.height * 0.5f;
            float startY = -halfH - 80f;
            Vector2 startPos = new Vector2(x, startY);

            float speed = Random.Range(speedRange.x, speedRange.y);
            float scale = Random.Range(scaleRange.x, scaleRange.y);

            var go = Instantiate(balloonTemplate.gameObject, _rt);
            var balloon = go.GetComponent<Balloon>();

            balloon.Init(sprite, speed, _rt, startPos, scale);
        }
    }
}
