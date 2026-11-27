using System.Collections.Generic;
using UnityEngine;

namespace WaterChaos.EditorTools.MiniVillage
{
    /// <summary>
    /// Low-poly prop models of the village and their prefabs. Pivot: bottom centre; front: -Z.
    /// Every movable prop gets a water push profile matching its weight class; scenery stays static.
    /// </summary>
    internal static class VillageProps
    {
        private static readonly Dictionary<string, GameObject> Prefabs = new Dictionary<string, GameObject>();

        public static GameObject Get(string name)
        {
            if (Prefabs.TryGetValue(name, out GameObject prefab))
                return prefab;

            Debug.LogError("Mini village: no prop named " + name);
            return null;
        }

        public static IEnumerable<string> Names => Prefabs.Keys;

        public static void BuildAll()
        {
            Prefabs.Clear();
            BuildPoolProps();
            BuildGardenProps();
            BuildStreetProps();
            BuildInteriorProps();
            BuildGasStationProps();
        }

        // ───────────── Pool ─────────────

        private static void BuildPoolProps()
        {
            var chair = new PropModel();
            chair.Part(new Vector3(0f, 0.44f, 0f), new Vector3(0.46f, 0.05f, 0.44f), "PlasticWhite");
            Legs(chair, 0.19f, 0.18f, 0.42f, 0.025f, "PlasticWhite");
            Rotated(chair, new Vector3(0f, 0.72f, 0.21f), new Vector3(12f, 0f, 0f), new Vector3(0.46f, 0.5f, 0.04f), "PlasticWhite");
            foreach (float side in Sides)
                chair.Part(new Vector3(side * 0.22f, 0.62f, 0f), new Vector3(0.04f, 0.04f, 0.4f), "PlasticWhite");
            chair.Collider(new Vector3(0f, 0.235f, 0f), new Vector3(0.46f, 0.47f, 0.44f));
            chair.Collider(new Vector3(0f, 0.72f, 0.22f), new Vector3(0.46f, 0.5f, 0.07f));
            Save("Pool", "PlasticChair", chair, VillageAssets.LightFurniture);

            var table = new PropModel();
            table.Mesh.Cylinder(new Vector3(0f, 0.7f, 0f), 0.5f, 0.5f, 0.05f, 10, "PlasticWhite");
            table.Mesh.Cylinder(new Vector3(0f, 0.05f, 0f), 0.04f, 0.04f, 0.65f, 6, "Metal");
            table.Mesh.Cylinder(Vector3.zero, 0.3f, 0.25f, 0.05f, 8, "MetalDark");
            table.Collider(new Vector3(0f, 0.725f, 0f), new Vector3(1f, 0.05f, 1f));
            table.Collider(new Vector3(0f, 0.375f, 0f), new Vector3(0.1f, 0.65f, 0.1f));
            table.Collider(new Vector3(0f, 0.025f, 0f), new Vector3(0.6f, 0.05f, 0.6f));
            Save("Pool", "PatioTable", table, VillageAssets.Furniture);

            var lounger = new PropModel();
            foreach (float side in Sides)
                lounger.Part(new Vector3(side * 0.3f, 0.3f, 0f), new Vector3(0.05f, 0.06f, 1.9f), "PlasticWhite");
            Legs(lounger, 0.3f, 0.85f, 0.3f, 0.03f, "PlasticWhite");
            lounger.Part(new Vector3(0f, 0.33f, -0.3f), new Vector3(0.6f, 0.04f, 1.25f), "PlasticWhite");
            lounger.Part(new Vector3(0f, 0.39f, -0.3f), new Vector3(0.58f, 0.08f, 1.22f), "PlasticBlue");
            Rotated(lounger, new Vector3(0f, 0.575f, 0.607f), new Vector3(-35f, 0f, 0f), new Vector3(0.6f, 0.04f, 0.75f), "PlasticWhite");
            Rotated(lounger, new Vector3(0f, 0.62f, 0.58f), new Vector3(-35f, 0f, 0f), new Vector3(0.58f, 0.08f, 0.72f), "PlasticBlue");
            lounger.Collider(new Vector3(0f, 0.22f, 0f), new Vector3(0.65f, 0.44f, 1.9f));
            lounger.Collider(new Vector3(0f, 0.62f, 0.62f), new Vector3(0.62f, 0.4f, 0.55f));
            Save("Pool", "SunLounger", lounger, VillageAssets.Furniture);

            Umbrella("Umbrella_Red", "PlasticRed");
            Umbrella("Umbrella_Blue", "PlasticBlue");

            var ball = new PropModel();
            ball.Mesh.Sphere(new Vector3(0f, 0.25f, 0f), Vector3.one * 0.25f, 10, 6, "PlasticWhite");
            foreach (var stripe in new[] { (0f, "PlasticRed"), (60f, "PlasticBlue"), (120f, "PlasticYellow") })
            {
                ball.Mesh.SetTransform(new Vector3(0f, 0.25f, 0f), Quaternion.Euler(90f, stripe.Item1, 0f));
                ball.Mesh.Ring(Vector3.zero, 0.235f, 0.06f, 10, stripe.Item2);
            }
            ball.Mesh.ResetTransform();
            ball.Spheres.Add(new Vector4(0f, 0.25f, 0f, 0.26f));
            Save("Pool", "BeachBall", ball, VillageAssets.LightPlastic);

            SwimRing("SwimRing_Pink", "PlasticPink");
            SwimRing("SwimRing_Orange", "PlasticOrange");

            var kickboard = new PropModel();
            kickboard.Part(new Vector3(0f, 0.025f, 0f), new Vector3(0.45f, 0.05f, 0.32f), "PlasticYellow", true);
            kickboard.Mesh.Cylinder(new Vector3(0f, 0f, -0.16f), 0.225f, 0.225f, 0.05f, 8, "PlasticYellow");
            Save("Pool", "Kickboard", kickboard, VillageAssets.LightPlastic);

            Bucket("Bucket_Red", "PlasticRed");
            Bucket("Bucket_Blue", "PlasticBlue");

            var net = new PropModel();
            net.Part(new Vector3(0f, 0.02f, 0f), new Vector3(2.2f, 0.035f, 0.035f), "Metal");
            net.Mesh.Ring(new Vector3(1.35f, 0.02f, 0f), 0.22f, 0.035f, 8, "PlasticBlue");
            net.Mesh.Cylinder(new Vector3(1.35f, 0f, 0f), 0.2f, 0.2f, 0.01f, 8, "FabricCream");
            net.Collider(new Vector3(0.25f, 0.03f, 0f), new Vector3(2.7f, 0.06f, 0.48f));
            Save("Pool", "PoolNet", net, VillageAssets.LightPlastic);

            var chlorine = new PropModel();
            chlorine.Part(new Vector3(0f, 0.18f, 0f), new Vector3(0.28f, 0.36f, 0.2f), "PlasticWhite", true);
            chlorine.Mesh.Cylinder(new Vector3(0.06f, 0.36f, 0f), 0.05f, 0.05f, 0.05f, 6, "PlasticBlue");
            chlorine.Part(new Vector3(0f, 0.2f, -0.1f), new Vector3(0.2f, 0.15f, 0.01f), "PlasticBlue");
            Save("Pool", "ChlorineCanister", chlorine, VillageAssets.LightPlastic);

            var filter = new PropModel();
            filter.Part(new Vector3(0f, 0.2f, 0f), new Vector3(0.7f, 0.4f, 0.45f), "MetalDark", true);
            Horizontal(filter.Mesh, new Vector3(-0.1f, 0.52f, 0f), 0.14f, 0.45f, "PlasticBlue");
            filter.Part(new Vector3(0.28f, 0.5f, 0f), new Vector3(0.08f, 0.2f, 0.08f), "PlasticWhite");
            filter.Collider(new Vector3(-0.1f, 0.52f, 0f), new Vector3(0.45f, 0.28f, 0.28f));
            Save("Pool", "PoolFilterPump", filter, VillageAssets.Furniture);

            var cooler = new PropModel();
            cooler.Part(new Vector3(0f, 0.175f, 0f), new Vector3(0.6f, 0.35f, 0.38f), "PlasticBlue");
            cooler.Part(new Vector3(0f, 0.39f, 0f), new Vector3(0.62f, 0.08f, 0.4f), "PlasticWhite");
            foreach (float side in Sides)
                cooler.Part(new Vector3(side * 0.32f, 0.28f, 0f), new Vector3(0.04f, 0.05f, 0.16f), "PlasticWhite");
            cooler.Collider(new Vector3(0f, 0.215f, 0f), new Vector3(0.62f, 0.43f, 0.4f));
            Save("Pool", "CoolerBox", cooler, VillageAssets.Furniture);

            // Ladder hooks over a pool wall at z = 0; its steps go down on the +Z side (into the water).
            var ladder = new PropModel();
            foreach (float side in Sides)
            {
                ladder.Part(new Vector3(side * 0.25f, 0.7f, 0.3f), new Vector3(0.05f, 1.4f, 0.05f), "Metal");
                ladder.Part(new Vector3(side * 0.25f, 1.4f, 0.02f), new Vector3(0.05f, 0.05f, 0.6f), "Metal");
                ladder.Part(new Vector3(side * 0.25f, 1.1f, -0.27f), new Vector3(0.05f, 0.6f, 0.05f), "Metal");
            }
            foreach (float y in new[] { 0.25f, 0.6f })
                ladder.Part(new Vector3(0f, y, 0.3f), new Vector3(0.5f, 0.04f, 0.12f), "Metal");
            Save("Pool", "PoolLadder", ladder, null);

            var shower = new PropModel();
            shower.Part(new Vector3(0f, 0.02f, 0f), new Vector3(0.8f, 0.04f, 0.8f), "Coping", true);
            shower.Mesh.Cylinder(new Vector3(0f, 0.04f, 0f), 0.04f, 0.04f, 2.3f, 6, "Metal");
            shower.Part(new Vector3(0f, 2.3f, -0.2f), new Vector3(0.05f, 0.05f, 0.45f), "Metal");
            shower.Mesh.Cylinder(new Vector3(0f, 2.2f, -0.42f), 0.12f, 0.06f, 0.08f, 8, "Metal");
            shower.Collider(new Vector3(0f, 1.15f, 0f), new Vector3(0.1f, 2.3f, 0.1f));
            Save("Pool", "PoolShower", shower, null);

            var sideTable = new PropModel();
            sideTable.Mesh.Cylinder(new Vector3(0f, 0.48f, 0f), 0.3f, 0.3f, 0.04f, 8, "WoodLight");
            for (int i = 0; i < 3; i++)
            {
                float angle = i * 120f * Mathf.Deg2Rad;
                sideTable.Part(new Vector3(Mathf.Cos(angle) * 0.2f, 0.24f, Mathf.Sin(angle) * 0.2f), new Vector3(0.04f, 0.48f, 0.04f), "WoodLight");
            }
            sideTable.Collider(new Vector3(0f, 0.5f, 0f), new Vector3(0.6f, 0.04f, 0.6f));
            sideTable.Collider(new Vector3(0f, 0.24f, 0f), new Vector3(0.42f, 0.48f, 0.42f));
            Save("Pool", "SideTable", sideTable, VillageAssets.LightFurniture);
        }

