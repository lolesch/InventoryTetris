using NUnit.Framework;
using ToolSmiths.InventorySystem.Items;

namespace ToolSmiths.InventorySystem.Tests.EditMode.Items
{
    /// <summary>
    /// <see cref="RarityCascade"/> binds the (already well-tested) <c>MagicFindCascade</c>
    /// transform to <see cref="ToolSmiths.InventorySystem.Data.Enums.ItemRarity"/>. These
    /// pin the binding: the fail slot is <c>NoDrop</c> at index 0, the rarest-first order
    /// and Diablo II factors match ADR-0004, and magic find of 0 is the identity - the
    /// invariant the generator's rarity roll rests on.
    ///
    /// Enum order is <c>[NoDrop, Common, Magic, Rare, Unique]</c>. Live weights
    /// <c>160 / 80 / 40 / 20</c> are the shipped <c>Item Rarity Distribution.asset</c>, so
    /// the regression rows match <c>MagicFindCascadeTests.RegressionTable_LiveWeights</c>.
    /// </summary>
    [TestFixture]
    public sealed class RarityCascadeTests
    {
        private const int NoDrop = 0, Common = 1, Magic = 2, Rare = 3, Unique = 4;
        private const float Tol = 3e-3f;

        private static float[] Authored() => Normalize(0f, 160f, 80f, 40f, 20f);

        [Test]
        public void MagicFindZero_ReturnsTheAuthoredVectorUnchanged()
        {
            var authored = Authored();

            var result = RarityCascade.Apply(authored, 0f);

            Assert.That(result, Is.SameAs(authored), "0 magic find must not even copy - it is the identity");
        }

        [Test]
        public void NegativeMagicFind_IsAlsoTheIdentity()
        {
            var authored = Authored();

            Assert.That(RarityCascade.Apply(authored, -50f), Is.SameAs(authored));
        }

        [Test]
        public void RegressionRows_MatchDiabloIIFactors_AtTheLiveWeights()
        {
            AssertRow(0f,   common: 0.533f, magic: 0.267f, rare: 0.133f, unique: 0.067f);
            AssertRow(100f, common: 0.217f, magic: 0.434f, rare: 0.235f, unique: 0.114f);
            AssertRow(200f, common: 0.000f, magic: 0.552f, rare: 0.307f, unique: 0.141f);
            AssertRow(800f, common: 0.000f, magic: 0.296f, rare: 0.510f, unique: 0.194f);

            void AssertRow(float magicFind, float common, float magic, float rare, float unique)
            {
                var r = RarityCascade.Apply(Authored(), magicFind);
                Assert.That(r[Common], Is.EqualTo(common).Within(Tol), $"Common @ {magicFind}");
                Assert.That(r[Magic],  Is.EqualTo(magic).Within(Tol),  $"Magic @ {magicFind}");
                Assert.That(r[Rare],   Is.EqualTo(rare).Within(Tol),   $"Rare @ {magicFind}");
                Assert.That(r[Unique], Is.EqualTo(unique).Within(Tol), $"Unique @ {magicFind}");
            }
        }

        [Test]
        public void PNoDrop_IsInvariantUnderMagicFind_EvenWithANonZeroFailWeight()
        {
            var withFail = NormalizeWithFail(pFail: 0.08f, 160f, 80f, 40f, 20f);

            foreach (var magicFind in new[] { 0f, 75f, 200f, 500f, 2000f })
                Assert.That(RarityCascade.Apply(withFail, magicFind)[NoDrop],
                    Is.EqualTo(0.08f).Within(1e-6f), $"P(NoDrop) @ {magicFind}");
        }

        [Test]
        public void RaisingMagicFind_NeverLowersTheUniqueShare_NorRaisesTheCommonShare()
        {
            var previousUnique = -1f;
            var previousCommon = 2f;

            for (var magicFind = 0f; magicFind <= 1500f; magicFind += 30f)
            {
                var r = RarityCascade.Apply(Authored(), magicFind);
                Assert.That(r[Unique], Is.GreaterThanOrEqualTo(previousUnique - 1e-5f), $"Unique dipped @ {magicFind}");
                Assert.That(r[Common], Is.LessThanOrEqualTo(previousCommon + 1e-5f), $"Common rose @ {magicFind}");
                previousUnique = r[Unique];
                previousCommon = r[Common];
            }
        }

        [Test]
        public void TheVectorAlwaysSumsToOne()
        {
            foreach (var magicFind in new[] { 0f, 50f, 250f, 1000f, 5000f })
            {
                var sum = 0f;
                foreach (var p in RarityCascade.Apply(Authored(), magicFind))
                    sum += p;
                Assert.That(sum, Is.EqualTo(1f).Within(1e-4f), $"sum @ {magicFind}");
            }
        }

        private static float[] Normalize(params float[] weights)
        {
            var sum = 0f;
            foreach (var w in weights) sum += w;
            var result = new float[weights.Length];
            for (var i = 0; i < weights.Length; i++) result[i] = weights[i] / sum;
            return result;
        }

        private static float[] NormalizeWithFail(float pFail, float white, float blue, float yellow, float orange)
        {
            var k = (1f - pFail) / (white + blue + yellow + orange);
            return new[] { pFail, white * k, blue * k, yellow * k, orange * k };
        }
    }
}
