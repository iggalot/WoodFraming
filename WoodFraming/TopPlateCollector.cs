using System;
using NDS_WoodLibrary.NDS2018;

namespace WoodFraming
{
    /// <summary>
    /// A wall's double top plate as a diaphragm collector (drag strut): the line's collector force (ASD) through one ply where
    /// the other is spliced, in tension and in compression, and the face nails lapping the plies on each side of a splice.
    ///
    /// Sources (quoted in <see cref="Citations"/>):
    ///  AWC SDPWS-2015 4.1.4: boundary elements (collectors) transfer the design tension and compression forces; the sheathing
    ///    shall not splice them.
    ///  NDS-2018 3.8.1: ft = T/An ≤ Ft'; 3.6.3 and 3.7.1: fc ≤ Fc' (CP = 1.0 here: a top plate is braced along its length by
    ///    the studs under it and the diaphragm framing over it); Table 4.3.1: Ft' = Ft × CD × CF, Fc' = Fc × CD × CF × CP
    ///    (dry, normal temperature: CM = Ct = Ci = 1.0); CD = 1.6 for wind (Table 2.3.2); NDS Supplement Table 4A/4B.
    ///  NDS-2018 12.3.1, Table 12.3.1A: one nail's Z in single shear, the 1-1/2" upper ply the side member, the lower ply the
    ///    main member (ℓm its thickness where the nail passes through it, else p − D, 12.3.5.3); Z' = Z × CD.
    /// </summary>
    public sealed class TopPlateCollector
    {
        public double ForceAsdLb { get; set; }
        public string Nominal { get; set; }
        public string Lumber { get; set; }
        public double AreaIn2 { get; set; }
        public double FtPrimePsi { get; set; }
        public double FcPrimePsi { get; set; }
        public double TensionLb => FtPrimePsi * AreaIn2;
        public double CompressionLb => FcPrimePsi * AreaIn2;
        public string Nail { get; set; }
        public double Z { get; set; }
        public double CD { get; set; } = 1.6;
        public double ZPrime => Z * CD;
        /// <summary>Nails on each side of a splice, lapping the plies.</summary>
        public int NailsEachSide { get; set; }
        public string Fails { get; set; }

        public static readonly string[] Citations =
        {
            "AWC SDPWS-2015 4.1.4: \"Shear wall and diaphragm boundary elements shall be provided to transfer the design tension and compression forces. Diaphragm and shear wall sheathing shall not be used to splice boundary elements.\"",
            "AWC NDS-2018 3.8.1 (tension parallel to grain, ft = T/An ≤ Ft') and 3.6.3, 3.7.1 (compression, CP = 1.0: the plate braced along its length by the studs and the diaphragm framing); Table 4.3.1: Ft' = Ft × CD × CF, Fc' = Fc × CD × CF × CP; CD = 1.6 (wind); NDS Supplement Table 4A/4B.",
            "AWC NDS-2018 Table 12.3.1A (single shear, the upper ply the side member, the lower the main member); Table L4 (the nail); Z' = Z × CD.",
        };

        /// <summary>The collector check of a double top plate of <paramref name="nominal"/> lumber for <paramref name="forceAsdLb"/>.</summary>
        public static TopPlateCollector Design(double forceAsdLb, Species species, Grade grade, string nominal, string nail)
        {
            var c = new TopPlateCollector { ForceAsdLb = Math.Abs(forceAsdLb), Nominal = nominal };
            Section s;
            try { s = Section.Of(nominal); }
            catch (ArgumentException e) { c.Fails = e.Message; return c; }
            var r = Table4.Lookup(species, grade, nominal);
            var cf = Table4.SizeFactor(species, grade, nominal);
            c.Lumber = $"{SpeciesInfo.Name(species)} {SpeciesInfo.Name(grade)} {nominal}";
            c.AreaIn2 = s.A;
            c.FtPrimePsi = r.Ft * c.CD * cf.Ft;
            c.FcPrimePsi = r.Fc * c.CD * cf.Fc;
            var dims = Nails.Common(nail);
            if (dims == null) { c.Nail = nail; c.Fails = $"\"{nail}\" is not a common nail in NDS Appendix Table L4."; return c; }
            c.Nail = $"{nail.Trim().ToLowerInvariant()} common";
            double ts = s.B, tm = s.B, d = dims.Value.D;
            double p = Math.Min(dims.Value.L - ts, tm + d); // through the lower ply: ℓm = tm
            double g = SpeciesInfo.SpecificGravity(species);
            var z = Nails.SingleShearZ(d, ts, p, g, g);
            if (z == null) { c.Fails = $"The {c.Nail} does not reach 6D into the lower ply (NDS 12.1.6.4)."; return c; }
            c.Z = z.Value;
            c.NailsEachSide = c.ForceAsdLb <= 0 ? 0 : (int)Math.Ceiling(c.ForceAsdLb / c.ZPrime - 1e-9);
            if (c.ForceAsdLb > c.TensionLb + 1e-9)
                c.Fails = $"{c.ForceAsdLb:0} lb is over one ply's tension {c.TensionLb:0} lb at a splice: a steel strap, or a collector in the floor.";
            else if (c.ForceAsdLb > c.CompressionLb + 1e-9)
                c.Fails = $"{c.ForceAsdLb:0} lb is over one ply's compression {c.CompressionLb:0} lb.";
            return c;
        }
    }
}
