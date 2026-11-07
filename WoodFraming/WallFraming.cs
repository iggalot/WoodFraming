using System;
using System.Collections.Generic;
using System.Linq;
using BuildingModel.Geometry;

namespace WoodFraming
{
    /// <summary>What a framing piece is.</summary>
    public enum FramingKind { SillPlate, TopPlate, Stud, EndPost, PostUnder, Blocking }

    /// <summary>
    /// One framing piece in the wall's elevation: inches, x along the wall from its low end (west or south), y up from the
    /// underside of the sill plate. Plates and blocking run along the wall (their length is x1 − x0); studs and posts stand
    /// up (y1 − y0). A post is several 2x plies side by side, each its own piece.
    /// </summary>
    public sealed class FramingPiece
    {
        public FramingKind Kind { get; set; }
        public double X0 { get; set; }
        public double X1 { get; set; }
        public double Y0 { get; set; }
        public double Y1 { get; set; }
        /// <summary>Nominal size: "2x4", "2x6".</summary>
        public string Size { get; set; }
        public bool Horizontal => Kind == FramingKind.SillPlate || Kind == FramingKind.TopPlate || Kind == FramingKind.Blocking;
        /// <summary>The piece's cut length (in).</summary>
        public double Length => Horizontal ? X1 - X0 : Y1 - Y0;
        public double CenterX => (X0 + X1) / 2;
    }

    /// <summary>One sheathing sheet (or cut piece) on a face, in the elevation's inches.</summary>
    public sealed class SheathingSheet
    {
        public double X0 { get; set; }
        public double X1 { get; set; }
        public double Y0 { get; set; }
        public double Y1 { get; set; }
        public double Width => X1 - X0;
        public double Height => Y1 - Y0;
        /// <summary>A whole 4 × 8 sheet (not cut).</summary>
        public bool Full => Math.Abs(Width - WallFraming.SheetWidth) < 0.01 && Math.Abs(Height - WallFraming.SheetHeight) < 0.01;
    }

    /// <summary>
    /// How a wall is built, for its elevation, 3D view and list of pieces (the assumptions are the designer's to
    /// confirm): one 2x sill plate and a double 2x top plate laid flat, as wide as the studs; studs at the wall's spacing
    /// from the low end, of the wall's size (both from the building model); a hold-down post of 2x plies at each end (as many as fill the
    /// hold-down's minimum post, at least two); a post of two plies under each hold-down from the story above that lands
    /// along the wall; 4 × 8 sheets standing up from the low end, so their vertical joints fall on studs (48 = 3 × 16), with
    /// 2x blocking along the horizontal joint of a wall over 8 ft. Edge nails go round every sheet 3/8 in. in from its edges
    /// at the edge spacing, field nails along the studs inside it at the field spacing.
    /// </summary>
    public sealed class WallFraming
    {
        public const double Ply = 1.5;          // a 2x's thickness (in)
        public const double SheetWidth = 48, SheetHeight = 96;
        public const double NailEdgeDistance = 0.375;

        public double Length { get; private set; }     // in
        public double Height { get; private set; }     // in
        public double Spacing { get; private set; }    // in, studs on centre
        public string StudSize { get; private set; }
        /// <summary>The studs' depth across the wall (in): 3.5 for a 2x4, 5.5 for a 2x6; the plates are as wide.</summary>
        public double StudDepth { get; private set; }
        public int PostPlies { get; private set; }
        public List<FramingPiece> Pieces { get; } = new List<FramingPiece>();
        public List<SheathingSheet> Sheets { get; } = new List<SheathingSheet>();

        public IEnumerable<FramingPiece> Uprights => Pieces.Where(p => !p.Horizontal);
        /// <summary>The underside of the top plates: the studs run from the sill's top up to here.</summary>
        public double StudTop => Height - 2 * Ply;

        /// <summary>The stud sizes a wall may be given, smallest first (the building model's, <see cref="BuildingModel.Model.StudSizes"/>).</summary>
        public static readonly string[] StudSizes = BuildingModel.Model.StudSizes.All;

        public static bool IsStudSize(string size) => Array.IndexOf(StudSizes, size) >= 0;

        /// <summary>Actual depth (in) of a nominal 2x size: the building model's table (<see cref="BuildingModel.Model.StudSizes.DepthOf"/>).</summary>
        public static double DepthOf(string size) => BuildingModel.Model.StudSizes.DepthOf(size);

        /// <summary>Plies of 2x in a post at least <paramref name="postAlongWall"/> in. along the wall, at least two.</summary>
        public static int PliesFor(double postAlongWall) => Math.Max(2, (int)Math.Ceiling(postAlongWall / Ply - 0.05));

