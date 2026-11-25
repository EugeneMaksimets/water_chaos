using NUnit.Framework;
using UnityEngine;
using WaterChaos.Interaction;

namespace WaterChaos.Tests
{
    public class InteractionHighlightTests
    {
        private const float Tolerance = 0.0001f;

        [Test]
        public void Show_SameTargetEveryFrame_KeepsHighlightingIt()
        {
            var truck = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var door = GameObject.CreatePrimitive(PrimitiveType.Cube);
            door.transform.SetParent(truck.transform);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            truck.GetComponent<Renderer>().sharedMaterial = material;
            door.GetComponent<Renderer>().sharedMaterial = material;
            var highlight = new InteractionHighlight(0.3f);

            try
            {
                for (int frame = 0; frame < 3; frame++)
                {
                    highlight.Show(door.transform, truck.transform);
                    Assert.AreEqual(door.transform, highlight.Current, $"frame {frame}");
                }

                Assert.IsTrue(door.GetComponent<Renderer>().HasPropertyBlock());
                Assert.IsFalse(truck.GetComponent<Renderer>().HasPropertyBlock());
            }
            finally
            {
                highlight.Clear();
                Object.DestroyImmediate(truck);
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void Clear_RemovesTheTint()
        {
            var door = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            door.GetComponent<Renderer>().sharedMaterial = material;
            var highlight = new InteractionHighlight(0.3f);

            try
            {
                highlight.Show(door.transform, door.transform);
                highlight.Clear();

                Assert.IsFalse(door.GetComponent<Renderer>().HasPropertyBlock());
                Assert.IsNull(highlight.Current);
            }
            finally
            {
                Object.DestroyImmediate(door);
                Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void Tint_MovesColorTowardsWhite()
        {
            Color tinted = InteractionHighlight.Tint(new Color(0.2f, 0.4f, 0f, 1f), 0.5f);

            Assert.AreEqual(0.6f, tinted.r, Tolerance);
            Assert.AreEqual(0.7f, tinted.g, Tolerance);
            Assert.AreEqual(0.5f, tinted.b, Tolerance);
        }

        [Test]
        public void Tint_KeepsAlpha()
        {
            Color tinted = InteractionHighlight.Tint(new Color(0f, 0f, 0f, 0.35f), 0.3f);

            Assert.AreEqual(0.35f, tinted.a, Tolerance);
        }

        [Test]
        public void Tint_ClampsAmount()
        {
            Color color = new Color(0.2f, 0.2f, 0.2f, 1f);

            Assert.AreEqual(color, InteractionHighlight.Tint(color, -1f));
            Assert.AreEqual(Color.white, InteractionHighlight.Tint(color, 2f));
            Assert.AreEqual(color, InteractionHighlight.Tint(color, float.NaN));
        }
    }
}
