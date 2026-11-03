using WoodFraming;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WoodFraming.Tests
{
    /// <summary>
    /// The stud check against IRC Table R602.3(5) / IBC Table 2308.9.1, read by hand: bearing walls ≤ 10 ft, spacing for the
    /// roof only / one floor + roof / two floors + roof: 2x4 24 / 16 / —, 2x6 24 / 24 / 16.
    /// </summary>
    [TestClass]
    public class StudRulesTests
    {
        private static readonly string[] Sizes = WallFraming.StudSizes;

        [TestMethod]
        public void TwoByFour_OneFloorAndRoof_At16_Passes_At24_Fails()
        {
            var a = StudRules.Check("2x4", 16, 8.6, 1, 115, "C", Sizes);
            Assert.IsTrue(a.Passes);
            Assert.AreEqual(16, a.MaxSpacingIn);
            Assert.AreEqual("2x4", a.Minimum);
            var b = StudRules.Check("2x4", 24, 8.6, 1, 115, "C", Sizes);
            Assert.IsFalse(b.Passes);
            Assert.AreEqual("2x6", b.Minimum); // 2x6: 24" with one floor and the roof
        }

        [TestMethod]
        public void TwoFloorsAndRoof_NeedsTwoBySix()
        {
            var a = StudRules.Check("2x4", 16, 8.6, 2, null, null, Sizes);
            Assert.IsFalse(a.Passes);
            Assert.AreEqual(0, a.MaxSpacingIn); // "—": not permitted
            Assert.AreEqual("2x6", a.Minimum);
            Assert.IsTrue(StudRules.Check("2x6", 16, 8.6, 2, null, null, Sizes).Passes);
            Assert.IsFalse(StudRules.Check("2x6", 24, 8.6, 2, null, null, Sizes).Passes);
        }

        [TestMethod]
        public void RoofOnly_TwoByFourAt24()
        {
            Assert.IsTrue(StudRules.Check("2x4", 24, 9.6, 0, null, null, Sizes).Passes);
        }

        [TestMethod]
        public void OverTenFeet_OrThreeFloors_OrHighWind_DesignRequired()
        {
            // lbltest: 15 ft walls, studs 15 − 4.5/12 = 14.625 ft
            var a = StudRules.Check("2x6", 16, 14.625, 0, 115, "C", Sizes);
            Assert.IsTrue(a.DesignRequired);
            Assert.IsFalse(a.Passes);
            Assert.IsNull(a.Minimum);
            Assert.IsTrue(StudRules.Check("2x6", 16, 8.6, 3, null, null, Sizes).DesignRequired);
            // 140 mph Exposure C: IRC (< 140) and IBC (≤ 130, or ≤ 140 in B) both out
            Assert.IsTrue(StudRules.Check("2x6", 16, 8.6, 0, 140, "C", Sizes).DesignRequired);
            // 140 mph Exposure B: IBC conventional still allowed (2308.2.4 exception 1)
            Assert.IsFalse(StudRules.Check("2x6", 16, 8.6, 0, 140, "B", Sizes).DesignRequired);
            // 135 mph C: within the IRC, a note only
            Assert.IsFalse(StudRules.Check("2x6", 16, 8.6, 0, 135, "C", Sizes).DesignRequired);
        }

        [TestMethod]
        public void TwoByEight_TakenAsTwoBySix()
        {
            Assert.IsTrue(StudRules.Check("2x8", 16, 8.6, 2, null, null, Sizes).Passes);
        }
    }
}
