using System.Linq;
using WoodFraming;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WoodFraming.Tests
{
    /// <summary>
    /// A wall's framing, sheets and nails, against hand counts. Studs at 16 in. on centre from the low end, a sill and a
    /// double top plate (1.5 in. each), the stud size and spacing given (the model's), 4 × 8 sheets from the low end.
    /// </summary>
    [TestClass]
    public class WallFramingTests
    {
        [TestMethod]
        public void TenByNine_FirstStory()
        {
            // L = 120 in, H = 108 in; a 3.5 in hold-down post → 3 plies (4.5 in) at each end
            var f = WallFraming.Build(10, 9, "2x6", 16, 3.5);
            Assert.AreEqual("2x6", f.StudSize);
            Assert.AreEqual(5.5, f.StudDepth);
            Assert.AreEqual(3, f.PostPlies);
            Assert.AreEqual(1, f.Pieces.Count(p => p.Kind == FramingKind.SillPlate));
            Assert.AreEqual(2, f.Pieces.Count(p => p.Kind == FramingKind.TopPlate));
            Assert.AreEqual(6, f.Pieces.Count(p => p.Kind == FramingKind.EndPost));
            // studs centred at 16, 32 … 112 (112 + 0.75 ≤ 120 − 4.5): 7, each 108 − 1.5 − 3 = 103.5 in
            var studs = f.Pieces.Where(p => p.Kind == FramingKind.Stud).ToList();
            Assert.AreEqual(7, studs.Count);
            Assert.AreEqual(16, studs[0].CenterX, 1e-9);
            Assert.AreEqual(112, studs[6].CenterX, 1e-9);
            Assert.AreEqual(103.5, studs[0].Length, 1e-9);
            // sheets: x 0–48, 48–96, 96–120 by y 0–96, 96–108: 6, of which 2 whole
            Assert.AreEqual(6, f.Sheets.Count);
            Assert.AreEqual(2, f.Sheets.Count(s => s.Full));
            // blocking along the joint at 96 in: post → stud 16 (10.75), six bays of 14.5, stud 112 → post (2.75)
            var blocks = f.Pieces.Where(p => p.Kind == FramingKind.Blocking).ToList();
            Assert.AreEqual(8, blocks.Count);
            Assert.AreEqual(10.75 + 6 * 14.5 + 2.75, blocks.Sum(b => b.Length), 1e-9);
            Assert.AreEqual(96, blocks[0].Y0 + 0.75, 1e-9);
        }

        [TestMethod]
        public void EightByEight_Upper_NoBlocking()
        {
            var f = WallFraming.Build(8, 8, "2x4", 16, 1.5); // a post never under two plies
            Assert.AreEqual("2x4", f.StudSize);
            Assert.AreEqual(3.5, f.StudDepth);
            Assert.AreEqual(2, f.PostPlies);
            Assert.AreEqual(5, f.Pieces.Count(p => p.Kind == FramingKind.Stud)); // 16 … 80
            Assert.AreEqual(2, f.Sheets.Count(s => s.Full));
            Assert.AreEqual(0, f.Pieces.Count(p => p.Kind == FramingKind.Blocking));
        }

        [TestMethod]
        public void PostUnderAHoldownFromAbove_ReplacesTheStudThere()
        {
            // a hold-down from above at 48 in: a two-ply post 46.5–49.5 in; the stud at 48 goes, 16, 32, 64, 80 stay
            var f = WallFraming.Build(8, 8, "2x4", 16, 1.5, new[] { 48.0 });
            var under = f.Pieces.Where(p => p.Kind == FramingKind.PostUnder).ToList();
            Assert.AreEqual(2, under.Count);
            Assert.AreEqual(46.5, under[0].X0, 1e-9);
            Assert.AreEqual(49.5, under[1].X1, 1e-9);
            CollectionAssert.AreEqual(new[] { 16.0, 32, 64, 80 }, f.Pieces.Where(p => p.Kind == FramingKind.Stud).Select(p => p.CenterX).ToArray());
            // one landing over an end post: that post carries it, no post added
            Assert.AreEqual(0, WallFraming.Build(8, 8, "2x4", 16, 1.5, new[] { 1.0 }).Pieces.Count(p => p.Kind == FramingKind.PostUnder));
        }

        [TestMethod]
        public void Nails_EdgeAndField()
        {
            var f = WallFraming.Build(8, 8, "2x4", 16, 1.5);
            var sheet = f.Sheets[0]; // 0–48 × 0–96
            var (edge, field) = f.Nails(sheet, 6);
            // a side 3/8 in in: 95.25 in → nails at 0, 6 … 90 and its end (17); top and bottom 47.25 in → 0 … 42 and end (9);
            // the four corners once: 2 × 17 + 2 × 9 − 4 = 48
            Assert.AreEqual(48, edge.Count);
            // field: the studs at 16 and 32, at 12.375, 24.375 … 84.375 (7 each); the end post is under the sheet's edge
            Assert.AreEqual(14, field.Count);
            // the wall: two sheets; the stud at 48 is under their joint (edge nails), not field
            Assert.AreEqual((96, 28), f.NailCount(6));
        }

        [TestMethod]
        public void StudSize_SetsStudsAndPlates()
        {
            // 2x6 on 8 ft: studs, posts and plates 2x6, 5 1/2" deep; the layout is the 2x4's (5 studs on 8 ft)
            var f = WallFraming.Build(8, 8, "2x6", 16, 1.5);
            Assert.AreEqual("2x6", f.StudSize);
            Assert.AreEqual(5.5, f.StudDepth);
            Assert.IsTrue(f.Pieces.All(p => p.Size == "2x6"));
            Assert.AreEqual(5, f.Pieces.Count(p => p.Kind == FramingKind.Stud));
            Assert.AreEqual(7.25, WallFraming.Build(8, 8, "2x8", 16, 1.5).StudDepth);
            // a size the program cannot draw, or no spacing: refused (the model supplies both)
            Assert.ThrowsExactly<System.ArgumentException>(() => WallFraming.Build(8, 8, "2x5", 16, 1.5));
            Assert.ThrowsExactly<System.ArgumentOutOfRangeException>(() => WallFraming.Build(8, 8, "2x4", 0, 1.5));
        }

        [TestMethod]
        public void TwentyFourInchSpacing_HandCount()
        {
            // 8 ft at 24" o.c., 2x4, two-ply end posts (0-3", 93-96"): marks at 24, 48, 72 (96 is the end post): 3 studs
            var f = WallFraming.Build(8, 8, "2x4", 24, 1.5);
            Assert.AreEqual(3, f.Pieces.Count(p => p.Kind == FramingKind.Stud));
            Assert.AreEqual(24, f.Spacing);
        }

        [TestMethod]
        public void PliesFor_FillTheMinimumPost()
        {
            Assert.AreEqual(2, WallFraming.PliesFor(1.5));
            Assert.AreEqual(2, WallFraming.PliesFor(3));
            Assert.AreEqual(3, WallFraming.PliesFor(3.5));
            Assert.AreEqual(4, WallFraming.PliesFor(5.5));
        }

        [TestMethod]
        public void SheetsToBuy_CutPiecesShareOnlyWhenTheyStack()
        {
            // 10 × 9: 2 whole, a 2 ft × 8 ft piece (its own sheet), the strips 4 × 1, 4 × 1, 2 × 1 (one sheet): 4
            Assert.AreEqual(4, WallFraming.Build(10, 9, "2x6", 16, 3.5).SheetsToBuy());
            // 39 × 15 (W11 on lbltest): 9 whole, nine 4 × 7, a 3 × 8 and a 3 × 7 (none stack): 20
            Assert.AreEqual(20, WallFraming.Build(39, 15, "2x6", 16, 3.5).SheetsToBuy());
            Assert.AreEqual(2, WallFraming.Build(8, 8, "2x4", 16, 1.5).SheetsToBuy());
        }

        [TestMethod]
        public void FtIn_FeetInchesEighths()
        {
            Assert.AreEqual("39'-0\"", FtIn.Of(468));
            Assert.AreEqual("4'-6 1/2\"", FtIn.Of(54.5));
            Assert.AreEqual("8'-7 1/2\"", FtIn.Of(103.5));
            Assert.AreEqual("7 1/4\"", FtIn.Of(7.25));
            Assert.AreEqual("10 3/4\"", FtIn.Of(10.75));
            Assert.AreEqual("2'-0\"", FtIn.Of(23.97)); // to the nearest 1/8: 24
            Assert.AreEqual("3 1/2", FtIn.Inches(3.5));
        }
    }
}