        private static void Umbrella(string name, string color)
        {
            var model = new PropModel();
            model.Mesh.Cylinder(Vector3.zero, 0.28f, 0.24f, 0.12f, 8, "Concrete");
            model.Mesh.Cylinder(new Vector3(0f, 0.12f, 0f), 0.03f, 0.03f, 2.3f, 6, "PlasticWhite");

            // Striped canopy, visible from above and below.
            var apex = new Vector3(0f, 2.45f, 0f);
            const int segments = 8;
            for (int i = 0; i < segments; i++)
            {
                Vector3 a = RingPoint(1.3f, 2.05f, i, segments);
                Vector3 b = RingPoint(1.3f, 2.05f, i + 1, segments);
                string material = i % 2 == 0 ? color : "PlasticWhite";
                model.Mesh.Tri(a, b, apex, new Vector3(0f, 1.8f, 0f), material);
                model.Mesh.Tri(a, b, apex - Vector3.up * 0.02f, new Vector3(0f, 2.7f, 0f), material);
            }
            model.Mesh.Sphere(new Vector3(0f, 2.47f, 0f), Vector3.one * 0.05f, 6, 4, "PlasticWhite");

            model.Collider(new Vector3(0f, 0.06f, 0f), new Vector3(0.56f, 0.12f, 0.56f));
            model.Collider(new Vector3(0f, 1.2f, 0f), new Vector3(0.08f, 2.2f, 0.08f));
            model.Collider(new Vector3(0f, 2.22f, 0f), new Vector3(1.9f, 0.34f, 1.9f));
            Save("Pool", name, model, VillageAssets.Tall);
        }

        private static void SwimRing(string name, string color)
        {
            var model = new PropModel();
            model.Mesh.Ring(new Vector3(0f, 0.08f, 0f), 0.33f, 0.16f, 10, color);
            model.ConvexCollider = true;
            Save("Pool", name, model, VillageAssets.LightPlastic);
        }

        private static void Bucket(string name, string color)
        {
            var model = new PropModel();
            model.Mesh.Cylinder(Vector3.zero, 0.15f, 0.19f, 0.32f, 8, color);
            model.Mesh.Ring(new Vector3(0f, 0.32f, 0f), 0.19f, 0.025f, 8, color);
            model.Part(new Vector3(0f, 0.44f, 0f), new Vector3(0.36f, 0.02f, 0.02f), "MetalDark");
            foreach (float side in Sides)
                model.Part(new Vector3(side * 0.18f, 0.38f, 0f), new Vector3(0.02f, 0.12f, 0.02f), "MetalDark");
            model.ConvexCollider = true;
            Save("Pool", name, model, VillageAssets.LightPlastic);
        }

        // ───────────── Garden ─────────────

