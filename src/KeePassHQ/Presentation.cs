using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace VaultOrganizer
{
    internal static class UiVisual
    {
        private static readonly Dictionary<string, string> Shapes = new Dictionary<string, string> {
            { "folder", "M1,5 L1,13 L15,13 L15,5 Z M1,5 L1,3 L6,3 L8,5" },
            { "key", "M9,3 A4,4 0 1 1 9,11 A4,4 0 1 1 9,3 M6,10 L1,15 M2,14 L2,11 M4,12 L4,9" },
            { "save", "M2,1 L12,1 L15,4 L15,15 L1,15 L1,1 Z M4,1 L4,6 L11,6 L11,1 M4,15 L4,10 L12,10 L12,15" },
            { "undo", "M6,3 L2,7 L6,11 M2,7 L10,7 Q15,7 15,12" },
            { "close", "M4,4 L12,12 M12,4 L4,12" },
            { "lock", "M4,7 L4,5 A4,4 0 0 1 12,5 L12,7 M3,7 L13,7 L13,15 L3,15 Z M8,10 L8,12" },
            { "settings", "M6,1 L10,1 L10.5,3 L12,4 L14,3.5 L16,7 L14.5,8 L14,10 L15,11.5 L12,14.5 L10.5,13.5 L9,14 L8,16 L4.5,14.5 L4.5,12.5 L3,11.5 L1,12 L0,8.5 L1.5,7 L2,5.5 L1.5,3.5 L5,2 L6,1 Z M8,5 A3,3 0 1 1 8,11 A3,3 0 1 1 8,5" },
            { "eye", "M1,8 Q8,-1 15,8 Q8,17 1,8 Z M8,5.5 A2.5,2.5 0 1 1 8,10.5 A2.5,2.5 0 1 1 8,5.5" },
            { "eye-off", "M1,8 Q8,-1 15,8 Q8,17 1,8 M2,2 L14,14" },
            { "viewer", "M2,2 L14,2 L14,14 L2,14 Z M5,5 L11,5 M5,8 L11,8 M5,11 L9,11" },
            { "organizer", "M1,2 L7,2 L7,14 L1,14 Z M10,2 L16,2 L16,14 L10,14 Z" },
            { "recovery", "M3,1 L10,1 L14,5 L14,15 L3,15 Z M10,1 L10,5 L14,5 M6,10 L11,10 M8.5,7.5 L8.5,12.5" },
            { "add", "M8,2 L8,14 M2,8 L14,8" },
            { "copy", "M5,5 L14,5 L14,15 L5,15 Z M11,2 L2,2 L2,12" },
            { "paste", "M5,3 L2,3 L2,15 L14,15 L14,3 L11,3 M5,1 L11,1 L11,5 L5,5 Z" },
            { "cut", "M6,11 L13,2 M6,5 L13,14 M3,2 A2.5,2.5 0 1 1 3,7 A2.5,2.5 0 1 1 3,2 M3,10 A2.5,2.5 0 1 1 3,15 A2.5,2.5 0 1 1 3,10" },
            { "chevron", "M4,6 L8,10 L12,6" },
            { "info", "M8,1 A7,7 0 1 1 8,15 A7,7 0 1 1 8,1 M8,7 L8,11 M8,4 L8,4.2" }
        };
        internal static FrameworkElement Icon(string name, string color = null)
        {
            string geometry; if (!Shapes.TryGetValue(name, out geometry)) geometry = Shapes["info"];
            var path = new Path { Data = Geometry.Parse(geometry), Width = 16, Height = 16, Stretch = Stretch.Uniform, StrokeThickness = 1.5, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round };
            if (color == null) path.SetBinding(Shape.StrokeProperty, new Binding("Foreground") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Control), 1) });
            else path.Stroke = (Brush)new BrushConverter().ConvertFromString(color);
            return path;
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
            DataObject.AddCopyingHandler(field.Revealed, (sender, args) => { if (args.DataObject.GetDataPresent(DataFormats.UnicodeText)) OwnClipboard(args.DataObject.GetData(DataFormats.UnicodeText) as string); });
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
