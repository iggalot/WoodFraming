using System;
using NDS_WoodLibrary.NDS2018;

namespace WoodFraming
{
    /// <summary>
    /// A floor's rim (one sawn ply of the joists' depth) as a diaphragm collector where no wall is under the line of shear
    /// walls (the walls stand on the floor only): the line's collector force (ASD) in the rim in tension and in compression.
    /// A rim's pieces only butt at a splice: the butt carries no tension, so each splice needs a strap across it for the
    /// whole force (the force given here; the strap is not picked from a catalog yet). Compression bears across a tight butt.
    ///
    /// Sources (quoted in <see cref="Citations"/>):
    ///  AWC SDPWS-2015 4.1.4: boundary elements (collectors) transfer the design tension and compression forces; the sheathing
    ///    shall not splice them.
    ///  NDS-2018 3.8.1: ft = T/An ≤ Ft'; 3.6.3 and 3.7.1: fc ≤ Fc' (CP = 1.0 here: the rim is braced along its length by the
    ///    joists framing into it and the floor sheathing nailed to it); Table 4.3.1: Ft' = Ft × CD × CF, Fc' = Fc × CD × CF × CP
    ///    (dry, normal temperature: CM = Ct = Ci = 1.0); CD = 1.6 for wind (Table 2.3.2); NDS Supplement Table 4A/4B.
    /// </summary>
    public sealed class RimCollector
    {
        public double ForceAsdLb { get; set; }
        public string Nominal { get; set; }
        public string Lumber { get; set; }
        public double AreaIn2 { get; set; }
        public double FtPrimePsi { get; set; }
        public double FcPrimePsi { get; set; }
        public double CD { get; set; } = 1.6;
        public double TensionLb => FtPrimePsi * AreaIn2;
        public double CompressionLb => FcPrimePsi * AreaIn2;
        /// <summary>The tension a strap across each splice of the rim must take (lb, ASD): the whole force.</summary>
        public double StrapLb => ForceAsdLb;
        public string Fails { get; set; }

        public static readonly string[] Citations =
        {
            "AWC SDPWS-2015 4.1.4: \"Shear wall and diaphragm boundary elements shall be provided to transfer the design tension and compression forces. Diaphragm and shear wall sheathing shall not be used to splice boundary elements.\"",
            "AWC NDS-2018 3.8.1 (tension parallel to grain, ft = T/An ≤ Ft') and 3.6.3, 3.7.1 (compression, CP = 1.0: the rim braced along its length by the joists and the floor sheathing); Table 4.3.1: Ft' = Ft × CD × CF, Fc' = Fc × CD × CF × CP; CD = 1.6 (wind); NDS Supplement Table 4A/4B.",
            "A rim's butt splice carries no tension: a strap across each splice takes the whole collector force (the strap is chosen from a catalog: not picked by the program yet).",
        };

        /// <summary>The collector check of a rim of <paramref name="nominal"/> lumber (the floor joists' size) for <paramref name="forceAsdLb"/>.</summary>
        public static RimCollector Design(double forceAsdLb, Species species, Grade grade, string nominal)
        {
            var c = new RimCollector { ForceAsdLb = Math.Abs(forceAsdLb), Nominal = nominal };
            Section s;
            ReferenceValues r;
            SizeFactors cf;
            try { s = Section.Of(nominal); r = Table4.Lookup(species, grade, nominal); cf = Table4.SizeFactor(species, grade, nominal); }
            catch (ArgumentException e) { c.Fails = e.Message; return c; }
            c.Lumber = $"{SpeciesInfo.Name(species)} {SpeciesInfo.Name(grade)} {nominal}";
            c.AreaIn2 = s.A;
            c.FtPrimePsi = r.Ft * c.CD * cf.Ft;
            c.FcPrimePsi = r.Fc * c.CD * cf.Fc;
            if (c.ForceAsdLb > c.TensionLb + 1e-9)
                c.Fails = $"{c.ForceAsdLb:0} lb is over the rim's tension {c.TensionLb:0} lb: a deeper rim, or a strap the whole length of the line.";
            else if (c.ForceAsdLb > c.CompressionLb + 1e-9)
                c.Fails = $"{c.ForceAsdLb:0} lb is over the rim's compression {c.CompressionLb:0} lb.";
            return c;
        }
    }
}
