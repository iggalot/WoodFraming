using System;
using System.Collections.Generic;
using NDS_WoodLibrary.NDS2018;

namespace WoodFraming
{
    /// <summary>
    /// The anchor bolts of a first-story wall's sill plate (a 2x sill on concrete): diameter, spacing, end distances,
    /// count, embedment and washers. The spacing is the smaller of the prescriptive 6 ft and the bolt's ASD lateral value
    /// over the wall's ASD unit shear (each bolt takes the shear of its own spacing).
    ///
    /// Sources (quoted in <see cref="Citations"/>):
    ///  IRC 2018/2021 R403.1.6 — 1/2" bolts, ≤ 6 ft o.c., ≥ 7" into concrete, middle third of the plate, two per plate
    ///    section, one ≤ 12" and ≥ 7 bolt diameters from each end, a nut and washer on each.
    ///  IBC 2021 2308.3.1 — the same, the end bolt ≤ 12" and ≥ 4" from the end (conventional framing).
    ///  AWC SDPWS-2021 4.3.6.4.1 — connections transfer the unit shear v into and out of each shear wall;
    ///    4.3.6.4.3 — a 0.229" × 3" × 3" steel plate washer under each nut, to within 1/2" of the sheathed edge of the plate
    ///    where the nominal unit shear capacity exceeds 400 plf; exception: standard cut washers where the bolts resist
    ///    shear only and (a) the hold-downs are sized neglecting dead load, (b) h:b ≤ 2:1, (c) nominal capacity ≤ 1370 plf.
    ///  AWC NDS-2018 Table 12E — Z (single shear, sawn lumber to concrete, 6" embedment assumed); Z' = Z·CD (Table 11.3.1),
    ///    CD = 1.6 for wind (Table 2.3.2); CM, Ct, Cg, CΔ taken as 1.0 (dry, normal temperature, one bolt per row, full
    ///    end and edge distances).
    /// </summary>
    public class SillAnchorage
    {
        public double LengthFt { get; set; }
        public double ShearAsdPlf { get; set; }      // v (ASD) the bolts take, plf
        public double BoltDia { get; set; }          // in
        public double SillThickness { get; set; } = 1.5;
        public string SpeciesGroup { get; set; }     // "DF-L (G = 0.50)" …
        public double Z { get; set; }                // NDS Table 12E, Z∥, lb
        public double CD { get; set; } = 1.6;
        public double ZPrime => Z * CD;              // lb, ASD
        public double SpacingByShearIn { get; set; } // Z' / v, in (∞ when v = 0)
        public double MaxSpacingIn { get; set; } = 72;
        public double SpacingIn { get; set; }        // used, whole inches
        public bool ShearGoverns { get; set; }
        public double EndMinIn => 7 * BoltDia;       // IRC: 7 bolt diameters
        public double EndMaxIn { get; set; }         // 12", or half the spacing when that is less (the end bolt's share)
        public int Count { get; set; }
        public double GapIn { get; set; }            // as laid out, between bolts (to 1/2")
        public double EndIn { get; set; }            // as laid out, each end to its bolt
        public double EmbedmentIn { get; set; } = 7;
        public bool PlateWasher { get; set; } = true;   // 0.229 × 3 × 3 (else standard cut washers allowed)
        public bool WasherToSheathedEdge { get; set; }   // within 1/2" of the edge with > 400 plf nominal
        public string WasherReason { get; set; }
        public string Note { get; set; }
        public List<double> PositionsIn { get; set; } = new List<double>(); // from the low end, in

        /// <summary>The bolt group's ASD capacity over the wall, lb, against v L.</summary>
        public double CapacityLb => Count * ZPrime;
        public double DemandLb => ShearAsdPlf * LengthFt;

        public static readonly string[] Citations =
        {
            "IRC 2018/2021 R403.1.6: \"minimum 1/2-inch-diameter anchor bolts spaced not greater than 6 feet on center … Bolts shall extend not less than 7 inches into concrete … located in the middle third of the width of the plate … A nut and washer shall be tightened on each anchor bolt. There shall be not fewer than two bolts per plate section with one bolt located not more than 12 inches or less than seven bolt diameters from each end of the plate section.\"",
            "IBC 2021 2308.3.1 (conventional light-frame): the same 1/2\" bolts, 7\" embedment and 6 ft spacing; at least two per piece, \"one located not more than 12 inches or less than 4 inches from each end.\"",
            "AWC SDPWS-2021 4.3.6.4.1: \"Connections shall be provided to transfer the unit shear force induced by the design load, ν, into and out of each shear wall.\"",
            "AWC SDPWS-2021 4.3.6.4.3: \"Foundation anchor bolts shall have a steel plate washer under each nut not less than 0.229\"x3\"x3\" in size … The plate washer shall extend to within 1/2\" of the edge of the bottom plate on the side(s) with sheathing or other material with nominal unit shear capacity greater than 400 plf.\" Exception: standard cut washers where the bolts resist shear only and (a) uplift anchorage is sized neglecting dead load, (b) h:b ≤ 2:1, (c) nominal unit shear capacity ≤ 1370 plf.",
            "AWC NDS-2018 Table 12E (bolts, single shear, sawn lumber to concrete; 6\" embedment, f'c ≥ 2,500 psi, Fyb = 45,000 psi), 1-1/2\" sill, Z∥: 1/2\" bolt SP 680, DF-L 650, HF 590, SPF 590 lb; 5/8\" bolt 970, 930, 860, 850 lb. Z' = Z × CD (Table 11.3.1), CD = 1.6 for wind (Table 2.3.2).",
        };

