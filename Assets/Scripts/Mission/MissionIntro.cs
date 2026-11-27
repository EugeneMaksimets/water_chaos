using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using WaterChaos.FireTruck;
using WaterChaos.UI;
using WaterChaos.Sounds;

namespace WaterChaos.Mission
{
    /// <summary>
    /// Arrival cut-scene before the mission: a shot of the truck with flashing beacons,
    /// then a first-person "getting out of the cab", then control goes to the player and the mission starts.
    /// It drives the player camera directly while the player components are off and puts it back at the end.
    /// Space or Esc skips it. The runner's "Start On Play" must be off, the intro starts the mission itself.
    /// </summary>
    public sealed class MissionIntro : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private MissionRunner _runner;
        [SerializeField] private Transform _truck;
        [SerializeField] private FireTruckBeacons _beacons;

        [Tooltip("The player camera (child of the player).")]
        [SerializeField] private Transform _camera;

        [Tooltip("Player components switched off during the intro: PlayerInputHandler, PlayerLook, PlayerMovement...")]
        [SerializeField] private Behaviour[] _playerControls = new Behaviour[0];

        [Tooltip("Where the player stands after getting out (position and facing); empty = keep the spawn point.")]
        [SerializeField] private Transform _exitPoint;

        [Header("Shot 1 · Beacons (truck local space, front is -Z)")]
        [SerializeField] private Vector3 _shotFrom = new Vector3(-7f, 2.4f, -13f);
        [SerializeField] private Vector3 _shotTo = new Vector3(-5f, 1.8f, -10f);
        [SerializeField] private Vector3 _shotLookAt = new Vector3(0f, 2.6f, -3f);
        [SerializeField, Min(0f)] private float _shotTime = 3.5f;

        [Header("Shot 2 · Getting Out (truck local space)")]
        [Tooltip("Eye point in the open driver door, just outside the cab wall.")]
        [SerializeField] private Vector3 _door = new Vector3(-1.6f, 2.15f, -3f);
        [SerializeField, Min(0f)] private float _lookAtDoorTime = 0.8f;
        [SerializeField, Min(0f)] private float _stepOutTime = 1.4f;

        [Header("Fades")]
        [SerializeField, Min(0f)] private float _fadeTime = 0.4f;

        private Image _fade;
        private RectTransform[] _bars;
        private Text _skipHint;
        private Vector3 _cameraLocalPosition;
        private Quaternion _cameraLocalRotation;
        private bool _skip;
        private readonly List<Renderer> _hiddenBody = new List<Renderer>();
        private SoundLoop _siren;
        private bool _sirenOn;
        private readonly List<Canvas> _hiddenUi = new List<Canvas>();

        private void Awake()
        {
            if (_runner == null || _truck == null || _camera == null)
            {
                Debug.LogError($"{name}: MissionIntro needs Runner, Truck and Camera references.", this);
                enabled = false;
                return;
            }

            BuildOverlay();
        }

        private void Start()
        {
            StartCoroutine(Play());
        }

