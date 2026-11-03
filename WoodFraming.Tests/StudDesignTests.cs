using System.Linq;
using NDS_WoodLibrary.NDS2018;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WoodFraming.Tests
{
    /// <summary>
    /// NDS-2018 3.9.2 stud design against a hand calculation. DF-L No. 2 2x6 at 16" o.c., 175.5" between the plates (a 15 ft
    /// wall less three 1 1/2" plates), D 300 plf, L 400 plf, W 30 psf (C&amp;C, strength).
    /// Section: A 8.25 in², Sx 7.5625 in³, Ix 20.797 in⁴. le/d = 31.909, FcE = 0.822 × 580,000 / 31.909² = 468.24 psi.
    /// D + L + 0.6W (CD 1.6): P = 700 × 16/12 = 933.33 lb, fc = 113.13 psi; w = 0.6 × 30 × 16/12 = 24 plf = 2 lb/in,
    /// M = 2 × 175.5² / 8 = 7,700.06 lb-in, fb = 1,018.19 psi; Fc* = 1350 × 1.6 × 1.1 = 2376, CP = 0.18833, Fc' = 447.48;
    /// Fb' = 900 × 1.6 × 1.3 × 1.15 = 2,152.8; (113.13/447.48)² + 1018.19 / [2152.8 (1 − 113.13/468.24)] = 0.0639 + 0.6236 = 0.6876.
    /// </summary>
    [TestClass]
    public class StudDesignTests
    {
        private static StudDesignInput Example() => new StudDesignInput
        {
            Species = Species.DouglasFirLarch, Grade = Grade.No2, Size = "2x6", SpacingIn = 16, LengthIn = 175.5,
            Loads = new WallTopLoads { Dead = 300, Live = 400 }, WindPressurePsf = 30, DeflectionLimit = 240,
        };

        [TestMethod]
        public void Combined_HandCalc()
        {
            var d = StudDesign.Check(Example());
            var c = d.Combinations.Single(x => x.Name == "D + L + 0.6W");
            Assert.AreEqual(1.6, c.CD);
            Assert.AreEqual(933.333, c.P, 1e-3);
            Assert.AreEqual(113.1313, c.fc, 1e-4);
            Assert.AreEqual(7700.063, c.M, 1e-3);
            Assert.AreEqual(1018.190, c.fb, 1e-3);
            Assert.AreEqual(447.4769, c.FcPrime, 1e-3);
            Assert.AreEqual(2152.8, c.FbPrime, 1e-6);
            Assert.AreEqual(0.68755, c.Ratio, 1e-5);
            Assert.AreSame(c, d.Governing);
            Assert.IsTrue(d.Passes);
        }

        [TestMethod]
        public void Gravity_CombinationsUseTheirOwnCD()
        {
            var d = StudDesign.Check(Example());
            // no Lr or S: D, D + L, D + 0.6W, D + L + 0.6W
            CollectionAssert.AreEqual(new[] { "D", "D + L", "D + 0.6W", "D + L + 0.6W" }, d.Combinations.Select(x => x.Name).ToArray());
            var dl = d.Combinations.Single(x => x.Name == "D + L");    // CD 1.0: Fc* = 1485, CP = 0.29136, Fc' = 432.66
            Assert.AreEqual(1.0, dl.CD);
            Assert.AreEqual(432.6648, dl.FcPrime, 1e-3);
            Assert.AreEqual(0.06837, dl.Ratio, 1e-5);
            var dd = d.Combinations.Single(x => x.Name == "D");        // CD 0.9: fc = 48.48, Fc' = 427.93
            Assert.AreEqual(0.9, dd.CD);
            Assert.AreEqual(0.01284, dd.Ratio, 1e-5);
            Assert.AreEqual(0.53933, d.Combinations.Single(x => x.Name == "D + 0.6W").Ratio, 1e-5);
        }

        [TestMethod]
        public void ShearAndDeflection_HandCalc()
        {
            var d = StudDesign.Check(Example());
            var c = d.Combinations.Single(x => x.Name == "D + L + 0.6W");
            Assert.AreEqual(175.5, c.V, 1e-6);                 // 2 lb/in × 175.5 / 2
            Assert.AreEqual(31.9091, c.fv, 1e-4);              // 1.5 V / A
            Assert.AreEqual(288, c.FvPrime, 1e-9);             // 180 × 1.6
            // 0.42 × 30 = 12.6 psf × 16/12 = 1.4 lb/in; 5 × 1.4 × 175.5⁴ / (384 × 1.6e6 × 20.797) = 0.5197" ≤ 175.5/240 = 0.7313"
            Assert.AreEqual(0.5197, d.DeflectionIn, 1e-4);
            Assert.AreEqual(0.73125, d.DeflectionAllowedIn, 1e-9);
            Assert.AreEqual(933.333 / 8.25, d.BearingFc, 1e-3); // ≤ 625
        }

        [TestMethod]
        public void TwoByFour_TooSlender()
        {
            var i = Example();
            i.Size = "2x4";
            var d = StudDesign.Check(i);
            Assert.AreEqual(50.143, d.Slenderness, 1e-3);      // 175.5 / 3.5 > 50 (3.7.1.4)
            Assert.IsFalse(d.SlendernessOk);
            Assert.IsFalse(d.Passes);
        }

        [TestMethod]
        public void Interior_FivePsf_LiveDuration()
        {
            var i = Example();
            i.WindPressurePsf = null;
            i.Loads = new WallTopLoads { Dead = 300, Live = 400, Snow = 100 };
            var d = StudDesign.Check(i);
            Assert.IsFalse(d.Combinations.Any(x => x.Name.Contains("W")));
            var c = d.Combinations.Single(x => x.Name == "D + L + Li");
            Assert.AreEqual(1.0, c.CD);
            Assert.AreEqual(5 * 16 / 12.0, c.WLateral, 1e-9);   // 6.667 plf
            Assert.AreEqual(1.15, d.Combinations.Single(x => x.Name == "D + L + 0.7S + Li").CD);
            Assert.AreEqual(5, d.DeflectionPressurePsf);
        }

        [TestMethod]
        public void SnowAndRoofLive_Durations()
        {
            var i = Example();
            i.Loads = new WallTopLoads { Dead = 300, RoofLive = 200, Snow = 250 };
            var d = StudDesign.Check(i);
            Assert.AreEqual(1.25, d.Combinations.Single(x => x.Name == "D + Lr").CD);
            var s = d.Combinations.Single(x => x.Name == "D + 0.7S");
            Assert.AreEqual(1.15, s.CD);
            Assert.AreEqual((300 + 0.7 * 250) * 16 / 12.0, s.P, 1e-9);
            var w = d.Combinations.Single(x => x.Name == "D + 0.7S + 0.6W/2");
            Assert.AreEqual(1.6, w.CD);
            Assert.AreEqual(0.3 * 30 * 16 / 12.0, w.WLateral, 1e-9);
        }
    }
}
