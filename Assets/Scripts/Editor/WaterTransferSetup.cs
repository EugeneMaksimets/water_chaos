using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WaterChaos.Hose;
using WaterChaos.Items;
using WaterChaos.Player;
using WaterChaos.Pump;
using WaterChaos.Tools;
using WaterChaos.UI;
using WaterChaos.Water;
using WaterChaos.WaterTransfer;

namespace WaterChaos.EditorTools
{
    /// <summary>
    /// One-click setup of the separate water transfer prototype:
    /// transfer hose prefabs (5, 10, 20 m), a transfer outlet on the Pump prefab (next to, not instead of, its regular outlet),
    /// the player's transfer interactor and HUD, three hoses next to the player and a second reservoir ("Reservoir B").
    /// The Nozzle prefab is only read (stream settings, hose wrap mask), never changed.
    /// Safe to run again: prefabs are rebuilt, existing scene parts are reused. Save the scene afterwards.
    /// </summary>
    public static class WaterTransferSetup
    {
        private const string UndoName = "Setup Water Transfer";

        private const string PrefabFolder = "Assets/WaterChaos/Prefabs";
        private const string TransferFolder = PrefabFolder + "/WaterTransfer";
        private const string PumpPrefabPath = PrefabFolder + "/Pump.prefab";
        private const string NozzlePrefabPath = PrefabFolder + "/Nozzle.prefab";

        private const string HoseMaterialPath = "Assets/WaterChaos/Materials/Hose/M_FireHose.mat";
        private const string CoilMaterialPath = "Assets/WaterChaos/Materials/Hose/M_TransferHoseCoil.mat";
        private const string CouplingMaterialPath = "Assets/WaterChaos/Materials/Hose/M_TransferHoseCoupling.mat";
        private const string SplashMaterialPath = "Assets/WaterChaos/Materials/Water/Materials/M_WaterSplash.mat";
        private const string StreamMaterialPath = "Assets/WaterChaos/Materials/Water/Materials/M_WaterStream.mat";
        private const string TankWaterMaterialPath = "Assets/WaterChaos/Materials/Water/Materials/LakeWater.mat";
        private const string IconPath = "UI/Skin/Knob.psd";

        private const string TransferOutletName = "TransferOutlet";
        private const string CarryPointName = "TransferCarryPoint";
        private const string ItemHolderName = "ItemHolder";
        private const string ReservoirName = "Reservoir B";

        /// <summary>Wrap mask if the nozzle hose has none: Default, TransparentFX, WaterTarget.</summary>
        private const int FallbackObstacleMask = 1 | 2 | 8;

        private const float TankSize = 2.4f;
        private const float TankHeight = 0.9f;
        private const float WallThickness = 0.1f;
        private const float FloorThickness = 0.05f;
        private const float TankCapacity = 2000f;
        private const float TankStartVolume = 100f;
        private const float TankOffset = 6f;

        /// <summary>
        /// Lake volume, in liters (L): small enough that filling Reservoir B visibly lowers the lake
        /// (about 1900 L moved = 38 % of its depth).
        /// </summary>
        private const float LakeVolume = 5000f;

        private const string LakeSurfaceName = "Surface";
        private const string OldCarryPointName = "HoseCarryPoint";

        private static readonly (string Name, float Length, float Mass)[] Hoses =
        {
            ("TransferHose_5m", 5f, 4f),
            ("TransferHose_10m", 10f, 7f),
            ("TransferHose_20m", 20f, 12f)
        };

        [MenuItem("Water Chaos/Setup Water Transfer")]
        private static void Setup()
        {
            if (Run())
                Debug.Log("Water transfer setup: done. Save the scene (Cmd+S).");
        }

