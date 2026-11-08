using System;
using WoodFraming;
using NDS_WoodLibrary.NDS2018;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WoodFraming.Tests
{
    /// <summary>A double top plate as a collector, against a hand calculation (NDS-2018).</summary>
    [TestClass]
    public class TopPlateCollectorTests
    {
        /// <summary>
        /// DF-L No. 2 2x4 (Table 4A: Ft 575, Fc 1350 psi; CF 1.5 and 1.15), one ply 1.5 × 3.5 = 5.25 in²: Ft' = 575 × 1.6 × 1.5 =
        /// 1380 psi, T' = 7245 lb; Fc' = 1350 × 1.6 × 1.15 = 2484 psi, C' = 13,041 lb. 16d (3-1/2 × 0.162) through both plies:
        /// ℓs = ℓm = 1.5, Fe 4650, Fyb 90,000, Rd 2.2: Im = Is = 513.6, II = 212.7, IIIm = IIIs = 190.0, IV = 140.9 lb: Z = 140.9,
        /// Z' = 225.4 lb; 1000 lb: 4.4, 5 nails each side.
        /// </summary>
        [TestMethod]
        public void DoubleTwoByFour_DFL_HandCalc()
        {
            var c = TopPlateCollector.Design(1000, Species.DouglasFirLarch, Grade.No2, "2x4", "16d");
            Assert.IsNull(c.Fails, c.Fails);
            Assert.AreEqual(7245, c.TensionLb, 0.5);
            Assert.AreEqual(13041, c.CompressionLb, 0.5);
            Assert.AreEqual(140.9, c.Z, 0.1);
            Assert.AreEqual(225.4, c.ZPrime, 0.2);
            Assert.AreEqual(5, c.NailsEachSide);
            Assert.AreEqual(0, TopPlateCollector.Design(0, Species.DouglasFirLarch, Grade.No2, "2x4", "16d").NailsEachSide, "no force, no splice nails");
        }

        [TestMethod]
        public void OverOnePly_Fails()
        {
            StringAssert.Contains(TopPlateCollector.Design(8000, Species.DouglasFirLarch, Grade.No2, "2x4", "16d").Fails, "tension");
            StringAssert.Contains(TopPlateCollector.Design(100, Species.DouglasFirLarch, Grade.No2, "2x4", "9d").Fails, "Table L4");
        }
    }
}