        private static void BuildGardenProps()
        {
            TrashBin("TrashBin_Green", "PlasticGreen");
            TrashBin("TrashBin_Blue", "PlasticBlue");

            var mailbox = new PropModel();
            mailbox.Part(new Vector3(0f, 0.025f, 0f), new Vector3(0.35f, 0.05f, 0.35f), "Concrete");
            mailbox.Part(new Vector3(0f, 0.55f, 0f), new Vector3(0.09f, 1.05f, 0.09f), "WoodLight");
            mailbox.Part(new Vector3(0f, 1.08f, 0f), new Vector3(0.3f, 0.03f, 0.5f), "WoodLight");
            mailbox.Part(new Vector3(0f, 1.18f, 0f), new Vector3(0.26f, 0.17f, 0.48f), "PlasticBlue");
            Horizontal(mailbox.Mesh, new Vector3(0f, 1.26f, 0f), 0.13f, 0.48f, "PlasticBlue", true);
            mailbox.Part(new Vector3(0.15f, 1.32f, 0.05f), new Vector3(0.02f, 0.25f, 0.04f), "PlasticRed");
            mailbox.Part(new Vector3(0.15f, 1.42f, 0.1f), new Vector3(0.02f, 0.08f, 0.12f), "PlasticRed");
            mailbox.Collider(new Vector3(0f, 0.55f, 0f), new Vector3(0.35f, 1.1f, 0.35f));
            mailbox.Collider(new Vector3(0f, 1.24f, 0f), new Vector3(0.3f, 0.32f, 0.5f));
            Save("Garden", "Mailbox", mailbox, VillageAssets.Sign);

            var bench = new PropModel();
            foreach (float side in Sides)
            {
                bench.Part(new Vector3(side * 0.7f, 0.225f, 0f), new Vector3(0.06f, 0.45f, 0.5f), "MetalDark");
                bench.Part(new Vector3(side * 0.7f, 0.62f, 0f), new Vector3(0.06f, 0.05f, 0.5f), "MetalDark");
            }
            foreach (float z in new[] { -0.15f, 0f, 0.15f })
                bench.Part(new Vector3(0f, 0.46f, z), new Vector3(1.6f, 0.04f, 0.12f), "WoodLight");
            foreach (float y in new[] { 0.62f, 0.8f })
                Rotated(bench, new Vector3(0f, y, 0.26f), new Vector3(10f, 0f, 0f), new Vector3(1.6f, 0.12f, 0.04f), "WoodLight");
            bench.Collider(new Vector3(0f, 0.24f, 0f), new Vector3(1.6f, 0.48f, 0.5f));
            bench.Collider(new Vector3(0f, 0.72f, 0.27f), new Vector3(1.6f, 0.4f, 0.08f));
            Save("Garden", "Bench", bench, VillageAssets.Heavy);

            var grill = new PropModel();
            for (int i = 0; i < 3; i++)
            {
                float angle = (i * 120f + 90f) * Mathf.Deg2Rad;
                grill.Part(new Vector3(Mathf.Cos(angle) * 0.2f, 0.32f, Mathf.Sin(angle) * 0.2f), new Vector3(0.04f, 0.64f, 0.04f), "MetalDark");
            }
            grill.Mesh.Sphere(new Vector3(0f, 0.85f, 0f), new Vector3(0.32f, 0.26f, 0.32f), 8, 5, "Black");
            grill.Mesh.Sphere(new Vector3(0f, 1.12f, 0f), Vector3.one * 0.04f, 6, 4, "Metal");
            grill.Part(new Vector3(0.42f, 0.7f, 0f), new Vector3(0.3f, 0.03f, 0.35f), "WoodLight");
            grill.Collider(new Vector3(0f, 0.32f, 0f), new Vector3(0.45f, 0.64f, 0.45f));
            grill.Collider(new Vector3(0.06f, 0.86f, 0f), new Vector3(0.76f, 0.52f, 0.64f));
            Save("Garden", "Grill", grill, VillageAssets.Furniture);

            var barrow = new PropModel();
            Rotated(barrow, new Vector3(0f, 0.55f, 0.05f), new Vector3(-8f, 0f, 0f), new Vector3(0.62f, 0.26f, 0.85f), "PlasticGreen");
            Horizontal(barrow.Mesh, new Vector3(0f, 0.2f, -0.55f), 0.2f, 0.1f, "Rubber");
            foreach (float side in Sides)
            {
                barrow.Part(new Vector3(side * 0.24f, 0.48f, 0.35f), new Vector3(0.04f, 0.04f, 1.3f), "Metal");
                barrow.Part(new Vector3(side * 0.22f, 0.2f, 0.45f), new Vector3(0.04f, 0.4f, 0.04f), "Metal");
            }
            barrow.Collider(new Vector3(0f, 0.55f, 0.05f), new Vector3(0.62f, 0.3f, 0.85f));
            barrow.Collider(new Vector3(0f, 0.2f, -0.55f), new Vector3(0.12f, 0.4f, 0.4f));
            barrow.Collider(new Vector3(0f, 0.3f, 0.6f), new Vector3(0.5f, 0.6f, 0.6f));
            Save("Garden", "Wheelbarrow", barrow, VillageAssets.Furniture);

            var pot = new PropModel();
            pot.Mesh.Cylinder(Vector3.zero, 0.16f, 0.21f, 0.32f, 8, "BrickDark");
            pot.Mesh.Sphere(new Vector3(0f, 0.45f, 0f), new Vector3(0.24f, 0.2f, 0.24f), 7, 4, "Leaves");
            for (int i = 0; i < 4; i++)
                pot.Mesh.Sphere(RingPoint(0.15f, 0.56f, i, 4), Vector3.one * 0.05f, 5, 3, i % 2 == 0 ? "FlowerPink" : "FlowerYellow");
            pot.Collider(new Vector3(0f, 0.3f, 0f), new Vector3(0.42f, 0.6f, 0.42f));
            Save("Garden", "FlowerPot", pot, VillageAssets.SmallDecor);

            var gnome = new PropModel();
            gnome.Mesh.Cylinder(Vector3.zero, 0.12f, 0.08f, 0.22f, 8, "PlasticBlue");
            gnome.Mesh.Sphere(new Vector3(0f, 0.28f, 0f), Vector3.one * 0.075f, 7, 4, "CeramicPink");
            gnome.Mesh.Cylinder(new Vector3(0f, 0.12f, -0.04f), 0.02f, 0.07f, 0.14f, 6, "FlowerWhite");
            gnome.Mesh.Cylinder(new Vector3(0f, 0.32f, 0f), 0.085f, 0f, 0.22f, 8, "PlasticRed");
            gnome.Collider(new Vector3(0f, 0.27f, 0f), new Vector3(0.24f, 0.54f, 0.24f));
            Save("Garden", "GardenGnome", gnome, VillageAssets.SmallDecor);

            var can = new PropModel();
            can.Mesh.Cylinder(Vector3.zero, 0.13f, 0.12f, 0.26f, 8, "PlasticGreen");
            Rotated(can, new Vector3(0f, 0.22f, -0.22f), new Vector3(-50f, 0f, 0f), new Vector3(0.04f, 0.04f, 0.32f), "PlasticGreen");
            can.Part(new Vector3(0f, 0.32f, 0.04f), new Vector3(0.03f, 0.03f, 0.2f), "PlasticGreen");
            can.Collider(new Vector3(0f, 0.15f, -0.05f), new Vector3(0.28f, 0.3f, 0.45f));
            Save("Garden", "WateringCan", can, VillageAssets.LightPlastic);

            var rake = new PropModel();
            rake.Part(new Vector3(0f, 0.02f, 0f), new Vector3(0.03f, 0.03f, 1.5f), "WoodLight");
            rake.Part(new Vector3(0f, 0.03f, -0.76f), new Vector3(0.42f, 0.04f, 0.05f), "Metal");
            for (int i = 0; i < 6; i++)
                rake.Part(new Vector3(-0.18f + i * 0.072f, 0.03f, -0.83f), new Vector3(0.015f, 0.03f, 0.1f), "Metal");
            rake.Collider(new Vector3(0f, 0.03f, -0.05f), new Vector3(0.42f, 0.06f, 1.65f));
            Save("Garden", "GardenRake", rake, VillageAssets.LightPlastic);

            var shovel = new PropModel();
            shovel.Part(new Vector3(0f, 0.02f, 0f), new Vector3(0.035f, 0.035f, 1.1f), "WoodLight");
            shovel.Part(new Vector3(0f, 0.02f, 0.58f), new Vector3(0.14f, 0.03f, 0.05f), "WoodLight");
            shovel.Part(new Vector3(0f, 0.015f, -0.68f), new Vector3(0.24f, 0.02f, 0.3f), "Metal");
            shovel.Collider(new Vector3(0f, 0.03f, -0.05f), new Vector3(0.25f, 0.06f, 1.5f));
            Save("Garden", "Shovel", shovel, VillageAssets.LightPlastic);

            var crate = new PropModel();
            crate.Part(new Vector3(0f, 0.25f, 0f), new Vector3(0.5f, 0.5f, 0.5f), "WoodLight", true);
            foreach (float y in new[] { 0.08f, 0.42f })
                crate.Part(new Vector3(0f, y, 0f), new Vector3(0.52f, 0.06f, 0.52f), "WoodDark");
            Save("Garden", "WoodCrate", crate, VillageAssets.LightFurniture);

            var box = new PropModel();
            box.Part(new Vector3(0f, 0.175f, 0f), new Vector3(0.45f, 0.35f, 0.4f), "Cardboard", true);
            box.Part(new Vector3(0f, 0.351f, 0f), new Vector3(0.08f, 0.005f, 0.41f), "FabricCream");
            Save("Garden", "CardboardBox", box, VillageAssets.LightPlastic);

            var picnic = new PropModel();
            picnic.Part(new Vector3(0f, 0.75f, 0f), new Vector3(1.8f, 0.05f, 0.75f), "WoodLight", true);
            foreach (float side in Sides)
            {
                picnic.Part(new Vector3(0f, 0.45f, side * 0.62f), new Vector3(1.8f, 0.05f, 0.3f), "WoodLight", true);
                picnic.Part(new Vector3(side * 0.7f, 0.37f, 0f), new Vector3(0.08f, 0.74f, 1.5f), "WoodDark");
            }
            picnic.Collider(new Vector3(0f, 0.37f, 0f), new Vector3(1.5f, 0.74f, 0.3f));
            Save("Garden", "PicnicTable", picnic, VillageAssets.Heavy);

            var dogHouse = new PropModel();
            dogHouse.Part(new Vector3(0f, 0.3f, 0f), new Vector3(0.8f, 0.6f, 0.9f), "WoodLight", true);
            dogHouse.Mesh.SetTransform(new Vector3(0f, 0.6f, 0f), Quaternion.Euler(0f, 90f, 0f));
            dogHouse.Mesh.Gable(Vector3.zero, 1.05f, 0.95f, 0.4f, "RoofRed");
            dogHouse.Mesh.ResetTransform();
            dogHouse.Part(new Vector3(0f, 0.22f, -0.451f), new Vector3(0.32f, 0.4f, 0.01f), "Black");
            dogHouse.Collider(new Vector3(0f, 0.8f, 0f), new Vector3(0.8f, 0.4f, 0.9f));
            Save("Garden", "DogHouse", dogHouse, VillageAssets.Furniture);

            var sign = new PropModel();
            foreach (float side in Sides)
            {
                Rotated(sign, new Vector3(0f, 0.42f, side * 0.11f), new Vector3(side * -14f, 0f, 0f), new Vector3(0.6f, 0.88f, 0.04f), "WoodLight");
                Rotated(sign, new Vector3(0f, 0.45f, side * 0.135f), new Vector3(side * -14f, 0f, 0f), new Vector3(0.48f, 0.66f, 0.01f), "Black");
            }
            sign.Collider(new Vector3(0f, 0.43f, 0f), new Vector3(0.6f, 0.86f, 0.45f));
            Save("Garden", "SidewalkSign", sign, VillageAssets.LightFurniture);
        }

