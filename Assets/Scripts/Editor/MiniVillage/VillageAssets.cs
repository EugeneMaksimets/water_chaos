using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using WaterChaos.Interaction;

namespace WaterChaos.EditorTools.MiniVillage
{
    /// <summary>Model under construction: its mesh plus the box colliders that approximate it.</summary>
    internal sealed class PropModel
    {
        public readonly LowPolyMesh Mesh = new LowPolyMesh();
        public readonly List<Bounds> Colliders = new List<Bounds>();
        /// <summary>Sphere colliders as (centre, radius) — for balls that should roll.</summary>
        public readonly List<Vector4> Spheres = new List<Vector4>();
        /// <summary>Use one convex MeshCollider of the whole model instead of boxes (round objects that roll).</summary>
        public bool ConvexCollider;

        /// <summary>Adds a box; with <paramref name="collide"/> the same box becomes a collider.</summary>
        public void Part(Vector3 center, Vector3 size, string material, bool collide = false)
        {
            Mesh.Box(center, size, material);
            if (collide)
                Colliders.Add(new Bounds(center, size));
        }

        public void Collider(Vector3 center, Vector3 size)
        {
            Colliders.Add(new Bounds(center, size));
        }
    }

    /// <summary>
    /// Creates the reusable assets of the village: water push profiles, meshes, prefabs,
    /// breakable windows and hinged doors. Everything lives under Assets/WaterChaos/Environment.
    /// </summary>
    internal static class VillageAssets
    {
        public const string Root = VillagePalette.Root;
        public const string ModelFolder = Root + "/Models";
        public const string PrefabFolder = Root + "/Prefabs";
        public const string ProfileFolder = Root + "/PhysicsProfiles";

        public const string LightPlastic = "LightPlastic";
        public const string SmallDecor = "SmallDecor";
        public const string LightFurniture = "LightFurniture";
        public const string Furniture = "Furniture";
        public const string Heavy = "Heavy";
        public const string Tall = "Tall";
        public const string Sign = "Sign";
        public const string Door = "Door";

        // Layer for glass shards: the water stream and player raycasts ignore it, physics does not.
        private const int ShardLayer = 2;

        private static readonly Dictionary<string, WaterPushProfile> Profiles = new Dictionary<string, WaterPushProfile>();

        public static void Prepare()
        {
            Profiles.Clear();
            _wet = null;
            _text = null;
            foreach (string folder in new[] { ModelFolder, PrefabFolder, ProfileFolder })
                Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();

            // mass kg, linear damping, angular damping, max speed m/s, min force N, force scale.
            CreateProfile(LightPlastic, 1.5f, 0.4f, 0.4f, 7f, 0f, 0.05f);
            CreateProfile(SmallDecor, 2.5f, 0.3f, 0.3f, 6f, 10f, 0.08f);
            CreateProfile(LightFurniture, 6f, 0.3f, 0.3f, 6f, 20f, 0.25f);
            CreateProfile(Furniture, 18f, 0.3f, 0.3f, 5f, 40f, 1f);
            CreateProfile(Heavy, 55f, 0.4f, 0.4f, 3f, 110f, 2f);
            CreateProfile(Tall, 10f, 0.2f, 0.1f, 6f, 25f, 0.45f);
            CreateProfile(Sign, 15f, 0.2f, 0.1f, 5f, 50f, 0.6f);
            CreateProfile(Door, 20f, 0.5f, 0.5f, 4f, 5f, 0.5f);
            AssetDatabase.SaveAssets();
        }

        public static WaterPushProfile Profile(string key)
        {
            return Profiles[key];
        }

        // ───────────── Props ─────────────

        /// <summary>
        /// Saves a single-mesh prop as a prefab. With a profile it becomes a water-pushable rigid body;
        /// without one it is static scenery that still blocks the water.
        /// </summary>
        public static GameObject SaveProp(string folder, string name, PropModel model, string profile, bool flammable = false)
        {
            GameObject root = CreateMeshObject(name, model.Mesh, name);
            AddColliders(root, model.Colliders);
            foreach (Vector4 sphere in model.Spheres)
            {
                var collider = root.AddComponent<SphereCollider>();
                collider.center = sphere;
                collider.radius = sphere.w;
            }

            if (model.ConvexCollider)
            {
                var collider = root.AddComponent<MeshCollider>();
                collider.sharedMesh = root.GetComponent<MeshFilter>().sharedMesh;
                collider.convex = true;
            }

            if (profile != null)
                MakePushable(root, profile);

            if (flammable)
                root.AddComponent<FlammableObject>();

            return SavePrefab(root, folder, name);
        }