        /// <summary>Runs the whole setup on the open scene; false if the scene is not the game scene or something is missing.</summary>
        internal static bool Run()
        {
            PlayerInventory inventory = Object.FindAnyObjectByType<PlayerInventory>(FindObjectsInactive.Include);
            PumpController pump = Object.FindAnyObjectByType<PumpController>(FindObjectsInactive.Include);
            if (inventory == null || pump == null)
            {
                Debug.LogError("Water transfer setup: the scene needs a PlayerInventory and a PumpController.");
                return false;
            }

            int waterLayer = LayerMask.NameToLayer("Water");
            int playerLayer = LayerMask.NameToLayer("Player");
            if (waterLayer < 0 || playerLayer < 0)
            {
                Debug.LogError("Water transfer setup: layers \"Water\" and \"Player\" are required.");
                return false;
            }

            int groundMask = ~((1 << waterLayer) | (1 << playerLayer) | (1 << 2) | (1 << 5) | LayerBit("FlowInfluence") | LayerBit("Fire"));

            if (!AddPumpOutlet())
                return false;

            if (!AssetDatabase.IsValidFolder(TransferFolder))
                AssetDatabase.CreateFolder(PrefabFolder, "WaterTransfer");

            ReadNozzle(out WaterStream nozzleStream, out int obstacleMask);
            Material coil = GetOrCreateMaterial(CoilMaterialPath, new Color(0.95f, 0.75f, 0.1f), 0f, 0.35f);
            Material coupling = GetOrCreateMaterial(CouplingMaterialPath, new Color(0.72f, 0.72f, 0.75f), 0.85f, 0.6f);

            GameObject[] prefabs = new GameObject[Hoses.Length];
            for (int i = 0; i < Hoses.Length; i++)
                prefabs[i] = BuildHose(Hoses[i].Name, Hoses[i].Length, Hoses[i].Mass, coil, coupling, nozzleStream, obstacleMask, groundMask, 1 << waterLayer);

            GameObject player = inventory.gameObject;
            SetupPlayer(player, inventory);
            PlaceHoses(prefabs, player.transform);
            CreateReservoir(pump.transform.position, waterLayer);

            EditorSceneManager.MarkSceneDirty(player.scene);
            return true;
        }

        private static int LayerBit(string layer)
        {
            int index = LayerMask.NameToLayer(layer);
            return index >= 0 ? 1 << index : 0;
        }

        /// <summary>
        /// Adds the transfer side of the pump outlet to the pump root (where its collider is). It has no connector of its own:
        /// the transfer hose starts at the nozzle outlet's anchor. A separate outlet left by an older setup is removed.
        /// </summary>
        private static bool AddPumpOutlet()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PumpPrefabPath);
            if (root == null)
            {
                Debug.LogError($"Water transfer setup: no prefab at {PumpPrefabPath}.");
                return false;
            }

