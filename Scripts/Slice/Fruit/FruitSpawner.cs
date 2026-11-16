using UnityEngine;

namespace Zerah.Slicer
{

    public class FruitSpawner : MonoBehaviour
    {
        [Header("Fruit Sprites (통 과일 5개)")]
        public Sprite[] fruitSprites;

        [Header("Slice FX Sprites (타입 인덱스 맞추기)")]
        [Tooltip("과일별 크러시/주스 이미지")]
        public Sprite[] crushSprites;

        [Tooltip("왼쪽 반쪽 스프라이트")]
        public Sprite[] leftHalfSprites;

        [Tooltip("오른쪽 반쪽 스프라이트")]
        public Sprite[] rightHalfSprites;

        [Header("Template")]
        [Tooltip("비활성 FruitTemplate (RawImage + Fruit)")]
        public Fruit fruitTemplate;

        [Header("Spawn Interval (과일 1개 간격, 초)")]
        public Vector2 spawnIntervalRange = new Vector2(0.5f, 1.8f);

        [Header("Throw Settings")]
        [Tooltip("X 방향 초기 속도 범위 (px/sec)")]
        public Vector2 initialSpeedXRange = new Vector2(-350f, 350f);

        [Tooltip("Y 방향(위로) 초기 속도 범위 (px/sec)")]
        public Vector2 initialSpeedYRange = new Vector2(700f, 1000f);

        [Tooltip("아래로 당기는 중력 (px/sec^2, 음수)")]
        public float gravity = -1500f;

        [Header("Scale / Size")]
        public Vector2 scaleRange = new Vector2(0.9f, 1.2f);

        private RectTransform _rt;
        private float _timer;
        private float _nextSpawnTime;

        private void Awake()
        {
            _rt = GetComponent<RectTransform>();
            if (fruitTemplate != null)
                fruitTemplate.gameObject.SetActive(false);

            Fruit.ConfigureFx(
                playArea: _rt,
                fxContainer: _rt,
                crush: crushSprites,
                left: leftHalfSprites,
                right: rightHalfSprites
            );

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
            if (fruitSprites == null || fruitSprites.Length == 0 || fruitTemplate == null)
                return;

            int index = Random.Range(0, fruitSprites.Length);
            Sprite sprite = fruitSprites[index];

            float halfW = _rt.rect.width * 0.5f;
            float halfH = _rt.rect.height * 0.5f;

            float x = Random.Range(-halfW * 0.8f, halfW * 0.8f);
            float startY = -halfH - 80f;
            Vector2 startPos = new Vector2(x, startY);

            float vx = Random.Range(initialSpeedXRange.x, initialSpeedXRange.y);
            float vy = Random.Range(initialSpeedYRange.x, initialSpeedYRange.y);
            Vector2 v0 = new Vector2(vx, vy);

            float scale = Random.Range(scaleRange.x, scaleRange.y);

            GameObject go = Instantiate(fruitTemplate.gameObject, _rt);
            Fruit fruit = go.GetComponent<Fruit>();

            fruit.Init(index, sprite, _rt, startPos, v0, gravity, scale);
        }
    }
}
