using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BuildingModel.Geometry;
using BuildingModel.Model;
using BuildingModel.Sketch;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace WoodFraming.Wpf
{
    /// <summary>The framing's colours in 3D, shared by every app that draws wood framing.</summary>
    public static class FramingMaterials
    {
        public static readonly Material Stud = Mat("#E6CFA3"), Plate = Mat("#D2AE78"), Post = Mat("#C08D4E"), Under = Mat("#E39A50"),
                                        Osb = Mat("#C9A66B"), Gypsum = Mat("#ECEEF0"), Steel = Mat("#4B5563"), Concrete = Mat("#C9CDD2"),
                                        Strap = Mat("#C2410C"),
                                        // the shear part marked along the top plates (the plans' shear-wall blue; red when not designed)
                                        ShearPart = Mat("#1D4ED8"), NotDesigned = Mat("#B91C1C");

        /// <summary>A piece's material by its kind.</summary>
        public static Material Of(FramingKind k) =>
            k == FramingKind.Stud || k == FramingKind.Blocking ? Stud : k == FramingKind.EndPost ? Post : k == FramingKind.PostUnder ? Under : Plate;

        public static Material Mat(string hex)
        {
            var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
            b.Freeze();
            var m = new DiffuseMaterial(b);
            m.Freeze();
            return m;
        }
    }

    /// <summary>A face's sheathing as drawn: its thickness (in) and whether it is gypsum (else wood structural panel).</summary>
    public sealed class SheathingLayer
    {
        public double Thickness { get; set; } = 0.5;
        public bool Gypsum { get; set; }
    }

    /// <summary>
    /// A wall's framing (<see cref="WallFraming"/>) as a 3D model, in inches: x along the wall from its low end, y up from the
    /// bottom of the sill, z through it (face 1 at +z beyond the stud depth, face 2 at −z). Every piece, and each face's sheets
    /// where given (the right half cut away when asked, to show the framing).
    /// </summary>
    public static class FramingModel
    {
        public static Model3DGroup Wall(WallFraming f, SheathingLayer face1 = null, SheathingLayer face2 = null, bool cutaway = false)
        {
            var g = new Model3DGroup();
            if (f == null) return g;
            double D = f.StudDepth, L = f.Length;
            foreach (var p in f.Pieces)
                g.Children.Add(Box(p.X0 + 0.04, p.Y0 + 0.04, 0, p.X1 - 0.04, p.Y1 - 0.04, D, FramingMaterials.Of(p.Kind)));
            void Sheets(SheathingLayer s, double z0, double z1)
            {
                var m = s.Gypsum ? FramingMaterials.Gypsum : FramingMaterials.Osb;
                foreach (var sh in f.Sheets)
                {
                    if (cutaway && sh.X0 >= L / 2 - 0.01) continue;
                    g.Children.Add(Box(sh.X0 + 0.06, sh.Y0 + 0.06, z0, sh.X1 - 0.06, sh.Y1 - 0.06, z1, m));
                }
            }
            if (face1 != null) Sheets(face1, D, D + face1.Thickness);
            if (face2 != null) Sheets(face2, -face2.Thickness, 0);
            return g;
        }

        /// <summary>A box from (x0, y0, z0) to (x1, y1, z1), both sides of each face drawn.</summary>
        public static GeometryModel3D Box(double x0, double y0, double z0, double x1, double y1, double z1, Material m)
        {
            var c = new[] { new Point3D(x0, y0, z0), new Point3D(x1, y0, z0), new Point3D(x1, y1, z0), new Point3D(x0, y1, z0),
                            new Point3D(x0, y0, z1), new Point3D(x1, y0, z1), new Point3D(x1, y1, z1), new Point3D(x0, y1, z1) };
            var mesh = new MeshGeometry3D();
            int[][] faces = { new[] { 0, 3, 2, 1 }, new[] { 4, 5, 6, 7 }, new[] { 0, 1, 5, 4 }, new[] { 1, 2, 6, 5 }, new[] { 2, 3, 7, 6 }, new[] { 3, 0, 4, 7 } };
            foreach (var fc in faces)
            {
                int i = mesh.Positions.Count;
                foreach (var k in fc) mesh.Positions.Add(c[k]);
                mesh.TriangleIndices.Add(i); mesh.TriangleIndices.Add(i + 1); mesh.TriangleIndices.Add(i + 2);
                mesh.TriangleIndices.Add(i); mesh.TriangleIndices.Add(i + 2); mesh.TriangleIndices.Add(i + 3);
            }
            mesh.Freeze();
            return new GeometryModel3D(mesh, m) { BackMaterial = m };
        }

        /// <summary>
        /// Places a model drawn in the wall's own inches (x along, y up, z through, the stud depth <paramref name="depthIn"/> centred
        /// on the wall's line) on the building's plan: x along the line from <paramref name="startX"/>, <paramref name="startY"/> to
        /// the end (ft), y up from <paramref name="baseFt"/>; the building's 3D axes (X east, Y up, −Z north).
        /// </summary>
        public static Transform3D OnPlan(double startX, double startY, double endX, double endY, double baseFt, double depthIn)
        {
            double len = Math.Sqrt((endX - startX) * (endX - startX) + (endY - startY) * (endY - startY));
            double ux = len > 0 ? (endX - startX) / len : 1, uy = len > 0 ? (endY - startY) / len : 0;
            // inches to feet, the depth centred; then x along (ux, uy) in plan, z across to the right (uy, −ux) in plan (the side the
            // wall is seen from with its start on the left: the south of an east-west wall, the east of a south-north one; a turn,
            // not a mirror); plan y north is −Z
            var t = new Transform3DGroup();
            t.Children.Add(new TranslateTransform3D(0, 0, -depthIn / 2));
            t.Children.Add(new ScaleTransform3D(1 / 12.0, 1 / 12.0, 1 / 12.0));
            var m = new Matrix3D(
                ux, 0, -uy, 0,      // local x → plan (ux, uy) → 3D (ux, 0, −uy)
                0, 1, 0, 0,         // local y → up
                uy, 0, ux, 0,       // local z → plan (uy, −ux) → 3D (uy, 0, ux)
                startX, baseFt, -startY, 1);
            t.Children.Add(new MatrixTransform3D(m));
            return t;
        }

        /// <summary>
        /// The other way: a plan point (ft) in the wall's own inches as <see cref="OnPlan"/> takes them (x along from the start, z
        /// across from the face seen, the depth <paramref name="depthIn"/> centred on the line), at <paramref name="upIn"/> inches up.
        /// For the building model's north arrow in a wall's own view.
        /// </summary>
        public static Func<Point2, Point3D> FromPlan(double startX, double startY, double endX, double endY, double depthIn, double upIn = 0.1)
        {
            double len = Math.Sqrt((endX - startX) * (endX - startX) + (endY - startY) * (endY - startY));
            double ux = len > 0 ? (endX - startX) / len : 1, uy = len > 0 ? (endY - startY) / len : 0;
            return p =>
            {
                double dx = p.X - startX, dy = p.Y - startY;
                return new Point3D(12 * (dx * ux + dy * uy), upIn, depthIn / 2 + 12 * (dx * uy - dy * ux));
            };
        }
    }

    /// <summary>
    /// A view to turn a framing model in: drag to turn, wheel to zoom, double-click to fit. The same wall shown again keeps the
    /// user's turn; another one is fitted.
    /// </summary>
    public sealed class Framing3DView : Border
    {
        private readonly Viewport3D viewport = new Viewport3D();
        private readonly PerspectiveCamera camera = new PerspectiveCamera { FieldOfView = 40 };
        private readonly ModelVisual3D content = new ModelVisual3D();
        private Point3D target;
        private double yaw = 28, pitch = 16, distance = 400, radius = 100;
        private Point? dragFrom;
        private bool moved;
        private string shownKey;
        private Point? panFrom;
        private Grid stage; // the view under the bottom bar: the mouse turns, pans and zooms it
        private Point3D centre;

        private void EndPan()
        {
            panFrom = null;
            Cursor = null;
            if (dragFrom == null) stage.ReleaseMouseCapture();
        }

        public Framing3DView()
        {
            Background = new LinearGradientBrush(Color.FromRgb(0xF7, 0xF9, 0xFC), Color.FromRgb(0xE4, 0xE9, 0xF0), 90);
            viewport.Camera = camera;
            var lights = new Model3DGroup();
            lights.Children.Add(new AmbientLight(Color.FromRgb(0x78, 0x78, 0x78)));
            lights.Children.Add(new DirectionalLight(Color.FromRgb(0xA8, 0xA8, 0xA8), new Vector3D(-0.4, -0.8, -0.9)));
            lights.Children.Add(new DirectionalLight(Color.FromRgb(0x50, 0x50, 0x50), new Vector3D(0.6, -0.2, 0.8)));
            viewport.Children.Add(new ModelVisual3D { Content = lights });
            viewport.Children.Add(content);
            // the view over the bottom bar (VIEW, zoom to all, the mouse hint), laid out as on every plan; the mouse works on the view only
            stage = new Grid { Background = Brushes.Transparent, Children = { viewport, new Canvas { IsHitTestVisible = false, Children = { northLabel } } } };
            var bar = Ui.ViewBar(() => { moved = false; Fit(); }, Ui.View3DHint, "Fit the model in the view (or double-click). The wheel zooms at the pointer, the middle button pans.");
            DockPanel.SetDock(bar, Dock.Bottom);
            Child = new DockPanel { Children = { bar, stage } };
            stage.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2) { moved = false; Fit(); return; }
                dragFrom = e.GetPosition(this);
                stage.CaptureMouse();
            };
            stage.MouseMove += (s, e) =>
            {
                if (dragFrom == null) return;
                var p = e.GetPosition(this);
                yaw -= (p.X - dragFrom.Value.X) * 0.4;
                pitch = Math.Max(-60, Math.Min(85, pitch + (p.Y - dragFrom.Value.Y) * 0.3));
                dragFrom = p;
                moved = true;
                PlaceCamera();
            };
            stage.MouseLeftButtonUp += (s, e) => { dragFrom = null; stage.ReleaseMouseCapture(); };
            // the middle button pans (double-click: fit), the wheel zooms toward the pointer: as on the plan (BuildingModel.Sketch.OrbitMath)
            stage.MouseDown += (s, e) =>
            {
                if (e.ChangedButton != MouseButton.Middle) return;
                if (e.ClickCount == 2) { moved = false; Fit(); return; }
                panFrom = e.GetPosition(this);
                stage.CaptureMouse();
                Cursor = Cursors.SizeAll;
                e.Handled = true;
            };
            stage.MouseMove += (s, e) =>
            {
                if (panFrom == null) return;
                if (e.MiddleButton != MouseButtonState.Pressed) { EndPan(); return; }
                var p = e.GetPosition(this);
                target = OrbitMath.Pan(camera, target, distance, p.X - panFrom.Value.X, p.Y - panFrom.Value.Y, viewport.ActualWidth);
                panFrom = p;
                moved = true;
                PlaceCamera();
            };
            stage.MouseUp += (s, e) => { if (e.ChangedButton == MouseButton.Middle) EndPan(); };
            stage.MouseWheel += (s, e) =>
            {
                (target, distance) = OrbitMath.ZoomAt(camera, target, distance, e.Delta > 0 ? 0.88 : 1 / 0.88, e.GetPosition(viewport), new Size(viewport.ActualWidth, viewport.ActualHeight));
                moved = true;
                PlaceCamera();
            };
            SizeChanged += (s, e) => { if (!moved) Fit(); else PlaceCamera(); };
        }

        /// <summary>
        /// A model, fitted around <paramref name="fitTo"/> (null: its bounds), unless the same <paramref name="key"/> is shown
        /// again and the view was turned.
        /// </summary>
        /// <remarks>
        /// <paramref name="north"/>: the building model's north arrow (as in every view), drawn through <paramref name="fromPlan"/>
        /// (plan ft to the model's own axes, e.g. <see cref="FramingModel.FromPlan"/>); null: none.
        /// </remarks>
        public void ShowModel(Model3DGroup g, string key, Rect3D? fitTo = null, NorthArrow north = null, Func<Point2, Point3D> fromPlan = null)
        {
            if (key != shownKey) moved = false;
            shownKey = key;
            var b = fitTo ?? g?.Bounds ?? Rect3D.Empty;
            northAt = null;
            if (north != null && fromPlan != null && g != null)
            {
                var arrow = NorthArrowDrawing.Model(north, fromPlan);
                g = new Model3DGroup { Children = { g, arrow } };
                northAt = fromPlan(north.LabelAt);
                if (!b.IsEmpty) b.Union(arrow.Bounds);   // kept in view
            }
            content.Content = g;
            if (b.IsEmpty) { NorthArrowDrawing.Pin(northLabel, null); return; }
            centre = new Point3D(b.X + b.SizeX / 2, b.Y + b.SizeY / 2, b.Z + b.SizeZ / 2);
            if (!moved) target = centre;
            radius = 0.5 * Math.Sqrt(b.SizeX * b.SizeX + b.SizeY * b.SizeY + b.SizeZ * b.SizeZ);
            if (!moved) Fit(); else PlaceCamera();
        }

        private void Fit()
        {
            target = centre;
            double aspect = viewport.ActualWidth > 0 && viewport.ActualHeight > 0 ? viewport.ActualHeight / viewport.ActualWidth : 0.6;
            double half = camera.FieldOfView / 2 * Math.PI / 180;
            double halfV = Math.Atan(Math.Tan(half) * aspect);
            distance = 1.1 * radius / Math.Sin(Math.Min(half, halfV));
            PlaceCamera();
        }

        private void PlaceCamera()
        {
            double y = yaw * Math.PI / 180, p = pitch * Math.PI / 180;
            var dir = new Vector3D(Math.Cos(p) * Math.Sin(y), Math.Sin(p), Math.Cos(p) * Math.Cos(y));
            camera.Position = target + dir * distance;
            camera.LookDirection = -dir * distance;
            camera.UpDirection = new Vector3D(0, 1, 0);
            camera.NearPlaneDistance = 1;
            camera.FarPlaneDistance = distance * 10;
            NorthArrowDrawing.Pin(northLabel, northAt is Point3D n ? OrbitMath.Project(camera, n, new Size(viewport.ActualWidth, viewport.ActualHeight)) : null);
        }

        private readonly TextBlock northLabel = NorthArrowDrawing.NewLabel();
        private Point3D? northAt;
    }
}
