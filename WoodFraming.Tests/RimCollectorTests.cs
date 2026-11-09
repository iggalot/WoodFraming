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
        public void OverTheRimsTension_Fails_SouthernPineWide_NotReadYet()
        {
            StringAssert.Contains(RimCollector.Design(15000, Species.DouglasFirLarch, Grade.No2, "2x10").Fails, "over the rim's tension 14042 lb");
            StringAssert.Contains(RimCollector.Design(1000, Species.SouthernPine, Grade.No2, "2x10").Fails, "not read into this library yet");
            StringAssert.Contains(RimCollector.Design(1000, Species.DouglasFirLarch, Grade.No2, "2x14").Fails, "Not a size here");
        }
    }
}
