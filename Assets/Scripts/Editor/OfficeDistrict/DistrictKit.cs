using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using WaterChaos.EditorTools.MiniVillage;
using WaterChaos.Interaction;

namespace WaterChaos.EditorTools.OfficeDistrict
{
    /// <summary>
    /// Shared helpers of the office district builder: prefab folders, loading the village's existing props,
    /// baking static scenery, placing instances and making them flammable or water-pushable.
    /// Models, materials and push profiles come from the Mini Village kit (one shared low-poly style).
    /// </summary>
    internal static class DistrictKit
    {
        public const string Folder = "OfficeDistrict";
        public const string FurnitureFolder = Folder + "/Furniture";
        public const string FactoryFolder = Folder + "/Factory";
        public const string PropsFolder = Folder + "/Props";
        public const string BuildingFolder = Folder + "/Buildings";
        public const string DestructionFolder = Folder + "/Destruction";
        public const string EffectFolder = "Assets/WaterChaos/Environment/Prefabs/" + Folder + "/VFX";
        private const string VillagePrefabs = VillageAssets.PrefabFolder;

        /// <summary>Surface heights in meters (m), the same as the village.</summary>
        public const float Road = 0.03f;
        public const float Walk = 0.14f;
        public const float Paving = 0.05f;

        private static readonly Dictionary<string, GameObject> Saved = new Dictionary<string, GameObject>();

        public static void Prepare()
        {
            Saved.Clear();
            Directory.CreateDirectory(EffectFolder);
            AssetDatabase.Refresh();
        }

        // ───────────── Prefabs ─────────────

        /// <summary>Saves a single-mesh prop (see <see cref="VillageAssets.SaveProp"/>) and remembers it by name.</summary>
        public static GameObject SaveProp(string folder, string name, PropModel model, string profile, bool flammable)
        {
            GameObject prefab = VillageAssets.SaveProp(folder, name, model, profile, flammable);
            Saved[name] = prefab;
            return prefab;
        }

        public static void Remember(string name, GameObject prefab)
        {
            Saved[name] = prefab;
        }

        /// <summary>A prop made by this build, or else an existing village prop ("Interior/Sofa").</summary>
        public static GameObject Get(string name)
        {
            if (Saved.TryGetValue(name, out GameObject prefab) && prefab != null)
                return prefab;

            prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{VillagePrefabs}/{name}.prefab");
            if (prefab == null)
            {
                // A district prop by its bare name ("SecurityFence") when this session has not built it.
                string file = name.Substring(name.LastIndexOf('/') + 1);
                string path = AssetDatabase.FindAssets(file + " t:Prefab", new[] { $"{VillagePrefabs}/{Folder}" })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == file);
                if (path != null)
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            if (prefab == null)
                Debug.LogWarning("Office district: prefab not found " + name);
            return prefab;
        }

        // ───────────── Scene objects ─────────────

        public static Transform Group(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        /// <summary>Static scenery from a model: one mesh object with its box colliders, marked for static batching.</summary>
        public static GameObject Bake(PropModel model, string name, Transform parent, bool colliders = true)
        {
            GameObject go = VillageAssets.CreateMeshObject(name, model.Mesh, name);
            if (colliders)
                VillageAssets.AddColliders(go, model.Colliders);
            go.transform.SetParent(parent, false);
            MarkStatic(go);
            return go;
        }

        public static void MarkStatic(GameObject go)
        {
            const StaticEditorFlags flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic;
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
        }

        /// <summary>Instance of a prefab in local space of <paramref name="parent"/>.</summary>
        public static GameObject Place(GameObject prefab, Transform parent, Vector3 localPosition, float yaw)
        {
            if (prefab == null)
                return null;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetLocalPositionAndRotation(localPosition, Quaternion.Euler(0f, yaw, 0f));
            return go;
        }

        /// <summary>
        /// Furniture instance: <paramref name="flammable"/> lets fire catch it (added on the instance if the prefab
        /// has no <see cref="FlammableObject"/>). Movable props keep their prefab's water push setup.
        /// </summary>
        public static GameObject Furnish(string prop, Transform parent, Vector3 localPosition, float yaw, bool flammable = true)
        {
            GameObject go = Place(Get(prop), parent, localPosition, yaw);
            if (go != null && flammable && go.GetComponent<FlammableObject>() == null)
                go.AddComponent<FlammableObject>();
            return go;
        }

        /// <summary>Copies a model's boxes as rotated child colliders (ramps, slopes).</summary>
        public static void RotatedCollider(Transform parent, string name, Vector3 center, Quaternion rotation, Vector3 size)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.SetLocalPositionAndRotation(center, rotation);
            child.AddComponent<BoxCollider>().size = size;
            MarkStatic(child);
        }

        public static void PointLight(Transform parent, string name, Vector3 localPosition, float range, float intensity, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = range;
            light.intensity = intensity;
            light.color = color;
            light.shadows = LightShadows.None;
        }

        /// <summary>
        /// One temperature zone (<see cref="WaterChaos.Rooms.RoomTemperature"/>) covering <paramref name="local"/>
        /// in the space of <paramref name="pose"/>: used per floor, never per room.
        /// </summary>
        public static void Zone(Transform parent, string displayName, Transform pose, Bounds local)
        {
            int layer = LayerMask.NameToLayer("Room");
            var go = new GameObject(displayName) { layer = layer < 0 ? 0 : layer };
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pose.TransformPoint(local.center), pose.rotation);

            // RequireComponent adds the trigger BoxCollider and the kinematic Rigidbody.
            var temperature = go.AddComponent<WaterChaos.Rooms.RoomTemperature>();
            var box = go.GetComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = Vector3.zero;
            box.size = local.size;
            var body = go.GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            var data = new SerializedObject(temperature);
            data.FindProperty("_displayName").stringValue = displayName;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>A fire axe lying on its side at a world position.</summary>
        public static GameObject PlaceAxe(Transform parent, Vector3 position, float yaw)
        {
            GameObject axe = Place(Get("Axe"), parent, Vector3.zero, 0f);
            if (axe != null)
                axe.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 90f));
            return axe;
        }

        public static void SetReference(Object target, string field, Object value)
        {
            var data = new SerializedObject(target);
            data.FindProperty(field).objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetFloat(Object target, string field, float value)
        {
            var data = new SerializedObject(target);
            data.FindProperty(field).floatValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        // ───────────── Effects ─────────────

        /// <summary>
        /// One-shot burst of low-poly chunks that shrink away and remove themselves (door splinters).
        /// Created once; kept if it already exists so it can be tuned.
        /// </summary>
        public static GameObject Burst(string name, string material, int count, Vector3 area)
        {
            string path = $"{EffectFolder}/{name}.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
                return existing;

            GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh cube = primitive.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(primitive);

            var go = new GameObject(name);
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.playOnAwake = true;
            main.duration = 1f;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
            main.startSizeY = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
            main.startSizeZ = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 1f;
            main.maxParticles = count + 4;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = area;

            ParticleSystem.CollisionModule collision = particles.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.quality = ParticleSystemCollisionQuality.Low;
            collision.dampen = 0.5f;
            collision.bounce = 0.2f;

            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.7f, 1f), new Keyframe(1f, 0f)));

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = cube;
            renderer.sharedMaterial = VillagePalette.Get(material);
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;
        }

        public static Bounds WorldBounds(GameObject go)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>()
                .Where(r => !(r is ParticleSystemRenderer) && !(r is TrailRenderer) && !(r is LineRenderer)).ToArray();
            if (renderers.Length == 0)
                return new Bounds(go.transform.position, Vector3.zero);
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers)
                bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
    }
}
