using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using WaterChaos.Interaction;
using WaterChaos.Water;
using WaterChaos.WaterTransfer;

namespace WaterChaos.EditorTools
{
    /// <summary>
    /// One click sets up toxic liquid and corrosion:
    /// materials, corrosion profiles, effects and demo walls (created only if missing, so tuning is kept);
    /// picket fences corrode easily; each house's interior partition becomes its own object that corrodes
    /// (outer, load-bearing walls stay in the house mesh with no corrosion, so they never dissolve);
    /// the nozzle tints the stream toxic green and bubbles where toxic liquid hits;
    /// every reservoir in the open scene tints its liquid look greenish while it holds toxic liquid. Safe to run again.
    /// </summary>
    public static class ToxicLiquidSetup
    {
        private const string MaterialFolder = "Assets/WaterChaos/Materials/Toxic";
        private const string ProfileFolder = "Assets/WaterChaos/ScriptableObjects/Corrosion";
        private const string PrefabFolder = "Assets/WaterChaos/Prefabs/Toxic";
        private const string BuildingFolder = "Assets/WaterChaos/Environment/Prefabs/Buildings";
        private const string EnvironmentMaterialFolder = "Assets/WaterChaos/Environment/Materials";
        private const string FencePath = "Assets/WaterChaos/Environment/Prefabs/Street/PicketFence.prefab";
        private const string NozzlePath = "Assets/WaterChaos/Prefabs/Nozzle.prefab";

        private const string PartitionName = "Partition";
        // Interior partitions are 0.15 m thick (VillageBuildings.BuildPartition); no other house collider is.
        private const float PartitionThickness = 0.15f;

        private struct ToxicAssets
        {
            public CorrosionProfile Fence;
            public CorrosionProfile InteriorWall;
            public CorrosionProfile ReinforcedWall;
            public ParticleSystem ImpactFumes;
            public ParticleSystem WallFumes;
            public GameObject Debris;
        }

        [MenuItem("Water Chaos/Setup Toxic Liquid")]
        public static void Setup()
        {
            ToxicAssets assets = EnsureAssets();
            ApplyToVillagePrefabs(assets);
            SetupNozzle(assets);
            SaveDemoWalls(assets);
            AssetDatabase.SaveAssets();
            SetupReservoirTints();
            Debug.Log("Toxic liquid: set up. Set a reservoir's Liquid to Toxic to try it, then save the scene.");
        }

        /// <summary>
        /// Gives every reservoir look in the scene (the object moved by its level or surface view) a
        /// <see cref="ReservoirLiquidTint"/>. In batch mode opens and saves the Mini Village scene; in the editor
        /// works on the open scene, which you save afterwards.
        /// </summary>
        [MenuItem("Water Chaos/Setup Reservoir Liquid Tint")]
        public static void SetupReservoirTints()
        {
            Scene scene = Application.isBatchMode
                ? EditorSceneManager.OpenScene(MiniVillage.MiniVillageBuilder.ScenePath)
                : SceneManager.GetActiveScene();

            int added = 0;
            foreach (MonoBehaviour view in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!(view is ReservoirLevelView || view is ReservoirSurfaceView) || view.gameObject.scene != scene)
                    continue;
                if (view.GetComponent<ReservoirLiquidTint>() != null)
                    continue;

                var so = new SerializedObject(view);
                var reservoir = so.FindProperty("_reservoir").objectReferenceValue as WaterReservoirComponent;
                SerializedProperty lookProperty = so.FindProperty(view is ReservoirLevelView ? "_water" : "_surface");
                var look = lookProperty != null ? lookProperty.objectReferenceValue as Transform : null;
                if (reservoir == null || look == null)
                    continue;

                var tint = view.gameObject.AddComponent<ReservoirLiquidTint>();
                var tintSo = new SerializedObject(tint);
                tintSo.FindProperty("_reservoir").objectReferenceValue = reservoir;
                Renderer[] renderers = look.GetComponentsInChildren<Renderer>(true);
                SerializedProperty list = tintSo.FindProperty("_renderers");
                list.arraySize = renderers.Length;
                for (int i = 0; i < renderers.Length; i++)
                    list.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
                tintSo.ApplyModifiedPropertiesWithoutUndo();
                added++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (Application.isBatchMode)
                EditorSceneManager.SaveScene(scene);
            Debug.Log($"Toxic liquid: reservoir liquid tint added to {added} reservoir(s) in {scene.name}.");
        }

