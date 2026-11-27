using UnityEngine;
using WaterChaos.Sounds;

namespace WaterChaos.Items
{
    /// <summary>
    /// The cat to rescue: meows now and then (more often while waiting to be found, so the player can follow the sound,
    /// right away when picked up) and wags its tail. It is carried like any item (<see cref="PickableItem"/>).
    /// </summary>
    [RequireComponent(typeof(PickableItem))]
    public sealed class CatItem : MonoBehaviour
    {
        [Header("Meow")]
        [Tooltip("Seconds (s) between meows while the cat waits to be found (random in this range).")]
        [SerializeField] private Vector2 _waitingInterval = new Vector2(2.5f, 5f);

        [Tooltip("Seconds (s) between meows while carried (random in this range).")]
        [SerializeField] private Vector2 _carriedInterval = new Vector2(6f, 11f);

        [Header("Tail")]
        [Tooltip("Optional: the tail, swung side to side.")]
        [SerializeField] private Transform _tail;

        [SerializeField, Range(0f, 60f)] private float _tailAngle = 25f;
        [SerializeField, Range(0.1f, 10f)] private float _tailSpeed = 3f;

        private PickableItem _item;
        private Quaternion _tailRest;
        private float _nextMeow;
        private bool _wasHeld;

        private void Awake()
        {
            _item = GetComponent<PickableItem>();
            if (_tail != null)
                _tailRest = _tail.localRotation;
            ScheduleMeow(false);
        }

        private void Update()
        {
            bool held = _item.IsHeld;
            if (held && !_wasHeld)
                Meow(held);
            _wasHeld = held;

            if (Time.time >= _nextMeow)
                Meow(held);

            if (_tail != null)
                _tail.localRotation = _tailRest * Quaternion.Euler(0f, Mathf.Sin(Time.time * _tailSpeed) * _tailAngle, 0f);
        }

        private void Meow(bool held)
        {
            Sound.Play(SoundId.CatMeow, transform.position);
            ScheduleMeow(held);
        }

        private void ScheduleMeow(bool held)
        {
            Vector2 range = held ? _carriedInterval : _waitingInterval;
            _nextMeow = Time.time + Random.Range(range.x, Mathf.Max(range.x, range.y));
        }
    }
}
