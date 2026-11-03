using System;
using System.Collections.Generic;
using System.Linq;

namespace WoodFraming
{
    /// <summary>
    /// The prescriptive stud size check for a bearing wall: IRC Table R602.3(5) (2024 IRC, the same cells as IBC Table
    /// 2308.9.1, 2308.5.1 before 2024). A stud size passes when the wall's laterally unsupported stud height is within the
    /// table's 10 ft and the spacing within the table's for what the wall carries (the roof only, one floor and the roof,
    /// two floors and the roof). Anything else, or a wind speed over the codes' prescriptive limits, is "design required"
    /// (NDS 3.9, combined bending and compression). A shear wall's own framing rules (SDPWS) are the shear wall program's.
    /// </summary>
    public sealed class StudCheck
    {
        public string Size { get; set; }
        public double SpacingIn { get; set; }
        public double StudHeightFt { get; set; }       // laterally unsupported: the stud's own length
        public int FloorsAbove { get; set; }           // floors the wall carries, besides the roof
        /// <summary>The table's spacing limit for this size and load (0: not permitted; null: outside the table).</summary>
        public double? MaxSpacingIn { get; set; }
        public bool Passes { get; set; }
        /// <summary>The smallest offered size the table allows here (null: none: design required).</summary>
        public string Minimum { get; set; }
        public bool DesignRequired { get; set; }
        public List<string> Reasons { get; } = new List<string>();
        public string Carries => FloorsAbove == 0 ? "the roof and ceiling only" : FloorsAbove == 1 ? "one floor and the roof" : $"{FloorsAbove} floors and the roof";
    }

    public static class StudRules
    {
        public const double TableHeightFt = 10;

        /// <summary>
        /// IRC Table R602.3(5) / IBC Table 2308.9.1, bearing walls: max spacing (in) supporting the roof-ceiling only, one
        /// floor plus the roof-ceiling, two floors plus the roof-ceiling (0: "—", not permitted). 2x8 is not in the table.
        /// </summary>
        private static readonly Dictionary<string, double[]> Bearing = new Dictionary<string, double[]>
        {
            { "2x4", new double[] { 24, 16, 0 } },
            { "3x4", new double[] { 24, 24, 16 } },
            { "2x5", new double[] { 24, 24, 0 } },
            { "2x6", new double[] { 24, 24, 16 } },
        };

        public static readonly string[] Citations =
        {
            "IRC 2024 R602.3.1 (2025 CRC): \"The size, height and spacing of studs shall be in accordance with Table R602.3(5).\" Exceptions: utility grade studs (16\" o.c., roof and ceiling only, 8 ft); 2x6 studs to 18 ft at 16\" o.c. (20 ft at 12\") carrying a roof with ≤ 6 ft tributary length, ground snow ≤ 25 psf, Vult ≤ 130 mph, No. 2 or better; exterior bearing studs to 12 ft per Table R602.3(6) (Exposure B, roof live load ≤ 20 psf, ground snow ≤ 30 psf).",
            "IRC Table R602.3(5) / IBC Table 2308.9.1 (2308.5.1 in the 2018 and 2021 IBC), bearing walls, laterally unsupported stud height 10 ft; max spacing roof-ceiling only / one floor + roof-ceiling / two floors + roof-ceiling: 2x4 24\" / 16\" / — ; 3x4 24 / 24 / 16; 2x5 24 / 24 / —; 2x6 24 / 24 / 16. Nonbearing: 2x4 14 ft at 24\", 2x6 20 ft at 24\"; 2x3 nonbearing only, never in exterior walls.",
            "Footnote a: \"Listed heights are distances between points of lateral support placed perpendicular to the plane of the wall. Bearing walls shall be sheathed on not less than one side or bridging shall be installed not greater than 4 feet apart … Increases in unsupported height are permitted where in compliance with Exception 2 of Section R602.3.1 or designed in accordance with accepted engineering practice.\" Footnote c (IRC): a habitable attic on 2x4 studs is limited to a 32 ft roof span, else 2x6 or design.",
            "IRC R301.2.1.1: the IRC's wind provisions do not apply where wind design is required (Figure R301.2.1.1) or where Vult ≥ 140 mph in a special wind region; then WFCM, ICC 600, ASCE 7 or the IBC.",
            "IBC 2024 2308.2.4: conventional framing only where V ≤ 130 mph (≤ 140 mph in Exposure B outside hurricane-prone regions; above 130 mph the AWC WFCM or ICC 600 may be used). 2308.2.2: stud height ≤ 10 ft, floor-to-floor ≤ 11'-7\". 2308.3: portions beyond the limits \"shall be designed in accordance with accepted engineering practice.\"",
        };