        /// <summary>Called by the Mini Village builder after it rebuilds the fence and house prefabs.</summary>
        public static void ApplyToVillagePrefabs()
        {
            ApplyToVillagePrefabs(EnsureAssets());
        }

        private static void ApplyToVillagePrefabs(ToxicAssets assets)
        {
            SetupFence(assets);
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { BuildingFolder }))
                SplitPartition(AssetDatabase.GUIDToAssetPath(guid), assets);
        }

        // ───────────── Assets ─────────────

        private static ToxicAssets EnsureAssets()
        {
            foreach (string folder in new[] { MaterialFolder, ProfileFolder, PrefabFolder })
                Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();

            Material liquid = LoadOrCreateMaterial("M_ToxicLiquid", new Color(0.45f, 1f, 0.12f), new Color(0.3f, 0.9f, 0.05f), 0.85f);
            Material fumes = LoadOrCreateMaterial("M_ToxicFumes", new Color(0.62f, 0.78f, 0.32f), new Color(0.12f, 0.25f, 0.02f), 0.2f);
            Material debris = LoadOrCreateMaterial("M_CorrosionDebris", new Color(0.36f, 0.4f, 0.16f), Color.black, 0.1f);

            return new ToxicAssets
            {
                // Fence: soft wood, gone after ~10 L (a few seconds of spraying).
                Fence = LoadOrCreateProfile("Corrosion_Fence", 0f, 10f, 1f, 1f, 2f),
                // Ordinary interior wall: ~80 L.
                InteriorWall = LoadOrCreateProfile("Corrosion_InteriorWall", 0.5f, 40f, 1f, 1f, 3f),
                // Reinforced wall: ~400 L.
                ReinforcedWall = LoadOrCreateProfile("Corrosion_ReinforcedWall", 0.85f, 60f, 1f, 1f, 3f),
                ImpactFumes = LoadOrCreateParticles("ToxicImpactFumes", () => BuildImpactFumes(liquid, fumes)),
                WallFumes = LoadOrCreateParticles("CorrosionFumes", () => BuildWallFumes(fumes)),
                Debris = LoadOrCreatePrefab("CorrosionBreakDebris", () => BuildDebris(debris, fumes)),
            };
        }

        private static Material LoadOrCreateMaterial(string name, Color color, Color emission, float smoothness)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            if (emission.maxColorComponent > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }

            material.enableInstancing = true;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static CorrosionProfile LoadOrCreateProfile(string name, float resistance, float threshold,
            float corrosionPerLiter, float soakRate, float wetDuration)
        {
            string path = $"{ProfileFolder}/{name}.asset";
            var profile = AssetDatabase.LoadAssetAtPath<CorrosionProfile>(path);
            if (profile != null)
                return profile;

            profile = ScriptableObject.CreateInstance<CorrosionProfile>();
            AssetDatabase.CreateAsset(profile, path);
            var so = new SerializedObject(profile);
            so.FindProperty("_resistance").floatValue = resistance;
            so.FindProperty("_threshold").floatValue = threshold;
            so.FindProperty("_corrosionPerLiter").floatValue = corrosionPerLiter;
            so.FindProperty("_soakRate").floatValue = soakRate;
            so.FindProperty("_wetDuration").floatValue = wetDuration;
            so.ApplyModifiedPropertiesWithoutUndo();
            return profile;
        }

        private static ParticleSystem LoadOrCreateParticles(string name, System.Func<GameObject> build)
        {
            return LoadOrCreatePrefab(name, build).GetComponent<ParticleSystem>();
        }

        private static GameObject LoadOrCreatePrefab(string name, System.Func<GameObject> build)
        {
            string path = $"{PrefabFolder}/{name}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
                return prefab;

            GameObject root = build();
            root.name = name;
            prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ───────────── Effects (low-poly mesh particles, opaque, shrink away) ─────────────

        /// <summary>Glowing toxic droplets bursting off the surface plus a few rising fume puffs.</summary>
        private static GameObject BuildImpactFumes(Material liquid, Material fumes)
        {
            ParticleSystem bubbles = CreateParticles("ToxicImpactFumes", PrimitiveMesh(PrimitiveType.Sphere), liquid, 60);
            ParticleSystem.MainModule main = bubbles.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
            main.gravityModifier = 0.6f;
            SetRate(bubbles, 28f);
            SetCone(bubbles, 40f, 0.05f);
            Shrink(bubbles, 1f);

            ParticleSystem puffs = CreateParticles("Fumes", PrimitiveMesh(PrimitiveType.Sphere), fumes, 16);
            puffs.transform.SetParent(bubbles.transform, false);
            ParticleSystem.MainModule puffMain = puffs.main;
            puffMain.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.5f);
            puffMain.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
            puffMain.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.22f);
            puffMain.gravityModifier = -0.08f;
            SetRate(puffs, 6f);
            SetCone(puffs, 25f, 0.1f);
            Grow(puffs);
            return bubbles.gameObject;
        }

        /// <summary>Slow fumes rising from a corroding wall while it is wet.</summary>
        private static GameObject BuildWallFumes(Material fumes)
        {
            ParticleSystem wall = CreateParticles("CorrosionFumes", PrimitiveMesh(PrimitiveType.Sphere), fumes, 14);
            ParticleSystem.MainModule main = wall.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.2f);
            main.gravityModifier = -0.05f;
            SetRate(wall, 5f);
            SetCone(wall, 20f, 0.2f);
            Grow(wall);
            return wall.gameObject;
        }

        /// <summary>One-shot burst of stained chunks and a green puff; removes itself when done.</summary>
        private static GameObject BuildDebris(Material debris, Material fumes)
        {
            ParticleSystem chunks = CreateParticles("CorrosionBreakDebris", PrimitiveMesh(PrimitiveType.Cube), debris, 30);
            ParticleSystem.MainModule main = chunks.main;
            main.loop = false;
            main.playOnAwake = true;
            main.duration = 1f;
            main.stopAction = ParticleSystemStopAction.Destroy;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 1f;
            SetBurst(chunks, 24);
            ParticleSystem.ShapeModule shape = chunks.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1.6f, 1.2f, 0.25f);
            ParticleSystem.CollisionModule collision = chunks.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.quality = ParticleSystemCollisionQuality.Low;
            collision.dampen = 0.5f;
            collision.bounce = 0.2f;
            Shrink(chunks, 0.6f);

            ParticleSystem puff = CreateParticles("Puff", PrimitiveMesh(PrimitiveType.Sphere), fumes, 12);
            puff.transform.SetParent(chunks.transform, false);
            ParticleSystem.MainModule puffMain = puff.main;
            puffMain.loop = false;
            puffMain.playOnAwake = true;
            puffMain.duration = 1f;
            puffMain.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
            puffMain.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
            puffMain.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            puffMain.gravityModifier = -0.05f;
            SetBurst(puff, 10);
            ParticleSystem.ShapeModule puffShape = puff.shape;
            puffShape.shapeType = ParticleSystemShapeType.Box;
            puffShape.scale = new Vector3(1.6f, 1.2f, 0.25f);
            Grow(puff);
            return chunks.gameObject;
        }

        private static ParticleSystem CreateParticles(string name, Mesh mesh, Material material, int maxParticles)
        {
            var go = new GameObject(name);
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = maxParticles;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = mesh;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return particles;
        }

        private static void SetRate(ParticleSystem particles, float rate)
        {
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = rate;
        }

        private static void SetBurst(ParticleSystem particles, short count)
        {
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });
        }

        private static void SetCone(ParticleSystem particles, float angle, float radius)
        {
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = angle;
            shape.radius = radius;
        }

        // Opaque particles cannot fade: they shrink to nothing instead, which suits the cartoon look.
        private static void Shrink(ParticleSystem particles, float holdUntil)
        {
            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            var curve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(Mathf.Clamp(holdUntil, 0f, 0.99f), 1f), new Keyframe(1f, 0f));
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        private static void Grow(ParticleSystem particles)
        {
            ParticleSystem.SizeOverLifetimeModule size = particles.sizeOverLifetime;
            size.enabled = true;
            var curve = new AnimationCurve(new Keyframe(0f, 0.4f), new Keyframe(0.35f, 1f), new Keyframe(1f, 0f));
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        private static Mesh PrimitiveMesh(PrimitiveType type)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            Mesh mesh = primitive.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(primitive);
            return mesh;
        }

        // ───────────── Corrodible objects ─────────────

        private static void AddCorrosion(GameObject target, CorrosionProfile profile, ToxicAssets assets)
        {
            var corrodible = target.GetComponent<CorrodibleObject>();
            if (corrodible == null)
                corrodible = target.AddComponent<CorrodibleObject>();

            var so = new SerializedObject(corrodible);
            so.FindProperty("_profile").objectReferenceValue = profile;
            so.FindProperty("_destroyEffect").objectReferenceValue = assets.Debris;
            so.ApplyModifiedPropertiesWithoutUndo();

            var view = target.GetComponent<CorrosionView>();
            if (view == null)
                view = target.AddComponent<CorrosionView>();

            var viewSo = new SerializedObject(view);
            viewSo.FindProperty("_fumesPrefab").objectReferenceValue = assets.WallFumes;
            viewSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetupFence(ToxicAssets assets)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(FencePath);
            try
            {
                if (root.GetComponent<CorrodibleObject>() != null)
                    return;

                AddCorrosion(root, assets.Fence, assets);
                PrefabUtility.SaveAsPrefabAsset(root, FencePath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// Moves a house's interior partition (its colliders and its triangles of the house mesh) into a child
        /// object that corrodes. The outer, load-bearing walls stay in the house itself and never corrode.
        /// </summary>
        private static void SplitPartition(string path, ToxicAssets assets)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (root.transform.Find(PartitionName) != null)
                    return;

                List<BoxCollider> boxes = root.GetComponents<BoxCollider>().Where(IsPartition).ToList();
                var filter = root.GetComponent<MeshFilter>();
                var renderer = root.GetComponent<MeshRenderer>();
                string meshPath = filter != null && filter.sharedMesh != null ? AssetDatabase.GetAssetPath(filter.sharedMesh) : null;
                if (boxes.Count == 0 || renderer == null || string.IsNullOrEmpty(meshPath))
                    return;

                List<Bounds> region = boxes.Select(b => new Bounds(b.center, b.size + Vector3.one * 0.004f)).ToList();
                Mesh partition = SplitMesh(filter.sharedMesh, region, renderer.sharedMaterials, out Material[] materials);
                partition.name = root.name + "_" + PartitionName;
                AssetDatabase.AddObjectToAsset(partition, meshPath);
                EditorUtility.SetDirty(filter.sharedMesh);

                var child = new GameObject(PartitionName) { layer = root.layer };
                child.transform.SetParent(root.transform, false);
                child.AddComponent<MeshFilter>().sharedMesh = partition;
                var childRenderer = child.AddComponent<MeshRenderer>();
                childRenderer.sharedMaterials = materials;
                childRenderer.shadowCastingMode = renderer.shadowCastingMode;

                foreach (BoxCollider box in boxes)
                {
                    var moved = child.AddComponent<BoxCollider>();
                    moved.center = box.center;
                    moved.size = box.size;
                    moved.sharedMaterial = box.sharedMaterial;
                    Object.DestroyImmediate(box);
                }

                AddCorrosion(child, assets.InteriorWall, assets);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool IsPartition(BoxCollider box)
        {
            return Mathf.Abs(box.size.x - PartitionThickness) < 0.002f && box.size.y > 0.3f && box.size.z > 0.3f;
        }

        /// <summary>
        /// Takes the triangles lying wholly inside <paramref name="region"/> out of <paramref name="source"/>
        /// and returns them as a new mesh, one sub-mesh per material they use.
        /// </summary>
        private static Mesh SplitMesh(Mesh source, List<Bounds> region, Material[] sourceMaterials, out Material[] materials)
        {
            Vector3[] vertices = source.vertices;
            Vector3[] normals = source.normals;
            Vector2[] uvs = source.uv;
            bool hasNormals = normals.Length == vertices.Length;
            bool hasUvs = uvs.Length == vertices.Length;

            var newVertices = new List<Vector3>();
            var newNormals = new List<Vector3>();
            var newUvs = new List<Vector2>();
            var remap = new Dictionary<int, int>();
            var subMeshes = new List<List<int>>();
            var usedMaterials = new List<Material>();

            bool Inside(Vector3 point) => region.Any(b => b.Contains(point));

            int Map(int index)
            {
                if (remap.TryGetValue(index, out int mapped))
                    return mapped;

                mapped = newVertices.Count;
                newVertices.Add(vertices[index]);
                if (hasNormals)
                    newNormals.Add(normals[index]);
                if (hasUvs)
                    newUvs.Add(uvs[index]);
                remap[index] = mapped;
                return mapped;
            }

            for (int s = 0; s < source.subMeshCount; s++)
            {
                int[] triangles = source.GetTriangles(s);
                var kept = new List<int>(triangles.Length);
                var taken = new List<int>();
                for (int i = 0; i + 2 < triangles.Length; i += 3)
                {
                    int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                    bool inside = Inside(vertices[a]) && Inside(vertices[b]) && Inside(vertices[c]);
                    List<int> target = inside ? taken : kept;
                    target.Add(a);
                    target.Add(b);
                    target.Add(c);
                }

                if (taken.Count == 0)
                    continue;

                source.SetTriangles(kept, s, false);
                subMeshes.Add(taken.Select(Map).ToList());
                usedMaterials.Add(s < sourceMaterials.Length ? sourceMaterials[s] : null);
            }

            source.RecalculateBounds();

            var mesh = new Mesh { indexFormat = source.indexFormat };
            mesh.SetVertices(newVertices);
            if (hasNormals)
                mesh.SetNormals(newNormals);
            if (hasUvs)
                mesh.SetUVs(0, newUvs);
            mesh.subMeshCount = subMeshes.Count;
            for (int i = 0; i < subMeshes.Count; i++)
                mesh.SetTriangles(subMeshes[i], i);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();

            materials = usedMaterials.ToArray();
            return mesh;
        }

        // ───────────── Nozzle ─────────────

        private static void SetupNozzle(ToxicAssets assets)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(NozzlePath);
            try
            {
                var tint = root.GetComponentInChildren<LiquidStreamTint>(true);
                if (tint != null)
                    AddToxicTint(tint);

                var stream = root.GetComponentInChildren<WaterStream>(true);
                if (stream != null && root.GetComponentInChildren<LiquidImpactEffect>(true) == null)
                {
                    var splash = root.GetComponentInChildren<WaterStreamParticleView>(true);
                    GameObject host = splash != null ? splash.gameObject : stream.gameObject;
                    var effect = (GameObject)PrefabUtility.InstantiatePrefab(assets.ImpactFumes.gameObject, host.transform);
                    effect.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);

                    var impact = host.AddComponent<LiquidImpactEffect>();
                    var so = new SerializedObject(impact);
                    so.FindProperty("_stream").objectReferenceValue = stream;
                    so.FindProperty("_liquid").intValue = (int)LiquidType.Toxic;
                    so.FindProperty("_effect").objectReferenceValue = effect.GetComponent<ParticleSystem>();
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, NozzlePath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void AddToxicTint(LiquidStreamTint tint)
        {
            var so = new SerializedObject(tint);
            SerializedProperty colors = so.FindProperty("_colors");
            for (int i = 0; i < colors.arraySize; i++)
            {
                if (colors.GetArrayElementAtIndex(i).FindPropertyRelative("_liquid").intValue == (int)LiquidType.Toxic)
                    return;
            }

            colors.InsertArrayElementAtIndex(colors.arraySize);
            SerializedProperty entry = colors.GetArrayElementAtIndex(colors.arraySize - 1);
            entry.FindPropertyRelative("_liquid").intValue = (int)LiquidType.Toxic;
            entry.FindPropertyRelative("_color").colorValue = LiquidStreamTint.ToxicStreamColor;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ───────────── Demo walls ─────────────

        private static void SaveDemoWalls(ToxicAssets assets)
        {
            SaveDemoWall("CorrodibleWall_Demo", "M_InteriorWall", assets.InteriorWall, assets);
            SaveDemoWall("CorrodibleWall_Reinforced", "M_Concrete", assets.ReinforcedWall, assets);
        }

        /// <summary>A free-standing 3 x 2.5 m wall (pivot at its center) that corrodes with the given profile.</summary>
        private static void SaveDemoWall(string name, string material, CorrosionProfile profile, ToxicAssets assets)
        {
            string path = $"{PrefabFolder}/{name}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                return;

            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = name;
            wall.transform.localScale = new Vector3(3f, 2.5f, 0.25f);
            var look = AssetDatabase.LoadAssetAtPath<Material>($"{EnvironmentMaterialFolder}/{material}.mat");
            if (look != null)
                wall.GetComponent<MeshRenderer>().sharedMaterial = look;

            AddCorrosion(wall, profile, assets);
            PrefabUtility.SaveAsPrefabAsset(wall, path);
            Object.DestroyImmediate(wall);
        }
    }
}