        public static GameObject CreateMeshObject(string name, LowPolyMesh mesh, string meshAssetName)
        {
            var go = new GameObject(name);
            Mesh asset = SaveMesh(mesh, meshAssetName, name, out string[] materials);
            go.AddComponent<MeshFilter>().sharedMesh = asset;
            go.AddComponent<MeshRenderer>().sharedMaterials = VillagePalette.Get(materials);
            return go;
        }

        public static void AddColliders(GameObject go, List<Bounds> colliders)
        {
            foreach (Bounds bounds in colliders)
            {
                var box = go.AddComponent<BoxCollider>();
                box.center = bounds.center;
                box.size = bounds.size;
            }
        }

        public static void MakePushable(GameObject go, string profile)
        {
            var body = go.AddComponent<Rigidbody>();
            WaterPushProfile settings = Profiles[profile];
            body.mass = settings.Mass;
            body.linearDamping = settings.LinearDamping;
            body.angularDamping = settings.AngularDamping;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            // Wet props slide: lower friction than the default material.
            foreach (Collider collider in go.GetComponents<Collider>())
                collider.sharedMaterial = WetMaterial();

            var handler = go.AddComponent<WaterPushHandler>();
            var data = new SerializedObject(handler);
            data.FindProperty("_profile").objectReferenceValue = settings;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        public static GameObject SavePrefab(GameObject root, string folder, string name)
        {
            string directory = $"{PrefabFolder}/{folder}";
            Directory.CreateDirectory(directory);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{directory}/{name}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        /// <summary>Saves the mesh as Models/&lt;asset&gt;.asset, or adds it to that file if it already holds this build's meshes.</summary>
        public static Mesh SaveMesh(LowPolyMesh model, string assetName, string meshName, out string[] materials)
        {
            Mesh mesh = model.ToMesh(meshName, out materials);
            string path = $"{ModelFolder}/{assetName}.asset";

            if (!SavedThisBuild.Contains(path))
            {
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(mesh, path);
                SavedThisBuild.Add(path);
            }
            else
            {
                AssetDatabase.AddObjectToAsset(mesh, path);
            }

            return mesh;
        }

        public static readonly HashSet<string> SavedThisBuild = new HashSet<string>();

        // ───────────── Breakable windows ─────────────

        /// <summary>
        /// Window with a static frame and a breakable glass pane (WaterBreakableGlass) that has prepared,
        /// inactive shards. The prefab's origin is the opening's centre; the glass faces -Z.
        /// </summary>
        public static GameObject SaveWindow(string name, float width, float height, string frameMaterial,
            string shutterMaterial, bool mullions, int shardColumns, int shardRows,
            float minForce, float crackImpulse, float breakImpulse)
        {
            const float frame = 0.09f;
            // 1 cm proud of a 0.3 m wall on each side: a frame exactly as deep as the wall shares its faces and flickers.
            const float depth = 0.32f;
            var frameModel = new LowPolyMesh();
            float hw = width * 0.5f;
            float hh = height * 0.5f;

            // Frame around the opening, a sill, and optional cross bars and shutters.
            // The frame reaches 1.5 cm into the opening: its inner faces must not lie in the plane of the
            // wall's opening edges, or the two surfaces flicker (z-fighting).
            const float overlap = 0.015f;
            frameModel.Box(new Vector3(0f, hh + frame * 0.5f - overlap, 0f), new Vector3(width + frame * 2f, frame, depth), frameMaterial);
            frameModel.Box(new Vector3(0f, -hh - frame * 0.5f + overlap, 0f), new Vector3(width + frame * 2f, frame, depth), frameMaterial);
            frameModel.Box(new Vector3(-hw - frame * 0.5f + overlap, 0f, 0f), new Vector3(frame, height, depth), frameMaterial);
            frameModel.Box(new Vector3(hw + frame * 0.5f - overlap, 0f, 0f), new Vector3(frame, height, depth), frameMaterial);
            frameModel.Box(new Vector3(0f, -hh - frame - 0.03f, -0.08f), new Vector3(width + 0.35f, 0.06f, 0.3f), frameMaterial);

            if (shutterMaterial != null)
            {
                float shutterWidth = width * 0.42f;
                foreach (float side in new[] { -1f, 1f })
                {
                    float x = side * (hw + frame + 0.06f + shutterWidth * 0.5f);
                    frameModel.Box(new Vector3(x, 0f, -0.14f), new Vector3(shutterWidth, height + 0.1f, 0.05f), shutterMaterial);
                    for (int slat = 0; slat < 5; slat++)
                    {
                        float y = -hh + (slat + 0.5f) * height / 5f;
                        frameModel.Box(new Vector3(x, y, -0.17f), new Vector3(shutterWidth * 0.8f, 0.04f, 0.02f), shutterMaterial);
                    }
                }
            }

            GameObject root = CreateMeshObject(name, frameModel, name);

            // Glass pane: its own collider, so the stream reports hits to WaterBreakableGlass.
            var paneModel = new LowPolyMesh();
            paneModel.Box(Vector3.zero, new Vector3(width, height, 0.02f), "Glass");
            GameObject glass = CreateMeshObject("Glass", paneModel, name);
            glass.transform.SetParent(root.transform, false);
            var paneCollider = glass.AddComponent<BoxCollider>();
            paneCollider.size = new Vector3(width, height, 0.06f);

            GameObject cracks = CreateMeshObject("Cracks", BuildCracks(width, height), name);
            cracks.transform.SetParent(glass.transform, false);

            var shards = new List<Rigidbody>();
            foreach (Vector2[] shard in BuildShardOutlines(width, height, shardColumns, shardRows))
                shards.Add(CreateShard(glass.transform, name, shard, shards.Count));

            // Window bars break with the glass: the intact cross is hidden and its four arms fall as fragments.
            GameObject bars = null;
            if (mullions)
            {
                const float bar = 0.05f;
                const float barZ = -0.05f;
                var barsModel = new LowPolyMesh();
                barsModel.Box(new Vector3(0f, 0f, barZ), new Vector3(bar, height, bar), frameMaterial);
                barsModel.Box(new Vector3(0f, 0f, barZ), new Vector3(width, bar, bar), frameMaterial);
                bars = CreateMeshObject("Bars", barsModel, name);
                bars.transform.SetParent(glass.transform, false);

                float arm = hh - bar * 0.5f;
                shards.Add(CreateBarPiece(glass.transform, name, frameMaterial, new Vector3(0f, (hh + bar * 0.5f) * 0.5f, barZ), new Vector3(bar, arm, bar), shards.Count));
                shards.Add(CreateBarPiece(glass.transform, name, frameMaterial, new Vector3(0f, -(hh + bar * 0.5f) * 0.5f, barZ), new Vector3(bar, arm, bar), shards.Count));
                shards.Add(CreateBarPiece(glass.transform, name, frameMaterial, new Vector3(-hw * 0.5f, 0f, barZ), new Vector3(hw, bar, bar), shards.Count));
                shards.Add(CreateBarPiece(glass.transform, name, frameMaterial, new Vector3(hw * 0.5f, 0f, barZ), new Vector3(hw, bar, bar), shards.Count));
            }

            var breakable = glass.AddComponent<WaterBreakableGlass>();
            var data = new SerializedObject(breakable);
            data.FindProperty("_minImpactForce").floatValue = minForce;
            data.FindProperty("_crackImpulse").floatValue = crackImpulse;
            data.FindProperty("_breakImpulse").floatValue = breakImpulse;
            data.FindProperty("_pane").objectReferenceValue = glass.GetComponent<MeshRenderer>();
            data.FindProperty("_cracks").objectReferenceValue = cracks;
            SerializedProperty breakOff = data.FindProperty("_breakOff");
            breakOff.arraySize = bars != null ? 1 : 0;
            if (bars != null)
                breakOff.GetArrayElementAtIndex(0).objectReferenceValue = bars;
            SerializedProperty fragments = data.FindProperty("_fragments");
            fragments.arraySize = shards.Count;
            for (int i = 0; i < shards.Count; i++)
                fragments.GetArrayElementAtIndex(i).objectReferenceValue = shards[i];
            data.ApplyModifiedPropertiesWithoutUndo();

            return SavePrefab(root, "Windows", name);
        }

        private static LowPolyMesh BuildCracks(float width, float height)
        {
            var mesh = new LowPolyMesh();
            var random = new System.Random(Mathf.RoundToInt(width * 100f + height * 10f));
            var origin = new Vector2(width * 0.12f, height * 0.08f);
            float maxLength = Mathf.Min(width, height) * 0.55f;

            for (int i = 0; i < 7; i++)
            {
                float angle = i * 360f / 7f + (float)random.NextDouble() * 30f;
                float length = maxLength * (0.5f + (float)random.NextDouble() * 0.5f);
                AddCrackLine(mesh, origin, angle, length, width, height);

                // A short branch from the middle of every second line.
                if (i % 2 == 0)
                {
                    Vector2 middle = origin + Direction(angle) * length * 0.55f;
                    AddCrackLine(mesh, middle, angle + 35f, length * 0.35f, width, height);
                }
            }

            return mesh;
        }

        private static void AddCrackLine(LowPolyMesh mesh, Vector2 from, float angle, float length, float width, float height)
        {
            Vector2 to = from + Direction(angle) * length;
            to.x = Mathf.Clamp(to.x, -width * 0.48f, width * 0.48f);
            to.y = Mathf.Clamp(to.y, -height * 0.48f, height * 0.48f);
            Vector2 delta = to - from;
            if (delta.magnitude < 0.02f)
                return;

            float rotation = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            Vector2 middle = (from + to) * 0.5f;
            mesh.SetTransform(new Vector3(middle.x, middle.y, 0f), Quaternion.Euler(0f, 0f, rotation));
            mesh.Box(Vector3.zero, new Vector3(delta.magnitude, 0.012f, 0.03f), "GlassCrack");
            mesh.ResetTransform();
        }

        private static Vector2 Direction(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        /// <summary>Pane split into a jittered grid; every cell becomes two triangular shards.</summary>
        private static List<Vector2[]> BuildShardOutlines(float width, float height, int columns, int rows)
        {
            var random = new System.Random(columns * 31 + rows * 7 + Mathf.RoundToInt(width * 10f));
            var points = new Vector2[columns + 1, rows + 1];
            for (int x = 0; x <= columns; x++)
            {
                for (int y = 0; y <= rows; y++)
                {
                    var point = new Vector2(-width * 0.5f + width * x / columns, -height * 0.5f + height * y / rows);
                    bool inner = x > 0 && x < columns && y > 0 && y < rows;
                    if (inner)
                    {
                        point.x += ((float)random.NextDouble() - 0.5f) * width / columns * 0.5f;
                        point.y += ((float)random.NextDouble() - 0.5f) * height / rows * 0.5f;
                    }
                    points[x, y] = point;
                }
            }

            var shards = new List<Vector2[]>();
            for (int x = 0; x < columns; x++)
            {
                for (int y = 0; y < rows; y++)
                {
                    bool flip = (x + y) % 2 == 0;
                    Vector2 a = points[x, y], b = points[x + 1, y], c = points[x + 1, y + 1], d = points[x, y + 1];
                    if (flip)
                    {
                        shards.Add(new[] { a, b, c });
                        shards.Add(new[] { a, c, d });
                    }
                    else
                    {
                        shards.Add(new[] { a, b, d });
                        shards.Add(new[] { b, c, d });
                    }
                }
            }

            return shards;
        }

        private static Rigidbody CreateShard(Transform glass, string assetName, Vector2[] outline, int index)
        {
            Vector2 centroid = (outline[0] + outline[1] + outline[2]) / 3f;
            var local = new Vector2[outline.Length];
            for (int i = 0; i < outline.Length; i++)
                local[i] = outline[i] - centroid;

            var mesh = new LowPolyMesh();
            mesh.Extrude(local, -0.01f, 0.01f, "Glass");
            GameObject shard = CreateMeshObject("Shard_" + index, mesh, assetName);
            shard.layer = ShardLayer;
            shard.transform.SetParent(glass, false);
            shard.transform.localPosition = new Vector3(centroid.x, centroid.y, 0f);

            // Exact convex hull (6 vertices): box colliders of neighbouring triangles would overlap and jam.
            var hull = shard.AddComponent<MeshCollider>();
            hull.sharedMesh = shard.GetComponent<MeshFilter>().sharedMesh;
            hull.convex = true;

            var body = shard.AddComponent<Rigidbody>();
            body.mass = 0.15f;
            body.isKinematic = true;
            body.linearDamping = 0.1f;
            body.angularDamping = 0.2f;
            shard.SetActive(false);
            return body;
        }

        private static Rigidbody CreateBarPiece(Transform glass, string assetName, string material, Vector3 center, Vector3 size, int index)
        {
            var mesh = new LowPolyMesh();
            mesh.Box(Vector3.zero, size, material);
            GameObject piece = CreateMeshObject("BarPiece_" + index, mesh, assetName);
            piece.layer = ShardLayer;
            piece.transform.SetParent(glass, false);
            piece.transform.localPosition = center;
            piece.AddComponent<BoxCollider>().size = size;

            var body = piece.AddComponent<Rigidbody>();
            body.mass = 0.3f;
            body.isKinematic = true;
            piece.SetActive(false);
            return body;
        }

        // ───────────── Doors ─────────────

        /// <summary>
        /// Hinged door leaf: pivot on the hinge edge, the leaf extends along +X.
        /// The hinge has no angle limits: stop strips in the door opening and the walls stop the leaf.
        /// </summary>
        public static GameObject SaveDoor(string name, float width, float height, string material, bool glassPanel)
        {
            const float thickness = 0.07f;
            var model = new PropModel();
            model.Part(new Vector3(width * 0.5f, height * 0.5f, 0f), new Vector3(width, height, thickness), material, true);

            if (glassPanel)
            {
                model.Part(new Vector3(width * 0.5f, height * 0.62f, 0f), new Vector3(width * 0.7f, height * 0.5f, thickness + 0.01f), "Glass");
            }
            else
            {
                foreach (float y in new[] { 0.3f, 0.72f })
                {
                    foreach (float side in new[] { -1f, 1f })
                        model.Part(new Vector3(width * 0.5f, height * y, side * thickness * 0.55f), new Vector3(width * 0.65f, height * 0.3f, 0.02f), material);
                }
            }

            foreach (float side in new[] { -1f, 1f })
                model.Mesh.Sphere(new Vector3(width - 0.12f, height * 0.47f, side * 0.07f), Vector3.one * 0.045f, 6, 4, "Metal");

            GameObject root = CreateMeshObject(name, model.Mesh, name);
            AddColliders(root, model.Colliders);
            MakePushable(root, Door);

            var hinge = root.AddComponent<HingeJoint>();
            hinge.anchor = new Vector3(0f, height * 0.5f, 0f);
            hinge.axis = Vector3.up;
            hinge.useLimits = false;

            return SavePrefab(root, "Doors", name);
        }

        // ───────────── Signs ─────────────

        /// <summary>3D text (TextMesh) readable from -Z; <paramref name="height"/> is the letter height in meters.</summary>
        public static void Label(Transform parent, string text, Vector3 localPosition, float height, Color color, Quaternion localRotation)
        {
            var go = new GameObject("Label " + text);
            go.transform.SetParent(parent, false);
            go.transform.SetLocalPositionAndRotation(localPosition, localRotation);

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var mesh = go.AddComponent<TextMesh>();
            mesh.font = font;
            mesh.text = text;
            mesh.fontSize = 100;
            mesh.characterSize = height / 10f;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = TextMaterial(font);
        }

        private static Material _text;

        /// <summary>Font material on WaterChaos/Text3D: depth-tested, so walls and boards hide text behind them.</summary>
        private static Material TextMaterial(Font font)
        {
            if (_text != null)
                return _text;

            string path = VillagePalette.Root + "/Materials/M_Text3D.mat";
            _text = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (_text == null)
            {
                _text = new Material(Shader.Find("WaterChaos/Text3D"));
                AssetDatabase.CreateAsset(_text, path);
            }

            _text.shader = Shader.Find("WaterChaos/Text3D");
            _text.mainTexture = font.material.mainTexture;
            EditorUtility.SetDirty(_text);
            return _text;
        }

        private static PhysicsMaterial _wet;

        private static PhysicsMaterial WetMaterial()
        {
            if (_wet != null)
                return _wet;

            string path = ProfileFolder + "/PM_WetProp.physicMaterial";
            _wet = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (_wet == null)
            {
                _wet = new PhysicsMaterial("WetProp");
                AssetDatabase.CreateAsset(_wet, path);
            }

            _wet.dynamicFriction = 0.3f;
            _wet.staticFriction = 0.35f;
            _wet.bounciness = 0.05f;
            _wet.frictionCombine = PhysicsMaterialCombine.Minimum;
            EditorUtility.SetDirty(_wet);
            return _wet;
        }

        // ───────────── Profiles ─────────────

        private static void CreateProfile(string key, float mass, float linearDamping, float angularDamping,
            float maxSpeed, float minForce, float forceScale)
        {
            string path = $"{ProfileFolder}/WaterPush_{key}.asset";
            WaterPushProfile profile = AssetDatabase.LoadAssetAtPath<WaterPushProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<WaterPushProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            var data = new SerializedObject(profile);
            data.FindProperty("_mass").floatValue = mass;
            data.FindProperty("_linearDamping").floatValue = linearDamping;
            data.FindProperty("_angularDamping").floatValue = angularDamping;
            data.FindProperty("_maxSpeed").floatValue = maxSpeed;
            data.FindProperty("_pushable").boolValue = true;
            data.FindProperty("_minImpactForce").floatValue = minForce;
            data.FindProperty("_forceMultiplier").floatValue = forceScale;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
            Profiles[key] = profile;
        }
    }
}
