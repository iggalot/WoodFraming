using System.Linq;
using BuildingModel.Edits;
using BuildingModel.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WoodFraming.Wpf;

namespace WoodFraming.Tests
{
    /// <summary>
    /// The wall views every app shows (the Building Model's wall window, the shear wall app's construction window): the
    /// whole wall framed, the hold-down posts at the shear part's ends, the hold-downs always, the shear wall's design
    /// behind Shear wall. Views of the model's data: nothing in them is calculated.
    /// </summary>
    [TestClass]
    public class WallViewTests
    {
        [TestMethod]
        public void Framing_AShearPart_ItsPostsAtItsEnds_ItsSheetsOverIt_TheWallsEndsAStud()
        {
            // a 20 ft wall, the shear part from 4 ft to 16 ft, a 3 in post (2 plies)
            var f = WallFraming.Build(20, 9, "2x4", 16, 3, null, 48, 192);
            Assert.AreEqual(48, f.PartFrom);
            Assert.AreEqual(192, f.PartTo);
            var posts = f.Pieces.Where(p => p.Kind == FramingKind.EndPost).OrderBy(p => p.X0).ToList();
            Assert.AreEqual(4, posts.Count);
            Assert.AreEqual(48, posts[0].X0, 1e-9);
            Assert.AreEqual(51, posts[1].X1, 1e-9);
            Assert.AreEqual(189, posts[2].X0, 1e-9);
            Assert.AreEqual(192, posts[3].X1, 1e-9);
            var studs = f.Pieces.Where(p => p.Kind == FramingKind.Stud).ToList();
            Assert.IsTrue(studs.Any(p => p.X0 == 0) && studs.Any(p => p.X1 == 240), "a stud at each of the wall's own ends");
            Assert.IsTrue(f.Sheets.All(s => s.X0 >= 48 - 1e-9 && s.X1 <= 192 + 1e-9), "the sheets over the shear part only");
            Assert.AreEqual(3, f.Sheets.Count(s => s.Y0 == 0), "12 ft of it: three 4 ft sheets");
            Assert.IsFalse(studs.Any(s => posts.Any(p => s.X0 < p.X1 - 0.01 && s.X1 > p.X0 + 0.01)), "no stud in a post");
        }

        [TestMethod]
        public void Framing_TheWholeWall_AsBefore()
        {
            var a = WallFraming.Build(10, 9, "2x6", 16, 3.5);
            var b = WallFraming.Build(10, 9, "2x6", 16, 3.5, null, 0, 120);
            Assert.AreEqual(a.Pieces.Count, b.Pieces.Count);
            Assert.AreEqual(a.Sheets.Count, b.Sheets.Count);
            Assert.AreEqual(0, a.PartFrom);
            Assert.AreEqual(120, a.PartTo);
        }

        /// <summary>Two stories, W1 under W2; W2 a shear wall over its middle 12 ft, its hold-downs strapped to W1.</summary>
        private static (Building b, Wall w1, Wall w2, Level l2, Level l3) TwoStories()
        {
            var b = Building.New();
            var l2 = BuildingEdits.AddStory(b, 9);
            var l3 = BuildingEdits.AddStory(b, 9);
            var w1 = b.AddWall(b.Ground, l2, 0, 0, 20, 0);
            var w2 = b.AddWall(l2, l3, 0, 0, 20, 0);
            var lat = new LateralSection();
            lat.Walls.Add(new ShearWallDesign
            {
                WallId = w2.Id, LevelId = l3.Id, X0 = 4, Y0 = 0, X1 = 16, Y1 = 0, ArmIn = 139.5, Check = "OK: v = 210 plf of 340 plf",
                Faces = { new ShearFace { Sheathing = "Structural I 15/32\"", ThicknessIn = 0.469 } },
            });
            lat.Holdowns.Add(new StoryHoldown { From = $"{w2.Id} west end", WallId = w2.Id, X = 4.25, Y = 0, LevelId = l3.Id, OntoLevelId = l2.Id, Connector = "MSTC40", TensionAsd = 1800, Onto = $"wall {w1.Id}", PostAlongWall = 3, Family = "strap" });
            lat.Holdowns.Add(new StoryHoldown { From = $"{w2.Id} east end", WallId = w2.Id, X = 15.75, Y = 0, LevelId = l3.Id, OntoLevelId = l2.Id, Connector = "MSTC40", TensionAsd = 1800, Onto = $"wall {w1.Id}", PostAlongWall = 3, Family = "strap" });
            LateralSection.Write(b, lat);
            // its nailing: a connection detail, not the model's (WoodFraming.Connections)
            var con = new WoodFraming.Connections.ConnectionsSection();
            con.Walls.Add(new WoodFraming.Connections.WallNailing { WallId = w2.Id, LevelId = l3.Id, Faces = { new WoodFraming.Connections.FaceNailing { Nail = "8d common", EdgeIn = 6, FieldIn = 12 } } });
            WoodFraming.Connections.ConnectionsSection.Write(b, con);
            return (b, w1, w2, l2, l3);
        }

