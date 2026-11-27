using UnityEngine;
using UnityEngine.Rendering;

namespace WaterChaos.Player
{
    /// <summary>
    /// The player's firefighter body: idles while standing and runs while walking.
    /// In first person it is only a shadow (the camera inside it would see the shoulders and the cut neck
    /// when looking down); off that, its head is hidden so the camera never sees it from within.
    /// </summary>
    public sealed class PlayerBodyModel : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The player's body; its speed picks the animation.")]
        [SerializeField] private CharacterController _body;

        [SerializeField] private FirefighterAnimationSwitcher _animation;

        [Tooltip("Head bone of the model, hidden in first person. Empty = the head stays visible.")]
        [SerializeField] private Transform _head;

        [Header("Settings")]
        [Tooltip("Horizontal speed above which the model runs, in meters per second (m/s).")]
        [SerializeField, Min(0f)] private float _runSpeed = 0.5f;

        [Tooltip("The body is not drawn, only its shadow: the usual first-person look. Off = the body is visible too.")]
        [SerializeField] private bool _shadowOnly = true;

        private void Awake()
        {
            if (!_shadowOnly)
                return;

            foreach (Renderer part in GetComponentsInChildren<Renderer>(true))
                part.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
        }

        private void Update()
        {
            if (_body == null || _animation == null)
                return;

            Vector3 velocity = _body.velocity;
            float speed = new Vector2(velocity.x, velocity.z).magnitude;
            _animation.Animation = speed > _runSpeed ? FirefighterAnimation.Run : FirefighterAnimation.Idle;
        }

        // After the animation has posed the bones.
        private void LateUpdate()
        {
            if (_head != null)
                _head.localScale = Vector3.zero;
        }
    }
}
