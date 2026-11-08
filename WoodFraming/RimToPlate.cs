using System;
using NDS_WoodLibrary.NDS2018;

namespace WoodFraming
{
    /// <summary>
    /// The rim joist (a floor) or the blocking (a roof) toe-nailed down into a shear wall's double top plate: the shear the
    /// diaphragm, and the walls standing on it, hand down into the wall below. One row of common toe-nails at the spacing one
    /// nail's ASD lateral value allows over the wall's ASD unit shear, taken over its shear part (the owner's choice,
    /// 2026-11-08: conservative, the same plf the wall is designed for).
    ///
    /// Sources (quoted in <see cref="Citations"/>):
    ///  AWC SDPWS-2015/2021 4.3.6.4.1: connections transfer the unit shear v into and out of each shear wall.
    ///  AWC NDS-2018 12.1.6.3 and Figure 12A: a toe-nail is driven at about 30° with the member and started about 1/3 of its
    ///    length from the member's end. The NDS gives no side member thickness or penetration for the yield equations; they
    ///    are taken along the nail as Figure 12A places it (the owner's choice, 2026-11-08): in the rim or blocking
    ///    ts = (L/3) / cos 30° = 0.385 L, the rest into the plate, p = L − ts = 0.615 L.
    ///  NDS-2018 12.3.1 (yield limit equations, single shear), Table 12.3.3 note 2 (Fe = 16,600 G^1.84), Table 12N note 2
    ///    (Fyb), Table 12.3.1B (Rd), 12.1.6.4 (pmin = 6D), 12.3.5.3 (ℓm = p − D); Appendix Table L4 (the nail).
    ///  NDS-2018 12.5.4.2: toe-nailed lateral values × Ctn = 0.83. Table 11.3.1: Z' = Z × CD × Ctn (CD = 1.6 for wind, Table
    ///    2.3.2; CM, Ct, Cg, CΔ, Ceg, Cdi 1.0: dry, normal temperature, D &lt; 1/4", side grain, not diaphragm sheathing nails).
    /// </summary>
    public class RimToPlate
    {
        /// <summary>Toe-nailing: the share of the nail's length started from the member's end and the angle with the member (NDS 12.1.6.3).</summary>
        public const double StartShare = 1.0 / 3.0, AngleDeg = 30, Ctn = 0.83;

        public double ShearAsdPlf { get; set; }
        /// <summary>The roof's blocking (true) or a floor's rim (false), for the words.</summary>
        public bool Blocking { get; set; }
        /// <summary>"8d common".</summary>
        public string Nail { get; set; }
        public double NailLengthIn { get; set; }
        public double NailDiaIn { get; set; }
        /// <summary>The nail's length in the rim or blocking and in the plate, along it (in).</summary>
        public double SideIn { get; set; }
        public double PenetrationIn { get; set; }
        public string SpeciesGroup { get; set; }
        /// <summary>One nail's reference lateral value Z (lb) and the governing mode's name.</summary>
        public double Z { get; set; }
        public string Mode { get; set; }
        public double CD { get; set; } = 1.6;
        public double ZPrime => Z * CD * Ctn;
        public double SpacingByShearIn { get; set; }
        /// <summary>The app's limits (not code values): the most and the least spacing it will give (in).</summary>
        public double MaxSpacingIn { get; set; } = 16;
        public double MinSpacingIn { get; set; } = 3;
        public double SpacingIn { get; set; }
        /// <summary>What does not work, in a sentence (null: it works).</summary>
        public string Fails { get; set; }

        public string Member => Blocking ? "blocking" : "rim";
        public double CapacityPlf => SpacingIn > 0 ? ZPrime * 12.0 / SpacingIn : 0;

        public static readonly string[] Citations =
        {
            "AWC SDPWS 4.3.6.4.1: \"Connections shall be provided to transfer the induced unit shear force, ν, into and out of each shear wall.\"",
            "AWC NDS-2018 12.1.6.3: \"Toe-nails shall be driven at an angle of approximately 30° with the member and started approximately 1/3 the length of the nail from the member end (see Figure 12A).\" The side member and penetration along the nail as Figure 12A places it: ts = (L/3)/cos 30°, p = L − ts (the owner's choice; the NDS gives no rule).",
            "AWC NDS-2018 12.3.1 (single shear yield limit equations); Table 12.3.3 note 2 (Fe = 16,600 G^1.84 for D < 1/4\"); Table 12N note 2 (Fyb); Table 12.3.1B (Rd = 2.2 for D ≤ 0.17\"); 12.1.6.4 (pmin = 6D); 12.3.5.3 (ℓm = p − E/2, E = 2D); Appendix Table L4 (common nails: L, D).",
            "AWC NDS-2018 12.5.4.2: \"Reference lateral design values, Z, for toe-nailed connections shall be multiplied by the toe-nail factor, Ctn = 0.83.\" Table 11.3.1: Z' = Z × CD × Ctn; CD = 1.6 for wind (Table 2.3.2); the others 1.0 here.",
        };

        /// <summary>
        /// The toe-nailing of a rim (or the roof's blocking, <paramref name="blocking"/>) onto the top plates of a wall carrying
        /// <paramref name="vAsdPlf"/> (ASD) at its top, with <paramref name="nail"/> (a common nail by pennyweight, "8d"), the
        /// rim of the same species as the plates.
        /// </summary>
        public static RimToPlate Design(double vAsdPlf, Species species, string nail, bool blocking = false)
        {
            var n = new RimToPlate { ShearAsdPlf = Math.Abs(vAsdPlf), Blocking = blocking, SpeciesGroup = SillAnchorage.GroupName(species) };
            var dims = Nails.Common(nail);
            if (dims == null) { n.Nail = nail; n.Fails = $"\"{nail}\" is not a common nail in NDS Appendix Table L4."; return n; }
            n.Nail = $"{nail.Trim().ToLowerInvariant()} common";
            n.NailLengthIn = dims.Value.L;
            n.NailDiaIn = dims.Value.D;
            n.SideIn = n.NailLengthIn * StartShare / Math.Cos(AngleDeg * Math.PI / 180);
            n.PenetrationIn = n.NailLengthIn - n.SideIn;
            double g = SpeciesInfo.SpecificGravity(species);
            var z = Nails.SingleShearZ(n.NailDiaIn, n.SideIn, n.PenetrationIn, g, g, out var modes);
            if (z == null)
            {
                n.Fails = $"The {n.Nail} toe-nail goes only {n.PenetrationIn:0.##}\" into the plate, under 6D = {6 * n.NailDiaIn:0.##}\" (NDS 12.1.6.4): a longer nail.";
                return n;
            }
            n.Z = z.Value;
            n.Mode = new[] { "Im", "Is", "II", "IIIm", "IIIs", "IV" }[Array.IndexOf(modes, z.Value)];
            n.SpacingByShearIn = n.ShearAsdPlf > 0 ? n.ZPrime / n.ShearAsdPlf * 12.0 : double.PositiveInfinity;
            n.SpacingIn = Math.Floor(Math.Min(n.MaxSpacingIn, n.SpacingByShearIn) * 2 + 1e-9) / 2; // to the 1/2" below
            if (n.SpacingIn < n.MinSpacingIn - 1e-9)
                n.Fails = $"{n.Nail} toe-nails would need {n.SpacingByShearIn:0.#}\" o.c., closer than {n.MinSpacingIn:0}\" (the app's least, for splitting: NDS 12.1.6.5 gives no number): framing clips or a stronger connection.";
            return n;
        }
    }
}
