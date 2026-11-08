using System;
using NDS_WoodLibrary.NDS2018;

namespace WoodFraming
{
    /// <summary>
    /// The nailing of an upper-story shear wall's sill (a flat 2x bottom plate) through the floor sheathing into the rim or
    /// blocking under it: the wall's shear out of its base (SDPWS 4.3.6.4.1). One row of common nails at the spacing one
    /// nail's ASD lateral value allows over the wall's ASD unit shear (each nail takes the shear of its own spacing).
    ///
    /// Sources (quoted in <see cref="Citations"/>):
    ///  AWC SDPWS-2015/2021 4.3.6.4.1: connections transfer the unit shear v into and out of each shear wall.
    ///  AWC NDS-2018 12.3.1: the yield limit equations, for members whose faces are in contact (a); the sill and the rim are
    ///    not: the floor sheathing is between them. AWC TR12 (2015) Table 1-1 gives the equations with a gap g between the
    ///    members (the NDS's with g = 0); the sheathing is taken as the gap (its own bearing neglected): the owner's choice,
    ///    2026-11-07, as neither the NDS nor SDPWS addresses it.
    ///  NDS-2018 Appendix Table L4: the nail's length and diameter; Table 12.3.3: Fe = 16,600 G^1.84; Table 12N note 2: Fyb;
    ///    Table 12.3.1B: Rd; 12.1.6.4: penetration into the rim at least 6D; 12.3.5.3: ℓm = p − D.
    ///  NDS-2018 Table 11.3.1: Z' = Z × CD × CM × Ct × Cg × CΔ × Ceg × Cdi × Ctn; here CD = 1.6 (wind, Table 2.3.2), the others
    ///    1.0 (dry, normal temperature; D &lt; 1/4": Cg and CΔ 1.0, 12.5.1.1; into side grain; not diaphragm sheathing nails; not
    ///    toe-nailed).
    /// </summary>
    public class SillNailing
    {
        public double LengthFt { get; set; }
        public double ShearAsdPlf { get; set; }
        /// <summary>"16d common".</summary>
        public string Nail { get; set; }
        public double NailLengthIn { get; set; }
        public double NailDiaIn { get; set; }
        public double SillIn { get; set; } = 1.5;
        public double SheathingIn { get; set; }
        /// <summary>The nail's penetration into the rim or blocking past the sheathing (in).</summary>
        public double PenetrationIn { get; set; }
        public string SpeciesGroup { get; set; }
        /// <summary>One nail's reference lateral value Z (lb, TR12 with the sheathing as the gap) and the governing mode's name.</summary>
        public double Z { get; set; }
        public string Mode { get; set; }
        public double CD { get; set; } = 1.6;
        public double ZPrime => Z * CD;
        public double SpacingByShearIn { get; set; }
        /// <summary>The app's limits (not code values): the most and the least spacing it will give (in).</summary>
        public double MaxSpacingIn { get; set; } = 16;
        public double MinSpacingIn { get; set; } = 3;
        public double SpacingIn { get; set; }
        /// <summary>What does not work, in a sentence (null: it works).</summary>
        public string Fails { get; set; }

        public double CapacityPlf => SpacingIn > 0 ? ZPrime * 12.0 / SpacingIn : 0;

        public static readonly string[] Citations =
        {
            "AWC SDPWS 4.3.6.4.1: \"Connections shall be provided to transfer the induced unit shear force, ν, into and out of each shear wall.\"",
            "AWC NDS-2018 12.3.1: the yield limit equations apply where \"(a) the faces of the connected members are in contact\". Here the floor sheathing is between the sill and the rim: AWC TR12 (2015) Table 1-1, single shear with the gap g (the sheathing, its bearing neglected; the owner's choice, as the NDS and SDPWS do not address it).",
            "AWC NDS-2018 Appendix Table L4 (common nails: L, D); Table 12.3.3 note 2 (Fe = 16,600 G^1.84 for D < 1/4\"); Table 12N note 2 (Fyb); Table 12.3.1B (Rd = 2.2 for D ≤ 0.17\"); 12.1.6.4 (pmin = 6D); 12.3.5.3 (ℓm = p − E/2, E = 2D).",
            "AWC NDS-2018 Table 11.3.1: Z' = Z × CD × CM × Ct × Cg × CΔ × Ceg × Cdi × Ctn; CD = 1.6 for wind (Table 2.3.2); the others 1.0 here (dry, normal temperature, D < 1/4\", side grain, not diaphragm sheathing, not toe-nailed).",
        };

        /// <summary>
        /// The sill nailing of a wall of <paramref name="lengthFt"/> carrying <paramref name="vAsdPlf"/> (ASD), with
        /// <paramref name="nail"/> (a common nail by pennyweight, "16d") through <paramref name="sheathingIn"/> of floor sheathing
        /// into a rim of the same species as the sill.
        /// </summary>
        public static SillNailing Design(double lengthFt, double vAsdPlf, Species species, double sheathingIn, string nail)
        {
            var n = new SillNailing { LengthFt = lengthFt, ShearAsdPlf = Math.Abs(vAsdPlf), SheathingIn = Math.Max(0, sheathingIn), SpeciesGroup = SillAnchorage.GroupName(species) };
            var dims = Nails.Common(nail);
            if (dims == null) { n.Nail = nail; n.Fails = $"\"{nail}\" is not a common nail in NDS Appendix Table L4."; return n; }
            n.Nail = $"{nail.Trim().ToLowerInvariant()} common";
            n.NailLengthIn = dims.Value.L;
            n.NailDiaIn = dims.Value.D;
            n.PenetrationIn = n.NailLengthIn - n.SillIn - n.SheathingIn;
            double g = SpeciesInfo.SpecificGravity(species);
            var z = Nails.SingleShearZWithGap(n.NailDiaIn, n.SillIn, n.SheathingIn, n.PenetrationIn, g, g, out var modes);
            if (z == null)
            {
                n.Fails = $"The {n.Nail} goes only {n.PenetrationIn:0.##}\" into the rim past the sill and the sheathing, under 6D = {6 * n.NailDiaIn:0.##}\" (NDS 12.1.6.4): a longer nail.";
                return n;
            }
            n.Z = z.Value;
            n.Mode = new[] { "Im", "Is", "II", "IIIm", "IIIs", "IV" }[Array.IndexOf(modes, z.Value)];
            n.SpacingByShearIn = n.ShearAsdPlf > 0 ? n.ZPrime / n.ShearAsdPlf * 12.0 : double.PositiveInfinity;
            n.SpacingIn = Math.Floor(Math.Min(n.MaxSpacingIn, n.SpacingByShearIn) * 2 + 1e-9) / 2; // to the 1/2" below
            if (n.SpacingIn < n.MinSpacingIn - 1e-9)
                n.Fails = $"{n.Nail} would need {n.SpacingByShearIn:0.#}\" o.c., closer than {n.MinSpacingIn:0}\" (the app's least, for splitting: NDS 12.1.6.5 gives no number): a stronger connection.";
            return n;
        }
    }
}
