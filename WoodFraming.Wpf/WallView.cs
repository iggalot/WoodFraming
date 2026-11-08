using System;
using System.Collections.Generic;
using System.Linq;
using BuildingModel.Geometry;
using BuildingModel.Model;
using WoodFraming.Connections;

namespace WoodFraming.Wpf
{
    /// <summary>A hold-down at the wall's bottom, as the lateral design placed and sized it.</summary>
    public sealed class WallViewHoldown
    {
        /// <summary>in, along the wall from its low end (west or south): on the end post's inside face.</summary>
        public double AlongIn { get; set; }
        /// <summary>At the shear part's low (left) end.</summary>
        public bool Low { get; set; }
        /// <summary>"HDU5-SDS2.5"; null: none strong enough.</summary>
        public string Connector { get; set; }
        public string Fasteners { get; set; }
        public double TensionAsd { get; set; }
        /// <summary>"the foundation", "wall W3", "beam B2"; null: nothing under it.</summary>
        public string Onto { get; set; }
        /// <summary>A strap (to the wall below, or a strap tie into the concrete), else a hold-down bolted to the post.</summary>
        public bool Strap { get; set; }
        public bool Supported => Onto != null;
        public bool Bad => !Supported || Connector == null;
    }

    /// <summary>A hold-down (or a beam end) of the story above standing on this wall's top.</summary>
    public sealed class WallViewLanding
    {
        public double AlongIn { get; set; }
        /// <summary>What it comes from: "W7", "beam B2 end".</summary>
        public string From { get; set; }
        public double TensionAsd { get; set; }
        public string Connector { get; set; }
    }

    /// <summary>
    /// What a wall view draws, in plain terms (the same in every app): the wall on one story as built (its framing laid
    /// out by <see cref="WallFraming"/>), its hold-downs, what lands on its top from above, and, when it is a shear
    /// wall, the shear wall app's design (<see cref="ShearWallDesign"/>: the part, the sheathing and nailing, the sill bolts).
    /// Made from the model by <see cref="WallViews.FromModel"/>; a view draws it, it designs nothing.
    /// </summary>
    public sealed class WallView
    {
        public string Name { get; set; }
        public string LevelName { get; set; }
        /// <summary>"story 1 of 2".</summary>
        public string StoryText { get; set; }
        public bool OnFoundation { get; set; }
        public bool EastWest { get; set; }
        /// <summary>As built (from a wall it butts into to the face of the next), ft.</summary>
        public double LengthFt { get; set; }
        public double HeightFt { get; set; }
        public string StudSize { get; set; }
        public double StudSpacing { get; set; } = 16;
        /// <summary>The hold-down's post (in along the wall; 0: a double stud).</summary>
        public double PostAlongWall { get; set; }
        public List<WallViewHoldown> Holdowns { get; } = new List<WallViewHoldown>();
        public List<WallViewLanding> Above { get; } = new List<WallViewLanding>();
        /// <summary>The shear wall design (null: a plain wall, or not designed yet).</summary>
        public ShearWallDesign Shear { get; set; }
        /// <summary>A first-story shear wall's sill anchor bolts as designed (the "Connections" section; null: none).</summary>
        public SillAnchors Anchors { get; set; }
        /// <summary>Its sheathing nailing, face by face as <see cref="ShearWallDesign.Faces"/> (the "Connections" section; null: none).</summary>
        public WallNailing Nailing { get; set; }
        /// <summary>The shear part (in from the low end as built).</summary>
        public double ShearFromIn { get; set; }
        public double ShearToIn { get; set; }
        /// <summary>The wall as built on plan, its low end first (ft): for the 3D view's north arrow.</summary>
        public Point2 PlanLow { get; set; }
        public Point2 PlanHigh { get; set; }
        /// <summary>The model's wall and the level on top of the story.</summary>
        public string WallId { get; set; }
        public string TopLevelId { get; set; }
        /// <summary>Why the lateral design may not match the model any more (null: it does).</summary>
        public string Stale { get; set; }

        public bool IsShearWall => Shear != null;

        /// <summary>The wall's framing: the whole wall, the hold-down posts at the shear part's ends, the sheets over it.</summary>
        public WallFraming Framing() =>
            WallFraming.Build(LengthFt, HeightFt, StudSize, StudSpacing > 0 ? StudSpacing : 16, PostAlongWall, Above.Select(a => a.AlongIn),
                              Shear != null ? ShearFromIn : 0, Shear != null ? ShearToIn : -1);
    }