        private static void TrashBin(string name, string color)
        {
            var model = new PropModel();
            model.Mesh.Cylinder(new Vector3(0f, 0.06f, 0f), 0.29f, 0.33f, 0.9f, 8, color);
            model.Mesh.Cylinder(new Vector3(0f, 0.96f, 0f), 0.36f, 0.34f, 0.07f, 8, color);
            model.Part(new Vector3(0f, 1.0f, 0.32f), new Vector3(0.3f, 0.05f, 0.06f), "Black");
            foreach (float side in Sides)
                Horizontal(model.Mesh, new Vector3(side * 0.25f, 0.09f, 0.26f), 0.09f, 0.06f, "Rubber");
            model.ConvexCollider = true;
            Save("Garden", name, model, VillageAssets.Furniture);
        }

        // ───────────── Street ─────────────

        private static void BuildStreetProps()
        {
            RoadSign("RoadSign_Stop", 0);
            RoadSign("RoadSign_Speed", 1);
            RoadSign("RoadSign_Warning", 2);

            var cone = new PropModel();
            cone.Part(new Vector3(0f, 0.02f, 0f), new Vector3(0.4f, 0.04f, 0.4f), "PlasticOrange");
            cone.Mesh.Cylinder(new Vector3(0f, 0.04f, 0f), 0.15f, 0.03f, 0.6f, 8, "PlasticOrange");
            cone.Mesh.Cylinder(new Vector3(0f, 0.28f, 0f), 0.108f, 0.088f, 0.1f, 8, "PlasticWhite");
            cone.ConvexCollider = true;
            Save("Street", "TrafficCone", cone, VillageAssets.LightPlastic);

            var lamp = new PropModel();
            lamp.Mesh.Cylinder(Vector3.zero, 0.18f, 0.16f, 0.3f, 8, "MetalDark");
            lamp.Mesh.Cylinder(new Vector3(0f, 0.3f, 0f), 0.07f, 0.05f, 4.8f, 8, "MetalDark");
            lamp.Part(new Vector3(0f, 5.05f, -0.5f), new Vector3(0.06f, 0.06f, 1.1f), "MetalDark");
            lamp.Part(new Vector3(0f, 4.98f, -1.1f), new Vector3(0.32f, 0.14f, 0.5f), "MetalDark");
            lamp.Part(new Vector3(0f, 4.9f, -1.1f), new Vector3(0.26f, 0.03f, 0.4f), "Lamp");
            lamp.Collider(new Vector3(0f, 2.6f, 0f), new Vector3(0.22f, 5.2f, 0.22f));
            Save("Street", "StreetLamp", lamp, null);

            var fence = new PropModel();
            foreach (float side in Sides)
                fence.Part(new Vector3(side * 0.97f, 0.5f, 0f), new Vector3(0.1f, 1f, 0.1f), "Picket");
            foreach (float y in new[] { 0.3f, 0.72f })
                fence.Part(new Vector3(0f, y, 0.05f), new Vector3(2f, 0.07f, 0.04f), "Picket");
            for (int i = 0; i < 9; i++)
            {
                float x = -0.8f + i * 0.2f;
                fence.Part(new Vector3(x, 0.42f, -0.01f), new Vector3(0.1f, 0.84f, 0.03f), "Picket");
                fence.Mesh.Tri(new Vector3(x - 0.05f, 0.84f, -0.01f), new Vector3(x + 0.05f, 0.84f, -0.01f), new Vector3(x, 0.94f, -0.01f), new Vector3(x, 0.86f, 0.5f), "Picket");
                fence.Mesh.Tri(new Vector3(x - 0.05f, 0.84f, -0.01f), new Vector3(x + 0.05f, 0.84f, -0.01f), new Vector3(x, 0.94f, -0.01f), new Vector3(x, 0.86f, -0.5f), "Picket");
            }
            fence.Collider(new Vector3(0f, 0.5f, 0f), new Vector3(2f, 1f, 0.14f));
            Save("Street", "PicketFence", fence, null);

            var hedge = new PropModel();
            hedge.Part(new Vector3(0f, 0.45f, 0f), new Vector3(2f, 0.9f, 0.8f), "Leaves", true);
            for (int i = 0; i < 3; i++)
                hedge.Mesh.Sphere(new Vector3(-0.6f + i * 0.6f, 0.9f, 0f), new Vector3(0.42f, 0.2f, 0.42f), 7, 3, "LeavesDark");
            Save("Street", "Hedge", hedge, null);

            Car("Car_Red", "CarRed");
            Car("Car_Blue", "CarBlue");
            Car("Car_Yellow", "CarYellow");

            var bollard = new PropModel();
            bollard.Mesh.Cylinder(Vector3.zero, 0.12f, 0.12f, 0.9f, 8, "GasYellow");
            bollard.Mesh.Cylinder(new Vector3(0f, 0.6f, 0f), 0.125f, 0.125f, 0.12f, 8, "Black");
            bollard.Collider(new Vector3(0f, 0.45f, 0f), new Vector3(0.24f, 0.9f, 0.24f));
            Save("Street", "Bollard", bollard, null);
        }

