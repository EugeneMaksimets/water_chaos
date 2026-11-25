using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WaterChaos.Interaction;
using WaterChaos.Player;

namespace WaterChaos.UI
{
    /// <summary>
    /// The only interaction UI. While actions are available, a primary-button prompt (left mouse button) shows the
    /// first primary-button action, which the primary button performs directly. Actions marked for E (Pick Up) are
    /// performed by E directly and shown on the "E" prompt; without one, the "E" prompt offers the menu.
    /// E opens a half-circle menu with every action <see cref="PlayerInteraction"/> offers right now.
    /// The mouse direction from the screen center highlights one, a click performs it and closes the menu.
    /// E again or Escape closes it; it also closes when the target changes or nothing is left to do.
    /// While open: the cursor is free, gameplay input is blocked in <see cref="PlayerInputHandler"/> and the listed
    /// player behaviours (look, movement, drop, slot keys) are switched off, so the mouse and the click never act
    /// on the game underneath. Action providers in that list stay on, so their actions stay in the menu.
    /// Generic: it knows nothing about pumps, hoses or items — only labels and actions.
    /// </summary>
    public sealed class RadialInteractionMenu : MonoBehaviour
    {
        private const int MaxOptions = 6;
        private const float PromptY = -90f;
        private const float PromptPairX = 110f;
        private const string MenuText = "More";

        [Header("References")]
        [SerializeField] private PlayerInputHandler _input;
        [SerializeField] private PlayerInteraction _interaction;

        [Tooltip("Player behaviours switched off while the menu is open, e.g. PlayerLook, PlayerMovement, PlayerItemThrower (drop), PlayerInventory. Action providers listed here are skipped.")]
        [SerializeField] private Behaviour[] _blockWhileOpen = new Behaviour[0];

        [Header("Look")]
        [Tooltip("Outer radius of the half circle, in canvas pixels (1920x1080).")]
        [SerializeField, Min(50f)] private float _outerRadius = 220f;

        [Tooltip("Inner radius of the half circle, in canvas pixels.")]
        [SerializeField, Min(10f)] private float _innerRadius = 90f;

        [Tooltip("Gap between options, in degrees (°).")]
        [SerializeField, Range(0f, 10f)] private float _gap = 3f;

        [Tooltip("Pointer closer to the center than this picks nothing, in canvas pixels.")]
        [SerializeField, Min(0f)] private float _deadZone = 40f;

        [SerializeField] private Color _optionColor = new Color(0f, 0f, 0f, 0.55f);
        [SerializeField] private Color _highlightColor = new Color(1f, 1f, 1f, 0.9f);
        [SerializeField] private Color _highlightTextColor = new Color(0.08f, 0.08f, 0.08f);

        private readonly InteractionAction[] _shown = new InteractionAction[MaxOptions];
        private readonly bool[] _wasEnabled = new bool[32];

        private Canvas _canvas;
        private RectTransform _root;
        private Image[] _sectors;
        private Text[] _labels;
        private Text _title;
        private KeyPrompt _prompt;
        private KeyPrompt _menuPrompt;
        private int _count;
        private int _hovered = -1;
        private Transform _openFocus;
        private readonly PromptText _primaryText = new PromptText();
        private readonly PromptText _interactText = new PromptText();

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            if (_input == null || _interaction == null)
            {
                Debug.LogError($"{name}: RadialInteractionMenu needs Input and Interaction references.", this);
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
            _input.PrimaryPressed += OnPrimaryPressed;
            _input.PointerReleased += OnClick;
        }

        private void OnDisable()
        {
            if (_input != null)
            {
                _input.InteractPressed -= OnInteractPressed;
                _input.CancelPressed -= Close;
                _input.PrimaryPressed -= OnPrimaryPressed;
                _input.PointerReleased -= OnClick;
            }

            Close();
            _prompt?.Hide();
            _menuPrompt?.Hide();
        }

        // LateUpdate: PlayerInteraction has gathered this frame's actions in Update.
        private void LateUpdate()
        {
            if (!IsOpen)
            {
                UpdatePrompt();
                return;
            }

            // Out of reach, another target or nothing left to do (e.g. the pump was taken away): close without acting.
            if (_interaction.Focus != _openFocus || _interaction.Actions.Count == 0)
            {
                Close();
                return;
            }

            if (!SameAsShown())
                Layout();

            Vector2 offset = (_input.PointerPosition - new Vector2(Screen.width, Screen.height) * 0.5f) / _canvas.scaleFactor;
            SetHovered(RadialMenuLayout.GetIndex(offset, _count, _deadZone));
        }

        /// <summary>
        /// Primary prompt with the first primary-button action; the E prompt with the E action (Pick Up)
        /// when there is one, else "More" when the menu has more to offer.
        /// </summary>
        private void UpdatePrompt()
        {
            IReadOnlyList<InteractionAction> actions = _interaction.Actions;
            if (actions.Count == 0)
            {
                _prompt.Hide();
                _menuPrompt.Hide();
                return;
            }

            int primary = PlayerInteraction.IndexOf(actions, InteractionButton.Primary);
            int interact = PlayerInteraction.IndexOf(actions, InteractionButton.Interact);

            // A learned action (e.g. "Water On/Off" after first use) still works on the primary button, without its prompt.
            bool showPrimary = primary >= 0 && actions[primary].ShowPrompt;
            bool showInteract = interact >= 0 && actions[interact].ShowPrompt;
            bool showMenu = showInteract || (interact < 0 && actions.Count > 1);

            if (showPrimary)
                _prompt.Show(GetText(actions[primary], _primaryText), showMenu ? -PromptPairX : 0f, 1f);
            else
                _prompt.Hide();

            if (showMenu)
            {
                string text = showInteract ? GetText(actions[interact], _interactText) : MenuText;
                _menuPrompt.Show(text, showPrimary ? PromptPairX : 0f, 1f);
            }
            else
            {
                _menuPrompt.Hide();
            }
        }

