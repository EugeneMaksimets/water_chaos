using UnityEngine;

namespace WaterChaos.UI
{
    /// <summary>
    /// Pure math of a half-circle menu over its center: options share the upper half from left (180°) to right (0°),
    /// in order. Offsets are in any consistent unit (e.g. canvas pixels); angles in degrees.
    /// </summary>
    public static class RadialMenuLayout
    {
        /// <summary>Angle covered by one option, in degrees (°).</summary>
        public static float SectorAngle(int count) => count > 0 ? 180f / count : 0f;

        /// <summary>Direction angle of the middle of option <paramref name="index"/>, in degrees (°); 90 = straight up.</summary>
        public static float CenterAngle(int index, int count) => 180f - (index + 0.5f) * SectorAngle(count);

        /// <summary>Unit direction from the menu center to the middle of the option.</summary>
        public static Vector2 CenterDirection(int index, int count)
        {
            float radians = CenterAngle(index, count) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        /// <summary>
        /// Option under a pointer at <paramref name="offset"/> from the menu center; -1 inside the dead zone,
        /// for no options or invalid input. Below the center line the nearest end option is picked.
        /// </summary>
        public static int GetIndex(Vector2 offset, int count, float deadZone)
        {
            if (count <= 0 || float.IsNaN(offset.x) || float.IsNaN(offset.y) || offset.magnitude < Mathf.Max(0f, deadZone))
                return -1;

            if (offset.y < 0f)
                return offset.x < 0f ? 0 : count - 1;

            float angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg; // 0..180
            int index = Mathf.FloorToInt((180f - angle) / SectorAngle(count));
            return Mathf.Clamp(index, 0, count - 1);
        }
    }
}