        /// <summary>Road sign on a weighted base: strong water tilts it and knocks it over.</summary>
        private static void RoadSign(string name, int kind)
        {
            var model = new PropModel();
            model.Part(new Vector3(0f, 0.03f, 0f), new Vector3(0.45f, 0.06f, 0.45f), "Concrete");
            model.Mesh.Cylinder(new Vector3(0f, 0.06f, 0f), 0.035f, 0.035f, 2.2f, 6, "Metal");

            model.Mesh.SetTransform(new Vector3(0f, 2.0f, -0.05f), Quaternion.identity);
            switch (kind)
            {
                case 0:
                    model.Mesh.Extrude(Polygon(8, 0.41f, 22.5f), 0.02f, 0.03f, "RoadWhite");
                    model.Mesh.Extrude(Polygon(8, 0.38f, 22.5f), -0.02f, 0.02f, "GasRed");
                    break;
                case 1:
                    model.Mesh.Box(new Vector3(0f, 0f, 0.025f), new Vector3(0.58f, 0.74f, 0.01f), "Black");
                    model.Mesh.Box(Vector3.zero, new Vector3(0.52f, 0.68f, 0.04f), "RoadWhite");
                    model.Mesh.Box(new Vector3(0f, -0.08f, -0.021f), new Vector3(0.3f, 0.26f, 0.005f), "Black");
                    break;
                default:
                    model.Mesh.Extrude(Polygon(4, 0.46f, 0f), 0.02f, 0.03f, "Black");
                    model.Mesh.Extrude(Polygon(4, 0.42f, 0f), -0.02f, 0.02f, "GasYellow");
                    break;
            }
            model.Mesh.ResetTransform();

            model.Collider(new Vector3(0f, 0.03f, 0f), new Vector3(0.45f, 0.06f, 0.45f));
            model.Collider(new Vector3(0f, 1.15f, 0f), new Vector3(0.1f, 2.2f, 0.1f));
            model.Collider(new Vector3(0f, 2f, -0.05f), new Vector3(0.8f, 0.8f, 0.07f));
            Save("Street", name, model, VillageAssets.Sign);
        }

        private static void Car(string name, string color)
        {
            var model = new PropModel();
            model.Part(new Vector3(0f, 0.6f, 0f), new Vector3(1.8f, 0.6f, 4f), color);
            model.Part(new Vector3(0f, 1.15f, 0.2f), new Vector3(1.6f, 0.55f, 2.1f), color);
            model.Part(new Vector3(0f, 1.15f, 0.2f), new Vector3(1.62f, 0.38f, 1.8f), "Screen");
            model.Part(new Vector3(0f, 1.15f, 0.2f), new Vector3(1.4f, 0.38f, 2.12f), "Screen");
            model.Part(new Vector3(0f, 0.42f, -2.02f), new Vector3(1.82f, 0.18f, 0.08f), "Metal");
            model.Part(new Vector3(0f, 0.42f, 2.02f), new Vector3(1.82f, 0.18f, 0.08f), "Metal");
            foreach (float side in Sides)
            {
                model.Part(new Vector3(side * 0.62f, 0.7f, -2.01f), new Vector3(0.3f, 0.14f, 0.04f), "Lamp");
                model.Part(new Vector3(side * 0.66f, 0.72f, 2.01f), new Vector3(0.26f, 0.12f, 0.04f), "PlasticRed");
                foreach (float z in new[] { -1.3f, 1.3f })
                    Horizontal(model.Mesh, new Vector3(side * 0.82f, 0.34f, z), 0.34f, 0.26f, "Rubber");
            }
            // The body collider reaches the ground (wheel bottoms), so the pushable car does not sink.
            model.Collider(new Vector3(0f, 0.525f, 0f), new Vector3(1.8f, 1.05f, 4f));
            model.Collider(new Vector3(0f, 1.15f, 0.2f), new Vector3(1.6f, 0.55f, 2.1f));
            Save("Street", name, model, VillageAssets.Car);
        }

        // ───────────── Interior ─────────────

