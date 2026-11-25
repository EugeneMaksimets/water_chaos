using UnityEditor;
using UnityEngine;
using WaterChaos.FireTruck;
using WaterChaos.Water;

namespace WaterChaos.EditorTools.MiniVillage
{
    /// <summary>
    /// Low-poly fire truck prefab: cab, pump panel with the nozzle outlet (a WaterConnectionPoint),
    /// and four equipment compartments behind hinged doors (CompartmentDoor) for hoses, pump and nozzle.
    /// The truck is a water source (FireTruckController); its tank is the WaterReservoirComponent on the root.
    /// Local space: pivot on the ground at the centre, front towards -Z, 8.6 m long, 2.5 m wide.
    /// </summary>
    internal static class VillageFireTruck
    {
        public const string Name = "FireTruck";
        private const float Half = 1.25f;
        private const float Spine = 0.45f;
        private const float CompartmentTop = 2.45f;

        /// <summary>One compartment: side (-1 left, +1 right), length along Z, floor height, optional shelf.</summary>
        public struct Compartment
        {
            public string Id;
            public float Side, Z0, Z1, Floor, Shelf;

            public Vector3 FloorCenter => new Vector3(Side * (Spine + Half) * 0.5f, Floor, (Z0 + Z1) * 0.5f);
        }

        public static readonly Compartment[] Compartments =
        {
            new Compartment { Id = "LeftFront", Side = -1f, Z0 = -0.95f, Z1 = 1.45f, Floor = 0.75f, Shelf = -1f },
            new Compartment { Id = "RightFront", Side = 1f, Z0 = -0.95f, Z1 = 1.45f, Floor = 0.75f, Shelf = 1.6f },
            new Compartment { Id = "LeftRear", Side = -1f, Z0 = 1.55f, Z1 = 3.55f, Floor = 1.29f, Shelf = -1f },
            new Compartment { Id = "RightRear", Side = 1f, Z0 = 1.55f, Z1 = 3.55f, Floor = 1.29f, Shelf = -1f },
        };

        public static GameObject Build()
        {
            var body = new PropModel();
            BuildCab(body);
            BuildPumpPanel(body);
            BuildBody(body);
            BuildWheels(body);

            GameObject root = VillageAssets.CreateMeshObject(Name, body.Mesh, Name);
            VillageAssets.AddColliders(root, body.Colliders);
            GameObjectUtility.SetStaticEditorFlags(root, StaticEditorFlags.BatchingStatic);

            foreach (float side in new[] { -1f, 1f })
            {
                Quaternion facing = Quaternion.Euler(0f, side < 0f ? 90f : -90f, 0f);
                VillageAssets.Label(root.transform, "67", new Vector3(side * (Half + 0.015f), 1.75f, -3.25f), 0.5f, Color.white, facing);
            }
            VillageAssets.Label(root.transform, "ENGINE 67", new Vector3(-0.45f, 1.95f, 4.37f), 0.24f, Color.white, Quaternion.Euler(0f, 180f, 0f));

            foreach (Compartment compartment in Compartments)
                CreateDoor(root.transform, compartment);

            CreateOutlet(root.transform);

            // The truck's own water: a tank set up in the Inspector, emptied through the outlet.
            root.AddComponent<FireTruckController>();
            var tank = new SerializedObject(root.GetComponent<WaterReservoirComponent>());
            tank.FindProperty("_capacity").floatValue = 3000f;
            tank.FindProperty("_initialVolume").floatValue = 3000f;
            tank.ApplyModifiedPropertiesWithoutUndo();

            return VillageAssets.SavePrefab(root, "Vehicles", Name);
        }

