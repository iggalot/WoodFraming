using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WoodFraming.Connections;

namespace WoodFraming.Wpf
{
    /// <summary>
    /// The project's connection defaults (<see cref="ConnectionDefaults"/>, its own "ConnectionDefaults" section, part of the
    /// connection details: owner 2026-11-08): the floors' and the roof's sheathing, nails and blocking for the diaphragms'
    /// boundary nailing, the framing width there, the upper sills' nail and the top plates' splice nail. It edits a copy;
    /// the app that opens it writes the result. The starting values are <see cref="ConnectionDefaults"/>'s own.
    /// </summary>
    public sealed class ConnectionDefaultsWindow : Window
    {
        /// <summary>The choices offered: panel thicknesses (in) with rows in SDPWS Tables 4.2A and 4.2C, and common nails (NDS Table L4).</summary>
        public static readonly double[] Thicknesses = { 5.0 / 16, 3.0 / 8, 7.0 / 16, 15.0 / 32, 19.0 / 32, 23.0 / 32 };
        public static readonly string[] DiaphragmNails = { "6d", "8d", "10d" };
        public static readonly string[] SillNails = { "10d", "12d", "16d", "20d" };
        public static readonly string[] SpliceNails = { "10d", "12d", "16d" };
        public static readonly string[] ToeNails = { "8d", "10d", "12d", "16d" };
        public static readonly double[] FramingWidths = { 2, 3 };
        private static readonly (string Key, string Name)[] Grades = { ("SheathingAndSingleFloor", "Sheathing and Single-Floor"), ("StructuralI", "Structural I") };

