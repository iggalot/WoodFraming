using WoodFraming;
using NDS_WoodLibrary.NDS2018;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WoodFraming.Tests
{
    /// <summary>
    /// A floor's rim as the collector where no wall is under the line, against a hand calculation: DF-L No. 2 2x10 (NDS
    /// Supplement Table 4A: Ft 575, Fc 1350 psi; CF 10" wide: Ft 1.1, Fc 1.0), A = 1.5 × 9.25 = 13.875 in², CD 1.6.
    /// Ft' = 575 × 1.6 × 1.1 = 1012 psi, T = 14,041.5 lb; Fc' = 1350 × 1.6 = 2160 psi, C = 29,970 lb.
    /// </summary>
    [TestClass]
    public class RimCollectorTests
    {
        [TestMethod]
        public void TwoByTen_DFL_HandCalc_TheStrapTakesTheWholeForce()
        {
            var c = RimCollector.Design(3000, Species.DouglasFirLarch, Grade.No2, "2x10");
            Assert.IsNull(c.Fails, c.Fails);
            Assert.AreEqual(13.875, c.AreaIn2, 1e-9);
            Assert.AreEqual(1012, c.FtPrimePsi, 1e-6);
            Assert.AreEqual(14041.5, c.TensionLb, 1e-6);
            Assert.AreEqual(2160, c.FcPrimePsi, 1e-6);
            Assert.AreEqual(29970, c.CompressionLb, 1e-6);
            Assert.AreEqual(3000, c.StrapLb, "a butt splice carries no tension: the strap takes it all");
        }

        [TestMethod]
        public void OverTheRimsTension_Fails_SouthernPineWide_ByHand()
        {
            StringAssert.Contains(RimCollector.Design(15000, Species.DouglasFirLarch, Grade.No2, "2x10").Fails, "over the rim's tension 14042 lb");
            // Southern Pine No. 2 2x10 (Table 4B, size-specific): A = 1.5 × 9.25 = 13.875; 475 × 1.6 × A = 10,545 lb, 1300 × 1.6 × A = 28,860 lb
            var sp = RimCollector.Design(1000, Species.SouthernPine, Grade.No2, "2x10");
            Assert.IsNull(sp.Fails, sp.Fails);
            Assert.AreEqual(10545, sp.TensionLb, 0.5);
            Assert.AreEqual(28860, sp.CompressionLb, 0.5);
            StringAssert.Contains(RimCollector.Design(1000, Species.DouglasFirLarch, Grade.No2, "2x14").Fails, "Not a size here");
        }

        /// <summary>
        /// Every joist depth the rim can be, DF-L No. 2 (Ft 575, Fc 1350 psi), CD 1.6, Table 4A's CF (Ft, Fc):
        /// 2x4 5.25 in² (1.5, 1.15): T = 575 × 1.6 × 1.5 × 5.25 = 7245, C = 1350 × 1.6 × 1.15 × 5.25 = 13,041;
        /// 2x6 8.25 in² (1.3, 1.1): 9867, 19,602; 2x8 10.875 in² (1.2, 1.05): 12,006, 24,664.5;
        /// 2x10 13.875 in² (1.1, 1.0): 14,041.5, 29,970; 2x12 16.875 in² (1.0, 1.0): 15,525, 36,450.
        /// </summary>
        [TestMethod]
        [DataRow("2x4", 7245.0, 13041.0)]
        [DataRow("2x6", 9867.0, 19602.0)]
        [DataRow("2x8", 12006.0, 24664.5)]
        [DataRow("2x10", 14041.5, 29970.0)]
        [DataRow("2x12", 15525.0, 36450.0)]
        public void EveryJoistSize_HandCalc(string nominal, double tension, double compression)
        {
            var c = RimCollector.Design(1000, Species.DouglasFirLarch, Grade.No2, nominal);
            Assert.IsNull(c.Fails, c.Fails);
            Assert.AreEqual(tension, c.TensionLb, 1e-6, nominal);
            Assert.AreEqual(compression, c.CompressionLb, 1e-6, nominal);
        }
    }
}
