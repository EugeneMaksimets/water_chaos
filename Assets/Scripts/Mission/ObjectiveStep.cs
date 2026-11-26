using System.Collections.Generic;
using UnityEngine;
using WaterChaos.Player;
using WaterChaos.UI;

namespace WaterChaos.Mission
{
    /// <summary>
    /// A step the player has to do: a line in the objective list, markers on the objects it needs
    /// and an optional hint (with a key) near the place where it is done.
    /// Subclasses only answer whether the condition is met (<see cref="Evaluate"/>) and, optionally, the progress text.
    /// </summary>
    public abstract class ObjectiveStep : MissionStep
    {
        [Header("Objective")]
        [Tooltip("Line in the objective list (Ukrainian). Empty = hidden objective: only its condition is waited for.")]
        [SerializeField, TextArea(1, 3)] private string _title;

        [Tooltip("Markers shown while the objective is active.")]
        [SerializeField] private MissionMarker[] _markers = new MissionMarker[0];

        [Header("Hint")]
        [Tooltip("Short hint shown while the objective is active (Ukrainian). Empty = no hint.")]
        [SerializeField, TextArea(1, 3)] private string _hint;

        [Tooltip("Shows the button of the action next to the hint.")]
        [SerializeField] private bool _showHintKey;

        [SerializeField] private PlayerControl _hintKey = PlayerControl.Drop;

        [Tooltip("Optional: the hint shows only while the player is inside this zone.")]
        [SerializeField] private MissionZone _hintZone;

        private readonly List<MissionMarker> _shownMarkers = new List<MissionMarker>();
        private ObjectiveLine _line;
        private string _shownProgress;
        private bool _hintShown;

        protected override void OnBegin()
        {
            if (!string.IsNullOrWhiteSpace(_title))
                _line = Runner.Objectives.Add(_title);

            foreach (MissionMarker marker in _markers)
                ShowMarker(marker);
        }

        protected override void OnTick()
        {
            bool done = Evaluate();

            if (_line != null)
            {
                string progress = Progress;
                if (progress != _shownProgress)
                {
                    _shownProgress = progress;
                    _line.SetProgress(progress);
                }
            }

            UpdateHint(!done);

            if (done)
                Complete();
        }

        protected override void OnEnd(bool completed)
        {
            UpdateHint(false);

            foreach (MissionMarker marker in _shownMarkers)
            {
                if (marker != null)
                    marker.Hide();
            }

            _shownMarkers.Clear();

            if (_line != null)
            {
                if (completed)
                    _line.Complete();
                else
                    _line.Dismiss();
                _line = null;
            }
        }

        /// <summary>True when the objective is met. Called every frame while running.</summary>
        protected abstract bool Evaluate();

        /// <summary>Text after the title, e.g. "1/2"; null = none.</summary>
        protected virtual string Progress => null;

        protected void ShowMarker(MissionMarker marker)
        {
            if (marker == null || _shownMarkers.Contains(marker))
                return;

            marker.Show();
            _shownMarkers.Add(marker);
        }

        protected void HideMarker(MissionMarker marker)
        {
            if (marker != null && _shownMarkers.Remove(marker))
                marker.Hide();
        }

        protected void SetMarkerShown(MissionMarker marker, bool shown)
        {
            if (shown)
                ShowMarker(marker);
            else
                HideMarker(marker);
        }

        private void UpdateHint(bool active)
        {
            bool show = active && !string.IsNullOrWhiteSpace(_hint)
                && (_hintZone == null || _hintZone.Contains(Runner.Player.position));

            if (show == _hintShown)
                return;

            _hintShown = show;
            if (show)
                Runner.Hints.Show(this, _hint, _showHintKey, _hintKey);
            else
                Runner.Hints.Hide(this);
        }
    }
}
