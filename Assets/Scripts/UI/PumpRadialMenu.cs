using UnityEngine;
using UnityEngine.UI;
using WaterChaos.Player;
using WaterChaos.WaterTransfer;

namespace WaterChaos.UI
{
    /// <summary>
    /// Half-circle pump menu. E at a pump opens it with the actions valid right now; the mouse direction from the
    /// screen center highlights one, a click performs it and closes the menu. E again or Escape closes it.
    /// While open: the cursor is free, and the listed player behaviours (look, movement, throw, pick up, slot keys)
    /// are switched off so the click and the mouse never act on the game underneath.
    /// Presentation only: actions go through <see cref="PlayerTransferHoseInteractor.Execute"/>.
    /// </summary>
    public sealed class PumpRadialMenu : MonoBehaviour
    {
        private const int MaxOptions = 4;

        [Header("References")]
        [SerializeField] private PlayerInputHandler _input;
        [SerializeField] private PlayerTransferHoseInteractor _interactor;

        [Tooltip("Player behaviours switched off while the menu is open, e.g. PlayerLook, PlayerMovement, PlayerItemThrower, PlayerItemInteractor, PlayerInventory.")]
        [SerializeField] private Behaviour[] _blockWhileOpen = new Behaviour[0];

        [Header("Look")]
        [Tooltip("Outer radius of the half circle, in canvas pixels (1920x1080).")]
        [SerializeField, Min(50f)] private float _outerRadius = 200f;

        [Tooltip("Inner radius of the half circle, in canvas pixels.")]
        [SerializeField, Min(10f)] private float _innerRadius = 90f;

        [Tooltip("Gap between options, in degrees (°).")]
        [SerializeField, Range(0f, 10f)] private float _gap = 3f;

        [Tooltip("Pointer closer to the center than this picks nothing, in canvas pixels.")]
        [SerializeField, Min(0f)] private float _deadZone = 40f;

        [SerializeField] private Color _optionColor = new Color(0f, 0f, 0f, 0.55f);
        [SerializeField] private Color _highlightColor = new Color(1f, 1f, 1f, 0.9f);
        [SerializeField] private Color _highlightTextColor = new Color(0.08f, 0.08f, 0.08f);

        private readonly PumpMenuAction[] _shown = new PumpMenuAction[MaxOptions];
        private readonly bool[] _wasEnabled = new bool[32];

        private Canvas _canvas;
        private RectTransform _root;
        private Image[] _sectors;
        private Text[] _labels;
        private int _count;
        private int _hovered = -1;
        private TransferConnectionPoint _openPoint;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            if (_input == null || _interactor == null)
            {
                Debug.LogError($"{name}: PumpRadialMenu needs Input and Interactor references.", this);
                enabled = false;
                return;
            }

            Build();
        }

        private void OnEnable()
        {
            if (_input == null)
                return;

            _input.InteractPressed += OnInteractPressed;
            _input.CancelPressed += Close;
            _input.ThrowReleased += OnClick;
        }

        private void OnDisable()
        {
            if (_input != null)
            {
                _input.InteractPressed -= OnInteractPressed;
                _input.CancelPressed -= Close;
                _input.ThrowReleased -= OnClick;
            }

            Close();
        }

        private void Update()
        {
            if (!IsOpen)
                return;

            // Out of reach or nothing left to do (e.g. the pump was taken away): close without acting.
            if (_interactor.TargetPoint != _openPoint || _interactor.MenuActions.Count == 0)
            {
                Close();
                return;
            }

            if (!SameAsShown())
                Layout();

            Vector2 offset = (_input.PointerPosition - new Vector2(Screen.width, Screen.height) * 0.5f) / _canvas.scaleFactor;
            SetHovered(RadialMenuLayout.GetIndex(offset, _count, _deadZone));
        }

        private void OnInteractPressed()
        {
            if (IsOpen)
                Close();
            else if (_interactor.CanOpenMenu)
                Open();
        }

        private void OnClick()
        {
            if (!IsOpen || _hovered < 0)
                return;

            PumpMenuAction action = _shown[_hovered];
            Close();
            _interactor.Execute(action);
        }

