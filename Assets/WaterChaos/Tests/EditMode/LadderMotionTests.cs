using NUnit.Framework;
using UnityEngine;
using WaterChaos.Environment;

namespace WaterChaos.Tests
{
    public class LadderMotionTests
    {
        private const float Climb = 2f;
        private const float Move = 1f;
        private const float Tolerance = 0.0001f;

        private static Vector3 Velocity(Vector2 input, Vector3 viewForward)
        {
            return LadderMotion.Velocity(input, viewForward, Vector3.right, Climb, Move);
        }

        [Test]
        public void Forward_LookingAhead_ClimbsUp()
        {
            Vector3 velocity = Velocity(new Vector2(0f, 1f), Vector3.forward);

            Assert.AreEqual(Climb, velocity.y, Tolerance);
        }

        [Test]
        public void Back_LookingAhead_ClimbsDown()
        {
            Vector3 velocity = Velocity(new Vector2(0f, -1f), Vector3.forward);

            Assert.AreEqual(-Climb, velocity.y, Tolerance);
        }

        [Test]
        public void Forward_LookingDown_ClimbsDown()
        {
            Vector3 lookDown = new Vector3(0f, -0.8f, 0.6f);

            Vector3 velocity = Velocity(new Vector2(0f, 1f), lookDown);

            Assert.AreEqual(-Climb, velocity.y, Tolerance);
        }

        [Test]
        public void Forward_AlsoMovesSlowlyAlongTheView_SoThePlayerCanStepOffAtTheTop()
        {
            Vector3 velocity = Velocity(new Vector2(0f, 1f), Vector3.forward);

            Assert.AreEqual(Move, velocity.z, Tolerance);
            Assert.AreEqual(0f, velocity.x, Tolerance);
        }

        [Test]
        public void Strafe_MovesSideways_WithoutClimbing()
        {
            Vector3 velocity = Velocity(new Vector2(1f, 0f), Vector3.forward);

            Assert.AreEqual(Move, velocity.x, Tolerance);
            Assert.AreEqual(0f, velocity.y, Tolerance);
        }

        [Test]
        public void NoInput_HoldsStill()
        {
            Assert.AreEqual(Vector3.zero, Velocity(Vector2.zero, Vector3.forward));
        }
    }
}