    /// <summary>The wall views made from the model and the lateral design saved in it: one routine for every app.</summary>
    public static class WallViews
    {
        /// <summary>
        /// The story of <paramref name="w"/> a view shows: the one under <paramref name="levelId"/> when the wall reaches it
        /// (the plan it was picked on), else its lowest story. The level on top of that story.
        /// </summary>
        public static Level StoryTop(Building b, Wall w, string levelId)
        {
            var bottom = b.Level(w.BaseLevelId);
            var top = b.Level(w.TopLevelId);
            if (bottom == null || top == null) return top;
            var here = levelId == null ? null : b.Level(levelId);
            if (here != null && here.Elevation > bottom.Elevation + 1e-9 && here.Elevation <= top.Elevation + 1e-9) return here;
            return b.Levels.Where(l => l.Elevation > bottom.Elevation + 1e-9 && l.Elevation <= top.Elevation + 1e-9).OrderBy(l => l.Elevation).FirstOrDefault() ?? top;
        }

        /// <summary>
        /// The view of <paramref name="w"/> on the story under <paramref name="levelId"/> (see <see cref="StoryTop"/>), from
        /// the model and its lateral design (<paramref name="lat"/>: the saved one when null; an app passes its live one), and
        /// the connections the design gave it (<paramref name="connections"/>, the same: its sill's anchor bolts, its nailing).
        /// </summary>
        public static WallView FromModel(Building b, Wall w, string levelId = null, LateralSection lat = null, ConnectionsSection connections = null)
        {
            if (b == null || w == null) return null;
            lat = lat ?? LateralSection.Read(b);
            connections = connections ?? ConnectionsSection.Read(b);
            var topLevel = StoryTop(b, w, levelId);
            var below = topLevel == null ? null : b.Levels.Where(l => l.Elevation < topLevel.Elevation - 1e-9).OrderByDescending(l => l.Elevation).FirstOrDefault();
            string top = topLevel?.Id;
            var ordered = b.Levels.OrderBy(l => l.Elevation).ToList();
            int story = topLevel == null ? 0 : ordered.IndexOf(topLevel);

            bool ew = Math.Abs(w.End.X - w.Start.X) >= Math.Abs(w.End.Y - w.Start.Y);
            var (s0, s1) = WallJoints.RealEnds(b, w);
            bool startLow = ew ? w.Start.X <= w.End.X : w.Start.Y <= w.End.Y;
            Point2 low = startLow ? s0 : s1, high = startLow ? s1 : s0;
            double Along(Point2 p) => ((ew ? p.X - low.X : p.Y - low.Y)) * 12; // in from the low end as built
            double centreLow = ew ? Math.Min(w.Start.X, w.End.X) : Math.Min(w.Start.Y, w.End.Y);
            double builtLow = ew ? low.X : low.Y;

            var v = new WallView
            {
                Name = w.Id, WallId = w.Id, TopLevelId = top, LevelName = topLevel?.Name,
                StoryText = $"story {story} of {Math.Max(1, ordered.Count - 1)}",
                OnFoundation = below != null && below.Elevation == 0 && below == b.Ground,
                EastWest = ew,
                LengthFt = new Segment2(low, high).Length,
                HeightFt = topLevel != null && below != null ? topLevel.Elevation - below.Elevation : 0,
                StudSize = b.StudSizeOf(w, top), StudSpacing = b.StudSpacingOf(w, top),
                PlanLow = low, PlanHigh = high,
                Stale = lat?.Stale(b),
            };
            if (lat == null) return v;

            var d = (lat.Walls ?? new List<ShearWallDesign>()).FirstOrDefault(x => x.WallId == w.Id && (x.LevelId == null || x.LevelId == top));
            if (d != null)
            {
                var (from, to) = d.Along(w); // ft from the centreline's low end
                v.Shear = d;
                v.ShearFromIn = Math.Max(0, (from - (builtLow - centreLow)) * 12);
                v.ShearToIn = Math.Min(v.LengthFt * 12, (to - (builtLow - centreLow)) * 12);
                v.Anchors = connections?.SillOf(w.Id, top)?.Anchors;
                v.Nailing = connections?.NailingOf(w.Id, top);
            }
            var own = lat.HoldownsOf(w.Id).Where(h => h.LevelId == null || h.LevelId == top).ToList();
            double partMid = d != null ? (v.ShearFromIn + v.ShearToIn) / 2 : v.LengthFt * 6;
            foreach (var h in own)
                v.Holdowns.Add(new WallViewHoldown
                {
                    AlongIn = Along(h.Where), Low = Along(h.Where) < partMid, Connector = h.Connector, TensionAsd = h.TensionAsd,
                    Onto = h.Onto, Strap = h.Family == "strap", Fasteners = d?.Fasteners,
                });
            v.PostAlongWall = own.Count == 0 ? 0 : own.Max(h => h.PostAlongWall);
            foreach (var h in lat.LandingOn($"wall {w.Id}").Where(h => h.OntoLevelId == null || h.OntoLevelId == top))
                v.Above.Add(new WallViewLanding { AlongIn = Math.Max(0, Math.Min(v.LengthFt * 12, Along(h.Where))), From = h.WallId ?? h.From, TensionAsd = h.TensionAsd, Connector = h.Connector });
            return v;
        }
    }
}
