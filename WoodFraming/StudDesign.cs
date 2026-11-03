using System;
using System.Collections.Generic;
using System.Linq;
using LoadManager;
using NDS_WoodLibrary.NDS2018;

namespace WoodFraming
{
    /// <summary>What a stud is checked for (see <see cref="StudDesign.Check"/>).</summary>
    public sealed class StudDesignInput
    {
        public Species Species { get; set; }
        public Grade Grade { get; set; }
        public string Size { get; set; }
        public double SpacingIn { get; set; }
        /// <summary>The stud's length between the plates (in): its unbraced length about the strong axis.</summary>
        public double LengthIn { get; set; }
        /// <summary>The gravity loads on the wall's top (plf, service level), from the LoadManager.</summary>
        public LineLoad Loads { get; set; } = new LineLoad();
        /// <summary>The load combinations to check (the LoadManager's); null: <see cref="LoadCombinations.IbcAlternativeAsd"/>.</summary>
        public IList<LoadCombination> Combinations { get; set; }
        /// <summary>
        /// Exterior wall: the components and cladding wind pressure on the stud (psf, strength level, the larger of the
        /// inward and outward, ASCE 7 Ch. 30). Null: an interior wall (<see cref="InteriorPressurePsf"/>).
        /// </summary>
        public double? WindPressurePsf { get; set; }
        /// <summary>Where <see cref="WindPressurePsf"/> came from ("Zone 5, A = 49 sq ft, qh = 25.1 psf ...").</summary>
        public string WindNote { get; set; }
        /// <summary>An interior wall's horizontal load (IBC 1607.16: not less than 5 psf), taken as a live load (L).</summary>
        public double InteriorPressurePsf { get; set; } = StudDesign.InteriorMinimumPsf;
        /// <summary>The deflection limit L/n (IBC Table 1604.3 exterior walls: 240 with brittle finishes, 120 flexible, 360 plaster).</summary>
        public double DeflectionLimit { get; set; }
        /// <summary>Studs repetitive (4.3.9: ≤ 24" o.c., three or more, sheathed): Cr = 1.15.</summary>
        public bool Repetitive { get; set; } = true;
        /// <summary>Ke: 1.0 for a stud pinned at both plates.</summary>
        public double Ke { get; set; } = 1.0;
    }

    /// <summary>One load combination's stresses.</summary>
    public sealed class StudCombination
    {
        public string Name { get; set; }             // "D + L + 0.6W"
        public string Equation { get; set; }          // "IBC Eq. 16-2"
        public LoadDuration Duration { get; set; }
        public double CD { get; set; }
        public double P { get; set; }                 // lb per stud
        public double WLateral { get; set; }          // plf along the stud
        public double M { get; set; }                 // lb-in
        public double fc { get; set; }
        public double FcStar { get; set; }
        public double CP { get; set; }
        public double FcPrime { get; set; }
        public double fb { get; set; }
        public double FbPrime { get; set; }
        public double FcE { get; set; }
        /// <summary>Eq. 3.9-3 (or fc/Fc' for axial load alone).</summary>
        public double Ratio { get; set; }
        public double V { get; set; }                 // lb
        public double fv { get; set; }
        public double FvPrime { get; set; }
    }

    /// <summary>
    /// A bearing wall stud designed per NDS-2018 3.9.2 (Eq. 3.9-3): axial load from the wall's top loads, bending from the
    /// wind (or the interior 5 psf) on its spacing, simply supported between the plates, braced on its weak axis by the
    /// sheathing; the combinations are the LoadManager's (by default the 2024 IBC's alternative allowable stress ones,
    /// 1605.2, each with the variable loads set to zero in turn), those with a load the stud does not carry skipped, CD by
    /// the shortest load in each (NDS 2.3.2.2). Also the bearing on the plate (Fc⊥),
    /// shear (Fv) and the deflection (IBC Table 1604.3, the wind at 0.42 of C&amp;C, footnote f).
    /// </summary>
    public sealed class StudDesign
    {
        public const double InteriorMinimumPsf = 5;
        public const double WindDeflectionFactor = 0.42;