            try
            {
                PumpController pump = root.GetComponent<PumpController>();
                if (pump == null)
                {
                    Debug.LogError("Water transfer setup: the Pump prefab has no PumpController on its root.");
                    return false;
                }

                Transform oldOutlet = root.transform.Find(TransferOutletName);
                if (oldOutlet != null)
                    Object.DestroyImmediate(oldOutlet.gameObject);

                if (!root.TryGetComponent(out TransferConnectionPoint point))
                    point = root.AddComponent<TransferConnectionPoint>();

                Set(point, data =>
                {
                    data.FindProperty("_pump").objectReferenceValue = pump;
                });

                PrefabUtility.SaveAsPrefabAsset(root, PumpPrefabPath);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Reads (never changes) the nozzle's stream settings and its hose wrap mask.</summary>
        private static void ReadNozzle(out WaterStream stream, out int obstacleMask)
        {
            GameObject nozzle = AssetDatabase.LoadAssetAtPath<GameObject>(NozzlePrefabPath);
            stream = nozzle != null ? nozzle.GetComponentInChildren<WaterStream>(true) : null;

            HoseController hose = nozzle != null ? nozzle.GetComponentInChildren<HoseController>(true) : null;
            obstacleMask = hose != null ? new SerializedObject(hose).FindProperty("_obstacleMask").intValue : FallbackObstacleMask;

            if (stream == null)
                Debug.LogWarning("Water transfer setup: no WaterStream in the Nozzle prefab; the open-end stream needs its Tuning set by hand.");
        }

        private static Material GetOrCreateMaterial(string path, Color color, float metallic, float smoothness)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            material = new Material(shader != null ? shader : Shader.Find("Standard"));
            material.SetColor("_BaseColor", color);
            material.color = color;
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>
        /// Coil look and collider while it is an item; laid out, a line from the pump to "End":
        /// trigger to aim at, metal coupling, splash particles into a reservoir and an open-end stream onto the ground.
        /// </summary>
        private static GameObject BuildHose(string name, float length, float mass, Material coilMaterial, Material couplingMaterial,
            WaterStream nozzleStream, int obstacleMask, int groundMask, int waterMask)
        {
            Mesh cylinder = Resources.GetBuiltinResource<Mesh>("Cylinder.fbx");
            GameObject root = new GameObject(name);

            try
            {
                GameObject coil = CreateMeshChild("Coil", root.transform, cylinder, coilMaterial);
                coil.transform.localPosition = new Vector3(0f, 0.06f, 0f);
                coil.transform.localScale = new Vector3(0.5f, 0.06f, 0.5f);
                coil.AddComponent<BoxCollider>().size = new Vector3(1f, 2f, 1f);

                Transform end = new GameObject("End").transform;
                end.SetParent(root.transform, false);
                SphereCollider trigger = end.gameObject.AddComponent<SphereCollider>();
                trigger.isTrigger = true;
                trigger.radius = 0.3f;

                GameObject coupling = CreateMeshChild("Coupling", end, cylinder, couplingMaterial);
                coupling.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                coupling.transform.localScale = new Vector3(0.075f, 0.05f, 0.075f);

                GameObject splash = new GameObject("Splash");
                splash.transform.SetParent(end, false);
                SetupSplash(splash.AddComponent<ParticleSystem>());

                GameObject discharge = new GameObject("Discharge");
                discharge.transform.SetParent(end, false);
                discharge.transform.localPosition = new Vector3(0f, 0.03f, 0.06f);
                discharge.transform.localRotation = Quaternion.Euler(-15f, 0f, 0f);
                WaterStream stream = SetupDischargeStream(discharge, nozzleStream);

                Rigidbody body = root.AddComponent<Rigidbody>();
                body.mass = mass;
                // No interpolation, like the other items: a kinematic body held under the moving camera would lag and jitter.
                body.interpolation = RigidbodyInterpolation.None;

                PickableItem item = root.AddComponent<PickableItem>();
                Set(item, data =>
                {
                    data.FindProperty("_icon").objectReferenceValue = AssetDatabase.GetBuiltinExtraResource<Sprite>(IconPath);
                    data.FindProperty("_heldRotation").vector3Value = new Vector3(0f, 0f, 90f);
                    data.FindProperty("_heldScale").floatValue = 0.6f;
                });

                TransferHose hose = root.AddComponent<TransferHose>();
                Set(hose, data =>
                {
                    data.FindProperty("_end").objectReferenceValue = end;
                    data.FindProperty("_coil").objectReferenceValue = coil;
                    data.FindProperty("_maxLength").floatValue = length;
                    data.FindProperty("_obstacleMask").intValue = obstacleMask;
                    data.FindProperty("_groundMask").intValue = groundMask;
                });

                WaterTransferController transfer = root.AddComponent<WaterTransferController>();
                Set(transfer, data =>
                {
                    data.FindProperty("_hose").objectReferenceValue = hose;
                    data.FindProperty("_discharge").objectReferenceValue = stream;
                    data.FindProperty("_waterMask").intValue = waterMask;
                });

                root.AddComponent<LineRenderer>().positionCount = 0;
                TransferHoseView view = root.AddComponent<TransferHoseView>();
                Set(view, data =>
                {
                    data.FindProperty("_hose").objectReferenceValue = hose;
                    data.FindProperty("_transfer").objectReferenceValue = transfer;
                    data.FindProperty("_material").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>(HoseMaterialPath);
                    data.FindProperty("_groundMask").intValue = groundMask;
                });

                TransferDischargeView splashView = splash.AddComponent<TransferDischargeView>();
                Set(splashView, data => data.FindProperty("_transfer").objectReferenceValue = transfer);

                root.AddComponent<TransferHoseItem>();

                return PrefabUtility.SaveAsPrefabAsset(root, $"{TransferFolder}/{name}.prefab");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateMeshChild(string name, Transform parent, Mesh mesh, Material material)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            child.AddComponent<MeshRenderer>().sharedMaterial = material;
            return child;
        }

        /// <summary>Existing WaterStream + WaterStreamView, with the nozzle's settings copied, aimed out of the hose end.</summary>
        private static WaterStream SetupDischargeStream(GameObject discharge, WaterStream nozzleStream)
        {
            LineRenderer line = discharge.AddComponent<LineRenderer>();
            line.positionCount = 0;
            line.useWorldSpace = true;
            line.widthMultiplier = 0.07f;
            line.numCapVertices = 4;
            line.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(StreamMaterialPath);

            WaterStream stream = discharge.AddComponent<WaterStream>();
            if (nozzleStream != null)
                EditorUtility.CopySerialized(nozzleStream, stream);

            Set(stream, data => data.FindProperty("_nozzle").objectReferenceValue = discharge.transform);

            WaterStreamView view = discharge.AddComponent<WaterStreamView>();
            Set(view, data => data.FindProperty("_stream").objectReferenceValue = stream);
            return stream;
        }

        /// <summary>Water pouring forward out of the end; emission stays 0 until TransferDischargeView sets it from the flow.</summary>
        private static void SetupSplash(ParticleSystem particles)
        {
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = 0.6f;
            main.startSpeed = 2f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            main.startColor = new Color(0.8f, 0.9f, 1f, 0.8f);
            main.gravityModifier = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.02f;

            Material material = AssetDatabase.LoadAssetAtPath<Material>(SplashMaterialPath);
            if (material != null)
                particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
        }

        private static void SetupPlayer(GameObject player, PlayerInventory inventory)
        {
            Camera camera = player.GetComponentInChildren<Camera>(true);
            PlayerInputHandler input = player.GetComponent<PlayerInputHandler>();
            if (camera == null || input == null)
            {
                Debug.LogError("Water transfer setup: the player needs a child Camera and a PlayerInputHandler.");
                return;
            }

            Transform carry = camera.transform.Find(CarryPointName);
            if (carry == null)
            {
                GameObject carryObject = new GameObject(CarryPointName);
                Undo.RegisterCreatedObjectUndo(carryObject, UndoName);
                carry = carryObject.transform;
                carry.SetParent(camera.transform, false);

                Transform holder = camera.transform.Find(ItemHolderName);
                carry.localPosition = holder != null ? holder.localPosition : new Vector3(0.45f, -0.35f, 0.7f);
            }

            PlayerHoseConnector nozzleConnector = player.GetComponent<PlayerHoseConnector>();

            if (!player.TryGetComponent(out PlayerTransferHoseInteractor interactor))
                interactor = Undo.AddComponent<PlayerTransferHoseInteractor>(player);

            SerializedObject data = new SerializedObject(interactor);
            data.FindProperty("_input").objectReferenceValue = input;
            data.FindProperty("_inventory").objectReferenceValue = inventory;
            data.FindProperty("_equipment").objectReferenceValue = player.GetComponent<PlayerEquipment>();
            data.FindProperty("_camera").objectReferenceValue = camera.transform;
            data.FindProperty("_carryPoint").objectReferenceValue = carry;
            data.FindProperty("_nozzleConnector").objectReferenceValue = nozzleConnector;
            data.FindProperty("_interactMask").intValue = ~(1 << player.layer);
            data.ApplyModifiedProperties();

            if (!player.TryGetComponent(out PumpRadialMenu menu))
                menu = Undo.AddComponent<PumpRadialMenu>(player);

            // Everything that would react to the mouse, the click or movement keys while the menu is open.
            Behaviour[] blocked =
            {
                player.GetComponentInChildren<PlayerLook>(true),
                player.GetComponentInChildren<PlayerMovement>(true),
                player.GetComponentInChildren<PlayerItemThrower>(true),
                player.GetComponentInChildren<PlayerItemInteractor>(true),
                inventory
            };

            SerializedObject menuData = new SerializedObject(menu);
            menuData.FindProperty("_input").objectReferenceValue = input;
            menuData.FindProperty("_interactor").objectReferenceValue = interactor;
            SerializedProperty list = menuData.FindProperty("_blockWhileOpen");
            list.arraySize = 0;
            foreach (Behaviour behaviour in blocked)
            {
                if (behaviour == null)
                    continue;

                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = behaviour;
            }
            menuData.ApplyModifiedProperties();

            if (!player.TryGetComponent(out TransferHoseHud hud))
                hud = Undo.AddComponent<TransferHoseHud>(player);

            SerializedObject hudData = new SerializedObject(hud);
            hudData.FindProperty("_interactor").objectReferenceValue = interactor;
            hudData.FindProperty("_menu").objectReferenceValue = menu;
            hudData.FindProperty("_itemInteractor").objectReferenceValue = player.GetComponent<PlayerItemInteractor>();
            hudData.ApplyModifiedProperties();
        }

        /// <summary>One hose of each length on the ground in front of the player, unless the scene already has transfer hoses.</summary>
        private static void PlaceHoses(GameObject[] prefabs, Transform player)
        {
            if (Object.FindAnyObjectByType<TransferHose>(FindObjectsInactive.Include) != null)
                return;

            Physics.SyncTransforms();
            for (int i = 0; i < prefabs.Length; i++)
            {
                if (prefabs[i] == null)
                    continue;

                Vector3 point = player.position + player.forward * 2.5f + player.right * ((i - 1) * 1.2f);
                GameObject hose = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i], player.gameObject.scene);
                hose.transform.position = GroundPoint(point) + Vector3.up * 0.2f;
                Undo.RegisterCreatedObjectUndo(hose, UndoName);
            }
        }