        /// <summary>
        /// Lays out a wall <paramref name="lengthFt"/> long and <paramref name="heightFt"/> high (sill to top of the top
        /// plates). <paramref name="postAlongWall"/>: the hold-down's minimum post (in, along the wall).
        /// <paramref name="postsUnderAt"/>: where hold-downs from above land (in from the low end), each gets a post.
        /// <paramref name="studSize"/> (one of <see cref="StudSizes"/>) and <paramref name="spacing"/> (in, on centre): the
        /// wall's, from the building model (its own, else the project's defaults: Building.StudSizeOf / StudSpacingOf).
        /// </summary>
        public static WallFraming Build(double lengthFt, double heightFt, string studSize, double spacing, double postAlongWall,
                                        IEnumerable<double> postsUnderAt = null)
        {
            if (!(spacing > 0)) throw new ArgumentOutOfRangeException(nameof(spacing), "The stud spacing must be more than 0.");
            var f = new WallFraming
            {
                Length = Math.Max(0, lengthFt * 12), Height = Math.Max(0, heightFt * 12), Spacing = spacing, StudSize = studSize,
            };
            f.StudDepth = DepthOf(f.StudSize);
            double L = f.Length, H = f.Height;
            if (L < 2 * Ply || H < 4 * Ply) return f;

            f.Pieces.Add(new FramingPiece { Kind = FramingKind.SillPlate, X0 = 0, X1 = L, Y0 = 0, Y1 = Ply, Size = f.StudSize });
            f.Pieces.Add(new FramingPiece { Kind = FramingKind.TopPlate, X0 = 0, X1 = L, Y0 = H - 2 * Ply, Y1 = H - Ply, Size = f.StudSize });
            f.Pieces.Add(new FramingPiece { Kind = FramingKind.TopPlate, X0 = 0, X1 = L, Y0 = H - Ply, Y1 = H, Size = f.StudSize });
            double y0 = Ply, y1 = f.StudTop;

            // the hold-down posts at the ends (no wider than half the wall)
            f.PostPlies = Math.Min(PliesFor(postAlongWall), Math.Max(1, (int)Math.Floor(L / 2 / Ply)));
            double post = f.PostPlies * Ply;
            for (int i = 0; i < f.PostPlies; i++)
            {
                f.Pieces.Add(new FramingPiece { Kind = FramingKind.EndPost, X0 = i * Ply, X1 = (i + 1) * Ply, Y0 = y0, Y1 = y1, Size = f.StudSize });
                f.Pieces.Add(new FramingPiece { Kind = FramingKind.EndPost, X0 = L - (i + 1) * Ply, X1 = L - i * Ply, Y0 = y0, Y1 = y1, Size = f.StudSize });
            }

            // a two-ply post under each hold-down from above, centred on it, kept clear of the end posts and each other
            var taken = new List<(double a, double b)> { (0, post), (L - post, L) };
            foreach (double at in (postsUnderAt ?? Enumerable.Empty<double>()).OrderBy(x => x))
            {
                if (at < post + Ply || at > L - post - Ply) continue; // over an end post: that post carries it
                double a = Math.Max(post, Math.Min(L - post - 2 * Ply, at - Ply));
                if (taken.Any(t => a < t.b - 0.01 && a + 2 * Ply > t.a + 0.01)) continue; // on an end post or a post already there
                taken.Add((a, a + 2 * Ply));
                for (int i = 0; i < 2; i++)
                    f.Pieces.Add(new FramingPiece { Kind = FramingKind.PostUnder, X0 = a + i * Ply, X1 = a + (i + 1) * Ply, Y0 = y0, Y1 = y1, Size = f.StudSize });
            }

            // studs at the spacing from the low end, centred on the layout marks, where there is no post
            for (double c = f.Spacing; c + Ply / 2 <= L - post + 1e-9; c += f.Spacing)
            {
                double a = c - Ply / 2, b = c + Ply / 2;
                if (taken.Any(t => a < t.b - 0.01 && b > t.a + 0.01)) continue;
                f.Pieces.Add(new FramingPiece { Kind = FramingKind.Stud, X0 = a, X1 = b, Y0 = y0, Y1 = y1, Size = f.StudSize });
            }

            // sheets standing up from the low end; a wall over 8 ft has a horizontal joint, blocked
            for (double x = 0; x < L - 0.01; x += SheetWidth)
                for (double y = 0; y < H - 0.01; y += SheetHeight)
                    f.Sheets.Add(new SheathingSheet { X0 = x, X1 = Math.Min(L, x + SheetWidth), Y0 = y, Y1 = Math.Min(H, y + SheetHeight) });
            for (double j = SheetHeight; j < y1 - Ply - 0.01; j += SheetHeight)
            {
                var ups = f.Uprights.OrderBy(p => p.X0).ToList();
                for (int i = 0; i + 1 < ups.Count; i++)
                {
                    double a = ups[i].X1, b = ups[i + 1].X0;
                    if (b - a > 0.5)
                        f.Pieces.Add(new FramingPiece { Kind = FramingKind.Blocking, X0 = a, X1 = b, Y0 = j - Ply / 2, Y1 = j + Ply / 2, Size = f.StudSize });
                }
            }
            f.Pieces.Sort((p, q) => p.Kind != q.Kind ? p.Kind.CompareTo(q.Kind) : p.X0.CompareTo(q.X0));
            return f;
        }