        [TestMethod]
        public void FromModel_AShearWall_TheWholeWall_ItsPart_ItsHoldowns()
        {
            var (b, w1, w2, l2, l3) = TwoStories();
            var v = WallViews.FromModel(b, w2, l3.Id);
            Assert.AreEqual(20, v.LengthFt, 1e-9, "the whole wall, not its shear part");
            Assert.AreEqual(9, v.HeightFt, 1e-9);
            Assert.IsFalse(v.OnFoundation);
            Assert.IsTrue(v.IsShearWall);
            Assert.AreEqual(48, v.ShearFromIn, 1e-9);
            Assert.AreEqual(192, v.ShearToIn, 1e-9);
            Assert.AreEqual(2, v.Holdowns.Count);
            Assert.AreEqual(51, v.Holdowns.Single(h => h.Low).AlongIn, 1e-9, "a post in from the part's end");
            Assert.AreEqual(189, v.Holdowns.Single(h => !h.Low).AlongIn, 1e-9);
            Assert.IsTrue(v.Holdowns.All(h => h.Strap && h.Onto == $"wall {w1.Id}"));
            Assert.AreEqual(3, v.PostAlongWall);
            Assert.AreEqual(6, v.Nailing.Face(0).EdgeIn, "its face nailing, from the Connections");
            Assert.IsNull(v.Nailing.Face(1), "one face");
            var f = v.Framing();
            Assert.AreEqual(48, f.PartFrom);
            Assert.AreEqual(192, f.PartTo);
        }

        [TestMethod]
        public void FromModel_APlainWall_ItsFraming_AndWhatComesDownOntoIt()
        {
            var (b, w1, w2, l2, l3) = TwoStories();
            var v = WallViews.FromModel(b, w1, l2.Id);
            Assert.IsFalse(v.IsShearWall, "W1 is not declared");
            Assert.IsTrue(v.OnFoundation);
            Assert.AreEqual(0, v.Holdowns.Count);
            Assert.AreEqual(2, v.Above.Count, "W2's two hold-downs come down onto it");
            CollectionAssert.AreEquivalent(new[] { 51.0, 189.0 }, v.Above.Select(a => a.AlongIn).ToArray());
            Assert.AreEqual(w2.Id, v.Above[0].From);
            var f = v.Framing();
            Assert.IsFalse(new WallElevation(v, f, true, 0, true).ShowShear, "not a shear wall: no sheathing drawn");
            Assert.AreEqual(4, f.Pieces.Count(p => p.Kind == FramingKind.PostUnder), "a post under each one from above");
        }

        [STATestMethod]
        public void Panel_ShearWall_OnByDefault_OffKeepsTheFramingAndTheHoldowns()
        {
            var (b, w1, w2, l2, l3) = TwoStories();
            var panel = new WallPanel();
            Assert.IsTrue(panel.ShowShear, "on by default");
            panel.Show(WallViews.FromModel(b, w2, l3.Id));
            Assert.IsTrue(panel.Elevation.ShowShear, "the shear wall's design drawn");
            var pieces = panel.Framing.Pieces.Count;
            panel.ShowShear = false;
            Assert.IsFalse(panel.Elevation.ShowShear, "off: not drawn");
            Assert.AreEqual(pieces, panel.Framing.Pieces.Count, "the same framing");
            Assert.AreEqual(2, panel.View.Holdowns.Count, "the hold-downs, drawn no matter what");
            var g = WallModel3D.Build(panel.View, panel.Framing, false, false, false, false);
            var withShear = WallModel3D.Build(panel.View, panel.Framing, true, true, false, false);
            Assert.IsTrue(withShear.Children.Count > g.Children.Count, "the sheathing in 3D only with the shear wall shown");
            Assert.IsTrue(g.Children.Count > panel.Framing.Pieces.Count, "the hold-downs in 3D always");

            var plain = new WallPanel();
            plain.Show(WallViews.FromModel(b, w1, l2.Id));
            Assert.IsFalse(plain.Elevation.ShowShear, "a plain wall: nothing to show");
        }

        /// <summary>
        /// A design stamped with what it was found from: the views say it is out of date once the building or the wind changes
        /// (LateralSection.OutOfDate, the one routine the plans and the Load Manager use too), not only when a wall is gone.
        /// </summary>
        [TestMethod]
        public void FromModel_TheWindChangedSinceTheDesign_TheViewSaysItIsOutOfDate()
        {
            var (b, w1, w2, l2, l3) = TwoStories();
            b.SetData("Wind", "{\"WindSpeed\":115}");
            var lat = LateralSection.Read(b);
            lat.BasedOn = new System.Collections.Generic.Dictionary<string, string>
            {
                [BuildingModel.Serialization.ModelFingerprint.GeometryPart] = BuildingModel.Serialization.ModelFingerprint.Geometry(b),
                ["data:Wind"] = BuildingModel.Serialization.ModelFingerprint.Data(b, "Wind"),
                ["shear walls"] = "abc", // the shear wall app's own: only it compares that
            };
            LateralSection.Write(b, lat);
            Assert.IsNull(WallViews.FromModel(b, w2, l3.Id).Stale, "as saved: up to date");
            b.SetData("Wind", "{\"WindSpeed\":130}");
            Assert.AreEqual("The wind inputs changed since the shear walls were saved.", WallViews.FromModel(b, w2, l3.Id).Stale);
            w2.End = new BuildingModel.Geometry.Point2(21, 0);
            Assert.AreEqual("The building and the wind inputs changed since the shear walls were saved.", WallViews.FromModel(b, w2, l3.Id).Stale);
        }

        [STATestMethod]
        public void Window_ShowsAWall_ItsHeading()
        {
            var (b, w1, w2, l2, l3) = TwoStories();
            var win = new WallWindow();
            var v = WallViews.FromModel(b, w2, l3.Id);
            win.Show(v);
            try
            {
                StringAssert.Contains(win.Title, w2.Id);
                var (t, s) = WallWindow.Heading(v, win.Panel.Framing);
                StringAssert.Contains(t, w2.Id);
                StringAssert.Contains(s, "Shear wall 12'-0\"");
            }
            finally { win.Close(); }
        }
    }
}
