using UnityEngine;

namespace WaterChaos.WaterTransfer
{
    /// <summary>
    /// Pure rules for where a transfer hose coil may be dropped next to the player:
    /// never closer than a clearance, so it does not land inside the player.
    /// Ground and geometry checks are done by the caller.
    /// </summary>
    public static class TransferHoseDrop
    {
        /// <summary>Spots tried in order: the preferred one, then right, left, behind and in front of the player.</summary>
        public const int CandidateCount = 5;

        private const float MinOffset = 1e-4f;

        /// <summary>
        /// <paramref name="preferred"/> pushed out horizontally to at least <paramref name="clearance"/> meters (m)
        /// from <paramref name="player"/>, keeping its height. Right above the player it goes along <paramref name="fallbackDirection"/>.
        /// </summary>
        public static Vector3 KeepClear(Vector3 player, Vector3 preferred, float clearance, Vector3 fallbackDirection)
        {
            Vector3 offset = new Vector3(preferred.x - player.x, 0f, preferred.z - player.z);
            float distance = offset.magnitude;
            if (distance >= clearance)
                return preferred;

            Vector3 direction = distance > MinOffset ? offset / distance : Flat(fallbackDirection);
            Vector3 spot = player + direction * clearance;
            spot.y = preferred.y;
            return spot;
        }

        /// <summary>
        /// Drop spot number <paramref name="index"/> (0..<see cref="CandidateCount"/>-1), at <paramref name="preferred"/>'s height.
        /// <paramref name="forward"/> is the player's facing.
        /// </summary>
        public static Vector3 GetCandidate(int index, Vector3 player, Vector3 preferred, Vector3 forward, float clearance)
        {
            Vector3 ahead = Flat(forward);
            Vector3 right = new Vector3(ahead.z, 0f, -ahead.x);

            Vector3 direction;
            switch (index)
            {
                case 0: return KeepClear(player, preferred, clearance, ahead);
                case 1: direction = right; break;
                case 2: direction = -right; break;
                case 3: direction = -ahead; break;
                default: direction = ahead; break;
            }

            Vector3 spot = player + direction * clearance;
            spot.y = preferred.y;
            return spot;
        }

        private static Vector3 Flat(Vector3 direction)
        {
            Vector3 flat = new Vector3(direction.x, 0f, direction.z);
            return flat.sqrMagnitude > MinOffset * MinOffset ? flat.normalized : Vector3.forward;
        }
    }
}
