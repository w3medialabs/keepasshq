using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Xml;

namespace VaultOrganizer
{
    internal static class UiVisual
    {
        // Unmodified Bootstrap Icons 1.13.1 SVGs are embedded, never fetched at runtime.
        // Preserve each path's fill rule and the full 16x16 viewBox (including whitespace).
        private static readonly Dictionary<string, string> IconNames = new Dictionary<string, string> {
            { "folder", "folder" }, { "key", "key" }, { "save", "floppy" },
            { "undo", "arrow-counterclockwise" }, { "close", "x-lg" }, { "lock", "lock" },
            { "settings", "gear" }, { "eye", "eye" }, { "eye-off", "eye-slash" },
            { "viewer", "view-list" }, { "organizer", "layout-split" }, { "recovery", "file-earmark-plus" },
            { "add", "plus-lg" }, { "copy", "copy" }, { "paste", "clipboard" },
            { "cut", "scissors" }, { "chevron", "chevron-down" }, { "info", "info-circle" }, { "search", "search" }, { "help", "question-circle" }
        };
        private static readonly Dictionary<string, Geometry[]> IconGeometry = new Dictionary<string, Geometry[]>();
        internal static FrameworkElement Icon(string name, string color = null, double size = 16)
        {
            string resource; if (!IconNames.TryGetValue(name, out resource)) resource = IconNames["info"];
            Geometry[] geometries;
            if (!IconGeometry.TryGetValue(resource, out geometries))
            {
                var document = new XmlDocument { XmlResolver = null };
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Bootstrap." + resource + ".svg"))
                using (var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null })) document.Load(reader);
                var paths = new List<Geometry>();
                foreach (XmlNode node in document.DocumentElement.ChildNodes)
                {
                    var element = node as XmlElement; if (element == null || element.LocalName != "path") continue;
                    var geometry = Geometry.Parse((element.GetAttribute("fill-rule") == "evenodd" ? "F0 " : "F1 ") + element.GetAttribute("d"));
                    geometry.Freeze(); paths.Add(geometry);
                }
                geometries = paths.ToArray(); IconGeometry.Add(resource, geometries);
            }
            var canvas = new Canvas { Width = 16, Height = 16, IsHitTestVisible = false };
            foreach (var geometry in geometries)
            {
                var path = new Path { Data = geometry };
                if (color == null) path.SetBinding(Shape.FillProperty, new Binding("Foreground") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Control), 1) });
                else path.Fill = (Brush)new BrushConverter().ConvertFromString(color);
                canvas.Children.Add(path);
            }
            return new Viewbox { Width = size, Height = size, Child = canvas };
        }
        internal static void Help(FrameworkElement element, string text)
        {
            element.ToolTip = text;
            ToolTipService.SetInitialShowDelay(element, 450); ToolTipService.SetShowDuration(element, 15000); ToolTipService.SetShowOnDisabled(element, true);
            if (element is Control) System.Windows.Automation.AutomationProperties.SetHelpText(element, text);
        }
    }

    // Keep plaintext only while explicitly revealed, and clear both representations
    // together on dialog close, lock, decryption or disabling the password factor.
    internal sealed class SecretInput : Border
    {
        internal readonly PasswordBox Masked;
        internal readonly TextBox Revealed;
        internal readonly Button Eye;
        internal event Action Submitted;
        private bool syncing;
        internal bool IsRevealed { get { return Revealed.Visibility == Visibility.Visible; } }
        internal SecretInput(PasswordBox masked, string label)
        {
            Masked = masked; Background = Brushes.White; BorderBrush = new SolidColorBrush(Color.FromRgb(221,228,237)); BorderThickness = new Thickness(1); CornerRadius = new CornerRadius(6);
            var row = new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) }); Child = row;
            Masked.BorderThickness = new Thickness(0); Masked.Background = Brushes.Transparent; Masked.MinHeight = 38;
            Revealed = new TextBox { Visibility = Visibility.Collapsed, BorderThickness = new Thickness(0), Background = Brushes.Transparent, MinHeight = 38, IsUndoEnabled = false };
            UiVisual.Help(Masked, label + ". Hidden until you choose Show password."); UiVisual.Help(Revealed, label + ". Visible until you choose Hide password or close this dialog.");
            System.Windows.Automation.AutomationProperties.SetName(Masked, label); System.Windows.Automation.AutomationProperties.SetName(Revealed, label + " (visible)");
            Eye = new Button { Style = (Style)Application.Current.FindResource("Quiet"), Content = UiVisual.Icon("eye"), Focusable = true, Padding = new Thickness(8), MinHeight = 32 };
            Grid.SetColumn(Eye, 1); row.Children.Add(Masked); row.Children.Add(Revealed); row.Children.Add(Eye);
            Eye.Click += (sender, args) => { SetRevealed(!IsRevealed); if (IsRevealed) { Revealed.Focus(); Revealed.CaretIndex=Revealed.Text.Length; } else Masked.Focus(); };
            Masked.KeyDown += SubmitOnEnter; Revealed.KeyDown += SubmitOnEnter;
            Masked.PasswordChanged += (sender, args) => { if (syncing) return; if (Masked.Password.Length == 0) { SetRevealed(false); return; } syncing = true; try { Revealed.Text = IsRevealed ? Masked.Password : ""; } finally { syncing = false; } };
            Revealed.TextChanged += (sender, args) => { if (syncing || !IsRevealed) return; syncing = true; try { Masked.Password = Revealed.Text; } finally { syncing = false; } };
            Masked.IsEnabledChanged += (sender, args) => { if (!Masked.IsEnabled) SetRevealed(false); Revealed.IsEnabled = Eye.IsEnabled = Masked.IsEnabled; };
            UpdateEye();
        }
        private void SubmitOnEnter(object sender, KeyEventArgs args)
        { if(args.Key==Key.Enter&&Submitted!=null) { args.Handled=true;Submitted(); } }
        internal void SetRevealed(bool show)
        {
            show &= Masked.IsEnabled;
            syncing = true;
            try { Revealed.Text = show ? Masked.Password : ""; Revealed.Visibility = show ? Visibility.Visible : Visibility.Collapsed; Masked.Visibility = show ? Visibility.Collapsed : Visibility.Visible; }
            finally { syncing = false; }
            UpdateEye();
        }
        private void UpdateEye()
        {
            string action = IsRevealed ? "Hide password" : "Show password";
            Eye.Content = UiVisual.Icon(IsRevealed ? "eye-off" : "eye"); UiVisual.Help(Eye, action); System.Windows.Automation.AutomationProperties.SetName(Eye, action);
        }
        internal void Clear()
        { syncing = true; try { Masked.Clear(); Revealed.Clear(); } finally { syncing = false; } SetRevealed(false); }
    }

    internal sealed partial class OrganizerWindow
    {
        private SecretInput SecretField(PasswordBox masked, string label)
        {
            var field = new SecretInput(masked, label);
            return field;
        }
        private static string ButtonHelp(string label)
        {
            switch (label)
            {
                case "Cancel": return "Cancel this dialog without applying changes.";
                case "Copy": return "Copy the generated password and close this dialog.";
                case "Use": return "Use the generated password in the calling field without copying it.";
                case "Done": return "Apply these settings and return to your database.";
                case "Unlock database": return "Decrypt the selected database with these key factors.";
                case "Create & open": return "Create an empty encrypted database at the selected new filename.";
                case "Open": return "Open the selected database with these key factors.";
                case "Recovery copy": return "Save your working edits to a new encrypted file, leaving the original unchanged, then close loaded databases.";
                case "Save database": case "Save now": return "Publish pending edits to all loaded database files.";
                case "Keep editing": return "Continue editing without publishing your changes.";
                case "Create new database…": return "Create a new empty database without overwriting existing files.";
                default: return label;
            }
        }
        private void EnsureTooltips(DependencyObject root)
        {
            var element = root as FrameworkElement;
            if (element != null && element.ToolTip == null)
            {
                var menu = element as MenuItem; var check = element as CheckBox; var expand = element as Expander; var input = element as TextBox;
                if (menu != null) UiVisual.Help(menu, Convert.ToString(menu.Header));
                else if (check != null) UiVisual.Help(check, Convert.ToString(check.Content));
                else if (expand != null) UiVisual.Help(expand, "Show or hide " + Convert.ToString(expand.Header).ToLowerInvariant() + " options.");
                else if (input != null) UiVisual.Help(input, input.IsReadOnly ? "Read-only information. Select text to review it." : "Edit this value.");
                else if (element is ScrollViewer) UiVisual.Help(element, "Scroll to browse more content.");
            }
            foreach (object child in LogicalTreeHelper.GetChildren(root)) { var dependency = child as DependencyObject; if (dependency != null) EnsureTooltips(dependency); }
        }
        private Grid FilePickerRow(TextBox location, string tip, Action choose)
        {
            var row = new Grid { Margin = new Thickness(0, 6, 0, 12) }; row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            location.IsReadOnly = true; location.MinHeight = 40; location.VerticalContentAlignment = VerticalAlignment.Center; UiVisual.Help(location, tip); row.Children.Add(location);
            var browse = Button("Browse…", choose, "Primary"); browse.Margin = new Thickness(8, 0, 0, 0); UiVisual.Help(browse, tip); Grid.SetColumn(browse, 1); row.Children.Add(browse); return row;
        }
    }
}
