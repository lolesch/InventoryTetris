using System;
using NUnit.Framework;
using ToolSmiths.InventorySystem.Probability;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Probability
{
    /// <summary>
    /// Locks MagicFindCascade against the spec's invariants and regression table.
    /// Tier order is [Nothing, White, Blue, Yellow, Orange]; rarest-first is
    /// [Orange, Yellow, Blue, White] with Diablo II's factors 250 / 600 / linear / linear.
    /// Live weights: White 160, Blue 80, Yellow 40, Orange 20 (== ItemRarity's asset).
    /// </summary>
    [TestFixture]
    public sealed class MagicFindCascadeTests
    {
        private const float Tol = 3e-3f; // the spec table is quoted to 0.1%; this covers its display rounding

        // indices into a Tier vector
        private const int Nothing = 0, White = 1, Blue = 2, Yellow = 3, Orange = 4;
        private static readonly int[] RarestFirst = { Orange, Yellow, Blue, White };
        private static readonly float[] Factors = { 250f, 600f, 0f, 0f };

        private static float[] Base(float nothing, float white, float blue, float yellow, float orange)
        {
            var raw = new[] { nothing, white, blue, yellow, orange };
            var sum = 0f;
            foreach (var v in raw) sum += v;
            for (var i = 0; i < raw.Length; i++) raw[i] /= sum;
            return raw;
        }

        private static float[] Live() => Base(0f, 160f, 80f, 40f, 20f);

        private static float[] Apply(float[] baseVector, float magicFind) =>
            MagicFindCascade.Apply(baseVector, Nothing, RarestFirst, Factors, magicFind);

        [Test]
        public void MagicFindZero_ReproducesTheAuthoredTableExactly()
        {
            var b = Live();
            var result = Apply(b, 0f);

            for (var i = 0; i < b.Length; i++)
                Assert.That(result[i], Is.EqualTo(b[i]).Within(1e-6f), $"index {i}");
        }

        [Test]
        public void RegressionTable_LiveWeights()
        {
            // design § "Behaviour at the live weights ... as the regression target"
            AssertRow(0f,   common: 0.533f, magic: 0.267f, rare: 0.133f, unique: 0.067f);
            AssertRow(100f, common: 0.217f, magic: 0.434f, rare: 0.235f, unique: 0.114f);
            AssertRow(200f, common: 0.000f, magic: 0.552f, rare: 0.307f, unique: 0.141f);
            AssertRow(400f, common: 0.000f, magic: 0.427f, rare: 0.404f, unique: 0.169f);
            AssertRow(800f, common: 0.000f, magic: 0.296f, rare: 0.510f, unique: 0.194f);

            void AssertRow(float mf, float common, float magic, float rare, float unique)
            {
                var r = Apply(Live(), mf);
                Assert.That(r[White],  Is.EqualTo(common).Within(Tol), $"Common @ {mf}");
                Assert.That(r[Blue],   Is.EqualTo(magic).Within(Tol),  $"Magic @ {mf}");
                Assert.That(r[Yellow], Is.EqualTo(rare).Within(Tol),   $"Rare @ {mf}");
                Assert.That(r[Orange], Is.EqualTo(unique).Within(Tol), $"Unique @ {mf}");
            }
        }

        [Test]
        public void PNoDrop_IsInvariantUnderMagicFind_EvenWithANonZeroFailWeight()
        {
            // headline regression: a table that ships fail probability 0.08
            var b = NormalizedWithFail(pFail: 0.08f, white: 160f, blue: 80f, yellow: 40f, orange: 20f);

            foreach (var mf in new[] { 0f, 50f, 100f, 250f, 500f, 1000f, 5000f })
                Assert.That(Apply(b, mf)[Nothing], Is.EqualTo(0.08f).Within(1e-6f), $"P(NoDrop) @ {mf}");
        }

        [Test]
        public void PUnique_IsMonotonicNonDecreasing_And_PCommon_NonIncreasing()
        {
            var prevU = -1f;
            var prevC = 2f;
            for (var mf = 0f; mf <= 2000f; mf += 25f)
            {
                var r = Apply(Live(), mf);
                Assert.That(r[Orange], Is.GreaterThanOrEqualTo(prevU - 1e-5f), $"Unique dipped @ {mf}");
                Assert.That(r[White], Is.LessThanOrEqualTo(prevC + 1e-5f), $"Common rose @ {mf}");
                prevU = r[Orange];
                prevC = r[White];
            }
        }

        [Test]
        public void VectorSumsToOne_AndStaysInRange_AcrossTheWholeSweep()
        {
            for (var mf = 0f; mf <= 5000f; mf += 50f)
            {
                var r = Apply(Live(), mf);
                var sum = 0f;
                foreach (var v in r)
                {
                    Assert.That(v, Is.InRange(-1e-5f, 1f + 1e-5f), $"entry out of range @ {mf}");
                    sum += v;
                }
                Assert.That(sum, Is.EqualTo(1f).Within(1e-4f), $"sum @ {mf}");
            }
        }

        [Test]
        public void ExtremeMagicFind_DoesNotThrow_AndNeverEmptiesTheDropTable()
        {
            foreach (var mf in new[] { 500f, 5000f, 1e6f, float.MaxValue })
            {
                Assert.That(() => Apply(Live(), mf), Throws.Nothing, $"mf {mf}");
                var r = Apply(Live(), mf);
                Assert.That(r[Nothing], Is.EqualTo(0f).Within(1e-6f), "still no phantom no-drop");
            }
        }

        [Test]
        public void Landmark_CommonReachesZero_At200PercentMagicFind()
        {
            // design § Further Notes — a deliberate, pinned consequence
            Assert.That(Apply(Live(), 199f)[White], Is.GreaterThan(0f));
            Assert.That(Apply(Live(), 200f)[White], Is.EqualTo(0f).Within(1e-4f));
        }

        [Test]
        public void Landmark_MagicOvertakesCommon_NearFiftyPercent()
        {
            Assert.That(Apply(Live(), 45f)[Blue],  Is.LessThan(Apply(Live(), 45f)[White]));
            Assert.That(Apply(Live(), 55f)[Blue],  Is.GreaterThan(Apply(Live(), 55f)[White]));
        }

        [Test]
        public void Landmark_RareOvertakesMagic_NearFourTwentyNine()
        {
            Assert.That(Apply(Live(), 420f)[Yellow], Is.LessThan(Apply(Live(), 420f)[Blue]));
            Assert.That(Apply(Live(), 440f)[Yellow], Is.GreaterThan(Apply(Live(), 440f)[Blue]));
        }

        private static float[] NormalizedWithFail(float pFail, float white, float blue, float yellow, float orange)
        {
            var k = (1f - pFail) / (white + blue + yellow + orange);
            return new[] { pFail, white * k, blue * k, yellow * k, orange * k };
        }
    }
}