        private void Update()
        {
            _siren?.Update(_sirenOn);

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame))
                _skip = true;
        }

        private IEnumerator Play()
        {
            SetPlayerControls(false);
            MovePlayerToExit();
            HidePlayerBody();
            HideOtherUi();
            _cameraLocalPosition = _camera.localPosition;
            _cameraLocalRotation = _camera.localRotation;
            if (_beacons != null)
                _beacons.Flashing = true;

            _siren = new SoundLoop(_truck.gameObject, SoundId.TruckSiren, 0.8f);
            _sirenOn = true;
            Sound.Play(SoundId.TruckBrakes, _truck.position);

            // Shot 1: the truck with flashing beacons, a slow dolly in.
            SetFade(1f);
            Vector3 lookAt = _truck.TransformPoint(_shotLookAt);
            Vector3 from = _truck.TransformPoint(_shotFrom);
            Vector3 to = _truck.TransformPoint(_shotTo);
            yield return Animate(_shotTime, t =>
            {
                Vector3 position = Vector3.Lerp(from, to, Smooth(t));
                _camera.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position));
                SetFade(1f - Mathf.Clamp01(t * _shotTime / Mathf.Max(_fadeTime, 0.01f)));
            });

            yield return Animate(_fadeTime, SetFade);

            // Shot 2: sitting in the open driver door looking down the road, a glance at the step, then down to the ground.
            Quaternion forward = _truck.rotation * Quaternion.LookRotation(new Vector3(-0.25f, -0.05f, -1f));
            Quaternion toStep = _truck.rotation * Quaternion.LookRotation(new Vector3(-1f, -0.6f, -0.35f));
            Vector3 door = _truck.TransformPoint(_door);
            _camera.SetPositionAndRotation(door, forward);
            _sirenOn = false;

            yield return Animate(_fadeTime, t => SetFade(1f - t));
            yield return Animate(_lookAtDoorTime, t => _camera.rotation = Quaternion.Slerp(forward, toStep, Smooth(t)));

            Sound.Play(SoundId.TruckDoor, door);
            Transform parent = _camera.parent;
            Vector3 end = parent != null ? parent.TransformPoint(_cameraLocalPosition) : _cameraLocalPosition;
            Quaternion endRotation = parent != null ? parent.rotation * _cameraLocalRotation : _cameraLocalRotation;
            // Out first, then down: a quadratic curve through a point beside the door at cab height.
            Vector3 outside = new Vector3(end.x, door.y, end.z);
            yield return Animate(_stepOutTime, t =>
            {
                float s = Smooth(t);
                Vector3 a = Vector3.Lerp(door, outside, s);
                Vector3 b = Vector3.Lerp(outside, end, s);
                Vector3 position = Vector3.Lerp(a, b, s) + Vector3.up * (Mathf.Sin(s * Mathf.PI * 2f) * 0.05f);
                _camera.SetPositionAndRotation(position, Quaternion.Slerp(toStep, endRotation, s));
            });

            Finish();
        }

        private void Finish()
        {
            StopAllCoroutines();
            _sirenOn = false;
            _siren?.Stop();
            _camera.localPosition = _cameraLocalPosition;
            _camera.localRotation = _cameraLocalRotation;
            SetFade(0f);
            foreach (RectTransform bar in _bars)
                bar.gameObject.SetActive(false);
            _skipHint.gameObject.SetActive(false);

            foreach (Renderer part in _hiddenBody)
                part.enabled = true;
            foreach (Canvas canvas in _hiddenUi)
            {
                if (canvas != null)
                    canvas.enabled = true;
            }
            _hiddenUi.Clear();
            SetPlayerControls(true);
            _runner.StartMission();
            enabled = false;
        }

        // Runs step(t) with t from 0 to 1 over the duration; a skip ends the whole intro.
        private IEnumerator Animate(float duration, System.Action<float> step)
        {
            for (float time = 0f; time < duration; time += Time.deltaTime)
            {
                if (_skip)
                {
                    Finish();
                    yield break;
                }

                step(time / duration);
                yield return null;
            }

            step(1f);
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);

        private void MovePlayerToExit()
        {
            if (_exitPoint == null)
                return;

            Transform player = _runner.Player;
            CharacterController body = player.GetComponent<CharacterController>();
            if (body != null)
                body.enabled = false;
            player.SetPositionAndRotation(_exitPoint.position, Quaternion.Euler(0f, _exitPoint.eulerAngles.y, 0f));
            if (body != null)
                body.enabled = true;
        }

        // The player body (capsule) would stand in the truck shot; what is under the camera (held tools) stays.
        private void HidePlayerBody()
        {
            foreach (Renderer part in _runner.Player.GetComponentsInChildren<Renderer>())
            {
                if (!part.enabled || part.transform.IsChildOf(_camera))
                    continue;

                part.enabled = false;
                _hiddenBody.Add(part);
            }
        }

        // The intro is a clean shot: every other screen canvas (HUD, inventory, objectives) is off until it ends.
        private void HideOtherUi()
        {
            foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
            {
                if (!canvas.isRootCanvas || !canvas.enabled || canvas.transform.IsChildOf(transform))
                    continue;

                canvas.enabled = false;
                _hiddenUi.Add(canvas);
            }
        }

        private void SetPlayerControls(bool enabled)
        {
            foreach (Behaviour part in _playerControls)
            {
                if (part != null)
                    part.enabled = enabled;
            }
        }

        private void SetFade(float alpha)
        {
            _fade.color = new Color(0f, 0f, 0f, alpha);
        }

        private void BuildOverlay()
        {
            Transform root = HudWidgets.CreateCanvas(transform, "Intro Canvas", 200).transform;

            _bars = new[] { CreateBar(root, 1f), CreateBar(root, 0f) };

            _fade = HudWidgets.CreateImage("Fade", root, Vector2.zero, Vector2.zero, null, Color.black);
            _fade.rectTransform.anchorMin = Vector2.zero;
            _fade.rectTransform.anchorMax = Vector2.one;

            _skipHint = HudWidgets.CreateText("Skip Hint", root, new Vector2(760f, -490f), new Vector2(360f, 40f), 24, TextAnchor.MiddleRight);
            _skipHint.text = "Пробіл — пропустити";
            _skipHint.color = new Color(1f, 1f, 1f, 0.7f);
        }

        // Black cinematic bar along the top (anchorY = 1) or the bottom (anchorY = 0) edge.
        private static RectTransform CreateBar(Transform parent, float anchorY)
        {
            RectTransform bar = HudWidgets.CreateImage("Letterbox", parent, Vector2.zero, Vector2.zero, null, Color.black).rectTransform;
            bar.anchorMin = new Vector2(0f, anchorY);
            bar.anchorMax = new Vector2(1f, anchorY);
            bar.pivot = new Vector2(0.5f, anchorY);
            bar.sizeDelta = new Vector2(0f, 120f);
            return bar;
        }
    }
}