        private static void BuildInteriorProps()
        {
            Sofa("Sofa", 2.1f, VillageAssets.Heavy);
            Sofa("Armchair", 1.0f, VillageAssets.Furniture);

            var coffee = new PropModel();
            coffee.Part(new Vector3(0f, 0.42f, 0f), new Vector3(1.1f, 0.06f, 0.6f), "WoodDark");
            coffee.Part(new Vector3(0f, 0.12f, 0f), new Vector3(1f, 0.03f, 0.5f), "WoodDark");
            Legs(coffee, 0.5f, 0.26f, 0.42f, 0.03f, "WoodDark");
            coffee.Collider(new Vector3(0f, 0.225f, 0f), new Vector3(1.1f, 0.45f, 0.6f));
            Save("Interior", "CoffeeTable", coffee, VillageAssets.Furniture);

            var shelf = new PropModel();
            foreach (float side in Sides)
                shelf.Part(new Vector3(side * 0.48f, 0.95f, 0f), new Vector3(0.04f, 1.9f, 0.35f), "WoodDark");
            shelf.Part(new Vector3(0f, 0.95f, 0.165f), new Vector3(1f, 1.9f, 0.02f), "WoodDark");
            string[] books = { "Book1", "Book2", "Book3", "FabricCream" };
            var random = new System.Random(7);
            foreach (float y in new[] { 0.03f, 0.47f, 0.92f, 1.37f, 1.88f })
            {
                shelf.Part(new Vector3(0f, y, 0f), new Vector3(0.92f, 0.04f, 0.33f), "WoodDark");
                if (y > 1.8f)
                    continue;
                float x = -0.42f;
                while (x < 0.3f)
                {
                    float width = 0.04f + (float)random.NextDouble() * 0.05f;
                    float height = 0.24f + (float)random.NextDouble() * 0.12f;
                    shelf.Part(new Vector3(x + width * 0.5f, y + 0.02f + height * 0.5f, 0f), new Vector3(width, height, 0.24f), books[random.Next(books.Length)]);
                    x += width + 0.005f;
                }
            }
            shelf.Collider(new Vector3(0f, 0.95f, 0f), new Vector3(1f, 1.9f, 0.35f));
            Save("Interior", "Bookshelf", shelf, VillageAssets.Heavy);

            var tvStand = new PropModel();
            tvStand.Part(new Vector3(0f, 0.25f, 0f), new Vector3(1.4f, 0.5f, 0.45f), "WoodDark", true);
            foreach (float x in new[] { -0.35f, 0.35f })
                tvStand.Part(new Vector3(x, 0.25f, -0.226f), new Vector3(0.6f, 0.36f, 0.01f), "WoodLight");
            Save("Interior", "TVStand", tvStand, VillageAssets.Furniture);

            var tv = new PropModel();
            tv.Part(new Vector3(0f, 0.43f, 0f), new Vector3(1.1f, 0.65f, 0.06f), "Black");
            tv.Part(new Vector3(0f, 0.43f, -0.032f), new Vector3(1.02f, 0.57f, 0.01f), "Screen");
            tv.Part(new Vector3(0f, 0.015f, 0f), new Vector3(0.35f, 0.03f, 0.2f), "Black");
            tv.Part(new Vector3(0f, 0.07f, 0f), new Vector3(0.06f, 0.1f, 0.04f), "Black");
            tv.Collider(new Vector3(0f, 0.38f, 0f), new Vector3(1.1f, 0.76f, 0.2f));
            Save("Interior", "Television", tv, VillageAssets.LightFurniture);

            var floorLamp = new PropModel();
            floorLamp.Mesh.Cylinder(Vector3.zero, 0.18f, 0.18f, 0.04f, 8, "MetalDark");
            floorLamp.Mesh.Cylinder(new Vector3(0f, 0.04f, 0f), 0.02f, 0.02f, 1.35f, 6, "Metal");
            floorLamp.Mesh.Cylinder(new Vector3(0f, 1.3f, 0f), 0.25f, 0.17f, 0.3f, 8, "FabricCream");
            floorLamp.Mesh.Sphere(new Vector3(0f, 1.36f, 0f), Vector3.one * 0.06f, 6, 4, "Lamp");
            floorLamp.Collider(new Vector3(0f, 0.025f, 0f), new Vector3(0.36f, 0.05f, 0.36f));
            floorLamp.Collider(new Vector3(0f, 0.7f, 0f), new Vector3(0.06f, 1.3f, 0.06f));
            floorLamp.Collider(new Vector3(0f, 1.45f, 0f), new Vector3(0.48f, 0.3f, 0.48f));
            Save("Interior", "FloorLamp", floorLamp, VillageAssets.Tall);

            var tableLamp = new PropModel();
            tableLamp.Mesh.Cylinder(Vector3.zero, 0.09f, 0.09f, 0.04f, 8, "WoodDark");
            tableLamp.Mesh.Sphere(new Vector3(0f, 0.14f, 0f), new Vector3(0.09f, 0.11f, 0.09f), 7, 4, "Ceramic");
            tableLamp.Mesh.Cylinder(new Vector3(0f, 0.24f, 0f), 0.17f, 0.11f, 0.2f, 8, "FabricCream");
            tableLamp.Collider(new Vector3(0f, 0.22f, 0f), new Vector3(0.34f, 0.44f, 0.34f));
            Save("Interior", "TableLamp", tableLamp, VillageAssets.SmallDecor);

            var dining = new PropModel();
            dining.Part(new Vector3(0f, 0.74f, 0f), new Vector3(1.6f, 0.06f, 0.9f), "WoodLight", true);
            Legs(dining, 0.72f, 0.38f, 0.72f, 0.04f, "WoodLight");
            dining.Collider(new Vector3(0f, 0.36f, 0f), new Vector3(1.4f, 0.72f, 0.1f));
            Save("Interior", "DiningTable", dining, VillageAssets.Heavy);

            var diningChair = new PropModel();
            diningChair.Part(new Vector3(0f, 0.46f, 0f), new Vector3(0.45f, 0.05f, 0.45f), "WoodLight");
            Legs(diningChair, 0.19f, 0.19f, 0.44f, 0.025f, "WoodLight");
            foreach (float side in Sides)
                diningChair.Part(new Vector3(side * 0.2f, 0.73f, 0.2f), new Vector3(0.04f, 0.5f, 0.04f), "WoodLight");
            foreach (float y in new[] { 0.65f, 0.82f, 0.96f })
                diningChair.Part(new Vector3(0f, y, 0.2f), new Vector3(0.4f, 0.06f, 0.03f), "WoodLight");
            diningChair.Collider(new Vector3(0f, 0.245f, 0f), new Vector3(0.45f, 0.49f, 0.45f));
            diningChair.Collider(new Vector3(0f, 0.74f, 0.2f), new Vector3(0.45f, 0.5f, 0.06f));
            Save("Interior", "DiningChair", diningChair, VillageAssets.LightFurniture);

            var counter = new PropModel();
            counter.Part(new Vector3(0f, 0.43f, 0f), new Vector3(2.4f, 0.86f, 0.6f), "Trim", true);
            counter.Part(new Vector3(0f, 0.885f, -0.01f), new Vector3(2.45f, 0.05f, 0.64f), "Stone");
            for (int i = 0; i < 4; i++)
                counter.Part(new Vector3(-0.9f + i * 0.6f, 0.43f, -0.301f), new Vector3(0.54f, 0.72f, 0.01f), "WoodLight");
            counter.Part(new Vector3(-0.5f, 0.9f, -0.02f), new Vector3(0.6f, 0.02f, 0.42f), "Metal");
            counter.Part(new Vector3(0.6f, 0.92f, -0.02f), new Vector3(0.62f, 0.04f, 0.5f), "Black");
            foreach (Vector2 burner in new[] { new Vector2(0.45f, -0.14f), new Vector2(0.75f, -0.14f), new Vector2(0.45f, 0.1f), new Vector2(0.75f, 0.1f) })
                counter.Mesh.Cylinder(new Vector3(burner.x, 0.94f, burner.y), 0.08f, 0.08f, 0.01f, 8, "MetalDark");
            Save("Interior", "KitchenCounter", counter, VillageAssets.Heavy);

            var vase = new PropModel();
            vase.Mesh.Cylinder(Vector3.zero, 0.07f, 0.13f, 0.2f, 8, "Ceramic");
            vase.Mesh.Cylinder(new Vector3(0f, 0.2f, 0f), 0.13f, 0.05f, 0.14f, 8, "Ceramic");
            vase.Mesh.Cylinder(new Vector3(0f, 0.34f, 0f), 0.05f, 0.07f, 0.06f, 8, "Ceramic");
            vase.ConvexCollider = true;
            Save("Interior", "Vase", vase, VillageAssets.SmallDecor);

            var rug = new PropModel();
            rug.Part(new Vector3(0f, 0.006f, 0f), new Vector3(2.4f, 0.012f, 1.6f), "Rug");
            rug.Part(new Vector3(0f, 0.008f, 0f), new Vector3(2.0f, 0.012f, 1.2f), "FabricCream");
            rug.Part(new Vector3(0f, 0.01f, 0f), new Vector3(1.8f, 0.012f, 1.0f), "Rug");
            Save("Interior", "Rug", rug, null);

            var sideboard = new PropModel();
            sideboard.Part(new Vector3(0f, 0.45f, 0f), new Vector3(1.4f, 0.8f, 0.45f), "WoodDark", true);
            foreach (float x in new[] { -0.35f, 0.35f })
            {
                sideboard.Part(new Vector3(x, 0.45f, -0.226f), new Vector3(0.62f, 0.66f, 0.01f), "WoodLight");
                sideboard.Mesh.Sphere(new Vector3(x + (x < 0f ? 0.24f : -0.24f), 0.5f, -0.24f), Vector3.one * 0.025f, 5, 3, "Metal");
            }
            Legs(sideboard, 0.62f, 0.17f, 0.05f, 0.03f, "WoodDark");
            Save("Interior", "Sideboard", sideboard, VillageAssets.Heavy);

            var bed = new PropModel();
            bed.Part(new Vector3(0f, 0.175f, 0f), new Vector3(1.5f, 0.35f, 2.1f), "WoodDark");
            bed.Part(new Vector3(0f, 0.45f, 0f), new Vector3(1.4f, 0.2f, 2.0f), "FabricCream");
            bed.Part(new Vector3(0f, 0.57f, -0.35f), new Vector3(1.44f, 0.06f, 1.3f), "FabricTeal");
            foreach (float side in Sides)
                bed.Part(new Vector3(side * 0.35f, 0.62f, 0.72f), new Vector3(0.55f, 0.12f, 0.35f), "PlasticWhite");
            bed.Part(new Vector3(0f, 0.55f, 1.04f), new Vector3(1.5f, 1.1f, 0.08f), "WoodDark");
            bed.Collider(new Vector3(0f, 0.3f, 0f), new Vector3(1.5f, 0.6f, 2.1f));
            bed.Collider(new Vector3(0f, 0.55f, 1.04f), new Vector3(1.5f, 1.1f, 0.08f));
            Save("Interior", "Bed", bed, VillageAssets.Heavy, true);

            var nightstand = new PropModel();
            nightstand.Part(new Vector3(0f, 0.25f, 0f), new Vector3(0.45f, 0.5f, 0.4f), "WoodLight", true);
            nightstand.Part(new Vector3(0f, 0.36f, -0.201f), new Vector3(0.38f, 0.14f, 0.01f), "WoodDark");
            Save("Interior", "Nightstand", nightstand, VillageAssets.LightFurniture);

            var wardrobe = new PropModel();
            wardrobe.Part(new Vector3(0f, 1f, 0f), new Vector3(1.2f, 2f, 0.55f), "WoodLight", true);
            wardrobe.Part(new Vector3(0f, 1f, -0.276f), new Vector3(0.02f, 1.85f, 0.01f), "WoodDark");
            foreach (float side in Sides)
                wardrobe.Part(new Vector3(side * 0.07f, 1.05f, -0.29f), new Vector3(0.03f, 0.2f, 0.03f), "Metal");
            Save("Interior", "Wardrobe", wardrobe, VillageAssets.Heavy);

            // Hangs from its pivot (the ceiling) downwards.
            var ceilingLamp = new PropModel();
            ceilingLamp.Mesh.Cylinder(new Vector3(0f, -0.4f, 0f), 0.01f, 0.01f, 0.4f, 4, "Black");
            ceilingLamp.Mesh.Cylinder(new Vector3(0f, -0.62f, 0f), 0.3f, 0.1f, 0.22f, 8, "Wainscot");
            ceilingLamp.Mesh.Sphere(new Vector3(0f, -0.62f, 0f), Vector3.one * 0.07f, 6, 4, "Lamp");
            Save("Interior", "CeilingLamp", ceilingLamp, null);

            var stack = new PropModel();
            float top = 0f;
            for (int i = 0; i < 4; i++)
            {
                float height = 0.035f + i % 2 * 0.015f;
                Rotated(stack, new Vector3(0f, top + height * 0.5f, 0f), new Vector3(0f, i * 9f - 12f, 0f), new Vector3(0.24f, height, 0.17f), books[i]);
                top += height;
            }
            stack.Collider(new Vector3(0f, top * 0.5f, 0f), new Vector3(0.26f, top, 0.2f));
            Save("Interior", "BookStack", stack, VillageAssets.SmallDecor);
        }

