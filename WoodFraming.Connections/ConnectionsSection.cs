using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using BuildingModel.Model;

namespace WoodFraming.Connections
{
    /// <summary>
    /// The connections the lateral design gave the model's joints (owner 2026-11-07: details, not the building model): each
    /// shear wall's sill (anchor bolts into the foundation, or nails through the floor below) and each diaphragm's lines of
    /// shear walls (boundary nailing, collector). Its own section of the project file ("Connections"), written by the shear wall
    /// app on every save next to its forces ("Lateral"); keyed by the model's wall and level IDs. The forces stay in the model
    /// (<see cref="LateralSection"/>, <see cref="ShearPairs"/>); this says what carries them (<see cref="Describe"/>).
    /// </summary>
    [DataContract]
    public sealed class ConnectionsSection
    {
        public const string Key = "Connections";

        /// <summary>The app that wrote it, when (UTC, ISO 8601), and the model revision it was found on.</summary>
        [DataMember(Order = 1)] public string Source { get; set; }
        [DataMember(Order = 2)] public string Saved { get; set; }
        [DataMember(Order = 3)] public int ModelRevision { get; set; }
        /// <summary>Each shear wall story's sill (<see cref="SillConnection"/>).</summary>
        [DataMember(Order = 4, EmitDefaultValue = false)] public List<SillConnection> Sills { get; set; } = new List<SillConnection>();
        /// <summary>Each diaphragm's lines of shear walls (<see cref="DiaphragmLine"/>).</summary>
        [DataMember(Order = 5, EmitDefaultValue = false)] public List<DiaphragmLine> DiaphragmLines { get; set; } = new List<DiaphragmLine>();

        public bool IsEmpty => (Sills == null || Sills.Count == 0) && (DiaphragmLines == null || DiaphragmLines.Count == 0);

        /// <summary>The sill of <paramref name="wallId"/>'s story under <paramref name="storyLevelId"/> (the level on top of it).</summary>
        public SillConnection SillOf(string wallId, string storyLevelId) =>
            (Sills ?? new List<SillConnection>()).FirstOrDefault(s => s.WallId == wallId && (s.LevelId == null || storyLevelId == null || s.LevelId == storyLevelId));

        /// <summary>The line of <paramref name="levelId"/>'s diaphragm that <paramref name="wallId"/> is on (null: none).</summary>
        public DiaphragmLine LineOf(string levelId, string wallId) =>
            (DiaphragmLines ?? new List<DiaphragmLine>()).FirstOrDefault(l => l.LevelId == levelId && l.WallIds != null && l.WallIds.Contains(wallId));

        /// <summary>
        /// What carries a shear pair, in words ("10d at 4\" o.c. at the boundary ...", "(4) 5/8\" anchor bolts at 48\" o.c.",
        /// "16d common at 6\" o.c. through ..."); the pair's own generic words where this has no design for it.
        /// </summary>
        public string Describe(ShearPair p, Building b)
        {
            if (p == null) return null;
            string level = b?.Level(p.LevelId)?.Name ?? p.LevelId;
            if (p.AtTop)
            {
                var l = LineOf(p.LevelId, p.WallId);
                if (l == null || l.BoundaryIn <= 0) return p.Connector;
                string s = $"the {level} diaphragm's boundary nailing, {l.Nailing}, into the top plates (the collector {l.CollectorLb:0} lb ASD)";
                return l.Fails == null ? s : $"{s}: {l.Fails}";
            }
            var sill = SillOf(p.WallId, p.StoryLevelId);
            if (p.OnFoundation)
            {
                var a = sill?.Anchors;
                if (a == null || a.BoltDiaIn <= 0) return p.Connector;
                int n = a.PositionsIn?.Count ?? 0;
                return $"{(n > 0 ? $"({n}) " : "")}{a.BoltDiaIn.ToString("0.###", CultureInfo.InvariantCulture)}\" anchor bolts at {a.SpacingIn:0}\" o.c.";
            }
            var nl = sill?.Nails;
            if (nl == null || nl.SpacingIn <= 0) return p.Connector;
            string t = $"{nl.Nail} at {nl.SpacingIn.ToString("0.#", CultureInfo.InvariantCulture)}\" o.c. through the {level} floor's sheathing into the rim or blocking";
            return nl.Fails == null ? t : $"{t}: {nl.Fails}";
        }