        /// <summary>
        /// Open tank opposite the lake side of the pump: solid floor and walls, a water volume
        /// (trigger on the Water layer that owns the liters) and a water look that follows the fill level.
        /// </summary>
        private static void CreateReservoir(Vector3 pumpPosition, int waterLayer)
        {
            if (Object.FindAnyObjectByType<ReservoirLevelView>(FindObjectsInactive.Include) != null)
                return;

            Physics.SyncTransforms();
            GameObject root = new GameObject(ReservoirName);
            root.transform.position = GroundPoint(pumpPosition + Vector3.left * TankOffset);
            Undo.RegisterCreatedObjectUndo(root, UndoName);

            float outer = TankSize + WallThickness * 2f;
            float half = TankSize * 0.5f + WallThickness * 0.5f;
            CreateBlock("Floor", root.transform, new Vector3(0f, FloorThickness * 0.5f, 0f), new Vector3(outer, FloorThickness, outer));
            CreateBlock("Wall North", root.transform, new Vector3(0f, TankHeight * 0.5f, half), new Vector3(outer, TankHeight, WallThickness));
            CreateBlock("Wall South", root.transform, new Vector3(0f, TankHeight * 0.5f, -half), new Vector3(outer, TankHeight, WallThickness));
            CreateBlock("Wall East", root.transform, new Vector3(half, TankHeight * 0.5f, 0f), new Vector3(WallThickness, TankHeight, TankSize));
            CreateBlock("Wall West", root.transform, new Vector3(-half, TankHeight * 0.5f, 0f), new Vector3(WallThickness, TankHeight, TankSize));

            GameObject volume = new GameObject("Water Volume");
            volume.layer = waterLayer;
            volume.transform.SetParent(root.transform, false);
            volume.AddComponent<WaterBody>();

            BoxCollider box = volume.GetComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, (TankHeight + FloorThickness) * 0.5f, 0f);
            box.size = new Vector3(TankSize, TankHeight - FloorThickness, TankSize);