        private static void Sofa(string name, float width, string profile)
        {
            var model = new PropModel();
            float inner = width - 0.44f;
            model.Part(new Vector3(0f, 0.2f, 0f), new Vector3(width, 0.4f, 0.9f), "FabricOrange");
            int seats = width > 1.5f ? 3 : 1;
            for (int i = 0; i < seats; i++)
            {
                float x = -inner * 0.5f + inner * (i + 0.5f) / seats;
                model.Part(new Vector3(x, 0.47f, -0.08f), new Vector3(inner / seats - 0.02f, 0.14f, 0.62f), "FabricOrange");
                Rotated(model, new Vector3(x, 0.74f, 0.2f), new Vector3(8f, 0f, 0f), new Vector3(inner / seats - 0.04f, 0.42f, 0.15f), "FabricOrange");
            }
            model.Part(new Vector3(0f, 0.65f, 0.34f), new Vector3(width, 0.55f, 0.22f), "FabricOrange");
            foreach (float side in Sides)
                model.Part(new Vector3(side * (width * 0.5f - 0.11f), 0.31f, 0f), new Vector3(0.22f, 0.62f, 0.9f), "FabricOrange");
            Legs(model, width * 0.5f - 0.1f, 0.38f, 0.06f, 0.03f, "WoodDark");
            model.Collider(new Vector3(0f, 0.31f, 0f), new Vector3(width, 0.62f, 0.9f));
            model.Collider(new Vector3(0f, 0.65f, 0.34f), new Vector3(width, 0.55f, 0.22f));
            Save("Interior", name, model, profile, true);
        }

        // ───────────── Gas station ─────────────

