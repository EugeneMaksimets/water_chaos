using UnityEngine;
using WaterChaos.Environment;

namespace WaterChaos.Surfaces
{
    /// <summary>Shared tuning of puddles, ice and snowdrifts.</summary>
    [CreateAssetMenu(menuName = "Water Chaos/Liquid Surface Settings", fileName = "LiquidSurfaceSettings")]
    public sealed class LiquidSurfaceSettings : ScriptableObject
    {
        [Header("Where Liquid Lands")]
        [Tooltip("Layers liquid can settle on (floors, ground). Exclude fires, the player and water volumes.")]
        [SerializeField] private LayerMask _surfaceMask = ~0;

        [Tooltip("A surface counts as floor when its normal points at least this much up (0.7 ≈ 45°).")]
        [SerializeField, Range(0f, 1f)] private float _floorMinUp = 0.7f;

        [Tooltip("Liquid hitting a wall or an object runs down onto the floor up to this far below, in meters (m).")]
        [SerializeField, Min(0f)] private float _maxDropToFloor = 3f;

        [Tooltip("Puddles and snowdrifts are drawn this far above the floor, in meters (m), so they never flicker.")]
        [SerializeField, Range(0.001f, 0.1f)] private float _surfaceOffset = 0.015f;

        [Tooltip("Seconds (s) between temperature updates of puddles and snowdrifts.")]
        [SerializeField, Range(0.05f, 2f)] private float _tickInterval = 0.25f;

        [Header("Puddles")]
        [Tooltip("Area one liter covers, in square meters per liter (m²/L): the growth rate.")]
        [SerializeField, Range(0.001f, 0.5f)] private float _puddleAreaPerLiter = 0.1f;

        [Tooltip("Radius of a fresh puddle, in meters (m).")]
        [SerializeField, Range(0.05f, 1f)] private float _puddleMinRadius = 0.4f;

        [Tooltip("A puddle never grows wider than this, in meters (m).")]
        [SerializeField, Range(0.1f, 5f)] private float _puddleMaxRadius = 3f;

        [Tooltip("Liquid landing this far past a puddle's edge joins it instead of starting a new one, in meters (m).")]
        [SerializeField, Range(0f, 2f)] private float _puddleMergeDistance = 0.6f;

        [Tooltip("Most puddles at once; past it the oldest puddle dries up.")]
        [SerializeField, Range(1, 500)] private int _maxPuddles = 150;

        [Header("Ice")]
        [Tooltip("Water puddles freeze in a temperature zone colder than this (°C). Outside every zone nothing freezes.")]
        [SerializeField] private float _freezeBelow = FreezingRules.DefaultFreezingPoint;

        [Tooltip("Ice melts in a zone warmer than this (°C) or outside every zone. Not below the freeze temperature.")]
        [SerializeField] private float _meltAbove = 0.5f;

        [Tooltip("Seconds (s) of steady cold before a puddle turns to ice.")]
        [SerializeField, Range(0f, 30f)] private float _freezeDelay = 1f;

        [Tooltip("Seconds (s) of steady warmth before ice turns back to water.")]
        [SerializeField, Range(0f, 60f)] private float _meltDelay = 3f;

        [Tooltip("Grip on ice compared to normal ground (0..1).")]
        [SerializeField, Range(0f, 1f)] private float _iceFriction = 0.05f;

        [Header("Snowdrifts")]
        [Tooltip("Snow added per liter of water arriving as snow: the accumulation rate.")]
        [SerializeField, Range(0.05f, 10f)] private float _snowPerLiter = 1f;

        [Tooltip("Snow (L) that makes a full-size drift.")]
        [SerializeField, Range(1f, 500f)] private float _snowdriftFullVolume = 20f;

        [Tooltip("Radius of a fresh snowdrift, in meters (m).")]
        [SerializeField, Range(0.05f, 1f)] private float _snowdriftMinRadius = 0.2f;

        [Tooltip("Radius of a full snowdrift, in meters (m).")]
        [SerializeField, Range(0.1f, 3f)] private float _snowdriftMaxRadius = 0.7f;

        [Tooltip("Height of a full snowdrift, in meters (m).")]
        [SerializeField, Range(0.05f, 1.5f)] private float _snowdriftMaxHeight = 0.35f;

        [Tooltip("Snow landing this far past a drift's edge builds it up instead of starting a new one, in meters (m).")]
        [SerializeField, Range(0f, 2f)] private float _snowdriftMergeDistance = 0.3f;

        [Tooltip("Snow melts in a zone warmer than this (°C) or outside every zone.")]
        [SerializeField] private float _snowMeltAbove = FreezingRules.DefaultFreezingPoint;

        [Tooltip("Snow lost while melting, in liters per second (L/s).")]
        [SerializeField, Range(0f, 20f)] private float _snowMeltRate = 0.5f;

        [Tooltip("Share of a full drift (0..1) from which it covers the surface and no new fire can start on it.")]
        [SerializeField, Range(0f, 1f)] private float _snowBlockingFill = 0.05f;

        [Tooltip("Most snowdrifts at once; past it the oldest drift goes.")]
        [SerializeField, Range(1, 500)] private int _maxSnowdrifts = 80;

        public LayerMask SurfaceMask => _surfaceMask;
        public float FloorMinUp => _floorMinUp;
        public float MaxDropToFloor => _maxDropToFloor;
        public float SurfaceOffset => _surfaceOffset;
        public float TickInterval => _tickInterval;
        public int MaxPuddles => _maxPuddles;
        public int MaxSnowdrifts => _maxSnowdrifts;

        public PuddleConfig ToPuddleConfig()
        {
            float maxRadius = Mathf.Max(_puddleMinRadius, _puddleMaxRadius);
            return new PuddleConfig(_puddleAreaPerLiter, _puddleMinRadius, maxRadius, _puddleMergeDistance,
                _freezeBelow, Mathf.Max(_freezeBelow, _meltAbove), _freezeDelay, _meltDelay, _iceFriction);
        }

        public SnowdriftConfig ToSnowdriftConfig()
        {
            float maxRadius = Mathf.Max(_snowdriftMinRadius, _snowdriftMaxRadius);
            return new SnowdriftConfig(_snowPerLiter, _snowdriftFullVolume, _snowdriftMinRadius, maxRadius,
                _snowdriftMaxHeight, _snowdriftMergeDistance, _snowMeltAbove, _snowMeltRate, _snowBlockingFill);
        }
    }
}