            WaterReservoirComponent reservoir = volume.GetComponent<WaterReservoirComponent>();
            Set(reservoir, data =>
            {
                data.FindProperty("_capacity").floatValue = TankCapacity;
                data.FindProperty("_initialVolume").floatValue = TankStartVolume;
                data.FindProperty("_lowLevelRatio").floatValue = 0f;
            });

            GameObject water = GameObject.CreatePrimitive(PrimitiveType.Cube);
            water.name = "Water";
            Object.DestroyImmediate(water.GetComponent<Collider>());
            water.transform.SetParent(root.transform, false);
            water.transform.localScale = new Vector3(TankSize - 0.02f, 0.1f, TankSize - 0.02f);
            Material waterMaterial = AssetDatabase.LoadAssetAtPath<Material>(TankWaterMaterialPath);
            if (waterMaterial != null)
                water.GetComponent<MeshRenderer>().sharedMaterial = waterMaterial;

            ReservoirLevelView level = root.AddComponent<ReservoirLevelView>();
            Set(level, data =>
            {
                data.FindProperty("_reservoir").objectReferenceValue = reservoir;
                data.FindProperty("_water").objectReferenceValue = water.transform;
                data.FindProperty("_bottom").floatValue = FloorThickness;
                data.FindProperty("_fullHeight").floatValue = TankHeight - FloorThickness;
            });
        }

        /// <summary>
        /// Brings an already set up scene up to date; true if anything changed:
        /// removes leftovers of the first (reverted) transfer version (a duplicate "Reservoir B" with a missing script,
        /// the unused "HoseCarryPoint") and lets natural water bodies (the lake) show their level.
        /// </summary>
        internal static bool UpgradeScene()
        {
            bool changed = RemoveOldLeftovers();
            changed |= AddSurfaceViews();

            return changed;
        }

        private static bool RemoveOldLeftovers()
        {
            bool changed = false;
            foreach (GameObject root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name != ReservoirName || !HasMissingScripts(root))
                    continue;

                Undo.DestroyObjectImmediate(root);
                changed = true;
            }

            PlayerInventory inventory = Object.FindAnyObjectByType<PlayerInventory>(FindObjectsInactive.Include);
            Camera camera = inventory != null ? inventory.GetComponentInChildren<Camera>(true) : null;
            Transform oldCarry = camera != null ? camera.transform.Find(OldCarryPointName) : null;
            if (oldCarry != null && oldCarry.childCount == 0 && oldCarry.GetComponents<Component>().Length == 1)
            {
                Undo.DestroyObjectImmediate(oldCarry.gameObject);
                changed = true;
            }

            return changed;
        }

        private static bool HasMissingScripts(GameObject root)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) > 0)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Water bodies with a "Surface" next to them and no level look yet (the lake): the surface now sinks
        /// as the water is pumped out, between the volume bottom and its current height.
        /// </summary>
        private static bool AddSurfaceViews()
        {
            bool changed = false;
            WaterBody[] bodies = Object.FindObjectsByType<WaterBody>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (WaterBody body in bodies)
            {
                Transform parent = body.transform.parent;
                if (parent == null || parent.GetComponent<ReservoirLevelView>() != null || parent.GetComponent<ReservoirSurfaceView>() != null)
                    continue;

                Transform surface = parent.Find(LakeSurfaceName);
                BoxCollider box = body.GetComponent<BoxCollider>();
                WaterReservoirComponent reservoir = body.GetComponent<WaterReservoirComponent>();
                if (surface == null || box == null || reservoir == null)
                    continue;

                Undo.RecordObject(reservoir, UndoName);
                Set(reservoir, data =>
                {
                    data.FindProperty("_capacity").floatValue = LakeVolume;
                    data.FindProperty("_initialVolume").floatValue = LakeVolume;
                });

                float bottom = parent.InverseTransformPoint(body.transform.TransformPoint(box.center - Vector3.up * (box.size.y * 0.5f))).y;
                ReservoirSurfaceView view = Undo.AddComponent<ReservoirSurfaceView>(parent.gameObject);
                Set(view, data =>
                {
                    data.FindProperty("_reservoir").objectReferenceValue = reservoir;
                    data.FindProperty("_surface").objectReferenceValue = surface;
                    data.FindProperty("_fullY").floatValue = surface.localPosition.y;
                    data.FindProperty("_emptyY").floatValue = bottom + 0.01f;
                });

                changed = true;
            }

            return changed;
        }

        private static void CreateBlock(string name, Transform parent, Vector3 position, Vector3 size)
        {
            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = position;
            block.transform.localScale = size;
        }

        private static Vector3 GroundPoint(Vector3 point)
        {
            if (Physics.Raycast(point + Vector3.up * 20f, Vector3.down, out RaycastHit hit, 100f, ~0, QueryTriggerInteraction.Ignore))
                point.y = hit.point.y;

            return point;
        }

        private static void Set(Object target, System.Action<SerializedObject> edit)
        {
            SerializedObject data = new SerializedObject(target);
            edit(data);
            data.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