        /// <summary>A describer for <see cref="ShearPairs.Of"/>: the project's connections, else the pairs' own words.</summary>
        public static Func<ShearPair, string> Describer(Building b, ConnectionsSection c = null)
        {
            var con = c ?? Read(b);
            return con == null ? (Func<ShearPair, string>)null : p => con.Describe(p, b);
        }

        private static DataContractJsonSerializer Serializer() =>
            new DataContractJsonSerializer(typeof(ConnectionsSection), new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });

        public static ConnectionsSection Read(Building b)
        {
            string json = b?.GetData(Key);
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                    return (ConnectionsSection)Serializer().ReadObject(ms);
            }
            catch (SerializationException) { return null; }
        }

        public string ToJson()
        {
            using (var ms = new MemoryStream())
            {
                Serializer().WriteObject(ms, this);
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        /// <summary>Writes it into the building's data (null or empty: removes it).</summary>
        public static void Write(Building b, ConnectionsSection s) => b?.SetData(Key, s == null || s.IsEmpty ? null : s.ToJson());
    }

    /// <summary>One shear wall story's sill as designed: its anchor bolts (on the foundation) or its nailing (on a floor).</summary>
    [DataContract]
    public sealed class SillConnection
    {
        [DataMember(Order = 1)] public string WallId { get; set; }
        /// <summary>The level on top of the wall's story (the key of its design).</summary>
        [DataMember(Order = 2)] public string LevelId { get; set; }
        [DataMember(Order = 3, EmitDefaultValue = false)] public SillAnchors Anchors { get; set; }
        [DataMember(Order = 4, EmitDefaultValue = false)] public SillNails Nails { get; set; }
    }

    /// <summary>A first-story shear wall's sill anchor bolts as designed.</summary>
    [DataContract]
    public sealed class SillAnchors
    {
        /// <summary>Each bolt (in) from the shear part's low end (west or south).</summary>
        [DataMember(Order = 1)] public List<double> PositionsIn { get; set; } = new List<double>();
        [DataMember(Order = 2)] public double BoltDiaIn { get; set; }
        [DataMember(Order = 3)] public double SpacingIn { get; set; }
        [DataMember(Order = 4, EmitDefaultValue = false)] public bool PlateWasher { get; set; }
    }

    /// <summary>
    /// An upper-story shear wall's sill nailed through the floor sheathing into the rim or blocking, as designed (the wall's
    /// shear out of its base, SDPWS 4.3.6.4.1): the nail, its spacing, and what one nail takes.
    /// </summary>
    [DataContract]
    public sealed class SillNails
    {
        /// <summary>"16d common".</summary>
        [DataMember(Order = 1)] public string Nail { get; set; }
        [DataMember(Order = 2)] public double SpacingIn { get; set; }
        /// <summary>One nail's adjusted lateral value Z' (lb, ASD, CD = 1.6) and the floor sheathing it crosses (in).</summary>
        [DataMember(Order = 3)] public double ZPrimeLb { get; set; }
        [DataMember(Order = 4)] public double SheathingIn { get; set; }
        /// <summary>No nail can take it at the closest spacing: what fails, in a sentence (null: it works).</summary>
        [DataMember(Order = 5, EmitDefaultValue = false)] public string Fails { get; set; }
    }

    /// <summary>
    /// One line of shear walls in one level's diaphragm, as the lateral design found it (owner 2026-11-07: the diaphragm's
    /// line shear and collectors): the walls on the line (under the diaphragm, their tops, and above it, their sills) take or
    /// give the line's force R per case; the diaphragm delivers it along its whole depth on the line, v = R / depth; the
    /// boundary nailing along the line is sized for it (SDPWS Table 4.2A or 4.2C), and the top plates carry the difference
    /// between the diaphragm's even delivery and the walls' shear parts (the collector, SDPWS 4.1.4).
    /// </summary>
    [DataContract]
    public sealed class DiaphragmLine
    {
        [DataMember(Order = 1)] public string LevelId { get; set; }
        /// <summary>The roof's diaphragm (else a floor's): which sheathing defaults it was designed with.</summary>
        [DataMember(Order = 2)] public bool Roof { get; set; }
        /// <summary>The line where the diaphragm reaches along it (ft): its two ends, west to east or south to north.</summary>
        [DataMember(Order = 3)] public double X0 { get; set; }
        [DataMember(Order = 4)] public double Y0 { get; set; }
        [DataMember(Order = 5)] public double X1 { get; set; }
        [DataMember(Order = 6)] public double Y1 { get; set; }
        /// <summary>The diaphragm's depth along the line (ft): its length on the line, any gap left out.</summary>
        [DataMember(Order = 7)] public double DepthFt { get; set; }
        /// <summary>The shear walls on the line (under it and standing on it).</summary>
        [DataMember(Order = 8)] public List<string> WallIds { get; set; } = new List<string>();
        /// <summary>Wind case → the line's unit shear (plf, W at strength level, R / depth, + east or north on the diaphragm).</summary>
        [DataMember(Order = 9)] public Dictionary<string, double> Shear { get; set; } = new Dictionary<string, double>();
        /// <summary>The worst unit shear, ASD (0.6 W, plf).</summary>
        [DataMember(Order = 10)] public double ShearAsdPlf { get; set; }
        /// <summary>The sheathing ("Sheathing and Single-Floor, 10d, 19/32\" min., 2\" framing").</summary>
        [DataMember(Order = 11)] public string Sheathing { get; set; }
        [DataMember(Order = 12)] public bool Blocked { get; set; }
        /// <summary>The nail spacing at the boundary (along the line) and at the other panel edges, or the supported edges (in).</summary>
        [DataMember(Order = 13)] public double BoundaryIn { get; set; }
        [DataMember(Order = 14)] public double EdgesIn { get; set; }
        [DataMember(Order = 15)] public string Nail { get; set; }
        /// <summary>The nailing's ASD allowable unit shear (plf).</summary>
        [DataMember(Order = 16)] public double AllowablePlf { get; set; }
        /// <summary>The worst collector force along the line (lb, ASD), and where (ft from the line's start).</summary>
        [DataMember(Order = 17)] public double CollectorLb { get; set; }
        [DataMember(Order = 18)] public double CollectorAtFt { get; set; }
        /// <summary>What carries it ("DF-L No. 2 2x4 double top plate: 5 16d common each side of a splice"), or why not designed.</summary>
        [DataMember(Order = 19, EmitDefaultValue = false)] public string Collector { get; set; }
        /// <summary>A construction requirement with the nailing (3" framing, staggered), else null.</summary>
        [DataMember(Order = 20, EmitDefaultValue = false)] public string Note { get; set; }
        /// <summary>What does not work (null: it works).</summary>
        [DataMember(Order = 21, EmitDefaultValue = false)] public string Fails { get; set; }

        /// <summary>"10d at 4\" o.c. at the boundary, 6\" at the other panel edges (blocked)".</summary>
        public string Nailing => BoundaryIn <= 0 ? "(not designed)" :
            $"{Nail} at {BoundaryIn.ToString("0.#", CultureInfo.InvariantCulture)}\" o.c. at the boundary, {EdgesIn.ToString("0.#", CultureInfo.InvariantCulture)}\" at the {(Blocked ? "other panel edges (blocked)" : "supported edges (unblocked)")}";
    }
}
