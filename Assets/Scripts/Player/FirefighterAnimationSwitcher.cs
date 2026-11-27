using UnityEngine;

namespace WaterChaos.Player
{
    public enum FirefighterAnimation { Idle, Run, Extinguish }

    [RequireComponent(typeof(Animator))]
    public class FirefighterAnimationSwitcher : MonoBehaviour
    {
        [Header("Animation")]
        [SerializeField] private FirefighterAnimation _animation = FirefighterAnimation.Idle;
        [SerializeField] private float _crossFadeTime = 0.2f; // seconds

        private Animator _animator;
        private FirefighterAnimation _current;

        /// <summary>Animation to play; changing it cross-fades to the new one.</summary>
        public FirefighterAnimation Animation
        {
            get => _animation;
            set => _animation = value;
        }

        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        private void Start()
        {
            _current = _animation;
            _animator.Play(_current.ToString());
        }

        private void Update()
        {
            if (_animation == _current)
                return;

            _current = _animation;
            _animator.CrossFade(_current.ToString(), _crossFadeTime);
        }
    }
}