        /// <summary>"Connect — Nozzle": the action and, when there is one, the object it is about.</summary>
        private string GetText(InteractionAction action, PromptText cache)
        {
            if (_interaction.Focus == null || _interaction.Title == action.Label)
                return action.Label;

            return cache.Get(action.Label, _interaction.Title);
        }

        /// <summary>Menu closed: the primary button performs the first primary-button action (never Pick Up).</summary>
        private void OnPrimaryPressed()
        {
            if (IsOpen || !_interaction.isActiveAndEnabled)
                return;

            int index = PlayerInteraction.IndexOf(_interaction.Actions, InteractionButton.Primary);
            if (index >= 0)
                _interaction.Actions[index].Execute();
        }

        /// <summary>E closes the open menu; otherwise performs the E action (Pick Up) if there is one, else opens the menu.</summary>
        private void OnInteractPressed()
        {
            if (IsOpen)
            {
                Close();
                return;
            }

            if (!_interaction.isActiveAndEnabled || _interaction.Actions.Count == 0)
                return;

            int index = PlayerInteraction.IndexOf(_interaction.Actions, InteractionButton.Interact);
            if (index >= 0)
                _interaction.Actions[index].Execute();
            else
                Open();
        }

        private void OnClick()
        {
            if (!IsOpen || _hovered < 0)
                return;

            InteractionAction action = _shown[_hovered];
            Close();
            action.Execute();
        }

        private void Open()
        {
            IsOpen = true;
            _openFocus = _interaction.Focus;
            _input.GameplayBlocked = true;
            _prompt.Hide();
            _menuPrompt.Hide();

            for (int i = 0; i < _blockWhileOpen.Length && i < _wasEnabled.Length; i++)
            {
                Behaviour behaviour = _blockWhileOpen[i];
                // Switching off a provider would remove its actions from the open menu.
                _wasEnabled[i] = behaviour != null && behaviour.enabled && !(behaviour is IInteractionProvider);
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
            _openFocus = null;
            _input.GameplayBlocked = false;
            _root.gameObject.SetActive(false);

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
            int count = Mathf.Min(_interaction.Actions.Count, MaxOptions);
            if (count != _count)
                return false;

            for (int i = 0; i < _count; i++)
            {
                InteractionAction action = _interaction.Actions[i];
                if (action.Label != _shown[i].Label || action.Target != _shown[i].Target)
                    return false;
            }

            return true;
        }

        /// <summary>Places the current actions on the half circle, left to right; unused slots are hidden.</summary>
        private void Layout()
        {
            _count = Mathf.Min(_interaction.Actions.Count, MaxOptions);
            _title.text = _interaction.Title;

            float sector = RadialMenuLayout.SectorAngle(_count);
            float labelRadius = (_outerRadius + _innerRadius) * 0.5f;

            for (int i = 0; i < MaxOptions; i++)
            {
                bool used = i < _count;
                _sectors[i].gameObject.SetActive(used);
                _labels[i].gameObject.SetActive(used);
                if (!used)
                {
                    _shown[i] = default;
                    continue;
                }

                _shown[i] = _interaction.Actions[i];
                _labels[i].text = _shown[i].Label;
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

        /// <summary>Everything is created once; opening only shows, places and colors it.</summary>
        private void Build()
        {
            _canvas = HudWidgets.CreateCanvas(transform, "Interaction Menu Canvas", 20);
            Vector2 size = Vector2.one * (_outerRadius * 2f);
            _root = HudWidgets.CreateRect("Interaction Menu", _canvas.transform, Vector2.zero, size);

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
                _labels[i] = HudWidgets.CreateText($"Label {i}", _root, Vector2.zero, new Vector2(160f, 30f), 20, TextAnchor.MiddleCenter);

            _title = HudWidgets.CreateText("Title", _root, new Vector2(0f, 24f), new Vector2(170f, 30f), 20, TextAnchor.MiddleCenter);
            _title.fontStyle = FontStyle.Bold;

            _root.gameObject.SetActive(false);

            _prompt = HudWidgets.CreateKeyPrompt("Primary Prompt", _canvas.transform, PlayerControl.Primary, new Vector2(0f, PromptY));
            _menuPrompt = HudWidgets.CreateKeyPrompt("Menu Prompt", _canvas.transform, PlayerControl.Interact, new Vector2(PromptPairX, PromptY));
        }

        /// <summary>"Label — Title" prompt text, built again only when the label or title changes (no garbage every frame).</summary>
        private sealed class PromptText
        {
            private string _label;
            private string _title;
            private string _text;

            public string Get(string label, string title)
            {
                if (_text == null || _label != label || _title != title)
                {
                    _label = label;
                    _title = title;
                    _text = $"{label} — {title}";
                }

                return _text;
            }
        }
    }
}
