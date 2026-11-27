using UnityEngine;
using WaterChaos.Water;

namespace WaterChaos.WaterTransfer
{
    /// <summary>
    /// Drops falling from the free end of a transfer hose while it is carried and its pump is already on:
    /// the pump waits for the hose to be laid out, a little liquid still runs out of the end.
    /// Coloured like the liquid the pump draws. Display only: nothing is drawn or spilled from the reservoirs.
    /// </summary>
    public sealed class TransferHoseDripView : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The hose; empty = the one on this object.")]
        [SerializeField] private TransferHose _hose;

        [Tooltip("Particle material for the drops (e.g. the water splash material).")]
        [SerializeField] private Material _material;

        [Header("Drops")]
        [Tooltip("Drops per second.")]
        [SerializeField, Min(0f)] private float _rate = 30f;

        [Tooltip("Drop size range, in meters (m).")]
        [SerializeField] private Vector2 _size = new Vector2(0.05f, 0.1f);

        [Header("Colors")]
        [SerializeField] private Color _water = new Color(0.45f, 0.75f, 1f, 0.9f);
        [SerializeField] private Color _gasoline = new Color(1f, 0.7f, 0.15f, 0.9f);
        [SerializeField] private Color _toxic = new Color(0.45f, 1f, 0.15f, 0.9f);

        private ParticleSystem _drops;
        private bool _dripping;

        private void Awake()
        {
            if (_hose == null)
                _hose = GetComponent<TransferHose>();
            if (_hose == null || _hose.End == null)
            {
                Debug.LogError($"{name}: TransferHoseDripView needs a TransferHose with an End.", this);
                enabled = false;
                return;
            }

            _drops = CreateDrops(_hose.End);
        }

        private void Update()
        {
            TransferConnectionPoint point = _hose.ConnectedPoint;
            bool drip = _hose.State == TransferHoseState.Carried && point != null && point.IsTurnedOn && point.HasWater;

            if (drip)
            {
                ParticleSystem.MainModule main = _drops.main;
                main.startColor = ColorOf(point.Pump != null ? point.Pump.Liquid : LiquidType.Water);
            }

            if (drip == _dripping)
                return;

            _dripping = drip;
            ParticleSystem.EmissionModule emission = _drops.emission;
            emission.enabled = drip;
        }

        private Color ColorOf(LiquidType liquid)
        {
            switch (liquid)
            {
                case LiquidType.Gasoline: return _gasoline;
                case LiquidType.Toxic: return _toxic;
                default: return _water;
            }
        }

        private ParticleSystem CreateDrops(Transform end)
        {
            GameObject go = new GameObject("Drips");
            go.transform.SetParent(end, false);

            ParticleSystem drops = go.AddComponent<ParticleSystem>();
            drops.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = drops.main;
            main.loop = true;
            main.startLifetime = 0.9f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(_size.x, _size.y);
            main.gravityModifier = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 120;
            main.startColor = _water;

            ParticleSystem.EmissionModule emission = drops.emission;
            emission.rateOverTime = _rate;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = drops.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.04f;

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            if (_material != null)
                renderer.sharedMaterial = _material;

            drops.Play();
            return drops;
        }
    }
}
