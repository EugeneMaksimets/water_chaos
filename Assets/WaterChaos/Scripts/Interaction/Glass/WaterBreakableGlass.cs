using System.Collections;
using UnityEngine;
using WaterChaos.Water;

namespace WaterChaos.Interaction
{
    /// <summary>
    /// Glass pane that strong water cracks and then shatters (rules in <see cref="GlassPaneModel"/>).
    /// Lives on the pane's collider, because the stream only notifies the collider it hits.
    /// The fragments are prepared inactive children: on breaking they are released with physics,
    /// then shrink and switch off after their lifetime, so nothing is created or destroyed during play.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class WaterBreakableGlass : MonoBehaviour, IWaterImpactReceiver
    {
        [Header("Strength")]
        [Tooltip("Weakest water force that harms the glass, in newtons (N).")]
        [SerializeField, Min(0f)] private float _minImpactForce = 60f;

        [Tooltip("Damage at which the glass cracks, in newton-seconds (N·s).")]
        [SerializeField, Min(0f)] private float _crackImpulse = 120f;

        [Tooltip("Damage at which the glass breaks, in newton-seconds (N·s).")]
        [SerializeField, Min(0f)] private float _breakImpulse = 360f;

        [Header("Visuals")]
        [Tooltip("Renderer of the intact pane; hidden when the glass breaks.")]
        [SerializeField] private Renderer _pane;

        [Tooltip("Crack lines shown while the glass is cracked.")]
        [SerializeField] private GameObject _cracks;

        [Tooltip("Intact parts that go with the glass when it breaks (e.g. window bars); their pieces are among the fragments.")]
        [SerializeField] private GameObject[] _breakOff = new GameObject[0];

        [Header("Fragments")]
        [Tooltip("Inactive child shards released when the glass breaks.")]
        [SerializeField] private Rigidbody[] _fragments = new Rigidbody[0];

        [Tooltip("Impulse given to each shard along the water, in newton-seconds (N·s).")]
        [SerializeField, Min(0f)] private float _fragmentImpulse = 0.6f;

        [Tooltip("Seconds (s) the shards stay before they shrink away.")]
        [SerializeField, Min(0f)] private float _fragmentLifetime = 6f;

        [Tooltip("Seconds (s) the shards take to shrink away.")]
        [SerializeField, Min(0.01f)] private float _shrinkTime = 0.8f;

        private GlassPaneModel _model;
        private Collider _collider;

        public GlassPaneState State => _model.State;

        private void Awake()
        {
            _model = new GlassPaneModel(_minImpactForce, _crackImpulse, _breakImpulse);
            _collider = GetComponent<Collider>();

            if (_cracks != null)
                _cracks.SetActive(false);

            foreach (Rigidbody fragment in _fragments)
            {
                if (fragment != null)
                    fragment.gameObject.SetActive(false);
            }
        }

        // Called during FixedUpdate by WaterStream.
        public void ReceiveImpact(in WaterImpact impact)
        {
            if (!_model.ApplyImpact(impact.Force, Time.fixedDeltaTime))
                return;

            if (_model.State == GlassPaneState.Cracked)
            {
                if (_cracks != null)
                    _cracks.SetActive(true);
                return;
            }

            Shatter(impact);
        }

        private void Shatter(in WaterImpact impact)
        {
            // Broken glass neither shows nor stops the water any more.
            _collider.enabled = false;
            if (_pane != null)
                _pane.enabled = false;
            if (_cracks != null)
                _cracks.SetActive(false);
            foreach (GameObject part in _breakOff)
            {
                if (part != null)
                    part.SetActive(false);
            }

            Vector3 push = impact.Direction.sqrMagnitude > 0f ? impact.Direction.normalized : -transform.forward;

            foreach (Rigidbody fragment in _fragments)
            {
                if (fragment == null)
                    continue;

                fragment.gameObject.SetActive(true);
                fragment.isKinematic = false;
                Vector3 spread = Random.insideUnitSphere * 0.35f;
                fragment.AddForce((push + spread) * _fragmentImpulse, ForceMode.Impulse);
                fragment.AddTorque(Random.insideUnitSphere * _fragmentImpulse * 0.2f, ForceMode.Impulse);
            }

            if (_fragments.Length > 0 && isActiveAndEnabled)
                StartCoroutine(ClearFragments());
        }

        private IEnumerator ClearFragments()
        {
            yield return new WaitForSeconds(_fragmentLifetime);

            Vector3[] sizes = new Vector3[_fragments.Length];
            for (int i = 0; i < _fragments.Length; i++)
            {
                if (_fragments[i] != null)
                    sizes[i] = _fragments[i].transform.localScale;
            }

            for (float t = 0f; t < _shrinkTime; t += Time.deltaTime)
            {
                float k = 1f - t / _shrinkTime;
                for (int i = 0; i < _fragments.Length; i++)
                {
                    if (_fragments[i] != null)
                        _fragments[i].transform.localScale = sizes[i] * k;
                }

                yield return null;
            }

            foreach (Rigidbody fragment in _fragments)
            {
                if (fragment != null)
                    fragment.gameObject.SetActive(false);
            }
        }
    }
}
