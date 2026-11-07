using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using BuildingModel.Model;

namespace WoodFraming.Wpf
{
    /// <summary>
    /// A wall's views, the same in every app: its framing in elevation and in 3D (<see cref="WallElevation"/>,
    /// <see cref="WallModel3D"/>), with <b>Shear wall</b> (on by default) showing the shear wall app's design over the
    /// framing when it is one. The whole wall's framing and its hold-downs are always drawn, with those coming down from
    /// above. An app adds tabs of its own (<see cref="AddTab"/>: the shear wall app its pieces and calculation).
    /// </summary>
    public sealed class WallPanel : DockPanel
    {
        private readonly TabControl tabs = new TabControl();
        private readonly Grid elevationHost = new Grid();
        private readonly Framing3DView view3D = new Framing3DView();
        private readonly StackPanel bar, elevBar, bar3;
        private static CheckBox Box(string text, bool on = true) =>
            new CheckBox { Content = text, IsChecked = on, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
        private readonly CheckBox shear = Box("Shear wall");
        private readonly ComboBox face = new ComboBox { Width = 150, Margin = new Thickness(0, 0, 12, 0) };
        private readonly CheckBox nails = Box("Nails"), face1In3D = Box("Face 1 sheathing"), face2In3D = Box("Face 2 sheathing"), cutaway = Box("Cut away the right half");
        private readonly TextBlock shearNote = new TextBlock { Foreground = WallInk.Muted, VerticalAlignment = VerticalAlignment.Center };
        private WallView view;
        private WallFraming framing;

        /// <summary>The wall shown (null: none, or another member shown by the app).</summary>
        public WallView View => view;
        public WallFraming Framing => framing;
        public TabControl Tabs => tabs;
        public Framing3DView View3D => view3D;

        /// <summary>The shear wall's design is drawn over the framing (on by default).</summary>
        public bool ShowShear { get => shear.IsChecked == true; set => shear.IsChecked = value; }

        /// <summary>The elevation drawn now (for the tests).</summary>
        public WallElevation Elevation => elevationHost.Children.OfType<WallElevation>().FirstOrDefault();

        public WallPanel()
        {
            System.Windows.Automation.AutomationProperties.SetName(shear, "Shear wall");
            shear.ToolTip = "Show the shear wall's design over the framing: its sheathing and nailing over the shear part, the sill bolts, the lever arm and the check. The framing and the hold-downs are always shown.";
            shear.Checked += (s, e) => Redraw();
            shear.Unchecked += (s, e) => Redraw();
            bar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
            bar.Children.Add(shear);
            bar.Children.Add(shearNote);
            SetDock(bar, Dock.Top);
            Children.Add(bar);

            foreach (var s in new[] { "Face 1 sheathing", "Face 2 sheathing", "Framing only" }) face.Items.Add(s);
            face.SelectedIndex = 0;
            face.SelectionChanged += (s, e) => ShowElevation();
            nails.Checked += (s, e) => ShowElevation();
            nails.Unchecked += (s, e) => ShowElevation();
            elevBar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 6) };
            elevBar.Children.Add(new TextBlock { Text = "Show ", VerticalAlignment = VerticalAlignment.Center });
            elevBar.Children.Add(face);
            elevBar.Children.Add(nails);
            elevBar.Children.Add(new TextBlock { Text = "Elevation to scale, the low end (west or south) on the left, seen from the face 1 side.", Foreground = WallInk.Muted, VerticalAlignment = VerticalAlignment.Center });
            var elevPage = new DockPanel();
            SetDock(elevBar, Dock.Top);
            elevPage.Children.Add(elevBar);
            elevPage.Children.Add(elevationHost);

