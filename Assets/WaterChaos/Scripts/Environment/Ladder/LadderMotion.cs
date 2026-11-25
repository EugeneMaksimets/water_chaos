using UnityEngine;

namespace WaterChaos.Environment
{
    /// <summary>
    /// How the player moves on a ladder: forward/back input climbs up/down (down while looking down,
    /// so the player can face the hole and descend), and the input also moves slowly along the view,
    /// which lets the player step off at the top or slide sideways.
    /// </summary>
    public static class LadderMotion
    {
        // Looking further down than about 30° below the horizon turns "forward" into "down".
        private const float LookDownY = -0.5f;

        /// <param name="input">Move input: x = strafe, y = forward/back, each -1..1.</param>
        /// <param name="viewForward">Where the player looks (camera forward).</param>
        /// <param name="viewRight">Camera right.</param>
        /// <param name="climbSpeed">Up/down speed, in meters per second (m/s).</param>
        /// <param name="moveSpeed">Horizontal speed on the ladder, in meters per second (m/s).</param>
        /// <returns>Velocity in meters per second (m/s).</returns>
        public static Vector3 Velocity(Vector2 input, Vector3 viewForward, Vector3 viewRight, float climbSpeed, float moveSpeed)
        {
            float direction = viewForward.y < LookDownY ? -1f : 1f;
            float vertical = input.y * direction * climbSpeed;

            Vector3 forward = Flat(viewForward);
            Vector3 right = Flat(viewRight);
            Vector3 horizontal = (forward * input.y + right * input.x) * moveSpeed;

            return horizontal + Vector3.up * vertical;
        }

        private static Vector3 Flat(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude > 0f ? direction.normalized : Vector3.zero;
        }
    }
}
