using WoodFraming;
using NDS_WoodLibrary.NDS2018;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WoodFraming.Tests
{
    /// <summary>
    /// The rim or blocking toe-nailed into a wall's top plates, against a hand calculation (NDS-2018 12.1.6.3 and Figure 12A
    /// for where the nail is, the yield limit equations of 12.3.1, Ctn = 0.83 of 12.5.4.2).
    /// </summary>
    [TestClass]
    public class RimToPlateTests
    {
        /// <summary>
        /// 8d common (L4: 2-1/2 × 0.131), DF-L (G 0.50, Fe 4650 psi as Table 12.3.3 prints it), Fyb 100,000, Rd 2.2: in the rim
        /// ts = (2.5/3)/cos 30° = 0.96225", into the plate p = 1.53775" (≥ 6D = 0.786), ℓm = p − D = 1.40675". Re = 1, Rt = 1.46194.
        /// Im 389.5, Is 266.4, II 139.9 (k1 0.52514), IIIm 141.7 (k2 1.09117), IIIs 105.7 (k3 1.19024), IV 97.1: Z = 97.1 lb.
        /// Z' = 97.1 × 1.6 × 0.83 = 129.0 lb. At 200 plf ASD: 129.0 / 200 × 12 = 7.74", 7-1/2" o.c.
        /// </summary>
        [TestMethod]
        public void EightPenny_DFL_200plf_HandCalc()
        {
            var n = RimToPlate.Design(200, Species.DouglasFirLarch, "8d");
            Assert.IsNull(n.Fails, n.Fails);
            Assert.AreEqual("8d common", n.Nail);
            Assert.AreEqual(0.96225, n.SideIn, 1e-5);
            Assert.AreEqual(1.53775, n.PenetrationIn, 1e-5);
            Assert.AreEqual(97.1, n.Z, 0.1);
            Assert.AreEqual("IV", n.Mode);
            Assert.AreEqual(129.0, n.ZPrime, 0.1);
            Assert.AreEqual(7.74, n.SpacingByShearIn, 0.01);
            Assert.AreEqual(7.5, n.SpacingIn);
            Assert.AreEqual("rim", n.Member);
            Assert.AreEqual("blocking", RimToPlate.Design(200, Species.DouglasFirLarch, "8d", blocking: true).Member);
        }

        [TestMethod]
        public void TooMuchShear_Fails_LightShear_TheAppsMost_NotANail()
        {
            var heavy = RimToPlate.Design(600, Species.DouglasFirLarch, "8d"); // 129 / 600 × 12 = 2.6" < 3"
            StringAssert.Contains(heavy.Fails, "closer than 3\"");
            Assert.AreEqual(16, RimToPlate.Design(50, Species.DouglasFirLarch, "8d").SpacingIn, "the app's most");
            StringAssert.Contains(RimToPlate.Design(100, Species.DouglasFirLarch, "9d").Fails, "not a common nail");
        }
    }
}