        public StudDesignInput Input { get; private set; }
        public ReferenceValues Values { get; private set; }
        public SizeFactors CF { get; private set; }
        public Section Section { get; private set; }
        public double Le => Input.Ke * Input.LengthIn;
        public double Slenderness => Le / Section.D;
        public double EminPrime => Values.Emin;          // CM, Ct, Ci, CT = 1
        public double EPrime => Values.E;
        public double Cr => Input.Repetitive ? Adjustments.Cr : 1.0;
        public List<StudCombination> Combinations { get; } = new List<StudCombination>();
        public StudCombination Governing { get; private set; }
        public double BearingFc { get; private set; }     // the largest stud load over its end, psi
        public double FcPerpPrime => Values.FcPerp;       // Cb taken as 1.0
        public double DeflectionIn { get; private set; }
        public double DeflectionAllowedIn => Input.LengthIn / Input.DeflectionLimit;
        public double DeflectionPressurePsf { get; private set; }
        public bool SlendernessOk => Slenderness <= Adjustments.MaxSlenderness + 1e-9;
        public double MaxRatio => Combinations.Count == 0 ? 0 : Combinations.Max(c => c.Ratio);
        public double MaxShearRatio => Combinations.Count == 0 ? 0 : Combinations.Max(c => c.FvPrime > 0 ? c.fv / c.FvPrime : 0);
        public bool Passes => SlendernessOk && MaxRatio <= 1 + 1e-9 && BearingFc <= FcPerpPrime + 1e-9 && MaxShearRatio <= 1 + 1e-9 && DeflectionIn <= DeflectionAllowedIn + 1e-9;
        public bool Exterior => Input.WindPressurePsf != null;
        public List<string> Notes { get; } = new List<string>();

        public static readonly string[] Citations =
        {
            LoadCombinations.IbcAlternativeAsdSource,
            Adjustments.CDSource,
            Adjustments.CrSource,
            Adjustments.CPSource,
            Adjustments.CombinedSource,
            "NDS-2018 3.7.1.2: le = Ke ℓu; Ke = 1.0 for a stud pinned at both plates (Appendix G, Table G1; not checked against the printed table here). The sheathing braces the weak axis (NDS Commentary C3.6.7), so the depth d governs.",
            Table4.Source4A,
            Table4.Source4B,
            "IBC 2024 1607.16: interior walls and partitions over 6 ft high, not less than a horizontal load of 5 psf (taken here as a live load, L: it goes with the floor live load in the combinations, ten-year duration).",
            "IBC 2024 Table 1604.3, exterior walls: with brittle finishes ℓ/240, with flexible finishes ℓ/120, with plaster or stucco ℓ/360; footnote f: \"The wind load is permitted to be taken as 0.42 times the 'component and cladding' loads for the purpose of determining deflection limits.\"",
        };