        private void Open()
        {
            IsOpen = true;
            _openPoint = _interactor.TargetPoint;
            _interactor.SetMenuOpen(true);

            for (int i = 0; i < _blockWhileOpen.Length && i < _wasEnabled.Length; i++)
            {
                Behaviour behaviour = _blockWhileOpen[i];
                _wasEnabled[i] = behaviour != null && behaviour.enabled;
                if (_wasEnabled[i])
                    behaviour.enabled = false;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Layout();
            _root.gameObject.SetActive(true);
        }

        private void Close()
        {
            if (!IsOpen)
                return;

            IsOpen = false;
            _openPoint = null;
            _root.gameObject.SetActive(false);
            _interactor.SetMenuOpen(false);

            for (int i = 0; i < _blockWhileOpen.Length && i < _wasEnabled.Length; i++)
            {
                if (_wasEnabled[i] && _blockWhileOpen[i] != null)
                    _blockWhileOpen[i].enabled = true;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private bool SameAsShown()
        {
            if (_interactor.MenuActions.Count != _count)
                return false;

            for (int i = 0; i < _count; i++)
            {
                if (_interactor.MenuActions[i] != _shown[i])
                    return false;
            }

            return true;
        }

        /// <summary>Places the current valid actions on the half circle, left to right; unused slots are hidden.</summary>
        private void Layout()
        {
            _count = Mathf.Min(_interactor.MenuActions.Count, MaxOptions);
            float sector = RadialMenuLayout.SectorAngle(_count);
            float labelRadius = (_outerRadius + _innerRadius) * 0.5f;

            for (int i = 0; i < MaxOptions; i++)
            {
                bool used = i < _count;
                _sectors[i].gameObject.SetActive(used);
                _labels[i].gameObject.SetActive(used);
                if (!used)
                    continue;

                _shown[i] = _interactor.MenuActions[i];
                _labels[i].text = GetLabel(_shown[i]);
                _labels[i].rectTransform.anchoredPosition = RadialMenuLayout.CenterDirection(i, _count) * labelRadius;

                // Filled clockwise from the left: rotating clockwise by the sectors before it puts it in place.
                _sectors[i].fillAmount = Mathf.Max(0f, sector - _gap) / 360f;
                _sectors[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, -(i * sector + _gap * 0.5f));
            }

            _hovered = -2;
            SetHovered(-1);
        }

        private void SetHovered(int index)
        {
            if (index == _hovered)
                return;

            _hovered = index;
            for (int i = 0; i < _count; i++)
            {
                bool hovered = i == index;
                _sectors[i].color = hovered ? _highlightColor : _optionColor;
                _labels[i].color = hovered ? _highlightTextColor : Color.white;
                _labels[i].fontStyle = hovered ? FontStyle.Bold : FontStyle.Normal;
            }
        }

        private static string GetLabel(PumpMenuAction action)
        {
            switch (action)
            {
                case PumpMenuAction.TurnOn: return "Turn On";
                case PumpMenuAction.TurnOff: return "Turn Off";
                case PumpMenuAction.Connect: return "Connect";
                case PumpMenuAction.PickUp: return "Pick Up";
                default: return string.Empty;
            }
        }

        /// <summary>Everything is created once; opening only shows, places and colors it.</summary>
        private void Build()
        {
            _canvas = HudWidgets.CreateCanvas(transform, "Pump Menu Canvas", 20);
            Vector2 size = Vector2.one * (_outerRadius * 2f);
            _root = HudWidgets.CreateRect("Pump Menu", _canvas.transform, Vector2.zero, size);

            Sprite ring = HudWidgets.CreateCircleSprite(256, _innerRadius / _outerRadius);

            _sectors = new Image[MaxOptions];
            _labels = new Text[MaxOptions];
            for (int i = 0; i < MaxOptions; i++)
            {
                Image sector = HudWidgets.CreateImage($"Option {i}", _root, Vector2.zero, size, ring, _optionColor);
                sector.type = Image.Type.Filled;
                sector.fillMethod = Image.FillMethod.Radial360;
                sector.fillOrigin = (int)Image.Origin360.Left;
                sector.fillClockwise = true;
                _sectors[i] = sector;
            }

            // Labels after all sectors, so no sector covers a label.
            for (int i = 0; i < MaxOptions; i++)
                _labels[i] = HudWidgets.CreateText($"Label {i}", _root, Vector2.zero, new Vector2(160f, 30f), 22, TextAnchor.MiddleCenter);

            Text title = HudWidgets.CreateText("Title", _root, new Vector2(0f, 24f), new Vector2(160f, 30f), 20, TextAnchor.MiddleCenter);
            title.text = "Pump";
            title.fontStyle = FontStyle.Bold;

            _root.gameObject.SetActive(false);
        }
    }
}
