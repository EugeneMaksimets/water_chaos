using UnityEngine;

namespace WaterChaos.Fire
{
    /// <summary>
    /// It sways gently and "breathes". If the sprite's pivot point should be at the bottom.
    /// </summary>
    public class FireSway : MonoBehaviour
    {
        [Header("Sway")]
        [SerializeField] private float _swayAngle = 10f;   
        [SerializeField] private float _swaySpeed = 0.8f;  

        [Header("Breathing")]
        [SerializeField] private float _scaleAmount = 0.1f; // 0.1 = ±10% by higth
        [SerializeField] private float _scaleSpeed = 1.5f;

        private Vector3 _baseScale;
        private float _seed;

        private void Awake()
        {
            _baseScale = transform.localScale;
            _seed = Random.value * 100f; // asynchronous for fire
        }

        private void Update()
        {
            float time = Time.time;

            float sway = Noise(_seed, time * _swaySpeed);
            float breath = Noise(_seed + 50f, time * _scaleSpeed);

            Quaternion tilt = Quaternion.Euler(0f, 0f, sway * _swayAngle);

            float height = 1f + breath * _scaleAmount;
            transform.localScale = new Vector3(_baseScale.x, _baseScale.y * height, _baseScale.z);
        }

        // Perlin noise  -1..1
        private static float Noise(float x, float y)
        {
            return Mathf.PerlinNoise(x, y) * 2f - 1f;
        }
    }
}