        /// <summary>
        /// The check for <paramref name="size"/> at <paramref name="spacingIn"/> in a wall whose studs are
        /// <paramref name="studHeightFt"/> long and carry <paramref name="floorsAbove"/> floors besides the roof.
        /// <paramref name="windMph"/> (ultimate, optional) and <paramref name="exposure"/> ("B", "C", "D") test the
        /// prescriptive codes' wind limits. <paramref name="offered"/>: the sizes the program can draw, smallest first.
        /// </summary>
        public static StudCheck Check(string size, double spacingIn, double studHeightFt, int floorsAbove, double? windMph, string exposure, IList<string> offered)
        {
            var c = new StudCheck { Size = size, SpacingIn = spacingIn, StudHeightFt = studHeightFt, FloorsAbove = Math.Max(0, floorsAbove) };
            if (studHeightFt > TableHeightFt + 1e-9)
            {
                c.DesignRequired = true;
                c.Reasons.Add($"Studs {FtIn.Of(studHeightFt * 12)} long: over the table's 10 ft laterally unsupported height (IRC Table R602.3(5) note a, IBC 2308.2.2). Design them (NDS 3.9), or use IRC R602.3.1 Exception 2 or 3 where it applies.");
            }
            if (c.FloorsAbove > 2)
            {
                c.DesignRequired = true;
                c.Reasons.Add($"It carries {c.FloorsAbove} floors and the roof: the table stops at two floors and the roof. Design required.");
            }
            if (windMph is double v)
            {
                bool irc = v < 140, ibc = v <= 130 || (v <= 140 && string.Equals(exposure, "B", StringComparison.OrdinalIgnoreCase));
                if (!irc && !ibc)
                {
                    c.DesignRequired = true;
                    c.Reasons.Add($"Wind {v:0} mph: at or over the IRC's 140 mph (R301.2.1.1) and over the IBC's conventional-framing limit (2308.2.4). Design required (or WFCM / ICC 600).");
                }
                else if (!ibc) c.Reasons.Add($"Wind {v:0} mph: within the IRC (under 140 mph, R301.2.1.1), but over IBC conventional framing's 130 mph (2308.2.4) unless Exposure B outside a hurricane-prone region.");
            }
            if (spacingIn > 24 + 1e-9)
            {
                c.DesignRequired = true;
                c.Reasons.Add($"Studs at {spacingIn:0}\" o.c.: over the table's 24\".");
            }

            double? Max(string s)
            {
                string key = s == "2x8" ? "2x6" : s; // not in the table; deeper than a 2x6, taken as at least as good
                return Bearing.TryGetValue(key, out var row) && c.FloorsAbove <= 2 ? row[c.FloorsAbove] : (double?)null;
            }
            c.MaxSpacingIn = Max(size);
            if (!c.DesignRequired)
            {
                c.Minimum = offered.FirstOrDefault(s => Max(s) is double m && m >= spacingIn - 1e-9);
                c.Passes = c.MaxSpacingIn is double ms && ms >= spacingIn - 1e-9;
                if (c.Passes)
                    c.Reasons.Add($"{size} at {spacingIn:0}\" o.c. carrying {c.Carries}: the table allows up to {c.MaxSpacingIn:0}\" o.c." + (size == "2x8" ? " (2x8 is not in the table: taken as at least a 2x6)." : "."));
                else
                    c.Reasons.Add($"{size} carrying {c.Carries}: " + (c.MaxSpacingIn == 0 ? "not permitted by the table" : $"the table allows {c.MaxSpacingIn:0}\" o.c., not {spacingIn:0}\"") + $". Use {c.Minimum ?? "a designed stud"}.");
            }
            return c;
        }

    }
}