        private readonly ComboBox grade = new ComboBox(), floorT = new ComboBox(), floorNail = new ComboBox(), roofT = new ComboBox(), roofNail = new ComboBox(),
                                  width = new ComboBox(), sillNail = new ComboBox(), spliceNail = new ComboBox(), toeNail = new ComboBox();
        private readonly CheckBox floorBlocked = new CheckBox { Content = "Blocked (SDPWS Table 4.2A; unblocked: 4.2C)" },
                                  roofBlocked = new CheckBox { Content = "Blocked (SDPWS Table 4.2A; unblocked: 4.2C)" };
        private ConnectionDefaults shown;
        private readonly TextBlock warning = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(0x8A, 0x5A, 0x00)), FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 8, 0, 0), Visibility = Visibility.Collapsed,
        };
        private Func<ConnectionDefaults, string> check;

        /// <summary>
        /// The designing app's word on the choices as they are shown (null: nothing to say), e.g. a nail and panel thickness its
        /// SDPWS tables do not have together; shown under the choices, in amber, as they change. OK still saves them (they are
        /// the project's choices): what cannot be designed says NOT DESIGNED where it is.
        /// </summary>
        public Func<ConnectionDefaults, string> Check { get => check; set { check = value; Recheck(); } }

        private void Recheck()
        {
            string w = null;
            try { w = shown == null ? null : check?.Invoke(Values()); } catch (Exception) { w = null; }
            warning.Text = w == null ? "" : "▲ " + w;
            warning.Visibility = w == null ? Visibility.Collapsed : Visibility.Visible;
        }

        public ConnectionDefaultsWindow(ConnectionDefaults d)
        {
            Title = "Connection defaults";
            Width = 560;
            SizeToContent = SizeToContent.Height;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var grid = new Grid { Margin = new Thickness(14) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            int row = 0;
            void Add(string label, UIElement e, string tip = null)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var l = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 4, 8, 4) };
                Grid.SetRow(l, row);
                grid.Children.Add(l);
                if (e is FrameworkElement fe) { fe.Margin = new Thickness(0, 4, 0, 4); if (tip != null) fe.ToolTip = tip; }
                if (e != null) { Grid.SetRow(e, row); Grid.SetColumn(e, 1); grid.Children.Add(e); }
                if (e != null) System.Windows.Automation.AutomationProperties.SetName(e, label);
                row++;
            }
            void Heading(string text)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                var t = new TextBlock { Text = text, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, row == 0 ? 0 : 10, 0, 2) };
                Grid.SetRow(t, row);
                Grid.SetColumnSpan(t, 2);
                grid.Children.Add(t);
                row++;
            }

            Heading("Diaphragms (each line of shear walls' boundary nailing)");
            Add("Panel grade", grade);
            Add("Floor sheathing", floorT, "The floors' panel thickness: the diaphragm's table row (the thickest at or under it), and the gap an upper sill's nails cross.");
            Add("Floor nails", floorNail);
            Add("", floorBlocked);
            Add("Roof sheathing", roofT);
            Add("Roof nails", roofNail);
            Add("", roofBlocked);
            Add("Framing at the boundaries", width, "The nominal width of the framing nailed at the boundaries; 2-1/2\" spacing or closer needs 3\" (SDPWS 4.2.7.1.1).");
            Heading("Walls");
            Add("Upper sill nail", sillNail, "An upper story's shear wall sill, nailed through the floor sheathing into the rim or blocking (AWC TR12, the sheathing as the gap).");
            Add("Top plate splice nail", spliceNail, "The face nails lapping the double top plate's plies at a splice, where it is a collector.");
            Add("Rim or blocking toe-nail", toeNail, "The rim (a floor) or the roof's blocking toe-nailed down into the top plates of the wall under it (NDS 12.1.6.3, Ctn 0.83).");

            var note = new TextBlock
            {
                Text = "These are the project's choices the connection details start from: the shear wall app designs the spacing. Saved with the project.",
                TextWrapping = TextWrapping.Wrap, Foreground = WallInk.Muted, Margin = new Thickness(0, 10, 0, 0),
            };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(note, row++);
            Grid.SetColumnSpan(note, 2);
            grid.Children.Add(note);
            // what the app that designs with them says of the choices (a nail and panel its tables do not have), live
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(warning, row++);
            Grid.SetColumnSpan(warning, 2);
            grid.Children.Add(warning);
            foreach (var c in new[] { grade, floorT, floorNail, roofT, roofNail, width }) c.SelectionChanged += (s, e) => Recheck();
            foreach (var c in new[] { floorBlocked, roofBlocked }) { c.Checked += (s, e) => Recheck(); c.Unchecked += (s, e) => Recheck(); }

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            var start = new Button { Content = "_Starting values", Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(0, 0, 8, 0) };
            var ok = new Button { Content = "OK", IsDefault = true, Width = 80, Margin = new Thickness(0, 0, 8, 0) };
            var cancel = new Button { Content = "Cancel", IsCancel = true, Width = 80 };
            start.Click += (s, e) => StartingValues();
            ok.Click += (s, e) => { DialogResult = true; };
            buttons.Children.Add(start);
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(buttons, row++);
            Grid.SetColumnSpan(buttons, 2);
            grid.Children.Add(buttons);
            Content = grid;

            Show(d ?? new ConnectionDefaults());
        }

        private static string Inches(double t)
        {
            foreach (int den in new[] { 2, 4, 8, 16, 32 })
            {
                double num = t * den;
                if (Math.Abs(num - Math.Round(num)) < 1e-6) return Math.Round(num) == den ? "1\"" : $"{Math.Round(num):0}/{den}\"";
            }
            return t.ToString("0.###", CultureInfo.InvariantCulture) + "\"";
        }

        /// <summary>Fills a list with the choices and picks <paramref name="value"/>; a value not offered (a hand-edited file) is kept as a choice.</summary>
        private static void Fill<T>(ComboBox c, IEnumerable<T> choices, T value, Func<T, string> text, Func<T, T, bool> same)
        {
            c.Items.Clear();
            var list = choices.ToList();
            if (!list.Any(x => same(x, value))) list.Add(value);
            foreach (var x in list) c.Items.Add(new ComboBoxItem { Content = text(x), Tag = x });
            c.SelectedIndex = list.FindIndex(x => same(x, value));
        }

        private static T Picked<T>(ComboBox c, T otherwise) => c.SelectedItem is ComboBoxItem i && i.Tag is T v ? v : otherwise;

        private void Show(ConnectionDefaults d)
        {
            shown = d.Clone();
            bool Near(double a, double b) => Math.Abs(a - b) < 1e-6;
            bool Same(string a, string b) => string.Equals((a ?? "").Trim(), (b ?? "").Trim(), StringComparison.OrdinalIgnoreCase);
            string gradeName(string k) => Grades.FirstOrDefault(g => g.Key == k).Name ?? k;
            Fill(grade, Grades.Select(g => g.Key), d.DiaphragmGrade, gradeName, Same);
            Fill(floorT, Thicknesses, d.FloorSheathing, Inches, Near);
            Fill(floorNail, DiaphragmNails, d.FloorNail, n => $"{n} common", Same);
            floorBlocked.IsChecked = d.FloorBlocked;
            Fill(roofT, Thicknesses, d.RoofSheathing, Inches, Near);
            Fill(roofNail, DiaphragmNails, d.RoofNail, n => $"{n} common", Same);
            roofBlocked.IsChecked = d.RoofBlocked;
            Fill(width, FramingWidths, d.DiaphragmFramingWidth, w => $"{w:0}\" nominal", Near);
            Fill(sillNail, SillNails, d.SillNail, n => $"{n} common", Same);
            Fill(spliceNail, SpliceNails, d.SpliceNail, n => $"{n} common", Same);
            Fill(toeNail, ToeNails, d.ToeNail, n => $"{n} common", Same);
            Recheck();
        }

        /// <summary>The program's starting values (<see cref="ConnectionDefaults"/>), shown to keep or cancel.</summary>
        public void StartingValues() => Show(new ConnectionDefaults());

        /// <summary>The values as shown now.</summary>
        public ConnectionDefaults Values()
        {
            var d = shown.Clone();
            d.DiaphragmGrade = Picked(grade, d.DiaphragmGrade);
            d.FloorSheathing = Picked(floorT, d.FloorSheathing);
            d.FloorNail = Picked(floorNail, d.FloorNail);
            d.FloorBlocked = floorBlocked.IsChecked == true;
            d.RoofSheathing = Picked(roofT, d.RoofSheathing);
            d.RoofNail = Picked(roofNail, d.RoofNail);
            d.RoofBlocked = roofBlocked.IsChecked == true;
            d.DiaphragmFramingWidth = Picked(width, d.DiaphragmFramingWidth);
            d.SillNail = Picked(sillNail, d.SillNail);
            d.SpliceNail = Picked(spliceNail, d.SpliceNail);
            d.ToeNail = Picked(toeNail, d.ToeNail);
            return d;
        }

        /// <summary>Shows the window over <paramref name="owner"/>: the values chosen, or null when cancelled.</summary>
        public static ConnectionDefaults Edit(Window owner, ConnectionDefaults d, Func<ConnectionDefaults, string> check = null)
        {
            var w = new ConnectionDefaultsWindow(d) { Owner = owner, Check = check };
            return w.ShowDialog() == true ? w.Values() : null;
        }
    }
}
