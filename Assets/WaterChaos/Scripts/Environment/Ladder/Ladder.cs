using UnityEngine;

namespace WaterChaos.Environment
{
    /// <summary>
    /// Climbable zone in front of a ladder: while the player's body is inside this trigger, the player
    /// climbs instead of walking (see <see cref="LadderMotion"/>). Make the box reach from the bottom of the
    /// ladder to a little above its top, so the player can climb out and step off.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class Ladder : MonoBehaviour
    {
        [Header("Climbing")]
        [Tooltip("Up/down speed on this ladder, in meters per second (m/s).")]
        [SerializeField, Min(0.1f)] private float _climbSpeed = 2.5f;

        [Tooltip("Sideways/forward speed while on the ladder, in meters per second (m/s).")]
        [SerializeField, Min(0f)] private float _moveSpeed = 1.5f;

        public float ClimbSpeed => _climbSpeed;
        public float MoveSpeed => _moveSpeed;

        private void Reset()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }

        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }
    }
}