        private static void BuildCab(PropModel m)
        {
            m.Part(new Vector3(0f, 1.45f, -3.05f), new Vector3(2.5f, 1.5f, 2.3f), "GasRed", true);
            m.Part(new Vector3(0f, 2.7f, -3f), new Vector3(2.44f, 1f, 2.1f), "GasRed", true);
            m.Part(new Vector3(0f, 3.26f, -3f), new Vector3(2.5f, 0.12f, 2.2f), "GasWhite");
            m.Part(new Vector3(0f, 2.72f, -4.06f), new Vector3(2.2f, 0.78f, 0.04f), "Screen");
            foreach (float s in new[] { -1f, 1f })
            {
                m.Part(new Vector3(s * 1.23f, 2.72f, -3.35f), new Vector3(0.04f, 0.7f, 1f), "Screen");
                m.Part(new Vector3(s * 1.23f, 2.72f, -2.3f), new Vector3(0.04f, 0.7f, 0.6f), "Screen");
                m.Part(new Vector3(s * 0.85f, 1.5f, -4.215f), new Vector3(0.36f, 0.22f, 0.03f), "Lamp");
                m.Part(new Vector3(s * 0.85f, 1.2f, -4.215f), new Vector3(0.36f, 0.12f, 0.03f), "PlasticOrange");
                m.Part(new Vector3(s * 1.42f, 2.55f, -3.9f), new Vector3(0.06f, 0.35f, 0.18f), "Metal");
                m.Part(new Vector3(s * 1.33f, 2.55f, -3.9f), new Vector3(0.16f, 0.04f, 0.04f), "Metal");
                m.Part(new Vector3(s * 1.2f, 0.45f, -2.6f), new Vector3(0.2f, 0.06f, 0.6f), "Metal");
                m.Part(new Vector3(s * 0.6f, 3.42f, -3.4f), new Vector3(0.45f, 0.14f, 0.34f), "PlasticRed");
            }

            // Grille, stripes, bumper, light bar.
            m.Part(new Vector3(0f, 1.35f, -4.22f), new Vector3(1.1f, 0.85f, 0.04f), "MetalDark");
            for (float y = 1.05f; y < 1.7f; y += 0.2f)
                m.Part(new Vector3(0f, y, -4.245f), new Vector3(1f, 0.05f, 0.02f), "Metal");
            m.Part(new Vector3(0f, 1.2f, -3.05f), new Vector3(2.52f, 0.16f, 2.32f), "GasYellow");
            m.Part(new Vector3(0f, 0.95f, -3.05f), new Vector3(2.52f, 0.08f, 2.32f), "GasWhite");
            m.Part(new Vector3(0f, 0.55f, -4.35f), new Vector3(2.6f, 0.3f, 0.3f), "Metal", true);
            m.Part(new Vector3(0f, 3.4f, -3.4f), new Vector3(1.7f, 0.16f, 0.32f), "GasWhite");
            m.Part(new Vector3(0f, 3.42f, -3.4f), new Vector3(0.5f, 0.14f, 0.34f), "Lamp");
            // Ends 5 cm inside the rear skirt: a shared end plane would flicker.
            m.Part(new Vector3(0f, 0.5f, 0.275f), new Vector3(1.9f, 0.3f, 7.95f), "MetalDark");
        }

        private static void BuildPumpPanel(PropModel m)
        {
            m.Part(new Vector3(0f, 1.6f, -1.45f), new Vector3(2.46f, 1.7f, 0.9f), "Metal", true);
            m.Part(new Vector3(0f, 2.68f, -1.45f), new Vector3(2.5f, 0.46f, 0.9f), "GasRed", true);
            foreach (float s in new[] { -1f, 1f })
            {
                for (int i = 0; i < 3; i++)
                {
                    Vector3 gauge = new Vector3(s * 1.25f, 2.05f, -1.75f + i * 0.3f);
                    Along(m.Mesh, gauge, 0.1f, 0.04f, "PlasticWhite");
                    Along(m.Mesh, gauge + Vector3.right * s * 0.02f, 0.07f, 0.02f, "Black");
                }
                m.Part(new Vector3(s * 1.25f, 1.6f, -1.7f), new Vector3(0.06f, 0.08f, 0.25f), "GasYellow");
                m.Part(new Vector3(s * 1.25f, 1.6f, -1.2f), new Vector3(0.06f, 0.08f, 0.25f), "GasRed");

            }

            // The nozzle outlet coupling, on the left side only (where the WaterConnectionPoint is).
            Along(m.Mesh, new Vector3(-1.32f, 1.2f, -1.45f), 0.09f, 0.16f, "Metal");
            Along(m.Mesh, new Vector3(-1.41f, 1.2f, -1.45f), 0.1f, 0.04f, "GasYellow");
        }

