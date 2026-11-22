using UnityEngine;

namespace WaterChaos.Fire
{
    /// <summary>
    /// Плавно качает и "дышит" спрайтом огня. Pivot спрайта должен быть внизу.
    /// </summary>
    public class FireSway : MonoBehaviour
    {
        [Header("Sway")]
        [SerializeField] private float _swayAngle = 10f;   // максимальный наклон, градусы
        [SerializeField] private float _swaySpeed = 0.8f;  // чем меньше, тем плавнее

        [Header("Breathing")]
        [SerializeField] private float _scaleAmount = 0.1f; // 0.1 = ±10% по высоте
        [SerializeField] private float _scaleSpeed = 1.5f;

        [Header("Camera")]
        [SerializeField] private bool _faceCamera = true;

        private Vector3 _baseScale;
        private float _seed;
        private Transform _camera;

        private void Awake()
        {
            _baseScale = transform.localScale;
            _seed = Random.value * 100f; // чтобы разные огни качались несинхронно

            if (Camera.main != null)
            {
                _camera = Camera.main.transform;
            }
        }

        private void Update()
        {
            float time = Time.time;

            float sway = Noise(_seed, time * _swaySpeed);
            float breath = Noise(_seed + 50f, time * _scaleSpeed);

            Quaternion tilt = Quaternion.Euler(0f, 0f, sway * _swayAngle);
            Quaternion facing = _faceCamera && _camera != null
                ? _camera.rotation
                : transform.parent != null ? transform.parent.rotation : Quaternion.identity;

            transform.rotation = facing * tilt;

            float height = 1f + breath * _scaleAmount;
            transform.localScale = new Vector3(_baseScale.x, _baseScale.y * height, _baseScale.z);
        }

        // Perlin noise в диапазоне -1..1
        private static float Noise(float x, float y)
        {
            return Mathf.PerlinNoise(x, y) * 2f - 1f;
        }
    }
}