using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using BuildingModel.Model;

namespace WoodFraming.Wpf
{
    /// <summary>The wall views' ink and text, the same in every app.</summary>
    public static class WallInk
    {
        public static Brush Frozen(string hex) { var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); b.Freeze(); return b; }

        public static readonly Brush Paper = Brushes.White, Ink = Frozen("#1F2933"), Secondary = Frozen("#52514E"), Muted = Frozen("#8A8A85"),
                                     Fail = Frozen("#A50E1F"), Hardware = Frozen("#1F2933"), Timber = Frozen("#8B4A14"), Concrete = Frozen("#D5D8DC"),
                                     Joint = Frozen("#C9D6E8"), Dim = Frozen("#52514E"), LabelBack = Frozen("#E6FFFFFF");

        public static Pen P(Brush b, double t) { var p = new Pen(b, t); p.Freeze(); return p; }

        public static FormattedText Text(Visual v, string s, double size, Brush ink, bool bold = false) =>
            new FormattedText(s ?? "", CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal),
                size, ink, VisualTreeHelper.GetDpi(v).PixelsPerDip);

        /// <summary>Text with its top-left (or its centre, or its top-right) at a point, on a white back.</summary>
        public static void Label(DrawingContext dc, Visual v, string s, Point at, Brush ink, double size = 11, bool bold = false, bool centre = false, bool rightAligned = false)
        {
            var t = Text(v, s, size, ink, bold);
            var p = centre ? new Point(at.X - t.Width / 2, at.Y - t.Height / 2) : rightAligned ? new Point(at.X - t.Width, at.Y) : at;
            dc.DrawRectangle(LabelBack, null, new Rect(p.X - 1, p.Y, t.Width + 2, t.Height));
            dc.DrawText(t, p);
        }

        public static void Arrow(DrawingContext dc, Pen pen, Point from, Point to)
        {
            dc.DrawLine(pen, from, to);
            var d = to - from;
            if (d.Length < 1) return;
            d.Normalize();
            var n = new Vector(-d.Y, d.X);
            var g = new StreamGeometry();
            using (var c = g.Open())
            {
                c.BeginFigure(to, true, true);
                c.PolyLineTo(new[] { to - d * 8 + n * 4, to - d * 8 - n * 4 }, true, false);
            }
            g.Freeze();
            dc.DrawGeometry(pen.Brush, null, g);
        }

        /// <summary>A dimension line with end ticks and its text in the middle.</summary>
        public static void Dimension(DrawingContext dc, Visual v, Point a, Point b, string text, bool vertical = false, Brush ink = null)
        {
            var pen = P(ink ?? Dim, 1);
            dc.DrawLine(pen, a, b);
            var t = vertical ? new Vector(4, 0) : new Vector(0, 4);
            dc.DrawLine(pen, a - t, a + t);
            dc.DrawLine(pen, b - t, b + t);
            Label(dc, v, text, new Point((a.X + b.X) / 2, (a.Y + b.Y) / 2), ink ?? Dim, 10.5, centre: true);
        }

        public static void Hatch(DrawingContext dc, Rect r, Brush fill)
        {
            dc.DrawRectangle(fill, P(Dim, 0.8), r);
            dc.PushClip(new RectangleGeometry(r));
            var pen = P(Dim, 0.6);
            for (double x = r.Left - r.Height; x < r.Right; x += 7) dc.DrawLine(pen, new Point(x, r.Bottom), new Point(x + r.Height, r.Top));
            dc.Pop();
        }
    }

    /// <summary>
    /// A wall in elevation, to scale, the low end (west or south) on the left, seen from the face 1 side: the whole wall's
    /// framing (plates, studs, the posts), its hold-downs at the bottom (always: each on its end post's inside face, with its
    /// connector, tension and what it anchors to) and what comes down onto its top from the story above, with the
    /// dimensions. With <see cref="ShowShear"/> (a shear wall): its shear part's sheathing and nails, the sheathing and
    /// nailing callouts, the sill bolts, the lever arm and the check. A view of the model's data: it calculates nothing.
    /// </summary>
    public sealed class WallElevation : FrameworkElement
    {
        private readonly WallView v;
        private readonly WallFraming f;
        private readonly ShearFace face;
        private readonly string note;
        private readonly bool nails, shear;

        private static readonly Brush StudFill = WallInk.Frozen("#EAD7B5"), PlateFill = WallInk.Frozen("#D9B98C"), PostFill = WallInk.Frozen("#C99A5B"),
                                      UnderFill = WallInk.Frozen("#E9A15C"), SheetFill = WallInk.Frozen("#4D9DB8D9"), GypsumFill = WallInk.Frozen("#55E5E7EB"),
                                      EdgeNail = WallInk.Frozen("#B42318"), FieldNail = WallInk.Frozen("#3F3F46"), AboveInk = WallInk.Frozen("#C2410C"),
                                      AnchorInk = WallInk.Frozen("#1F5F8B"), SheetInk = WallInk.Frozen("#3B6EA8");
        private static readonly Pen Edge = WallInk.P(WallInk.Timber, 0.7), SheetEdge = WallInk.P(SheetInk, 1.3);

        /// <summary>The shear wall's information is drawn (its sheathing, nailing, sill bolts, arm and check).</summary>
        public bool ShowShear => shear;

        /// <param name="faceIndex">The face of sheathing shown (0, 1; -1 none).</param>
        public WallElevation(WallView view, WallFraming framing, bool showShear, int faceIndex, bool showNails)
        {
            v = view;
            f = framing;
            shear = showShear && view?.Shear != null;
            var faces = view?.Shear?.Faces ?? new List<ShearFace>();
            face = shear && faceIndex >= 0 && faceIndex < faces.Count ? faces[faceIndex] : null;
            note = shear && faceIndex == 1 && faces.Count < 2 ? "This wall has one sheathed face." : null;
            nails = showNails;
            SnapsToDevicePixels = true;
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            InvalidateVisual();
        }

        /// <summary>The sill bolts' dimension chain: each end to its bolt, and the spacing once with "TYP." when every space is the same.</summary>
        private void AnchorChain(DrawingContext dc, SillAnchors a, double p0, double p1, Func<double, double> X, double y)
        {
            var pen = WallInk.P(AnchorInk, 1);
            var at = a.PositionsIn.Select(x => p0 + x).ToList();
            var ticks = new List<double> { p0 };
            ticks.AddRange(at);
            ticks.Add(p1);
            dc.DrawLine(pen, new Point(X(p0), y), new Point(X(p1), y));
            foreach (double t in ticks)
            {
                dc.DrawLine(pen, new Point(X(t) - 3, y + 3), new Point(X(t) + 3, y - 3));
                if (t > p0 && t < p1) dc.DrawLine(WallInk.P(AnchorInk, 0.6), new Point(X(t), y - 6), new Point(X(t), y + 4));
            }
            var gaps = at.Zip(at.Skip(1), (p, q) => (p, q)).ToList();
            bool same = gaps.Count > 0 && gaps.All(g => Math.Abs((g.q - g.p) - (gaps[0].q - gaps[0].p)) < 1.0 / 16);
            void Put(double from, double to, string text, int outside)
            {
                double w = WallInk.Text(this, text, 10, AnchorInk).Width;
                double px = X(to) - X(from);
                if (w + 6 <= px || outside == 0) WallInk.Label(dc, this, text, new Point((X(from) + X(to)) / 2, y - 8), AnchorInk, 10, centre: true);
                else if (outside < 0) WallInk.Label(dc, this, text, new Point(X(from) - 6, y - 15), AnchorInk, 10, rightAligned: true);
                else WallInk.Label(dc, this, text, new Point(X(to) + 6, y - 15), AnchorInk, 10);
            }
            Put(p0, at[0], FtIn.Of(at[0] - p0), -1);
            Put(at.Last(), p1, FtIn.Of(p1 - at.Last()), +1);
            if (same) { var mid = gaps[gaps.Count / 2]; Put(mid.p, mid.q, $"{FtIn.Of(mid.q - mid.p)} TYP.", 0); }
            else foreach (var g in gaps) Put(g.p, g.q, FtIn.Of(g.q - g.p), 0);
            WallInk.Label(dc, this, $"Sill anchor bolts ({at.Count})", new Point(X(p0), y + 5), AnchorInk, 9.5);
        }

        protected override void OnRender(DrawingContext dc)
        {
            double W = ActualWidth, Hh = ActualHeight;
            dc.DrawRectangle(WallInk.Paper, null, new Rect(0, 0, W, Hh));
            if (v == null || f == null || f.Length <= 0 || f.Height <= 0) return;
            double L = f.Length, H = f.Height, p0 = f.PartFrom, p1 = f.PartTo;
            double left = 96, right = 230, top = 120, bottom = 206;
            double s = Math.Min((W - left - right) / L, (Hh - top - bottom) / H);
            if (s <= 0) return;
            double x0 = left + Math.Max(0, ((W - left - right) - L * s) / 2), yb = top + H * s + Math.Max(0, ((Hh - top - bottom) - H * s) / 2);
            double X(double x) => x0 + x * s;
            double Y(double y) => yb - y * s;
            Rect R(double a, double b, double c, double e) => new Rect(new Point(X(a), Y(c)), new Point(X(b), Y(e)));
            var d = v.Shear;

            // what it stands on
            if (v.OnFoundation) WallInk.Hatch(dc, new Rect(X(0) - 24, yb, L * s + 48, 22), WallInk.Concrete);
            else dc.DrawRectangle(WallInk.Joint, WallInk.P(WallInk.Dim, 0.8), new Rect(X(0) - 14, yb, L * s + 28, 10));
            WallInk.Label(dc, this, v.OnFoundation ? "Foundation" : "Floor of the story below", new Point(X(L / 2), yb + (v.OnFoundation ? 11 : 5)), WallInk.Dim, 10, centre: true);

            // the framing, the whole wall
            foreach (var p in f.Pieces)
            {
                Brush fill = p.Kind == FramingKind.Stud ? StudFill : p.Kind == FramingKind.EndPost ? PostFill : p.Kind == FramingKind.PostUnder ? UnderFill : PlateFill;
                dc.DrawRectangle(fill, Edge, R(p.X0, p.X1, p.Y1, p.Y0));
            }

            // the shear wall: one face of its sheathing over its part, and the nails
            if (face != null)
                foreach (var sh in f.Sheets)
                {
                    var rr = R(sh.X0, sh.X1, sh.Y1, sh.Y0);
                    rr.Inflate(-0.6, -0.6);
                    dc.DrawRectangle(face.Gypsum ? GypsumFill : SheetFill, SheetEdge, rr);
                    if (rr.Width > 60 && rr.Height > 30)
                        WallInk.Label(dc, this, $"{FtIn.Of(sh.Width)} × {FtIn.Of(sh.Height)}", new Point(rr.X + rr.Width / 2, rr.Y + Math.Min(rr.Height / 2, 40)), SheetInk, 9.5, centre: true);
                    if (nails && face.EdgeIn > 0)
                    {
                        var (edge, field) = f.Nails(sh, face.EdgeIn, face.FieldIn > 0 ? face.FieldIn : 12);
                        double re = Math.Max(1.1, Math.Min(2.2, 0.28 * s)), rf = re * 0.8;
                        foreach (var n in edge) dc.DrawEllipse(EdgeNail, null, new Point(X(n.X), Y(n.Y)), re, re);
                        foreach (var n in field) dc.DrawEllipse(FieldNail, null, new Point(X(n.X), Y(n.Y)), rf, rf);
                    }
                }
            if (note != null) WallInk.Label(dc, this, note, new Point(X((p0 + p1) / 2), Y(H / 2)), WallInk.Muted, 12, centre: true);
            if (shear && d?.Check != null)
                WallInk.Label(dc, this, $"Shear wall {FtIn.Of(p1 - p0)}: {d.Check}", new Point(X(0), 8), d.Fails ? WallInk.Fail : WallInk.Ink, 11.5, bold: true);
            if (v.Stale != null)
                WallInk.Label(dc, this, $"The lateral design is out of date: {v.Stale}", new Point(X(0), 26), WallInk.Muted, 10.5);

            // the hold-downs at the bottom (always), each on its end post's inside face, with its anchor
            double post = f.PostPlies * WallFraming.Ply;
            foreach (var h in v.Holdowns)
            {
                double at = Math.Max(0, Math.Min(L, h.AlongIn));
                if (h.Strap)
                {
                    // a strap up the post's face, nailed, down past the sill into what is under it
                    double sx = h.Low ? at - post / 2 : at + post / 2;
                    dc.DrawRectangle(WallInk.Hardware, null, R(sx - 0.625, sx + 0.625, 30, -14));
                }
                else
                {
                    double hx0 = h.Low ? at : at - 2.9, hx1 = hx0 + 2.9;
                    dc.DrawRectangle(WallInk.Hardware, null, R(hx0, hx1, WallFraming.Ply + 12, WallFraming.Ply));
                    double rx = (hx0 + hx1) / 2;
                    dc.DrawLine(WallInk.P(WallInk.Hardware, 2.4), new Point(X(rx), Y(WallFraming.Ply)), new Point(X(rx), yb + (v.OnFoundation ? 20 : 8)));
                }
                var ink = h.Bad ? WallInk.Fail : WallInk.Ink;
                double tx = h.Low ? X(Math.Max(0, at - post)) : X(Math.Min(L, at + post));
                double ty = yb + 34;
                WallInk.Label(dc, this, h.Connector ?? "none strong enough", new Point(tx, ty), ink, 12, bold: true, rightAligned: !h.Low);
                if (!string.IsNullOrEmpty(h.Fasteners)) WallInk.Label(dc, this, "fasteners " + h.Fasteners, new Point(tx, ty + 16), ink, 10.5, rightAligned: !h.Low);
                WallInk.Label(dc, this, $"T = {h.TensionAsd:N0} lb", new Point(tx, ty + 30), ink, 10.5, rightAligned: !h.Low);
                WallInk.Label(dc, this, h.Supported ? $"→ {h.Onto}" : "→ nothing under it", new Point(tx, ty + 44), ink, 10.5, rightAligned: !h.Low);
            }

            // the sill's anchor bolts (a first-story shear wall): each down into the concrete, a washer and nut on the sill
            var an = shear ? d?.Anchors : null;
            if (an != null && an.PositionsIn.Count > 0)
                foreach (double ax in an.PositionsIn.Select(x => p0 + x))
                {
                    dc.DrawLine(WallInk.P(AnchorInk, 2), new Point(X(ax), Y(WallFraming.Ply) - 3), new Point(X(ax), yb + 17));
                    dc.DrawLine(WallInk.P(AnchorInk, 2), new Point(X(ax), yb + 17), new Point(X(ax) + 5, yb + 17));
                    dc.DrawRectangle(AnchorInk, null, new Rect(X(ax) - Math.Max(3, 1.5 * s), Y(WallFraming.Ply) - 1.5, 2 * Math.Max(3, 1.5 * s), 1.5));
                }

            // what comes down from above (always): the hold-downs of the story above standing on the top plates, each over its post
            int k = 0;
            foreach (var a in v.Above.OrderBy(x => x.AlongIn))
            {
                double x = X(Math.Max(0, Math.Min(L, a.AlongIn)));
                WallInk.Arrow(dc, WallInk.P(AboveInk, 1.8), new Point(x, Y(H)), new Point(x, Y(H) - 30));
                double ly = Y(H) - 46 - 30 * (k++ % 2);
                WallInk.Label(dc, this, $"↑ {a.TensionAsd / 1000:0.0}k  {a.Connector ?? ""}", new Point(x, ly), AboveInk, 10.5, bold: true, centre: true);
                WallInk.Label(dc, this, $"from {a.From} above", new Point(x, ly + 14), AboveInk, 10, centre: true);
            }

            // dimensions: the sheet widths along the top, the shear part, the length and the height
            double yd = Y(H) - (v.Above.Count > 0 ? 96 : 26);
            if (face != null)
                foreach (var sh in f.Sheets.Where(x => x.Y0 < 0.01))
                    WallInk.Dimension(dc, this, new Point(X(sh.X0), yd), new Point(X(sh.X1), yd), FtIn.Of(sh.Width));
            if (an != null && an.PositionsIn.Count > 0) AnchorChain(dc, an, p0, p1, X, yb + 108);
            if (v.IsShearWall && (p0 > 0.01 || p1 < L - 0.01))
                WallInk.Dimension(dc, this, new Point(X(p0), yb + 174), new Point(X(p1), yb + 174), $"shear wall {FtIn.Of(p1 - p0)}", ink: SheetInk);
            WallInk.Dimension(dc, this, new Point(X(0), yb + 130), new Point(X(L), yb + 130), FtIn.Of(L));
            WallInk.Dimension(dc, this, new Point(X(0) - 48, Y(H)), new Point(X(0) - 48, Y(0)), FtIn.Of(H), vertical: true);
            var studs = f.Pieces.Where(p => p.Kind == FramingKind.Stud).OrderBy(p => p.X0).ToList();
            var inner = studs.Where(p => p.X0 > 0.01 && p.X1 < L - 0.01).ToList();
            if (inner.Count >= 2)
            {
                double ys = Y(WallFraming.Ply + 24);
                WallInk.Dimension(dc, this, new Point(X(inner[0].CenterX), ys), new Point(X(inner[1].CenterX), ys), $"{f.Spacing:0}\" o.c.");
            }
            if (shear && d.ArmIn > 0)
                WallInk.Dimension(dc, this, new Point(X(p0 + post + 1.45), yb + 152), new Point(X(p1 - post - 1.45), yb + 152), $"d = {FtIn.Of(d.ArmIn)}");
            WallInk.Label(dc, this, v.EastWest ? "W" : "S", new Point(X(0) - 16, Y(H) - 2), WallInk.Dim, 11, bold: true);
            WallInk.Label(dc, this, v.EastWest ? "E" : "N", new Point(X(L) + 6, Y(H) - 2), WallInk.Dim, 11, bold: true);

            // callouts at the right
            double cx = X(L) + 46;
            void Callout(string text, double y, double atX)
            {
                dc.DrawLine(WallInk.P(WallInk.Dim, 0.8), new Point(cx - 4, y), new Point(atX, y));
                WallInk.Label(dc, this, text, new Point(cx, y - 8), WallInk.Ink, 10.5);
            }
            string sz = f.StudSize;
            Callout($"Double top plate, 2-{sz} flat", Y(H - WallFraming.Ply), X(L - 2));
            if (studs.Count > 0) Callout($"{sz} studs @ {f.Spacing:0}\" o.c., {FtIn.Of(studs[0].Length)}", Y(H * 0.62), X(studs.Last().CenterX));
            if (v.IsShearWall) Callout($"Hold-down post, {f.PostPlies}-{sz}", Y(H * 0.44), X(p1 - post / 2));
            var blk = f.Pieces.FirstOrDefault(p => p.Kind == FramingKind.Blocking);
            if (blk != null && shear) Callout($"{sz} blocking at the sheet joint", Y(blk.Y0 + 0.75), X(f.Pieces.Last(p => p.Kind == FramingKind.Blocking).CenterX));
            Callout($"Sill plate, {sz} flat", Y(WallFraming.Ply / 2), X(L - 2));
            if (an != null && an.PositionsIn.Count > 0)
                WallInk.Label(dc, this, $"{an.PositionsIn.Count} anchor bolts {(an.BoltDiaIn == 0.5 ? "1/2\"" : "5/8\"")} @ {FtIn.Of(an.SpacingIn)} max, {FtIn.Of(an.PositionsIn[0])} from the ends", new Point(cx, Y(WallFraming.Ply / 2) + 8), AnchorInk, 10);
            if (face != null)
            {
                Callout(face.Sheathing ?? "Sheathing", Y(H * 0.80), X(Math.Max(p0, p1 - 6)));
                if (face.Nail != null && face.EdgeIn > 0)
                    WallInk.Label(dc, this, $"{face.Nail} @ {face.EdgeIn:0.#}\" edges / {(face.FieldIn > 0 ? face.FieldIn : 12):0.#}\" field", new Point(cx, Y(H * 0.80) + 8), WallInk.Secondary, 10);
                if (nails)
                {
                    dc.DrawEllipse(EdgeNail, null, new Point(cx + 4, yb + 72), 2, 2);
                    WallInk.Label(dc, this, "edge nail", new Point(cx + 10, yb + 64), WallInk.Secondary, 10);
                    dc.DrawEllipse(FieldNail, null, new Point(cx + 4, yb + 88), 1.6, 1.6);
                    WallInk.Label(dc, this, "field nail", new Point(cx + 10, yb + 80), WallInk.Secondary, 10);
                }
            }
            if (f.Pieces.Any(p => p.Kind == FramingKind.PostUnder))
            {
                dc.DrawRectangle(UnderFill, Edge, new Rect(cx, yb + 98, 10, 10));
                WallInk.Label(dc, this, "post under a hold-down above", new Point(cx + 14, yb + 96), WallInk.Secondary, 10);
            }
        }
    }

    /// <summary>
    /// The wall in 3D, the same in every app: the whole wall's framing (<see cref="FramingModel"/>), its hold-downs on the
    /// end posts with their anchors (always), the straps from above, and with the shear wall shown its sheathing over the
    /// shear part and the sill's anchor bolts; the foundation under a first-story wall. Inches: x along the wall from its
    /// low end, y up, z through it (face 1 at +z).
    /// </summary>
    public static class WallModel3D
    {
        public static Model3DGroup Build(WallView v, WallFraming f, bool showShear, bool face1, bool face2, bool cut)
        {
            if (f == null || v == null) return new Model3DGroup();
            var faces = showShear ? v.Shear?.Faces ?? new List<ShearFace>() : new List<ShearFace>();
            SheathingLayer Face(int i) => i < faces.Count ? new SheathingLayer { Thickness = faces[i].ThicknessIn > 0 ? faces[i].ThicknessIn : 0.5, Gypsum = faces[i].Gypsum } : null;
            var g = FramingModel.Wall(f, face1 ? Face(0) : null, face2 ? Face(1) : null, cut);
            double D = f.StudDepth, L = f.Length, H = f.Height;
            GeometryModel3D Box(double x0, double y0, double z0, double x1, double y1, double z1, Material m) => FramingModel.Box(x0, y0, z0, x1, y1, z1, m);
            var steel = FramingMaterials.Steel;
            double post = f.PostPlies * WallFraming.Ply;

            foreach (var h in v.Holdowns)
            {
                double at = Math.Max(0, Math.Min(L, h.AlongIn));
                if (h.Strap)
                {
                    double sx = h.Low ? at - post / 2 : at + post / 2;
                    g.Children.Add(Box(sx - 0.625, -14, -0.1, sx + 0.625, 30, -0.02, steel));
                }
                else
                {
                    double x0 = h.Low ? at : at - 2.9;
                    g.Children.Add(Box(x0, WallFraming.Ply, D / 2 - 1.5, x0 + 2.9, WallFraming.Ply + 12, D / 2 + 1.5, steel));
                    g.Children.Add(Box(x0 + 1.15, v.OnFoundation ? -10 : -6, D / 2 - 0.3, x0 + 1.75, WallFraming.Ply + 3, D / 2 + 0.3, steel));
                }
            }
            foreach (var a in v.Above)
                g.Children.Add(Box(a.AlongIn - 0.625, H - 2, D + 0.05, a.AlongIn + 0.625, H + 18, D + 0.15, FramingMaterials.Strap));
            var an = showShear ? v.Shear?.Anchors : null;
            if (an != null)
                foreach (double ax in an.PositionsIn.Select(x => f.PartFrom + x))
                {
                    double r2 = an.BoltDiaIn / 2;
                    g.Children.Add(Box(ax - r2, -7, D / 2 - r2, ax + r2, WallFraming.Ply + 1.2, D / 2 + r2, steel));
                    double w = an.PlateWasher ? 1.5 : 0.9;
                    g.Children.Add(Box(ax - w, WallFraming.Ply, D / 2 - w, ax + w, WallFraming.Ply + 0.23, D / 2 + w, steel));
                }
            if (v.OnFoundation) g.Children.Add(Box(-8, -14, -4, L + 8, 0, D + 4, FramingMaterials.Concrete));
            return g;
        }
    }
}