            bar3 = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 6) };
            foreach (var cb in new[] { face1In3D, face2In3D, cutaway })
            {
                cb.Checked += (s, e) => Show3D();
                cb.Unchecked += (s, e) => Show3D();
                bar3.Children.Add(cb);
            }
            var page3 = new DockPanel();
            SetDock(bar3, Dock.Top);
            page3.Children.Add(bar3);
            page3.Children.Add(new Border { Child = view3D, BorderBrush = WallInk.Joint, BorderThickness = new Thickness(1) });

            tabs.Items.Add(new TabItem { Header = "Elevation", Content = elevPage });
            tabs.Items.Add(new TabItem { Header = "3D", Content = page3 });
            Children.Add(tabs);
        }

        /// <summary>Adds an app's own tab after the views.</summary>
        public TabItem AddTab(string header, object content)
        {
            var t = new TabItem { Header = header, Content = content };
            tabs.Items.Add(t);
            return t;
        }

        /// <summary>Shows <paramref name="v"/> (null: nothing).</summary>
        public void Show(WallView v)
        {
            view = v;
            framing = v == null || !(v.LengthFt > 0) || !(v.HeightFt > 0) ? null : v.Framing();
            bar.Visibility = elevBar.Visibility = bar3.Visibility = Visibility.Visible;
            Redraw();
        }

        /// <summary>
        /// Another member in the views' place (a beam, a column: the app's own drawing and model), the wall's toolbars hidden.
        /// </summary>
        public void ShowOther(FrameworkElement elevation, Model3DGroup model, string key, NorthArrow north = null, Func<BuildingModel.Geometry.Point2, Point3D> fromPlan = null)
        {
            view = null;
            framing = null;
            bar.Visibility = elevBar.Visibility = bar3.Visibility = Visibility.Collapsed;
            elevationHost.Children.Clear();
            if (elevation != null) elevationHost.Children.Add(elevation);
            view3D.ShowModel(model ?? new Model3DGroup(), key, null, north, fromPlan);
        }

        private void Redraw()
        {
            bool isShear = view?.IsShearWall == true;
            shear.IsEnabled = isShear;
            shearNote.Text = view == null ? "" : isShear ? "" : "Not a shear wall (it is declared in the Shear Wall Calculator): its framing and what lands on it.";
            int faces = view?.Shear?.Faces?.Count ?? 0;
            bool on = isShear && ShowShear;
            face.IsEnabled = nails.IsEnabled = face1In3D.IsEnabled = on && faces > 0;
            face2In3D.IsEnabled = on && faces > 1;
            ShowElevation();
            Show3D();
        }

        private void ShowElevation()
        {
            if (view == null && bar.Visibility != Visibility.Visible) return; // another member shown
            elevationHost.Children.Clear();
            if (view == null || framing == null) return;
            int pick = face.SelectedIndex == 2 ? -1 : face.SelectedIndex;
            elevationHost.Children.Add(new WallElevation(view, framing, ShowShear, pick, nails.IsChecked == true));
        }

        private void Show3D()
        {
            if (view == null && bar.Visibility != Visibility.Visible) return;
            if (view == null || framing == null) { view3D.ShowModel(new Model3DGroup(), null); return; }
            int faces = view.Shear?.Faces?.Count ?? 0;
            bool on = ShowShear && view.IsShearWall;
            var model = WallModel3D.Build(view, framing, on, on && face1In3D.IsChecked == true && faces > 0, on && face2In3D.IsChecked == true && faces > 1, cutaway.IsChecked == true);
            // the same wall (its size) keeps the turn; fitted on the wall itself; the building model's north arrow off its corner
            var lo = view.PlanLow;
            var hi = view.PlanHigh;
            double x0 = Math.Min(lo.X, hi.X), x1 = Math.Max(lo.X, hi.X), y0 = Math.Min(lo.Y, hi.Y), y1 = Math.Max(lo.Y, hi.Y);
            var north = NorthArrow.For(new BuildingModel.Geometry.Rect2(x0, y0, x1, y1), framing.Height / 12);
            var fromPlan = view.EastWest ? FramingModel.FromPlan(x0, y0, x1, y0, framing.StudDepth) : FramingModel.FromPlan(x0, y0, x0, y1, framing.StudDepth);
            view3D.ShowModel(model, $"wall {framing.Length:0.##} x {framing.Height:0.##}", new Rect3D(0, 0, 0, framing.Length, framing.Height, framing.StudDepth), north, fromPlan);
        }
    }

    /// <summary>
    /// A wall's window, the same in every app: its name and story, and its views (<see cref="WallPanel"/>). It follows the
    /// pick when the app shows another wall in it. Esc closes it.
    /// </summary>
    public sealed class WallWindow : Window
    {
        private readonly TextBlock title = new TextBlock { FontSize = 18, FontWeight = FontWeights.SemiBold };
        private readonly TextBlock subtitle = new TextBlock { Foreground = WallInk.Secondary, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 6) };
        public WallPanel Panel { get; } = new WallPanel();

        public WallWindow()
        {
            Title = "Wall";
            Width = 1180;
            Height = 860;
            BuildingModel.Sketch.WindowFit.Fit(this);
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 12;
            Background = Brushes.White;
            ShowInTaskbar = false;
            KeyDown += (s, e) =>
            {
                if (e.Key != Key.Escape || e.Handled) return;
                e.Handled = true;
                var owner = Owner;
                Close();
                owner?.Activate();
            };
            var root = new DockPanel { Margin = new Thickness(14, 10, 14, 12) };
            var head = new StackPanel { Children = { title, subtitle } };
            DockPanel.SetDock(head, Dock.Top);
            root.Children.Add(head);
            root.Children.Add(Panel);
            Content = root;
        }

        /// <summary>The heading for a wall view: "W4 · Level 2", and its size and studs in a line.</summary>
        public static (string title, string subtitle) Heading(WallView v, WallFraming f)
        {
            if (v == null) return ("Pick a wall on the plan", "");
            string size = f == null ? "" : $"{FtIn.Of(f.Length)} long × {FtIn.Of(f.Height)} high";
            string sub = $"{(v.EastWest ? "East-west" : "North-south")} wall, {size} as built ({v.StoryText}). Studs {v.StudSize} at {v.StudSpacing:0}\" o.c. " +
                         (v.IsShearWall ? $"Shear wall {FtIn.Of(v.ShearToIn - v.ShearFromIn)}: {v.Shear.Check ?? "designed"}." : "Not a shear wall.");
            return ($"{v.Name} · {v.LevelName}", sub);
        }

        public void Show(WallView v)
        {
            Panel.Show(v);
            var (t, s) = Heading(v, Panel.Framing);
            Title = v == null ? "Wall" : $"Wall {v.Name}: framing and 3D";
            title.Text = t;
            subtitle.Text = s;
        }
    }
}
