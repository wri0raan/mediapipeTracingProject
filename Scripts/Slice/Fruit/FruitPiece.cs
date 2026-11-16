using UnityEngine;

namespace Zerah.Slicer
{

    [RequireComponent(typeof(RectTransform))]
    public class FruitPiece : MonoBehaviour
    {
        private RectTransform _rt;
        private Vector2 _velocity;
        private float _gravity;
        private float _life;
        private float _time;

        public void Init(Vector2 startVelocity, float gravity, float life)
        {
            if (_rt == null) _rt = GetComponent<RectTransform>();
            _velocity = startVelocity;
            _gravity = gravity;
            _life = life;
            _time = 0f;
        }

        private void Awake()
        {
            _rt = GetComponent<RectTransform>();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _time += dt;

            _velocity.y += _gravity * dt;
            _rt.anchoredPosition += _velocity * dt;

            if (_time >= _life)
            {
                Destroy(gameObject);
            }
        }
    }
}
