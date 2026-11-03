using System.Linq;
using WoodFraming;
using NDS_WoodLibrary.NDS2018;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WoodFraming.Tests
{
    /// <summary>
    /// Sill anchor bolts against hand calculations. NDS-2018 Table 12E, 1-1/2" sill on concrete, Z∥: 1/2" 650 lb (DF-L),
    /// 5/8" 930 lb; Z' = 1.6 Z (wind). Spacing = min(72", Z'/v); end bolt min(12", s/2) from each end.
    /// </summary>
    [TestClass]
    public class SillAnchorageTests
    {
        [TestMethod]
        public void TenFeet_200plf_ShearGoverns()
        {
            // Z' = 650 × 1.6 = 1040 lb; 1040 / 200 × 12 = 62.4 → 62" < 72"; run 120 − 24 = 96 → ceil(96/62) + 1 = 3 bolts
            var a = SillAnchorage.Design(10, 200, Species.DouglasFirLarch, 500, 1.0, false);
            Assert.AreEqual(0.5, a.BoltDia);
            Assert.AreEqual(1040, a.ZPrime, 1e-9);
            Assert.AreEqual(62.4, a.SpacingByShearIn, 1e-9);
            Assert.AreEqual(62, a.SpacingIn);
            Assert.IsTrue(a.ShearGoverns);
            Assert.AreEqual(12, a.EndMaxIn);
            Assert.AreEqual(3, a.Count);
            CollectionAssert.AreEqual(new[] { 12.0, 60, 108 }, a.PositionsIn.ToArray());
            Assert.IsTrue(a.CapacityLb >= a.DemandLb); // 3120 ≥ 2000
        }

        [TestMethod]
        public void TwentyFeet_LightShear_SixFeetGoverns()
        {
            // 1040 / 50 × 12 = 249.6" → 72"; run 240 − 24 = 216 → 216/72 + 1 = 4 bolts at 12, 84, 156, 228
            var a = SillAnchorage.Design(20, 50, Species.DouglasFirLarch, 300, 0.5, false);
            Assert.AreEqual(72, a.SpacingIn);
            Assert.IsFalse(a.ShearGoverns);
            CollectionAssert.AreEqual(new[] { 12.0, 84, 156, 228 }, a.PositionsIn.ToArray());
            Assert.AreEqual(3.5, a.EndMinIn, 1e-9); // 7 × 1/2"
        }

        [TestMethod]
        public void HeavyShear_FiveEighthsBolts()
        {
            // 1/2": 1040 / 900 × 12 = 13.9" < 16" → 5/8": 930 × 1.6 = 1488; 1488 / 900 × 12 = 19.84 → 19";
            // end ≤ floor(19/2) = 9"; run 120 − 18 = 102 → ceil(5.37) + 1 = 7 bolts; 7 × 1488 = 10,416 ≥ 9,000
            var a = SillAnchorage.Design(10, 900, Species.DouglasFirLarch, 2000, 1.0, true);
            Assert.AreEqual(0.625, a.BoltDia);
            Assert.AreEqual(19, a.SpacingIn);
            Assert.AreEqual(9, a.EndMaxIn);
            Assert.AreEqual(7, a.Count);
            Assert.IsTrue(a.CapacityLb >= a.DemandLb);
        }

        [TestMethod]
        public void LaidOut_SpacingToHalfInch_EndsTakeTheRest()
        {
            // SW2-2 on lbltest: 45'-6" = 546", v = 247 plf → 1040 / 247 × 12 = 50.5 → 50"; run 546 − 24 = 522 → 12 bolts;
            // 522 / 11 = 47.45 → 47 1/2"; ends (546 − 11 × 47.5) / 2 = 11 3/4"
            var a = SillAnchorage.Design(45.5, 247, Species.DouglasFirLarch, 500, 0.33, false);
            Assert.AreEqual(12, a.Count);
            Assert.AreEqual(47.5, a.GapIn, 1e-9);
            Assert.AreEqual(11.75, a.EndIn, 1e-9);
            Assert.AreEqual(546 - 11.75, a.PositionsIn.Last(), 1e-9);
        }

        [TestMethod]
        public void Spf_LowerValue()
        {
            // 590 × 1.6 = 944; 944 / 200 × 12 = 56.6 → 56"
            var a = SillAnchorage.Design(10, 200, Species.SprucePineFir, 500, 1.0, false);
            Assert.AreEqual(944, a.ZPrime, 1e-9);
            Assert.AreEqual(56, a.SpacingIn);
        }

        [TestMethod]
        public void SouthernPine_HigherValue()
        {
            // 680 × 1.6 = 1088; 1088 / 200 × 12 = 65.3 → 65"; the group named with its G
            var a = SillAnchorage.Design(10, 200, Species.SouthernPine, 500, 1.0, false);
            Assert.AreEqual(1088, a.ZPrime, 1e-9);
            Assert.AreEqual(65, a.SpacingIn);
            Assert.AreEqual("SP (G = 0.55)", a.SpeciesGroup);
        }

        [TestMethod]
        public void Washers_Sdpws4_3_6_4_3()
        {
            // the exception: shear only, no dead load in the hold-down, h:b ≤ 2, nominal ≤ 1370 → cut washers
            Assert.IsFalse(SillAnchorage.Design(10, 200, Species.DouglasFirLarch, 670, 1.5, false).PlateWasher);
            // dead load counted → plate washer; nominal 670 > 400 → to within 1/2" of the sheathed edge
            var b = SillAnchorage.Design(10, 200, Species.DouglasFirLarch, 670, 1.5, true);
            Assert.IsTrue(b.PlateWasher);
            Assert.IsTrue(b.WasherToSheathedEdge);
            // plate washer for h:b over 2, but 300 plf nominal: no edge rule
            var c = SillAnchorage.Design(10, 100, Species.DouglasFirLarch, 300, 2.5, false);
            Assert.IsTrue(c.PlateWasher);
            Assert.IsFalse(c.WasherToSheathedEdge);
            Assert.IsTrue(SillAnchorage.Design(10, 200, Species.DouglasFirLarch, 1400, 1.0, false).PlateWasher);
        }

        [TestMethod]
        public void ShortPlateSections()
        {
            Assert.AreEqual(1, SillAnchorage.Design(1.5, 100, Species.DouglasFirLarch, 300, 1, false).Count);
            Assert.AreEqual(0, SillAnchorage.Design(1.0, 100, Species.DouglasFirLarch, 300, 1, false).Count);
        }
    }
}
