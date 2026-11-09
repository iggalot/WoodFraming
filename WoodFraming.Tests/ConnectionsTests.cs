using System.Collections.Generic;
using System.Linq;
using BuildingModel.Model;
using WoodFraming.Connections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WoodFraming.Tests
{
    /// <summary>
    /// The connections (details, not the building model's): their own sections of the project file, "Connections" (what the
    /// shear wall app designs) and "ConnectionDefaults" (the starting choices), and the words each shear pair is given.
    /// </summary>
    [TestClass]
    public class ConnectionsTests
    {
        private static ConnectionsSection Sample() => new ConnectionsSection
        {
            Source = "test", ModelRevision = 3,
            Sills = new List<SillConnection>
            {
                new SillConnection { WallId = "w1", LevelId = "L2", Anchors = new SillAnchors { PositionsIn = new List<double> { 12, 60, 108 }, BoltDiaIn = 0.625, SpacingIn = 48 } },
                new SillConnection { WallId = "w2", LevelId = "R", Nails = new SillNails { Nail = "16d common", SpacingIn = 6, ZPrimeLb = 150.2, SheathingIn = 0.71875 } },
            },
            DiaphragmLines = new List<DiaphragmLine>
            {
                new DiaphragmLine { LevelId = "R", Roof = true, X1 = 40, DepthFt = 40, WallIds = new List<string> { "w2" },
                    Shear = new Dictionary<string, double> { ["A (rigid)"] = 210.5 }, ShearAsdPlf = 126.3, Sheathing = "Sheathing", Nail = "8d",
                    BoundaryIn = 6, EdgesIn = 6, AllowablePlf = 215, CollectorLb = 1263 },
            },
            Walls = new List<WallNailing>
            {
                new WallNailing { WallId = "w1", LevelId = "L2", Faces = { new FaceNailing { Nail = "8d common", EdgeIn = 4, FieldIn = 12 }, new FaceNailing() } },
            },
            Tops = new List<TopConnection>
            {
                new TopConnection { WallId = "w2", LevelId = "R", Blocking = true, Nail = "8d common", SpacingIn = 7.5, ZPrimeLb = 129.0 },
                new TopConnection { WallId = "w1", LevelId = "L2", Nail = "8d common", SpacingIn = 6, ZPrimeLb = 129.0 },
            },
            Chords = new List<DiaphragmChord>
            {
                new DiaphragmChord { LevelId = "L2", WindAlongX = true, FromFt = 0, ToFt = 30, WPlf = 393.4, MomentLbFt = 26551.4, AtFt = 15, DepthFt = 40, ForceLb = 663.8,
                    EdgeLoFt = 0, EdgeHiFt = 40, WallIds = new List<string> { "w1", "w3" }, Chord = "plates" },
            },
        };

        [TestMethod]
        public void TheSection_RoundTrips_ThroughTheProjectFile()
        {
            var b = new Building();
            ConnectionsSection.Write(b, Sample());
            var c = ConnectionsSection.Read(b);
            Assert.AreEqual(3, c.ModelRevision);
            Assert.AreEqual(2, c.Sills.Count);
            CollectionAssert.AreEqual(new[] { 12.0, 60, 108 }, c.SillOf("w1", "L2").Anchors.PositionsIn.ToArray());
            Assert.IsNull(c.SillOf("w1", "L2").Nails, "bolts only");
            Assert.AreEqual(6, c.SillOf("w2", "R").Nails.SpacingIn);
            Assert.IsNull(c.SillOf("w2", "L2"), "another story's");
            var l = c.LineOf("R", "w2");
            Assert.AreEqual(210.5, l.Shear["A (rigid)"], "the cases by name");
            Assert.AreEqual(1263, l.CollectorLb);
            Assert.IsNull(c.LineOf("L2", "w2"));
            var n = c.NailingOf("w1", "L2");
            Assert.AreEqual(2, n.Faces.Count, "both faces, in order");
            Assert.AreEqual(4, n.Face(0).EdgeIn);
            Assert.IsNull(n.Face(1).Nail, "the second face not nailed at a spacing (gypsum)");
            Assert.IsNull(n.Face(2));
            Assert.IsNull(c.NailingOf("w2", "R"));
            Assert.IsTrue(c.TopOf("w2", "R").Blocking, "the roof's blocking");
            Assert.AreEqual(7.5, c.TopOf("w2", "R").SpacingIn);
            Assert.AreEqual("rim", c.TopOf("w1", "L2").Member);
            Assert.IsNull(c.TopOf("w1", "R"));
            var ch = c.ChordsOn("L2", "w3").Single();
            Assert.IsTrue(ch.WindAlongX);
            Assert.AreEqual(663.8, ch.ForceLb);
            Assert.IsFalse(ch.Cantilever);
            Assert.AreEqual(0, c.ChordsOn("R", "w3").Count);
            ConnectionsSection.Write(b, new ConnectionsSection());
            Assert.IsNull(b.GetData(ConnectionsSection.Key), "an empty one removes the section");
        }

        [TestMethod]
        public void EachPair_IsGivenItsConnection_InWords()
        {
            var c = Sample();
            string Words(ShearPair p) => c.Describe(p, null);
            var bolts = new ShearPair { WallId = "w1", LevelId = "G", StoryLevelId = "L2", OnFoundation = true, Connector = "the sill's anchor bolts" };
            Assert.AreEqual("(3) 0.625\" anchor bolts at 48\" o.c.", Words(bolts));
            var nails = new ShearPair { WallId = "w2", LevelId = "L2", StoryLevelId = "R", Connector = "generic" };
            Assert.AreEqual("16d common at 6\" o.c. through the L2 floor's sheathing into the rim or blocking", Words(nails));
            var top = new ShearPair { WallId = "w2", LevelId = "R", StoryLevelId = "R", AtTop = true, Connector = "generic" };
            StringAssert.StartsWith(Words(top), "the R diaphragm's boundary nailing, ");
            StringAssert.Contains(Words(top), "into the blocking over the top plates (the collector 1263 lb ASD), toe-nailed to them with 8d common at 7.5\" o.c.");
            // a top with no diaphragm line designed: the toe-nailing alone
            var lower = new ShearPair { WallId = "w1", LevelId = "L2", StoryLevelId = "L2", AtTop = true, Connector = "generic" };
            Assert.AreEqual("the L2 rim on the top plates, toe-nailed to them with 8d common at 6\" o.c.", Words(lower));
            // nothing designed for it: the pair's own words
            var none = new ShearPair { WallId = "w9", LevelId = "R", StoryLevelId = "R", AtTop = true, Connector = "generic" };
            Assert.AreEqual("generic", Words(none));
            // failing: said
            c.SillOf("w2", "R").Nails.Fails = "Fails: 16d at 3\" is the closest";
            StringAssert.EndsWith(Words(nails), ": Fails: 16d at 3\" is the closest");
            Assert.IsNull(ConnectionsSection.Describer(new Building()), "no section, no describer: the pairs keep their words");
        }

        [TestMethod]
        public void TheDefaults_StartingValues_AndAMissingValueKeepsIts()
        {
            var b = new Building();
            var d = ConnectionDefaults.Read(b);
            Assert.AreEqual(23.0 / 32, d.FloorSheathing, 1e-9);
            Assert.AreEqual("16d", d.SillNail);
            Assert.AreEqual("10d", d.FloorNail);
            Assert.IsTrue(d.FloorBlocked);
            Assert.AreEqual(7.0 / 16, d.RoofSheathing, 1e-9);
            Assert.AreEqual("8d", d.RoofNail);
            Assert.IsFalse(d.RoofBlocked);
            Assert.AreEqual(2, d.DiaphragmFramingWidth);
            Assert.AreEqual("16d", d.SpliceNail);
            Assert.AreEqual("8d", d.ToeNail);
            Assert.IsFalse(d.StructuralI);
            b.SetData(ConnectionDefaults.Key, "{\"RoofNail\":\"10d\",\"DiaphragmGrade\":\"StructuralI\"}");
            d = ConnectionDefaults.Read(b);
            Assert.AreEqual("10d", d.RoofNail, "the file's");
            Assert.IsTrue(d.StructuralI, "the file's");
            Assert.AreEqual("10d", d.FloorNail, "missing: its starting value");
            Assert.IsTrue(d.FloorBlocked, "missing: its starting value");
            Assert.AreEqual(23.0 / 32, d.FloorSheathing, 1e-9, "missing: its starting value");
            ConnectionDefaults.Write(b, d);
            Assert.AreEqual("10d", ConnectionDefaults.Read(b).RoofNail, "written and read back");
        }

        /// <summary>The defaults window: it shows the project's values (one not offered kept as a choice), and its starting values are the defaults' own.</summary>
        [STATestMethod]
        public void TheDefaultsWindow_ShowsTheValues_KeepsOneNotOffered_StartsFromTheDefaults()
        {
            var d = new ConnectionDefaults { RoofNail = "10d", RoofBlocked = true, DiaphragmGrade = "StructuralI", FloorSheathing = 7.0 / 8, SillNail = "16d", DiaphragmFramingWidth = 3 };
            var w = new WoodFraming.Wpf.ConnectionDefaultsWindow(d);
            var v = w.Values();
            Assert.AreEqual("10d", v.RoofNail);
            Assert.IsTrue(v.RoofBlocked);
            Assert.IsTrue(v.StructuralI);
            Assert.AreEqual(7.0 / 8, v.FloorSheathing, 1e-9, "7/8\" is not offered: kept as it is");
            Assert.AreEqual(3, v.DiaphragmFramingWidth);
            Assert.AreNotSame(d, v, "a copy");
            w.StartingValues();
            var s = w.Values();
            var start = new ConnectionDefaults();
            Assert.AreEqual(start.RoofNail, s.RoofNail);
            Assert.AreEqual(start.RoofBlocked, s.RoofBlocked);
            Assert.AreEqual(start.FloorSheathing, s.FloorSheathing, 1e-9);
            Assert.AreEqual(start.DiaphragmGrade, s.DiaphragmGrade);
            Assert.AreEqual(start.DiaphragmFramingWidth, s.DiaphragmFramingWidth);
            w.Close();
        }
    }
}