        /// <summary>
        /// The nails of one sheet: edge nails round its edges, 3/8 in. in, at <paramref name="edgeSpacing"/> (each corner once);
        /// field nails along each stud or post under a hold-down from above inside it (the end posts are under the sheets' edges) (its centre more than 1 in. from the sheet's sides), between the edge
        /// rows, at <paramref name="fieldSpacing"/>.
        /// </summary>
        public (List<Point2> edge, List<Point2> field) Nails(SheathingSheet s, double edgeSpacing, double fieldSpacing = 12)
        {
            var edge = new List<Point2>();
            var seen = new HashSet<(long, long)>();
            void Add(double x, double y)
            {
                if (seen.Add(((long)Math.Round(x * 100), (long)Math.Round(y * 100)))) edge.Add(new Point2(x, y));
            }
            double e = NailEdgeDistance, xa = s.X0 + e, xb = s.X1 - e, ya = s.Y0 + e, yb = s.Y1 - e;
            if (edgeSpacing <= 0 || xb <= xa || yb <= ya) return (edge, new List<Point2>());
            void Line(double x0, double y0, double x1, double y1)
            {
                double len = Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
                int n = (int)Math.Floor(len / edgeSpacing + 1e-9);
                for (int i = 0; i <= n; i++) Add(x0 + (x1 - x0) * i * edgeSpacing / len, y0 + (y1 - y0) * i * edgeSpacing / len);
                Add(x1, y1);
            }
            Line(xa, ya, xa, yb); Line(xb, ya, xb, yb); Line(xa, ya, xb, ya); Line(xa, yb, xb, yb);
            var field = new List<Point2>();
            if (fieldSpacing > 0)
                foreach (var p in Uprights.Where(p => p.Kind != FramingKind.EndPost && p.CenterX > s.X0 + 1 && p.CenterX < s.X1 - 1))
                    for (double y = ya + fieldSpacing; y < yb - 1; y += fieldSpacing) field.Add(new Point2(p.CenterX, y));
            return (edge, field);
        }

        /// <summary>Every sheet's nails on one face: (edge, field) counts.</summary>
        public (int edge, int field) NailCount(double edgeSpacing, double fieldSpacing = 12)
        {
            int a = 0, b = 0;
            foreach (var s in Sheets)
            {
                var (e, f) = Nails(s, edgeSpacing, fieldSpacing);
                a += e.Count;
                b += f.Count;
            }
            return (a, b);
        }

        /// <summary>
        /// 4 × 8 sheets to buy for one face: a whole sheet each, and the cut pieces stacked up one sheet's height, tallest
        /// first, a piece going in the first sheet it fits (a 4 ft × 1 ft strip shares a sheet with seven more; a 4 ft × 7 ft
        /// piece takes a sheet of its own).
        /// </summary>
        public int SheetsToBuy()
        {
            int whole = Sheets.Count(s => s.Full);
            var left = new List<double>(); // height still free in each sheet being cut
            foreach (var s in Sheets.Where(s => !s.Full).OrderByDescending(s => s.Height))
            {
                int i = left.FindIndex(h => h >= s.Height - 0.01);
                if (i >= 0) left[i] -= s.Height;
                else left.Add(SheetHeight - s.Height);
            }
            return whole + left.Count;
        }
    }

    /// <summary>Lengths in feet and inches, to the nearest 1/8 in.: 39'-0", 4'-6 1/2", 7 1/4".</summary>
    public static class FtIn
    {
        public static string Of(double inches)
        {
            double r = Math.Round(inches * 8) / 8;
            int ft = (int)Math.Floor(r / 12 + 1e-9);
            double rest = r - ft * 12;
            int whole = (int)Math.Floor(rest + 1e-9);
            int eighths = (int)Math.Round((rest - whole) * 8);
            string frac = "";
            if (eighths > 0)
            {
                int num = eighths, den = 8;
                while (num % 2 == 0) { num /= 2; den /= 2; }
                frac = $" {num}/{den}";
            }
            string inch = $"{whole}{frac}\"";
            return ft > 0 ? $"{ft}'-{inch}" : inch;
        }

        /// <summary>Inches as a short number: 3.5 → 3 1/2.</summary>
        public static string Inches(double inches) => Of(inches).Replace("\"", "");
    }
}