        private static void BuildBody(PropModel m)
        {
            const float length = 5.3f;
            const float centerZ = 1.65f;
            m.Part(new Vector3(0f, 1.6f, centerZ), new Vector3(Spine * 2f, 1.7f, length), "MetalDark", true);
            m.Part(new Vector3(0f, 2.68f, centerZ), new Vector3(Half * 2f, 0.46f, length), "GasRed", true);
            m.Part(new Vector3(0f, 2.62f, centerZ), new Vector3(Half * 2f + 0.02f, 0.12f, length + 0.02f), "GasYellow");
            m.Part(new Vector3(0f, 2.8f, centerZ), new Vector3(Half * 2f + 0.02f, 0.05f, length + 0.02f), "GasWhite");
            m.Part(new Vector3(0f, 2.95f, centerZ), new Vector3(2.4f, 0.08f, 5.2f), "MetalDark", true);
            m.Part(new Vector3(0f, 1.6f, 4.32f), new Vector3(Half * 2f, 1.7f, 0.04f), "GasRed");
            m.Part(new Vector3(0f, 0.55f, 4.1f), new Vector3(Half * 2f, 0.4f, 0.4f), "GasRed", true);
            m.Part(new Vector3(0f, 0.4f, 4.45f), new Vector3(2.3f, 0.08f, 0.4f), "Metal", true);

            foreach (float s in new[] { -1f, 1f })
            {
                float x = s * (Spine + Half) * 0.5f;
                const float width = Half - Spine;

                // Front compartment floor and skirt, rear compartment above the wheel well.
                m.Part(new Vector3(x, 0.7f, 0.25f), new Vector3(width, 0.1f, 2.4f), "MetalDark", true);
                m.Part(new Vector3(x, 0.5f, 0.25f), new Vector3(width, 0.3f, 2.5f), "GasRed");
                m.Part(new Vector3(x, 1f, 2.55f), new Vector3(width, 0.5f, 2f), "GasRed", true);
                m.Part(new Vector3(x, 1.27f, 2.55f), new Vector3(width - 0.02f, 0.04f, 1.98f), "MetalDark", true);

                // Posts between the doors and the solid rear end.
                m.Part(new Vector3(x, 1.6f, -0.975f), new Vector3(width, 1.7f, 0.05f), "GasRed", true);
                m.Part(new Vector3(x, 1.6f, 1.5f), new Vector3(width, 1.7f, 0.1f), "GasRed", true);
                m.Part(new Vector3(x, 1.6f, 3.925f), new Vector3(width, 1.7f, 0.75f), "GasRed", true);

                m.Part(new Vector3(s * 1f, 1.1f, 4.35f), new Vector3(0.25f, 0.3f, 0.03f), "PlasticRed");
                m.Part(new Vector3(s * 1.15f, 3.05f, 4.2f), new Vector3(0.12f, 0.12f, 0.12f), "PlasticRed");
            }

            Compartment shelf = Compartments[1];
            m.Part(new Vector3(shelf.FloorCenter.x, shelf.Shelf, shelf.FloorCenter.z), new Vector3(Half - Spine - 0.02f, 0.04f, shelf.Z1 - shelf.Z0 - 0.02f), "Metal", true);

            // Roof: ladder on the right, yellow hose reel on the left, ladder up the back.
            foreach (float x in new[] { 0.72f, 1.12f })
                m.Part(new Vector3(x, 3.05f, 1.6f), new Vector3(0.05f, 0.06f, 4.8f), "Metal");
            for (float z = -0.6f; z < 3.9f; z += 0.4f)
                m.Part(new Vector3(0.92f, 3.05f, z), new Vector3(0.38f, 0.04f, 0.04f), "Metal");
            m.Part(new Vector3(-0.55f, 3.1f, 0f), new Vector3(0.6f, 0.2f, 0.2f), "MetalDark");
            Along(m.Mesh, new Vector3(-0.55f, 3.38f, 0f), 0.36f, 0.55f, "GasYellow");
            Along(m.Mesh, new Vector3(-0.55f, 3.38f, 0f), 0.12f, 0.6f, "Metal");
            foreach (float x in new[] { 0.45f, 0.75f })
                m.Part(new Vector3(x, 1.8f, 4.36f), new Vector3(0.05f, 2.2f, 0.05f), "Metal");
            for (float y = 0.9f; y < 2.9f; y += 0.35f)
                m.Part(new Vector3(0.6f, y, 4.36f), new Vector3(0.3f, 0.04f, 0.04f), "Metal");
        }