        /// <summary>Designs the stud for every combination.</summary>
        public static StudDesign Check(StudDesignInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (!(input.SpacingIn > 0) || !(input.LengthIn > 0) || !(input.DeflectionLimit > 0)) throw new ArgumentOutOfRangeException(nameof(input), "Spacing, length and the deflection limit must be more than 0.");
            var d = new StudDesign
            {
                Input = input,
                Section = Section.Of(input.Size),
                Values = Table4.Lookup(input.Species, input.Grade, input.Size),
                CF = Table4.SizeFactor(input.Species, input.Grade, input.Size),
            };
            double s = input.SpacingIn / 12.0, L = input.LengthIn;
            var ld = input.Loads ?? new LineLoad();
            double lateral = input.WindPressurePsf ?? input.InteriorPressurePsf;
            var lateralType = input.WindPressurePsf != null ? LoadType.Wind : LoadType.Live;
            double fcE = Adjustments.FcE(d.EminPrime, d.Slenderness);

            // a load is on the stud when its line load is, or it is the lateral pressure's kind (dead always: the stud's own)
            bool OnStud(LoadType t) => t == LoadType.Dead || ld.Of(t) > 0 || (t == lateralType && lateral > 0);
            foreach (var c in input.Combinations ?? LoadCombinations.IbcAlternativeAsd())
            {
                var types = c.Types.ToList();
                if (!types.All(OnStud)) continue;      // it has a load this stud does not carry: another combination covers it
                double P = types.Sum(t => c.Factor(t) * ld.Of(t)) * s;                  // lb per stud
                double w = c.Factor(lateralType) * lateral * s;                         // plf along the stud
                var dur = types.Select(Duration).OrderBy(x => Adjustments.CD(x)).Last();        // the shortest load governs (2.3.2.2)
                double cd = Adjustments.CD(dur);
                double fcStar = d.Values.Fc * cd * d.CF.Fc;
                double cp = Adjustments.CP(fcStar, fcE);
                double M = w / 12.0 * L * L / 8.0;
                double fc = P / d.Section.A, fb = M / d.Section.Sx;
                double fcPrime = fcStar * cp, fbPrime = d.Values.Fb * cd * d.CF.Fb * d.Cr;
                double V = w / 12.0 * L / 2.0;
                d.Combinations.Add(new StudCombination
                {
                    Name = c.Name, Equation = c.Equation, Duration = dur, CD = cd, P = P, WLateral = w, M = M,
                    fc = fc, FcStar = fcStar, CP = cp, FcPrime = fcPrime, fb = fb, FbPrime = fbPrime, FcE = fcE,
                    Ratio = Adjustments.CombinedCompressionBending(fc, fcPrime, fb, fbPrime, fcE),
                    V = V, fv = 1.5 * V / d.Section.A, FvPrime = d.Values.Fv * cd,
                });
            }
            d.Governing = d.Combinations.OrderByDescending(c => c.Ratio).FirstOrDefault();
            d.BearingFc = d.Combinations.Count == 0 ? 0 : d.Combinations.Max(c => c.P) / d.Section.A;

            // deflection: E' (not Emin), the wind at 0.42 of C&C (an interior wall's 5 psf as is)
            d.DeflectionPressurePsf = input.WindPressurePsf is double p ? WindDeflectionFactor * p : input.InteriorPressurePsf;
            double wd = d.DeflectionPressurePsf * s / 12.0;  // lb/in
            d.DeflectionIn = 5 * wd * Math.Pow(L, 4) / (384 * d.EPrime * d.Section.Ix);

            if (!d.SlendernessOk) d.Notes.Add($"le/d = {d.Slenderness:0.0}: over the 50 NDS 3.7.1.4 allows. A deeper stud is needed.");
            if (input.SpacingIn > 24 && input.Repetitive) d.Notes.Add("Over 24\" o.c.: not a repetitive member (4.3.9); Cr should be 1.0.");
            d.Notes.Add("Bearing on the plates: Fc⊥ with Cb = 1.0 (conservative); the plates are the stud's species and grade.");
            return d;
        }

        /// <summary>
        /// A load's duration for CD (NDS Table 2.3.2): dead permanent, floor live ten years, snow two months, roof live
        /// seven days, wind and earthquake ten minutes. Rain is not in the table.
        /// </summary>
        public static LoadDuration Duration(LoadType t)
        {
            switch (t)
            {
                case LoadType.Dead: return LoadDuration.Permanent;
                case LoadType.Live: return LoadDuration.TenYears;
                case LoadType.Snow: return LoadDuration.TwoMonths;
                case LoadType.RoofLive: return LoadDuration.SevenDays;
                case LoadType.Wind:
                case LoadType.Earthquake: return LoadDuration.TenMinutes;
                default: throw new NotSupportedException($"No NDS Table 2.3.2 load duration for {t}.");
            }
        }
    }
}