        private static void BuildGasStationProps()
        {
            var dispenser = new PropModel();
            dispenser.Part(new Vector3(0f, 1f, 0f), new Vector3(0.9f, 1.7f, 0.55f), "GasWhite", true);
            dispenser.Part(new Vector3(0f, 0.12f, 0f), new Vector3(0.94f, 0.24f, 0.58f), "GasRed");
            dispenser.Part(new Vector3(0f, 1.95f, 0f), new Vector3(0.96f, 0.3f, 0.6f), "GasRed");
            foreach (float side in Sides)
            {
                dispenser.Part(new Vector3(0f, 1.4f, side * 0.28f), new Vector3(0.5f, 0.3f, 0.01f), "Screen");
                dispenser.Part(new Vector3(side * 0.47f, 1.05f, 0f), new Vector3(0.06f, 0.24f, 0.14f), "GasYellow");
                dispenser.Part(new Vector3(side * 0.47f, 0.95f, 0f), new Vector3(0.05f, 0.1f, 0.06f), "Black");
                dispenser.Mesh.Cylinder(new Vector3(side * 0.49f, 0.3f, 0.12f), 0.02f, 0.02f, 0.75f, 5, "Black");
            }
            Save("GasStation", "FuelDispenser", dispenser, null);

            var island = new PropModel();
            island.Part(new Vector3(0f, 0.1f, 0f), new Vector3(1.4f, 0.2f, 5f), "Concrete", true);
            foreach (float side in Sides)
            {
                island.Part(new Vector3(side * 0.69f, 0.1f, 0f), new Vector3(0.03f, 0.2f, 5f), "GasYellow");
                island.Mesh.Cylinder(new Vector3(0f, 0.2f, side * 2.3f), 0.11f, 0.11f, 0.9f, 8, "GasYellow");
                island.Collider(new Vector3(0f, 0.65f, side * 2.3f), new Vector3(0.22f, 0.9f, 0.22f));
            }
            Save("GasStation", "FuelIsland", island, null);

            var canister = new PropModel();
            canister.Part(new Vector3(0f, 0.16f, 0f), new Vector3(0.18f, 0.32f, 0.34f), "GasRed", true);
            canister.Part(new Vector3(0f, 0.35f, 0.05f), new Vector3(0.04f, 0.06f, 0.2f), "GasRed");
            Rotated(canister, new Vector3(0f, 0.36f, -0.13f), new Vector3(-30f, 0f, 0f), new Vector3(0.05f, 0.05f, 0.12f), "Black");
            canister.Collider(new Vector3(0f, 0.36f, 0.02f), new Vector3(0.18f, 0.08f, 0.3f));
            Save("GasStation", "GasCanister", canister, VillageAssets.LightPlastic);

            var oil = new PropModel();
            oil.Part(new Vector3(0f, 0.06f, 0f), new Vector3(0.45f, 0.12f, 0.3f), "PlasticBlue");
            for (int x = 0; x < 3; x++)
            {
                for (int z = 0; z < 2; z++)
                {
                    var position = new Vector3(-0.14f + x * 0.14f, 0.17f, -0.07f + z * 0.14f);
                    oil.Part(position, new Vector3(0.1f, 0.22f, 0.08f), "GasYellow");
                    oil.Part(position + new Vector3(0f, 0.13f, 0f), new Vector3(0.03f, 0.04f, 0.03f), "Black");
                }
            }
            oil.Collider(new Vector3(0f, 0.16f, 0f), new Vector3(0.45f, 0.32f, 0.3f));
            Save("GasStation", "OilBottleCrate", oil, VillageAssets.SmallDecor);

            var squeegee = new PropModel();
            squeegee.Mesh.Cylinder(Vector3.zero, 0.16f, 0.19f, 0.34f, 8, "PlasticBlue");
            Rotated(squeegee, new Vector3(0.08f, 0.6f, 0f), new Vector3(0f, 0f, -12f), new Vector3(0.03f, 0.8f, 0.03f), "Black");
            Rotated(squeegee, new Vector3(0.16f, 1.0f, 0f), new Vector3(0f, 0f, -12f), new Vector3(0.05f, 0.06f, 0.3f), "PlasticYellow");
            squeegee.Collider(new Vector3(0f, 0.17f, 0f), new Vector3(0.38f, 0.34f, 0.38f));
            Save("GasStation", "WindshieldBucket", squeegee, VillageAssets.LightPlastic);

            var tires = new PropModel();
            for (int i = 0; i < 3; i++)
                tires.Mesh.Cylinder(new Vector3(0f, i * 0.22f, 0f), 0.34f, 0.34f, 0.21f, 10, "Rubber");
            tires.Mesh.Cylinder(new Vector3(0f, 0.66f, 0f), 0.18f, 0.18f, 0.005f, 8, "Metal");
            tires.ConvexCollider = true;
            Save("GasStation", "TireStack", tires, VillageAssets.Furniture);

            var ice = new PropModel();
            ice.Part(new Vector3(0f, 0.7f, 0f), new Vector3(1f, 1.4f, 0.7f), "GasWhite", true);
            ice.Part(new Vector3(0f, 0.95f, -0.351f), new Vector3(0.88f, 0.6f, 0.01f), "PlasticBlue");
            ice.Part(new Vector3(0.38f, 0.6f, -0.36f), new Vector3(0.04f, 0.3f, 0.04f), "Metal");
            Save("GasStation", "IceChest", ice, VillageAssets.Heavy);

            var rack = new PropModel();
            foreach (float side in Sides)
                rack.Part(new Vector3(side * 0.88f, 0.8f, 0f), new Vector3(0.04f, 1.6f, 0.5f), "Metal", true);
            rack.Part(new Vector3(0f, 0.8f, 0.24f), new Vector3(1.8f, 1.6f, 0.02f), "MetalDark", true);
            string[] goods = { "PlasticRed", "PlasticYellow", "PlasticBlue", "PlasticGreen", "PlasticOrange" };
            var stock = new System.Random(3);
            foreach (float y in new[] { 0.1f, 0.55f, 1.0f, 1.45f })
            {
                rack.Part(new Vector3(0f, y, 0f), new Vector3(1.72f, 0.03f, 0.48f), "Metal", true);
                if (y > 1.4f)
                    continue;
                for (float x = -0.78f; x < 0.75f; x += 0.17f)
                    rack.Part(new Vector3(x, y + 0.13f, 0.02f), new Vector3(0.14f, 0.22f + (float)stock.NextDouble() * 0.06f, 0.3f), goods[stock.Next(goods.Length)]);
            }
            Save("GasStation", "ShopShelf", rack, VillageAssets.Heavy);

            var shopCounter = new PropModel();
            shopCounter.Part(new Vector3(0f, 0.5f, 0f), new Vector3(2.2f, 1f, 0.7f), "GasRed", true);
            shopCounter.Part(new Vector3(0f, 1.02f, 0f), new Vector3(2.3f, 0.05f, 0.8f), "Stone");
            Save("GasStation", "ShopCounter", shopCounter, VillageAssets.Heavy);

            var register = new PropModel();
            register.Part(new Vector3(0f, 0.06f, 0f), new Vector3(0.4f, 0.12f, 0.35f), "MetalDark");
            Rotated(register, new Vector3(0f, 0.22f, 0.05f), new Vector3(-20f, 0f, 0f), new Vector3(0.3f, 0.2f, 0.03f), "Screen");
            register.Collider(new Vector3(0f, 0.15f, 0f), new Vector3(0.4f, 0.3f, 0.35f));
            Save("GasStation", "CashRegister", register, VillageAssets.SmallDecor);

            SnackBox("SnackBox_Red", "PlasticRed");
            SnackBox("SnackBox_Yellow", "PlasticYellow");
            SnackBox("SnackBox_Green", "PlasticGreen");

            var bottles = new PropModel();
            for (int x = 0; x < 3; x++)
            {
                for (int z = 0; z < 2; z++)
                    bottles.Mesh.Cylinder(new Vector3(-0.08f + x * 0.08f, 0f, -0.04f + z * 0.08f), 0.035f, 0.035f, 0.28f, 6, "PlasticBlue");
            }
            bottles.Part(new Vector3(0f, 0.12f, 0f), new Vector3(0.25f, 0.1f, 0.17f), "PlasticWhite");
            bottles.Collider(new Vector3(0f, 0.14f, 0f), new Vector3(0.25f, 0.28f, 0.17f));
            Save("GasStation", "BottlePack", bottles, VillageAssets.LightPlastic);

            var air = new PropModel();
            air.Part(new Vector3(0f, 0.6f, 0f), new Vector3(0.4f, 1.2f, 0.3f), "GasYellow", true);
            air.Part(new Vector3(0f, 0.95f, -0.151f), new Vector3(0.28f, 0.2f, 0.01f), "Screen");
            air.Mesh.SetTransform(new Vector3(0.24f, 0.6f, 0f), Quaternion.Euler(0f, 0f, 90f));
            air.Mesh.Ring(Vector3.zero, 0.16f, 0.03f, 8, "Black");
            air.Mesh.ResetTransform();
            Save("GasStation", "AirPump", air, VillageAssets.Heavy);

            var cage = new PropModel();
            cage.Part(new Vector3(0f, 0.03f, 0f), new Vector3(1.2f, 0.06f, 0.6f), "Metal");
            cage.Part(new Vector3(0f, 1.4f, 0f), new Vector3(1.2f, 0.06f, 0.6f), "Metal");
            for (int i = 0; i <= 8; i++)
            {
                float x = -0.6f + i * 0.15f;
                cage.Part(new Vector3(x, 0.7f, -0.3f), new Vector3(0.02f, 1.4f, 0.02f), "Metal");
                cage.Part(new Vector3(x, 0.7f, 0.3f), new Vector3(0.02f, 1.4f, 0.02f), "Metal");
            }
            for (int i = 0; i < 3; i++)
            {
                cage.Mesh.Cylinder(new Vector3(-0.38f + i * 0.38f, 0.06f, 0f), 0.15f, 0.15f, 0.55f, 8, "PlasticWhite");
                cage.Mesh.Sphere(new Vector3(-0.38f + i * 0.38f, 0.61f, 0f), new Vector3(0.15f, 0.08f, 0.15f), 8, 3, "PlasticWhite");
            }
            cage.Collider(new Vector3(0f, 0.72f, 0f), new Vector3(1.2f, 1.44f, 0.62f));
            Save("GasStation", "PropaneCage", cage, VillageAssets.Heavy);
        }

        private static void SnackBox(string name, string color)
        {
            var model = new PropModel();
            model.Part(new Vector3(0f, 0.16f, 0f), new Vector3(0.25f, 0.32f, 0.09f), color, true);
            model.Part(new Vector3(0f, 0.2f, -0.046f), new Vector3(0.2f, 0.1f, 0.005f), "PlasticWhite");
            Save("GasStation", name, model, VillageAssets.LightPlastic);
        }

        // ───────────── Helpers ─────────────

        private static readonly float[] Sides = { -1f, 1f };

        private static void Save(string folder, string name, PropModel model, string profile, bool flammable = false)
        {
            Prefabs[name] = VillageAssets.SaveProp(folder, name, model, profile, flammable);
        }

        private static void Legs(PropModel model, float x, float z, float height, float radius, string material)
        {
            foreach (float sx in Sides)
            {
                foreach (float sz in Sides)
                    model.Mesh.Cylinder(new Vector3(sx * x, 0f, sz * z), radius, radius, height, 6, material);
            }
        }

        private static void Rotated(PropModel model, Vector3 position, Vector3 euler, Vector3 size, string material)
        {
            model.Mesh.SetTransform(position, Quaternion.Euler(euler));
            model.Mesh.Box(Vector3.zero, size, material);
            model.Mesh.ResetTransform();
        }

        /// <summary>Cylinder lying along X (wheels, motors); <paramref name="alongZ"/> lays it along Z instead.</summary>
        private static void Horizontal(LowPolyMesh mesh, Vector3 center, float radius, float length, string material, bool alongZ = false)
        {
            mesh.SetTransform(center, alongZ ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.Euler(0f, 0f, 90f));
            mesh.Cylinder(new Vector3(0f, -length * 0.5f, 0f), radius, radius, length, 8, material);
            mesh.ResetTransform();
        }

        private static Vector3 RingPoint(float radius, float y, int index, int count)
        {
            float angle = index * Mathf.PI * 2f / count;
            return new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
        }

        public static Vector2[] Polygon(int sides, float radius, float startDegrees)
        {
            var points = new Vector2[sides];
            for (int i = 0; i < sides; i++)
            {
                float angle = (startDegrees + i * 360f / sides) * Mathf.Deg2Rad;
                points[i] = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
            }
            return points;
        }
    }
}
