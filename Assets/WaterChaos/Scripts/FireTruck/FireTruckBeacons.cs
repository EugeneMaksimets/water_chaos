using UnityEngine;

namespace WaterChaos.FireTruck
{
    /// <summary>
    /// Emergency lights on the truck roof: the red and the blue group flash in turn, each with a double blink.
    /// Presentation only: it switches the group objects (a light and a glowing lens) on and off.
    /// Uses unscaled time, so the lights keep flashing in menus and while the game is paused.
    /// </summary>
    public sealed class FireTruckBeacons : MonoBehaviour
    {
        [Header("Lights")]
        [SerializeField] private GameObject[] _red = new GameObject[0];
        [SerializeField] private GameObject[] _blue = new GameObject[0];

        [Header("Timing")]
        [Tooltip("Full red + blue cycle, in seconds.")]
        [SerializeField, Min(0.1f)] private float _cycle = 0.8f;

        [SerializeField] private bool _flashing = true;

        public bool Flashing
        {
            get => _flashing;
            set
            {
                _flashing = value;
                if (!value)
                    Show(false, false);
            }
        }

        private void OnDisable()
        {
            Show(false, false);
        }

        private void Update()
        {
            if (!_flashing)
                return;

            // Each half of the cycle belongs to one colour: on, off, on, off (a double blink).
            float phase = Mathf.Repeat(Time.unscaledTime / _cycle, 1f);
            bool redHalf = phase < 0.5f;
            float local = Mathf.Repeat(phase * 2f, 1f);
            bool lit = local < 0.2f || (local > 0.35f && local < 0.55f);

            Show(redHalf && lit, !redHalf && lit);
        }

        private void Show(bool red, bool blue)
        {
            SetActive(_red, red);
            SetActive(_blue, blue);
        }

        private static void SetActive(GameObject[] group, bool active)
        {
            foreach (GameObject part in group)
            {
                if (part != null && part.activeSelf != active)
                    part.SetActive(active);
            }
        }
    }
}