        private static void BuildWheels(PropModel m)
        {
            foreach (float z in new[] { -2.9f, 2.05f, 3.05f })
            {
                foreach (float s in new[] { -1f, 1f })
                {
                    Along(m.Mesh, new Vector3(s * 1f, 0.5f, z), 0.5f, 0.4f, "Rubber");
                    Along(m.Mesh, new Vector3(s * 1.205f, 0.5f, z), 0.24f, 0.03f, "GasRed");
                    m.Collider(new Vector3(s * 1f, 0.5f, z), new Vector3(0.4f, 1f, 1f));
                }
            }
        }

        private static void CreateDoor(Transform truck, Compartment compartment)
        {
            float height = CompartmentTop - compartment.Floor;
            float length = compartment.Z1 - compartment.Z0;
            float s = compartment.Side;

            // Pivot on the hinge (front edge); the panel runs back along +Z.
            var model = new PropModel();
            model.Part(new Vector3(0f, height * 0.5f, length * 0.5f), new Vector3(0.05f, height, length), "GasRed");
            model.Part(new Vector3(s * 0.035f, height * 0.5f, length - 0.15f), new Vector3(0.03f, 0.28f, 0.05f), "Metal");
            if (compartment.Floor < 1f)
                model.Part(new Vector3(s * 0.012f, 0.45f, length * 0.5f - 0.005f), new Vector3(0.05f, 0.16f, length - 0.03f), "GasYellow");

            string name = "Door_" + compartment.Id;
            GameObject door = VillageAssets.CreateMeshObject(name, model.Mesh, VillageFireTruck.Name + "_Doors");
            door.transform.SetParent(truck, false);
            door.transform.localPosition = new Vector3(s * (Half + 0.03f), compartment.Floor, compartment.Z0);

            var box = door.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, height * 0.5f, length * 0.5f);
            box.size = new Vector3(0.05f, height, length);
            door.AddComponent<Rigidbody>().isKinematic = true;

            var swing = door.AddComponent<CompartmentDoor>();
            var data = new SerializedObject(swing);
            data.FindProperty("_openAngle").floatValue = s * 100f;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateOutlet(Transform truck)
        {
            var outlet = new GameObject("Outlet");
            outlet.transform.SetParent(truck, false);
            outlet.transform.localPosition = new Vector3(-1.38f, 1.2f, -1.45f);
            outlet.AddComponent<SphereCollider>().radius = 0.15f;

            var anchor = new GameObject("HoseAnchor");
            anchor.transform.SetParent(outlet.transform, false);
            anchor.transform.localPosition = new Vector3(-0.06f, 0f, 0f);

            var point = outlet.AddComponent<WaterConnectionPoint>();
            var data = new SerializedObject(point);
            data.FindProperty("_hoseAnchor").objectReferenceValue = anchor.transform;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Cylinder lying along X (wheels, gauges, couplings).</summary>
        private static void Along(LowPolyMesh mesh, Vector3 center, float radius, float length, string material)
        {
            mesh.SetTransform(center, Quaternion.Euler(0f, 0f, 90f));
            mesh.Cylinder(new Vector3(0f, -length * 0.5f, 0f), radius, radius, length, 10, material);
            mesh.ResetTransform();
        }
    }
}