        /// <summary>NDS-2018 Table 12E, Z∥ (lb), a 1-1/2" sawn-lumber sill on concrete (NDS_WoodLibrary).</summary>
        public static double TableZ(double boltDia, Species species) => Bolts.SillToConcreteZ(boltDia, species);

        public static string GroupName(Species species) => $"{SpeciesInfo.Abbreviation(species)} (G = {SpeciesInfo.SpecificGravity(species):0.00})";

        /// <summary>
        /// The sill bolts of a wall of <paramref name="lengthFt"/> carrying <paramref name="vAsdPlf"/> (ASD). 1/2" bolts unless
        /// they would need closer than 16" o.c.; then 5/8". <paramref name="nominalPlf"/>, <paramref name="aspect"/> (h/b) and
        /// <paramref name="deadLoadInHoldown"/> decide the washer (SDPWS 4.3.6.4.3).
        /// </summary>
        public static SillAnchorage Design(double lengthFt, double vAsdPlf, Species species, double nominalPlf, double aspect, bool deadLoadInHoldown)
        {
            var a = Try(lengthFt, vAsdPlf, species, 0.5);
            if (a.SpacingByShearIn < 16) a = Try(lengthFt, vAsdPlf, species, 0.625);

            // washers
            bool cutOk = !deadLoadInHoldown && aspect <= 2 + 1e-9 && nominalPlf <= 1370;
            a.PlateWasher = !cutOk;
            a.WasherToSheathedEdge = a.PlateWasher && nominalPlf > 400;
            a.WasherReason = cutOk
                ? "Standard cut washers allowed (SDPWS 4.3.6.4.3 exception: shear only, hold-downs sized without dead load, h:b ≤ 2, nominal ≤ 1,370 plf); a 3 × 3 plate washer is still good practice."
                : "0.229\" × 3\" × 3\" steel plate washer under each nut (SDPWS 4.3.6.4.3" +
                  (deadLoadInHoldown ? ": the hold-downs count on dead load" : aspect > 2 ? ": h:b over 2:1" : ": nominal over 1,370 plf") + ")" +
                  (a.WasherToSheathedEdge ? "; it reaches to within 1/2\" of the sheathed edge of the plate (nominal over 400 plf)." : ".");
            return a;
        }

        private static SillAnchorage Try(double lengthFt, double v, Species species, double dia)
        {
            var a = new SillAnchorage { LengthFt = lengthFt, ShearAsdPlf = v, BoltDia = dia, Z = TableZ(dia, species), SpeciesGroup = GroupName(species) };
            a.SpacingByShearIn = v > 0 ? a.ZPrime / v * 12.0 : double.PositiveInfinity;
            a.SpacingIn = Math.Floor(Math.Min(a.MaxSpacingIn, a.SpacingByShearIn) + 1e-9);
            a.ShearGoverns = a.SpacingByShearIn < a.MaxSpacingIn;
            a.EndMaxIn = Math.Min(12, Math.Floor(a.SpacingIn / 2));
            double L = lengthFt * 12;
            if (L <= 12) { a.Count = 0; a.Note = "A plate section 12\" or shorter: no bolt required (IRC R403.1.6 exception)."; return a; }
            if (L <= 24) { a.Count = 1; a.PositionsIn.Add(L / 2); a.Note = "A plate section 24\" or shorter: one bolt in its middle third (IRC R403.1.6 exception)."; return a; }
            double e = a.EndMaxIn, run = L - 2 * e;
            a.Count = Math.Max(2, (int)Math.Ceiling(run / a.SpacingIn - 1e-9) + 1);
            // a spacing a builder can tape: up to the next 1/2" (still within the maximum, a whole inch), the two ends
            // taking what is left, equally (a little under the end maximum; never under 7 diameters)
            double g = Math.Ceiling(run / (a.Count - 1) * 2 - 1e-9) / 2;
            double e2 = (L - (a.Count - 1) * g) / 2;
            if (g > a.SpacingIn + 1e-9 || e2 < a.EndMinIn - 1e-9) { g = run / (a.Count - 1); e2 = e; }
            a.GapIn = g;
            a.EndIn = e2;
            for (int i = 0; i < a.Count; i++) a.PositionsIn.Add(e2 + g * i);
            return a;
        }
    }
}
