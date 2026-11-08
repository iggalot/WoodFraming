using System;
using WoodFraming;
using NDS_WoodLibrary.NDS2018;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WoodFraming.Tests
{
    /// <summary>
    /// An upper-story sill nailed through the floor sheathing into the rim, against a hand calculation (AWC TR12 Table 1-1,
    /// the sheathing as the gap; NDS-2018 for the rest).
    /// </summary>
    [TestClass]
    public class SillNailingTests
    {
        /// <summary>
        /// 16d common (L4: 3-1/2 × 0.162), 1-1/2" DF-L sill (G 0.50, Fe 4650 psi as Table 12.3.3 prints it), 23/32" sheathing:
        /// p = 3.5 − 1.5 − 0.71875 = 1.28125" (≥ 6D = 0.972), ℓm = p − D = 1.11925". q = 4650 × 0.162 = 753.3 lb/in; M = 90,000 ×
        /// 0.162³ / 6 = 63.77 in-lb; Rd 2.2. Mode IV: A = 1/2q + 1/2q, B = g = 0.71875, C = −2M: P = 140.8, Z = 64.0 lb (the other
        /// modes: Im 383, Is 514, II 135, IIIm 92, IIIs 127). Z' = 1.6 × 64.0 = 102.4 lb. At 200 plf ASD: 102.4 / 200 × 12 = 6.1",
        /// 6" o.c. With no sheathing Table 12N's 141 lb.
        /// </summary>
        [TestMethod]
        public void SixteenPenny_ThroughTwentyThreeThirtySeconds_HandCalc()
        {
            var n = SillNailing.Design(10, 200, Species.DouglasFirLarch, 23.0 / 32.0, "16d");
            Assert.IsNull(n.Fails, n.Fails);
            Assert.AreEqual("16d common", n.Nail);
            Assert.AreEqual(1.28125, n.PenetrationIn, 1e-9);
            Assert.AreEqual(64.0, n.Z, 0.1);
            Assert.AreEqual("IV", n.Mode);
            Assert.AreEqual(102.4, n.ZPrime, 0.2);
            Assert.AreEqual(6.1, n.SpacingByShearIn, 0.05);
            Assert.AreEqual(6.0, n.SpacingIn);
            Assert.IsTrue(n.CapacityPlf >= 200, "the spacing takes the shear");

            var bare = SillNailing.Design(10, 200, Species.DouglasFirLarch, 0, "16d");
            Assert.AreEqual(141, Math.Round(bare.Z), "no sheathing: Table 12N");
        }

        [TestMethod]
        public void LowShear_TheAppsMost_HighShear_Fails_ShortNail_Fails()
        {
            Assert.AreEqual(16, SillNailing.Design(10, 50, Species.DouglasFirLarch, 23.0 / 32.0, "16d").SpacingIn, "the app's most, 16\"");
            StringAssert.Contains(SillNailing.Design(10, 500, Species.DouglasFirLarch, 23.0 / 32.0, "16d").Fails, "closer than 3\"");
            StringAssert.Contains(SillNailing.Design(10, 200, Species.DouglasFirLarch, 23.0 / 32.0, "8d").Fails, "6D");
            StringAssert.Contains(SillNailing.Design(10, 200, Species.DouglasFirLarch, 23.0 / 32.0, "9d").Fails, "Table L4");
        }
    }
}
