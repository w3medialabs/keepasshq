using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using KeePassLib;
using KeePassLib.Keys;
using KeePassLib.Security;

namespace VaultOrganizer
{
    internal sealed class BrowserPane
    {
        public PwGroup Root;
        public VaultSession Session;
        public Grid Layout;
        public TextBlock Heading;
        public List<PwGroup> Path = new List<PwGroup>();
        public StackPanel Columns;
        public ScrollViewer Scroll;
        public TextBlock Count;
        public TextBlock FilePath;
        public string Expanded;
        public readonly List<TextBox> Editors = new List<TextBox>();
        public readonly Dictionary<string, Button> EntryHeaders = new Dictionary<string, Button>();
    }
    internal sealed class UndoItem { public string Label; public Action Restore; }

    internal sealed partial class OrganizerWindow : Window
    {
        private VaultSession session;
        private readonly List<BrowserPane> panes = new List<BrowserPane>();
        private readonly List<UndoItem> undo = new List<UndoItem>();
        private readonly Grid shell = new Grid();
        private readonly Grid workspace = new Grid();
        private readonly Grid unlock = new Grid();
        private readonly StackPanel unlockContent = new StackPanel();
        private readonly Grid veil = new Grid();
        private readonly Border toast = new Border();
        private readonly TextBlock toastText = new TextBlock();
        private readonly TextBlock saveStatus = new TextBlock();
        private readonly TextBlock fileLabel = new TextBlock();
        private readonly TextBlock unlockHint = new TextBlock();
        private readonly TextBlock unlockTitle = new TextBlock();
        private readonly TextBox pathBox = new TextBox();
        private readonly TextBox keyBox = new TextBox();
        private readonly PasswordBox password = new PasswordBox();
        private readonly CheckBox usePassword = new CheckBox { Content = "Use master password", IsChecked = true };
        private readonly CheckBox windowsKey = new CheckBox { Content = "Use Windows account key" };
        private readonly Button unlockButton;
        private readonly Button undoButton;
        private readonly Button recoveryButton;
        private readonly DispatcherTimer heartbeat = new DispatcherTimer();
        private readonly DispatcherTimer toastTimer = new DispatcherTimer();
        private readonly DispatcherTimer hoverTimer = new DispatcherTimer();
        private long lastActivityTicks = Stopwatch.GetTimestamp();
        private bool locked = true, sealing, opening, allowClose, saveBlocked;
        private int saveSeconds = 30, lockMinutes = 5;
        private Task<bool> saveTask;
        private string selectedPath = "";
        private PwGroup hoverGroup;
        private BrowserPane hoverPane;
        private int hoverDepth;
        private object dragging;
        private string pendingField;
        private readonly string settingsPath;
        private bool rendering = false;
        private TaskCompletionSource<MergeChoice> mergeChoice;

        private static SolidColorBrush Brush(string hex) { return (SolidColorBrush)new BrushConverter().ConvertFromString(hex); }
        private static TextBlock Text(string value, double size, string color)
        { return new TextBlock { Text = value, FontFamily = new FontFamily("Segoe UI"), FontSize = size, Foreground = Brush(color), VerticalAlignment = VerticalAlignment.Center }; }
        private Button Button(string label, Action action, string style)
        {
            var b = new Button { Content = label, Style = (Style)Application.Current.FindResource(style != null ? (object)style : typeof(Button)), MinHeight = 32 };
            UiVisual.Help(b, ButtonHelp(label));
            b.Click += (s, e) => { Activity(); action(); }; return b;
        }
        private Button IconButton(string icon, string tip, Action action, double size = 16)
        {
            var b = Button("", action, "Glyph"); b.Content = UiVisual.Icon(icon, null, size); UiVisual.Help(b, tip);
            System.Windows.Automation.AutomationProperties.SetName(b, tip); return b;
        }
        private Button FieldAction(string icon, string tip, Action action)
        {
            var button = IconButton(icon, tip, action, 12); button.Focusable = false;
            button.Width = button.Height = button.MinWidth = button.MinHeight = 22;
            button.Padding = new Thickness(4); button.Margin = new Thickness(0);
            return button;
        }
        private static Border Rule() { return new Border { Background = Brush("#E1E7EE"), Width = 1, Margin = new Thickness(12, 0, 12, 0) }; }
        private void Activity() { lastActivityTicks = Stopwatch.GetTimestamp(); }
        private static double Elapsed(long ticks) { return ticks == 0 ? Double.PositiveInfinity : Math.Max(0, (Stopwatch.GetTimestamp() - ticks) / (double)Stopwatch.Frequency); }

        public OrganizerWindow()
        {
            DataObject.AddCopyingHandler(this, TrackTextCopy);
            Title = "KeePassHQ 1.2"; Width = Math.Min(1280, SystemParameters.WorkArea.Width * 0.9); Height = Math.Min(820, SystemParameters.WorkArea.Height * 0.9); MinWidth = 980; MinHeight = 640;
            FontFamily = new FontFamily("Segoe UI"); FontSize = 13; Foreground = Brush("#202B3C");
            WindowState = WindowState.Normal; WindowStartupLocation = WindowStartupLocation.CenterScreen;
            settingsPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "KeePassHQ.settings");
            ReadSettings();
            shell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(76) });
            shell.RowDefinitions.Add(new RowDefinition());
            shell.RowDefinitions.Add(new RowDefinition { Height = new GridLength(38) });
            Content = shell; shell.Background = Brushes.White;
            var header = new Grid { Background = Brushes.White };
            header.ColumnDefinitions.Add(new ColumnDefinition());
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            header.ColumnDefinitions.Add(new ColumnDefinition());
            var left = new Grid { Margin = new Thickness(20, 0, 16, 0) }; left.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); left.ColumnDefinitions.Add(new ColumnDefinition()); header.Children.Add(left);
            var branding = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 20, 0), VerticalAlignment = VerticalAlignment.Center };
            branding.Children.Add(BrandImage(30)); branding.Children.Add(Text("KeePassHQ", 17, "#202B3C")); left.Children.Add(branding);
            var fileArea = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            fileLabel.FontSize = 13; fileLabel.TextTrimming = TextTrimming.CharacterEllipsis; fileLabel.Foreground = Brush("#46536A");
            saveStatus.FontSize = 10; saveStatus.Margin = new Thickness(0, 4, 0, 0); saveStatus.Foreground = Brush("#758196"); saveStatus.TextTrimming = TextTrimming.CharacterEllipsis;
            fileArea.Children.Add(fileLabel); fileArea.Children.Add(saveStatus); Grid.SetColumn(fileArea, 1); left.Children.Add(fileArea);
            var modes = new Border { Background = Brush("#F2F6FC"), BorderBrush = Brush("#DDE5EF"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(9), Padding = new Thickness(3), VerticalAlignment = VerticalAlignment.Center };
            var modeButtons = new StackPanel { Orientation = Orientation.Horizontal }; modes.Child = modeButtons; Grid.SetColumn(modes, 1); header.Children.Add(modes);
            viewerButton = IconButton("viewer", "Viewer · one card browser", () => SwitchMode(false)); organizerButton = IconButton("organizer", "Organizer · two independent card browsers", () => SwitchMode(true));
            viewerButton.Style = organizerButton.Style = (Style)Application.Current.FindResource("Segment"); modeButtons.Children.Add(viewerButton); modeButtons.Children.Add(organizerButton);
            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 18, 0) };
            undoButton = IconButton("undo", "Undo last change · Ctrl+Z", Undo); actions.Children.Add(undoButton);
            var saveButton = IconButton("save", "Save · publish pending edits in all loaded databases · Ctrl+S", async () => { await SaveNow(true); }); saveButton.Style = (Style)Application.Current.FindResource("Primary"); saveButton.Padding = new Thickness(10); actions.Children.Add(saveButton);
            recoveryButton = IconButton("recovery", "Save recovery copy · write your working edits to a new encrypted file and close loaded databases", async () => { await Recovery(); }); recoveryButton.Visibility = Visibility.Collapsed; actions.Children.Add(recoveryButton);
            actions.Children.Add(Rule());
            actions.Children.Add(IconButton("close", "Close databases · choose Save or Keep draft, then return to the open screen", async () => { await CloseDatabases(); }));
            actions.Children.Add(IconButton("lock", "Lock · keep unpublished edits in an encrypted draft and release databases · Ctrl+L", async () => { await Seal(false); }));
            actions.Children.Add(Rule()); actions.Children.Add(IconButton("settings", "Settings · checkpoint interval and inactivity lock", ShowSettings));
            Grid.SetColumn(actions, 2); header.Children.Add(actions); shell.Children.Add(header);
            workspace.ColumnDefinitions.Add(new ColumnDefinition()); workspace.ColumnDefinitions.Add(new ColumnDefinition());
            Grid.SetRow(workspace, 1); shell.Children.Add(workspace);
            var footer = new Grid { Background = Brushes.White };
            var tip = Text("Drag headers to move cards · Drop between cards to reorder · Hover over a group to nest · Ctrl+S publishes changes", 11, "#8290A3");
            tip.Margin = new Thickness(26, 0, 16, 0); footer.Children.Add(tip); Grid.SetRow(footer, 2); shell.Children.Add(footer);
            BuildUnlock(); Grid.SetRowSpan(unlock, 3); shell.Children.Add(unlock);
            veil.Background = new SolidColorBrush(Color.FromArgb(100, 25, 39, 58)); veil.Visibility = Visibility.Collapsed; Grid.SetRowSpan(veil, 3); shell.Children.Add(veil);
            toast.Background = Brush("#396BD7"); toast.BorderBrush = Brush("#2858B8"); toast.BorderThickness = new Thickness(1); toast.CornerRadius = new CornerRadius(10); toast.Padding = new Thickness(15, 12, 15, 12);
            toast.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 18, ShadowDepth = 4, Opacity = 0.15, Color = Color.FromRgb(36,70,125) };
            toast.HorizontalAlignment = HorizontalAlignment.Center; toast.VerticalAlignment = VerticalAlignment.Top;
            toast.Margin = new Thickness(20, 86, 20, 0); toast.MaxWidth = 620; toast.Visibility = Visibility.Collapsed;
            toastText.Foreground = Brushes.White; toastText.TextWrapping = TextWrapping.Wrap; toastText.FontSize = 13; var toastBody = new DockPanel();
            toastSave = Button("Save now", async () => { toast.Visibility = Visibility.Collapsed; await SaveNow(true); }, "Primary"); toastSave.Margin = new Thickness(18, 0, 0, 0); toastSave.Visibility = Visibility.Collapsed;
            DockPanel.SetDock(toastSave, Dock.Right); toastBody.Children.Add(toastSave); toastBody.Children.Add(toastText); toast.Child = toastBody;
            Grid.SetRowSpan(toast, 3); shell.Children.Add(toast);
            toastTimer.Interval = TimeSpan.FromSeconds(5); toastTimer.Tick += (s, e) => { toast.Visibility = Visibility.Collapsed; toastTimer.Stop(); };
            hoverTimer.Interval = TimeSpan.FromMilliseconds(700); hoverTimer.Tick += HoverOpen;
            heartbeat.Interval = TimeSpan.FromSeconds(1); heartbeat.Tick += Heartbeat; heartbeat.Start();
            PreviewKeyDown += OnKey; PreviewMouseDown += (s, e) => Activity(); PreviewMouseWheel += (s, e) => Activity();
            PreviewMouseMove += (s, e) => { if (e.LeftButton == MouseButtonState.Pressed) Activity(); };
            Closing += OnClosing;
            SystemEvents.SessionSwitch += OnSystemSession;
            Closed += (s, e) => { StopTimers(); SystemEvents.SessionSwitch -= OnSystemSession; };
            SizeChanged += (s, e) => { foreach (Border dialog in veil.Children.OfType<Border>()) dialog.MaxHeight = Math.Max(300, e.NewSize.Height - 160); foreach (Border card in unlock.Children.OfType<Border>()) card.MaxHeight = Math.Max(300, e.NewSize.Height - 80); };
            unlockButton = Button("Unlock database", async () => { await Open(); }, "Primary");
            unlockButton.Margin = new Thickness(0, 16, 0, 0); unlockButton.HorizontalAlignment = HorizontalAlignment.Stretch;
            unlockContent.Children.Add(unlockButton);
            unlockContent.Children.Add(Button("Create new database…", () => DatabaseDialog(null, true), "Quiet"));
            EnsureTooltips(shell); SetStatus();
        }

        private void BuildUnlock()
        {
            unlock.Background = Brushes.White;
            var card = new Border { Width = 470, MaxHeight = Math.Max(300, Height - 80), Padding = new Thickness(32), Background = Brushes.White, CornerRadius = new CornerRadius(14), BorderBrush = Brush("#E4E9F0"), BorderThickness = new Thickness(1), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var stack = unlockContent; card.Child = new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }; unlock.Children.Add(card);
            stack.Children.Add(BrandImage(64));
            var mark = Text("KEEPASSHQ", 11, "#396BD7"); mark.FontWeight = FontWeights.SemiBold; mark.Margin = new Thickness(0, 0, 0, 14); stack.Children.Add(mark);
            unlockTitle.Text = "Open your database"; unlockTitle.FontSize = 25; unlockTitle.FontWeight = FontWeights.SemiBold; unlockTitle.TextWrapping = TextWrapping.Wrap; stack.Children.Add(unlockTitle);
            unlockHint.Text = "A clear workspace for your KeePass entries. Everything stays local."; unlockHint.Foreground = Brush("#7B8799"); unlockHint.TextWrapping = TextWrapping.Wrap; unlockHint.Margin = new Thickness(0, 10, 0, 24); stack.Children.Add(unlockHint);
            stack.Children.Add(Text("DATABASE", 10, "#7B8799"));
            pathBox.Text = "Choose a .kdbx database"; pathBox.Foreground = Brush("#8B96A6"); stack.Children.Add(FilePickerRow(pathBox, "Choose the KeePass database to open.", Browse));
            usePassword.Checked += (s, e) => { password.IsEnabled = true; };
            usePassword.Unchecked += (s, e) => { password.IsEnabled = false; password.Clear(); };
            stack.Children.Add(usePassword); var master=SecretField(password,"Master password");master.Submitted+=async()=>{await Open();};stack.Children.Add(master);
            var advanced = new Expander { Header = "Key file / Windows account", Foreground = Brush("#7B8799"), Margin = new Thickness(0, 17, 0, 0) };
            var keys = new StackPanel { Margin = new Thickness(0, 10, 0, 0) }; advanced.Content = keys;
            var keyRow = new Grid(); keyRow.ColumnDefinitions.Add(new ColumnDefinition()); keyRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            keyBox.IsReadOnly = true; keyBox.ToolTip = "Optional key file"; keyRow.Children.Add(keyBox);
            var keyBrowse = Button("Browse…", () => { var d = new OpenFileDialog { Title = "Choose the database key file", Filter = "Key files|*.keyx;*.key|All files|*.*" }; if (d.ShowDialog(this) == true) keyBox.Text = d.FileName; }, null);
            keyBrowse.Margin = new Thickness(8, 0, 0, 0); Grid.SetColumn(keyBrowse, 1); keyRow.Children.Add(keyBrowse); keys.Children.Add(keyRow);
            keys.Children.Add(Button("Clear key file", () => keyBox.Clear(), "Quiet")); keys.Children.Add(windowsKey); stack.Children.Add(advanced);
        }

        private void Browse()
        {
            if (opening || sealing) return;
            if (session != null) { Toast("Unlock the current session before choosing another database.", true); return; }
            var d = new OpenFileDialog { Filter = "KeePass databases (*.kdbx)|*.kdbx", Title = "Choose a database — decrypted only on this computer", CheckFileExists = true };
            if (d.ShowDialog(this) == true) { selectedPath = d.FileName; pathBox.Text = selectedPath; pathBox.Foreground = Brush("#46536A"); password.Focus(); }
        }
        private async Task Open()
        {
            if (opening || sealing || protectionFailure || veil.Visibility == Visibility.Visible || String.IsNullOrEmpty(selectedPath)) { if (String.IsNullOrEmpty(selectedPath)) Toast("Choose a .kdbx database first.", false); return; }
            opening = true; unlockButton.IsEnabled = false; unlockButton.Content = "Decrypting…";
            CompositeKey key = null;
            try
            {
                key = VaultSession.MakeKey(password.Password, usePassword.IsChecked == true, keyBox.Text, windowsKey.IsChecked == true); password.Clear();
                if (session != null)
                {
                    bool correct = await Task.Run(() => session.Database.MasterKey.EqualsValue(key));
                    if (!correct) throw new InvalidOperationException("The database key did not match.");
                }
                else session = await Task.Run(() => VaultSession.Open(selectedPath, key));
                if (closeAfterOpen || lockAfterOpen) { DisposeSession(); ShowLocked("Database closed and file released."); return; }
                locked = false; Activity(); unlock.Visibility = Visibility.Collapsed;
                BuildPanes(); UpdateFileLabel();
                SetStatus(); if (session.DraftExists) await OfferDraft();
            }
            catch (Exception ex) { Toast(OpenError(ex), true); }
            finally { key = null; password.Clear(); opening = false; unlockButton.IsEnabled = true; unlockButton.Content = "Unlock database"; if (closeAfterOpen) { closeAfterOpen = false; DisposeSession(); QueueClose(); } else if (lockAfterOpen) { lockAfterOpen = false; DisposeSession(); ShowLocked("Database closed and file released."); } UpdateInteraction(); }
        }
        private string OpenError(Exception ex)
        {
            if (ex is UnauthorizedAccessException) return "This folder is not writable. Choose a writable local copy of the database.";
            if (ex is IOException && ex.Message.Contains("organizer-lock")) return "This database is already open in another Organizer window.";
            if (ex is FileNotFoundException) return "The database or key file could not be found.";
            return "Could not unlock the database. Check the password and selected key options. It may also be open elsewhere or use an unsupported plugin key/cipher.";
        }

        private void BuildPanes()
        {
            workspace.Children.Clear(); panes.Clear();
            for (int side = 0; side < 2; ++side)
            {
                var owner = side == 0 ? session : (otherSession ?? session);
                var pane = new BrowserPane { Root = owner.Database.RootGroup, Session = owner }; pane.Path.Add(pane.Root); panes.Add(pane);
                var layout = new Grid { Margin = new Thickness(8, 10, 8, 8) }; pane.Layout = layout;
                layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); layout.RowDefinitions.Add(new RowDefinition());
                var panelFrame = new Border { BorderBrush = Brush("#DDE5EF"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Background = Brushes.White, IsHitTestVisible = false }; Grid.SetRowSpan(panelFrame, 2); layout.Children.Add(panelFrame);
                var heading = new StackPanel { Margin = new Thickness(13, 12, 48, 12) };
                pane.Heading = Text("Database", 18, "#25334B"); pane.Heading.FontWeight = FontWeights.SemiBold; pane.Heading.TextTrimming = TextTrimming.CharacterEllipsis;
                pane.Count = Text("", 11, "#718096"); pane.Count.Margin = new Thickness(0, 3, 0, 3);
                pane.FilePath = Text("", 10.5, "#8694A8"); pane.FilePath.TextWrapping = TextWrapping.Wrap;
                heading.Children.Add(pane.Heading); heading.Children.Add(pane.Count); heading.Children.Add(pane.FilePath); layout.Children.Add(heading);
                var databaseButton = IconButton("folder", "Database actions · open, create, close, or save an encrypted recovery copy", () => PaneDatabaseMenu(pane)); databaseButton.HorizontalAlignment = HorizontalAlignment.Right; databaseButton.VerticalAlignment = VerticalAlignment.Top; databaseButton.Margin = new Thickness(0, 10, 10, 0); layout.Children.Add(databaseButton);
                pane.Columns = new StackPanel { Orientation = Orientation.Horizontal };
                pane.Scroll = new ScrollViewer { Content = pane.Columns, Margin = new Thickness(10, 0, 10, 10), HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, PanningMode = PanningMode.HorizontalOnly };
                pane.Scroll.Style = (Style)Application.Current.FindResource(typeof(ScrollViewer));
                Grid.SetRow(pane.Scroll, 1); layout.Children.Add(pane.Scroll); Grid.SetColumn(layout, side); workspace.Children.Add(layout); if (side == 0 || organizerMode) RenderPane(pane);
            }
            SwitchMode(organizerMode);
            UpdateInteraction();
        }
        private int EntryCount(PwGroup group) { return group.Entries.Count() + group.Groups.Sum(g => EntryCount(g)); }
        private void RenderPane(BrowserPane pane)
        {
            if (session == null || locked) return;
            for (int i = 1; i < pane.Path.Count; ++i)
                if (pane.Path[i].ParentGroup != pane.Path[i - 1]) { pane.Path.RemoveRange(i, pane.Path.Count - i); break; }
            double offset = pane.Scroll.HorizontalOffset;
            pane.Editors.Clear(); pane.EntryHeaders.Clear();
            pane.Columns.Children.Clear();
            int recordCount = EntryCount(pane.Root);
            pane.Count.Text = recordCount + (recordCount == 1 ? " record" : " records");
            pane.Count.TextTrimming = TextTrimming.CharacterEllipsis;
            pane.Heading.Text = System.IO.Path.GetFileName(pane.Session.PathName);
            pane.FilePath.Text = rendering ? (pane.Session == otherSession ? @"C:\Examples\KeePassHQ\Second database\Demo.kdbx" : @"C:\Examples\KeePassHQ\Demo.kdbx") : pane.Session.PathName;
            UiVisual.Help(pane.FilePath, pane.FilePath.Text);
            for (int depth = 0; depth < pane.Path.Count; ++depth)
            {
                int columnDepth = depth; PwGroup current = pane.Path[depth];
                var column = new Grid { Width = 280, Margin = new Thickness(0, 0, 8, 0), Background = Brushes.White };
                column.RowDefinitions.Add(new RowDefinition { Height = new GridLength(36) }); column.RowDefinitions.Add(new RowDefinition());
                var columnFrame = new Border { BorderBrush = Brush("#E1E7F0"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(7), IsHitTestVisible = false }; Grid.SetRowSpan(columnFrame, 2); column.Children.Add(columnFrame);
                var header = new Grid { Margin = new Thickness(11, 0, 7, 0) };
                header.ColumnDefinitions.Add(new ColumnDefinition()); header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var caption = Text(depth == 0 ? "TOP LEVEL" : current.Name.ToUpperInvariant(), 10, "#8390A3"); caption.FontWeight = FontWeights.SemiBold; caption.TextTrimming = TextTrimming.CharacterEllipsis; caption.ToolTip = current.Name;
                caption.TextAlignment = TextAlignment.Center; caption.HorizontalAlignment = HorizontalAlignment.Stretch; caption.Margin = new Thickness(24, 0, 24, 0); Grid.SetColumnSpan(caption, 2); header.Children.Add(caption);
                var add = IconButton("add", "Create in " + current.Name, () => { });
                var menu = new ContextMenu();
                var newGroup = new MenuItem { Header = "New group" }; newGroup.Click += (s, e) => PromptName("New group", "Group name", value => { var g = pane.Session.NewGroup(current, value); Push("Create group", () => { current.Groups.Remove(g); pane.Session.Database.DeletedObjects.Add(new PwDeletedObject(g.Uuid, DateTime.UtcNow)); pane.Session.Changed(); }); RenderAll(); Toast("Group created.", false); });
                var newEntry = new MenuItem { Header = "New entry" }; newEntry.Click += (s, e) => AddEntry(pane, current);
                menu.Items.Add(newGroup); menu.Items.Add(newEntry); add.ContextMenu = menu; menu.Opened += (s, e) => EnsureTooltips(menu);
                add.Click += (s, e) => { menu.PlacementTarget = add; menu.IsOpen = true; }; Grid.SetColumn(add, 1); header.Children.Add(add); column.Children.Add(header);
                var cards = new StackPanel { Margin = new Thickness(7, 0, 7, 8) };
                foreach (object item in VaultSession.Ordered(current))
                {
                    cards.Children.Add(ReceiveGap(pane, current, VaultSession.ItemId(item)));
                    var group = item as PwGroup;
                    cards.Children.Add(group != null ? GroupCard(pane, group, columnDepth) : EntryCard(pane, (PwEntry)item));
                }
                cards.Children.Add(ReceiveGap(pane, current, null));
                if (VaultSession.Ordered(current).Count == 0)
                {
                    var empty = Text("Drop a card here\nor use + to create one", 12, "#9AA5B5"); empty.TextAlignment = TextAlignment.Center; empty.Margin = new Thickness(9, 32, 9, 0); cards.Children.Add(empty);
                }
                var vertical = new ScrollViewer { Content = cards, Margin = new Thickness(1, 0, 1, 1), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, PanningMode = PanningMode.VerticalOnly };
                vertical.Style = (Style)Application.Current.FindResource(typeof(ScrollViewer));
                Grid.SetRow(vertical, 1); column.Children.Add(vertical);
                WireDrop(column, current, pane, columnDepth, false); pane.Columns.Children.Add(column);
            }
            pane.Scroll.ScrollToHorizontalOffset(offset); EnsureTooltips(pane.Layout);
        }
        private void RenderAll() { pendingField = null; foreach (var pane in panes) if (pane.Layout.Visibility == Visibility.Visible) RenderPane(pane); SetStatus(); }

        private Border GroupCard(BrowserPane pane, PwGroup group, int depth)
        {
            bool active = pane.Path.Count > depth + 1 && pane.Path[depth + 1] == group;
            var border = new Border { CornerRadius = new CornerRadius(6), Background = Brush(active ? "#DDF2E5" : "#EFF4FF"), BorderBrush = Brush(active ? "#5AA57A" : "#DCE7FA"), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 0, 4) };
            var header = CardHeader(group.Name, true, () => Navigate(pane, group, depth), active);
            var context = new ContextMenu(); var rename = new MenuItem { Header = "Rename group" };
            rename.Click += (s, e) => PromptName("Rename group", group.Name, value => { string old = group.Name; group.Name = value; group.Touch(true, false); pane.Session.Changed(); Push("Rename group", () => { group.Name = old; group.Touch(true, false); pane.Session.Changed(); }); RenderAll(); });
            context.Items.Add(rename); header.ContextMenu = context; context.Opened += (s, e) => EnsureTooltips(context);
            if (active) header.Foreground = Brush("#245C3A");
            border.Child = header; WireDrag(header, group); WireDrop(border, group, pane, depth, true); return border;
        }
        private Button CardHeader(string title, bool group, Action click, bool active)
        {
            var header = Button("", click, "Quiet"); header.Padding = new Thickness(9, 6, 9, 6); header.MinHeight = 30; header.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            string display=String.IsNullOrWhiteSpace(title)?"Untitled":title;
            UiVisual.Help(header, (group ? "Open group: " : (active ? "Collapse entry: " : "Expand entry: ")) + display + ". Drag this header to move or reorder the card.");
            var content = new Grid(); content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) }); content.ColumnDefinitions.Add(new ColumnDefinition()); content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
            var icon = UiVisual.Icon(group ? "folder" : "key", group ? (active ? "#388260" : "#557CB7") : "#8193AE"); content.Children.Add(icon);
            var name = Text(String.IsNullOrWhiteSpace(title) ? "Untitled" : title, 13, group ? (active ? "#245C3A" : "#375F96") : "#344158"); name.TextTrimming = TextTrimming.CharacterEllipsis; name.ToolTip = title; Grid.SetColumn(name, 1); content.Children.Add(name);
            var arrow = Text(group ? "›" : (active ? "−" : "+"), 15, "#8C9AAF"); arrow.HorizontalAlignment = HorizontalAlignment.Right; Grid.SetColumn(arrow, 2); content.Children.Add(arrow); header.Content = content;
            System.Windows.Automation.AutomationProperties.SetName(header, (group ? "Group: " : "Entry: ") + display);
            if (!group) header.Tag = title;
            return header;
        }
        private Border EntryCard(BrowserPane pane, PwEntry entry)
        {
            bool expanded = pane.Expanded == entry.Uuid.ToHexString();
            var border = new Border { CornerRadius = new CornerRadius(6), Background = Brushes.White, BorderBrush = Brush(expanded ? "#B9CBE9" : "#E0E6EE"), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 0, 4) };
            var stack = new StackPanel(); border.Child = stack;
            var header = CardHeader(VaultSession.Read(entry, PwDefs.TitleField), false, () => { pendingField = null; pane.Expanded = expanded ? null : entry.Uuid.ToHexString(); RenderPane(pane); }, expanded);
            header.Tag = entry.Uuid.ToHexString(); pane.EntryHeaders[entry.Uuid.ToHexString()] = header; WireDrag(header, entry); stack.Children.Add(header);
            if (expanded)
            {
                var fields = new StackPanel { Margin = new Thickness(12, 0, 12, 13) };
                AddField(fields, entry, "Name", PwDefs.TitleField, false, header, pane);
                AddField(fields, entry, "Username", PwDefs.UserNameField, false, header, pane);
                AddField(fields, entry, "Password", PwDefs.PasswordField, false, header, pane);
                AddField(fields, entry, "URL", PwDefs.UrlField, false, header, pane);
                AddField(fields, entry, "Notes", PwDefs.NotesField, true, header, pane);
                stack.Children.Add(fields);
            }
            return border;
        }
        private void AddField(StackPanel fields, PwEntry entry, string label, string field, bool multiline, Button cardHeader, BrowserPane pane)
        {
            var caption = Text(label.ToUpperInvariant(), 9, "#7D8A9E"); caption.Margin = new Thickness(0, 6, 0, 4); fields.Children.Add(caption);
            var frame = new Border { Background = Brushes.White, BorderBrush = Brush("#DDE4ED"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(5) };
            var row = new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); frame.Child = row;
            var input = new TextBox { Text = VaultSession.Read(entry, field), BorderThickness = new Thickness(0), AcceptsReturn = multiline, TextWrapping = multiline ? TextWrapping.Wrap : TextWrapping.NoWrap, VerticalScrollBarVisibility = multiline ? ScrollBarVisibility.Auto : ScrollBarVisibility.Hidden, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, MinHeight = multiline ? 120 : 32, MaxHeight = multiline ? 220 : Double.PositiveInfinity, VerticalContentAlignment = multiline ? VerticalAlignment.Top : VerticalAlignment.Center, Tag = new FieldBinding { Entry = entry, Field = field } };
            UiVisual.Help(input, "Edit " + label.ToLowerInvariant() + ". Changes stay private until you save the database.");
            System.Windows.Automation.AutomationProperties.SetName(input, label); SpellCheck.SetIsEnabled(input, false); row.Children.Add(input);
            pane.Editors.Add(input);
            input.GotKeyboardFocus += (sender, args) => frame.BorderBrush = Brush("#7198ED");
            input.LostKeyboardFocus += (sender, args) => frame.BorderBrush = Brush("#DDE4ED");
            string editId = pane.Session.PathName + entry.Uuid.ToHexString() + field;
            input.LostKeyboardFocus += (sender, args) => { pendingField = null; };
            input.TextChanged += (sender, args) =>
            {
                if (session == null || locked || fieldGuard || VaultSession.Read(entry, field) == input.Text) return;
                if (pendingField != editId || Elapsed(lastFieldEditTicks) > 1.5)
                { string previous = VaultSession.Read(entry, field); entry.CreateBackup(pane.Session.Database); Push("Edit " + label.ToLowerInvariant(), () => pane.Session.Set(entry, field, previous)); pendingField = editId; }
                pane.Session.Set(entry, field, input.Text); lastFieldEditTicks = Stopwatch.GetTimestamp(); Activity(); SyncField(input, entry, field); SetStatus();
                if (field == PwDefs.TitleField) RefreshNames(entry);
            };
            var tools = new StackPanel { Background = Brushes.Transparent, Orientation = multiline ? Orientation.Vertical : Orientation.Horizontal, VerticalAlignment = multiline ? VerticalAlignment.Top : VerticalAlignment.Center, Margin = new Thickness(0, 3, 3, 3) };
            tools.Children.Add(FieldAction("cut", "Cut selected text, or the whole field", () => ClipboardAction(input, "cut")));
            tools.Children.Add(FieldAction("copy", "Copy selected text, or the whole field", () => ClipboardAction(input, "copy")));
            tools.Children.Add(FieldAction("paste", "Paste at the cursor", () => ClipboardAction(input, "paste")));
            if (field == PwDefs.PasswordField) tools.Children.Add(FieldAction("key", "Generate password", () => ShowGenerator(input)));
            Grid.SetColumn(tools, 1); row.Children.Add(tools); fields.Children.Add(frame);
        }
        private void ClipboardAction(TextBox input, string action)
        {
            try
            {
                if (action == "paste") { string text = clipboard.Read(); if (text != null) input.SelectedText = text; input.Focus(); return; }
                string value = input.SelectionLength > 0 ? input.SelectedText : input.Text;
                if (value.Length == 0) return;
                clipboard.Write(value); OwnClipboard(value);
                if (action == "cut") { if (input.SelectionLength > 0) input.SelectedText = ""; else input.Clear(); }
                Toast(action == "copy" ? "Copied." : "Cut to clipboard.", false);
            }
            catch { Toast("The clipboard is busy. Try again.", true); }
        }
        private void Navigate(BrowserPane pane, PwGroup group, int depth)
        {
            pendingField = null;
            if (pane.Path.Count > depth + 1) pane.Path.RemoveRange(depth + 1, pane.Path.Count - depth - 1);
            pane.Path.Add(group); RenderPane(pane);
            pane.Scroll.Dispatcher.BeginInvoke(new Action(() => pane.Scroll.ScrollToRightEnd()), DispatcherPriority.Loaded);
        }
        private void AddEntry(BrowserPane pane, PwGroup parent)
        {
            PwEntry entry = pane.Session.NewEntry(parent); Push("Create entry", () => { parent.Entries.Remove(entry); pane.Session.Database.DeletedObjects.Add(new PwDeletedObject(entry.Uuid, DateTime.UtcNow)); pane.Session.Changed(); });
            pane.Expanded = entry.Uuid.ToHexString(); RenderAll(); Toast("New entry created.", false);
        }
        private void WireDrag(Button handle, object item)
        {
            Point start = new Point(); bool pressed = false;
            handle.QueryContinueDrag += (s, e) => { if (locked || sealing) { e.Action = DragAction.Cancel; e.Handled = true; } };
            handle.PreviewMouseLeftButtonDown += (s, e) => { start = e.GetPosition(handle); pressed = true; };
            handle.PreviewMouseLeftButtonUp += (s, e) => { pressed = false; };
            handle.PreviewMouseMove += (s, e) =>
            {
                if (!pressed || e.LeftButton != MouseButtonState.Pressed || locked || sealing || !workspace.IsEnabled) return;
                Point now = e.GetPosition(handle);
                if (Math.Abs(now.X - start.X) < 9 && Math.Abs(now.Y - start.Y) < 9) return;
                pressed = false; dragging = item;
                try { DragDrop.DoDragDrop(handle, new DataObject("VaultOrganizer.Card", CardId(item)), DragDropEffects.Move); }
                finally { dragging = null; CloseGap(); hoverTimer.Stop(); hoverGroup = null; RenderAll(); }
                e.Handled = true;
            };
        }
        private static string CardId(object item)
        { var group = item as PwGroup; return group != null ? group.Uuid.ToHexString() : ((PwEntry)item).Uuid.ToHexString(); }
        private object ReadDrag(IDataObject data)
        { return dragging != null && data.GetData("VaultOrganizer.Card") as string == CardId(dragging) ? dragging : null; }
        private void WireDrop(FrameworkElement element, PwGroup group, BrowserPane pane, int depth, bool hover)
        {
            element.AllowDrop = true;
            element.DragOver += (s, e) =>
            {
                e.Handled = true; object item = ReadDrag(e.Data);
                bool valid = session != null && !locked && workspace.IsEnabled && item != null && CanDrop(item, group, null);
                e.Effects = valid ? DragDropEffects.Move : DragDropEffects.None;
                if (!valid) return;
                CloseGap(); ScrollDuringDrag(pane, element, e);
                Activity();
                var border = element as Border; if (border != null) border.BorderBrush = Brush("#396BD7");
                if (hover && hoverGroup != group) { hoverGroup = group; hoverPane = pane; hoverDepth = depth; hoverTimer.Stop(); hoverTimer.Start(); }
                else if (!hover && hoverGroup != null) { hoverTimer.Stop(); hoverGroup = null; }
                // The horizontal scrollbar and edge scrolling keep deeper destinations reachable.
                Point point = e.GetPosition(pane.Scroll);
                if (point.X > pane.Scroll.ActualWidth - 28) pane.Scroll.ScrollToHorizontalOffset(pane.Scroll.HorizontalOffset + 12);
                if (point.X < 28) pane.Scroll.ScrollToHorizontalOffset(Math.Max(0, pane.Scroll.HorizontalOffset - 12));
            };
            element.DragLeave += (s, e) => { var border = element as Border; if (border != null) border.BorderBrush = Brush("#D6E3F6"); };
            element.Drop += (s, e) =>
            {
                e.Handled = true; hoverTimer.Stop(); hoverGroup = null;
                object item = ReadDrag(e.Data);
                if (item == null || session == null || locked || !workspace.IsEnabled || !CanDrop(item, group, null)) return;
                MoveCard(item, group); e.Effects = DragDropEffects.Move;
            };
        }
        private void HoverOpen(object sender, EventArgs e)
        {
            hoverTimer.Stop();
            if (dragging != null && hoverGroup != null && hoverPane != null && CanDrop(dragging, hoverGroup, null))
            {
                if (hoverPane.Path.Count <= hoverDepth + 1 || hoverPane.Path[hoverDepth + 1] != hoverGroup) Navigate(hoverPane, hoverGroup, hoverDepth);
            }
        }
        private void MoveCard(object item, PwGroup target) { PlaceCard(item, target, null); }
        private void PlaceCard(object item, PwGroup target, string before)
        {
            var owner = SessionFor(item); var destination = SessionFor(target);
            if (owner == null || destination == null) return;
            if (owner != destination) { var transfer = CrossDatabaseDrop(owner, destination, item, target, before); return; }
            PwGroup previous = VaultSession.ParentOf(item); var from = VaultSession.Ordered(previous).Select(VaultSession.ItemId).ToArray(); var to = VaultSession.Ordered(target).Select(VaultSession.ItemId).ToArray();
            string id = VaultSession.ItemId(item); int index = Array.IndexOf(from, id); string after = index + 1 < from.Length ? from[index + 1] : null;
            if (!owner.Place(item, target, before)) return;
            Push("Move / reorder card", () => { owner.Place(item, previous, after); VaultSession.WriteOrder(previous, from); if (target != previous) VaultSession.WriteOrder(target, to); owner.Changed(); });
            RenderAll(); Toast("Card moved. Changes are not published until Save.", false);
        }
        private static string Short(string value) { return value.Length > 55 ? value.Substring(0, 52) + "…" : value; }
        private void Push(string label, Action restore) { RecordAction(); undo.Add(new UndoItem { Label = label, Restore = restore }); if (undo.Count > 40) undo.RemoveAt(0); undoButton.IsEnabled = true; }
        private void Undo()
        {
            if (locked || session == null || undo.Count == 0 || sealing || !workspace.IsEnabled || veil.Visibility == Visibility.Visible) return;
            UndoItem item = undo[undo.Count - 1]; undo.RemoveAt(undo.Count - 1); item.Restore(); RecordAction(); pendingField = null; RenderAll(); Toast("Undid: " + item.Label + ".", false);
        }

        private async void Heartbeat(object sender, EventArgs args) { await Tick(); }
        private async Task Tick()
        {
            ExpireClipboard();
            if (session == null || locked || opening || sealing || rendering || protectionFailure || transferring) return;
            SetStatus();
            if (Elapsed(lastActivityTicks) >= lockMinutes * 60) { await Seal(false); return; }
            if (editingSuspended || transferring || saveTask != null) return;
            if (LoadedSessions.Any(s => s.Dirty && s.DraftRevision != s.Revision) && draftTask == null && saveTask == null && Elapsed(lastDraftTicks) >= saveSeconds) await ProtectDraft();
            NudgeSave();
        }
        private async Task<bool> SaveNow(bool explicitRequest)
        {
            if (opening || protectionFailure || transferring || changingDatabase || editingSuspended || veil.Visibility == Visibility.Visible) return false;
            if (draftTask != null) await draftTask;
            if (session == null || locked || sealing || opening || protectionFailure) return false;
            if (saveTask != null) return await saveTask;
            if (!AnyDirty && !explicitRequest) return true;
            if (saveBlocked && !explicitRequest) return false;
            saveTask = SaveAllWork(); SetStatus(); UpdateInteraction();
            bool success;
            try { success = await saveTask; }
            finally { saveTask = null; UpdateInteraction(); SetStatus(); }
            if (success) { completedActions = 0; dirtyTicks = AnyDirty ? Stopwatch.GetTimestamp() : 0; lastNudgeTicks = Stopwatch.GetTimestamp(); }
            if (success && explicitRequest && !locked) Toast("All loaded databases saved.", false);
            return success;
        }
        private async Task<bool> SaveWork(VaultSession working)
        {
            try
            {
                for (int attempt = 0; attempt < 3; ++attempt)
                {
                    if (working.HasPendingMerge && !await BringInChanges(working)) { saveBlocked = true; recoveryButton.Visibility = Visibility.Visible; return false; }
                    SaveResult late = null;
                    try { SaveResult result = await working.SaveAsync(); working.AcceptSave(result); if (result.ConcurrentPath == null) { saveBlocked = false; recoveryButton.Visibility = Visibility.Collapsed; return true; } late = result; working.Changed(); }
                    catch (ExternalChangeException)
                    {
                        working.SaveFailed();
                    }
                    bool reconciled;
                    try { reconciled = await BringInChanges(working, late == null ? null : late.ConcurrentPath, late == null ? null : late.ConcurrentBase); }
                    finally { if (late != null && late.ConcurrentBase != null) late.ConcurrentBase.Close(); }
                    if (!reconciled) { saveBlocked = true; recoveryButton.Visibility = Visibility.Visible; return false; }
                }
                throw new IOException("The file kept changing during saving. Your edits are retained; try again when the other app finishes.");
            }
            catch (Exception ex)
            {
                working.SaveFailed(); saveBlocked = true; recoveryButton.Visibility = Visibility.Visible;
                string message = working.PublishingBlocked ? "A competing file version could not be verified or secured. Further saves are blocked to protect it. Keep the version backups and save a recovery copy." : (ex is IOException && ex.Message.Contains("changed") ? ex.Message : "Save failed. Your edits remain in memory. Check the file/folder, retry with Ctrl+S, or save a recovery copy.");
                Toast(message, true); return false;
            }
        }
        private async Task<bool> BringInChanges(VaultSession working, string candidatePath = null, PwDatabase candidateBase = null)
        {
            MergePlan plan = null; editingSuspended = true; UpdateInteraction(); Keyboard.ClearFocus(); pendingField = null;
            if (veil.Visibility == Visibility.Visible) CloseModal();
            try
            {
                if (candidatePath == null && working.HasPendingMerge) plan = await working.PreparePendingAsync();
                else plan = await working.PrepareMergeAsync(candidatePath, candidateBase);
                if (candidatePath != null) { working.HoldPending(plan.External, plan.Baseline); plan.Revision = working.Revision; plan.CompletesPending = true; }
                if ((sealing || locked) && plan.Conflicts.Count > 0) return false;
                foreach (MergeConflict conflict in plan.Conflicts)
                {
                    conflict.Choice = await ReviewConflict(conflict);
                    if (conflict.Choice == MergeChoice.Cancel || sealing || locked) { Toast("Merge paused. Neither version was overwritten. Your edits remain in memory.", true); return false; }
                }
                var paths = panes.Select(p => p.Path.Select(g => g.Uuid.ToHexString()).ToArray()).ToArray();
                var expanded = panes.Select(p => p.Expanded).ToArray();
                working.ApplyMerge(plan); undo.Clear(); if (!locked) BuildPanes();
                var groups = MergePlan.Groups(working.Database);
                for (int i = 0; i < panes.Count && i < paths.Length; ++i)
                {
                    var pane = panes[i]; if (pane.Session != working) continue; pane.Path.Clear(); pane.Path.Add(pane.Root); pane.Expanded = expanded[i];
                    foreach (string id in paths[i].Skip(1)) { PwGroup group; if (!groups.TryGetValue(id, out group) || group.ParentGroup != pane.Path.Last()) break; pane.Path.Add(group); }
                    if (pane.Layout.Visibility == Visibility.Visible) RenderPane(pane);
                }
                if (!locked) Toast("Changes from the file merged. Saving the complete database…", false); return true;
            }
            catch (Exception ex)
            {
                if (candidatePath != null) working.BlockUnresolvedPublishing();
                string detail = ex is IOException && ex.Message.Contains("cycle") ? ex.Message : "Could not safely merge the changed file. Your edits remain in memory. Save a recovery copy or retry after checking the file and its key.";
                Toast(detail, true); return false;
            }
            finally { if (plan != null) plan.Dispose(); editingSuspended = false; UpdateInteraction(); }
        }
        private Task<MergeChoice> ReviewConflict(MergeConflict conflict)
        {
            mergeChoice = new TaskCompletionSource<MergeChoice>();
            var body = new StackPanel();
            var title = Text(conflict.Title, 15, "#344158"); title.FontWeight = FontWeights.SemiBold; title.TextWrapping = TextWrapping.Wrap; body.Children.Add(title);
            var detail = Text(String.Join(" · ", conflict.Fields), 12, "#7D8A9E"); detail.TextWrapping = TextWrapping.Wrap; detail.Margin = new Thickness(0, 7, 0, 17); body.Children.Add(detail);
            var compare = new Grid(); compare.ColumnDefinitions.Add(new ColumnDefinition()); compare.ColumnDefinitions.Add(new ColumnDefinition());
            for (int side = 0; side < 2; ++side)
            {
                var column = new StackPanel { Margin = side == 0 ? new Thickness(0, 0, 7, 0) : new Thickness(7, 0, 0, 0) };
                column.Children.Add(Text(side == 0 ? "THIS APP" : "CHANGED FILE", 10, "#7D8A9E"));
                string value;
                if (conflict.IsGroup)
                {
                    PwGroup group = side == 0 ? conflict.MineGroup : conflict.FileGroup;
                    value = group == null ? "Deleted, including its contents." : (conflict.Fields.Contains("Card order") ? group.Name + "\n\n" + String.Join("\n", VaultSession.Ordered(group).Select(item => item is PwGroup ? ((PwGroup)item).Name : VaultSession.Read((PwEntry)item, PwDefs.TitleField))) : group.Name + "\n" + EntryCount(group) + " records\nParent: " + (group.ParentGroup == null ? "Top level" : group.ParentGroup.Name));
                }
                else
                {
                    PwEntry entry = side == 0 ? conflict.MineEntry : conflict.FileEntry;
                    value = entry == null ? "Deleted." : String.Join("\n\n", conflict.Presence ? new[] { "Name: " + VaultSession.Read(entry, PwDefs.TitleField), "Username: " + VaultSession.Read(entry, PwDefs.UserNameField), "Notes: " + VaultSession.Read(entry, PwDefs.NotesField) } : conflict.Fields.Select(f => f == "Group location" ? "Group: " + entry.ParentGroup.Name : f + ":\n" + VaultSession.Read(entry, f)).ToArray());
                }
                column.Children.Add(new TextBox { Text = value, IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalContentAlignment = VerticalAlignment.Top, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Height = conflict.Presence || conflict.Fields.Contains(PwDefs.NotesField) ? 230 : (conflict.Fields.Count > 2 ? 180 : 110), Margin = new Thickness(0, 7, 0, 0) });
                Grid.SetColumn(column, side); compare.Children.Add(column);
            }
            body.Children.Add(compare);
            var hint = Text(conflict.Credentials ? "Username, password and URL are selected together. Other nonconflicting changes are kept." : "Other, nonconflicting changes are kept automatically.", 11, "#7D8A9E"); hint.TextWrapping = TextWrapping.Wrap; hint.Margin = new Thickness(0, 12, 0, 15); body.Children.Add(hint);
            var buttons = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
            buttons.Children.Add(Button("Later", () => FinishReview(MergeChoice.Cancel), "Quiet"));
            buttons.Children.Add(Button("Use app edits", () => FinishReview(MergeChoice.Mine), null));
            var file = Button("Use file edits", () => FinishReview(MergeChoice.File), null); file.Margin = new Thickness(7, 0, 0, 0); buttons.Children.Add(file);
            if (conflict.CanKeepBoth) { var both = Button("Keep both entries", () => FinishReview(MergeChoice.Both), "Primary"); both.Margin = new Thickness(7, 0, 0, 0); buttons.Children.Add(both); }
            body.Children.Add(buttons); Border dialog = Dialog("Resolve concurrent edits", body); dialog.Width = 760;
            return mergeChoice.Task;
        }
        private void FinishReview(MergeChoice choice)
        {
            if (mergeChoice == null) return;
            var completion = mergeChoice; mergeChoice = null;
            CloseModal(); completion.TrySetResult(choice);
        }
        private void SetStatus()
        {
            undoButton.IsEnabled = !locked && undo.Count > 0;
            saveStatus.Foreground = Brush("#758196");
            if (locked) { saveStatus.Text = "Closed and locked · file released"; return; }
            if (transferring) { saveStatus.Text = "Saving and verifying transfer…"; return; }
            if (changingDatabase) { saveStatus.Text = "Closing database safely…"; return; }
            if (saveTask != null) { saveStatus.Text = "Validating and saving database…"; return; }
            if (saveBlocked) { saveStatus.Text = "Shared save failed · Ctrl+S to retry"; saveStatus.Foreground = Brush("#B05447"); return; }
            if (AnyDirty) saveStatus.Text = LoadedSessions.Where(s => s.Dirty).All(s => s.DraftRevision == s.Revision) ? "Unsaved changes · encrypted draft protected · Ctrl+S to publish" : "Unsaved changes · Ctrl+S to publish";
            else saveStatus.Text = "Database saved · lock after " + lockMinutes + " min";
        }
        private void ClearViewsAndClipboard()
        {
            hoverTimer.Stop(); hoverGroup = null; hoverPane = null; dragging = null; pendingField = null;
            foreach (BrowserPane pane in panes) ClearText(pane.Columns);
            workspace.Children.Clear(); panes.Clear();
            ClearOwnedClipboard(true);
        }
        private void ClearText(DependencyObject element)
        {
            var revealed = element as SecretInput; if (revealed != null) { revealed.Clear(); return; }
            var secret = element as PasswordBox; if (secret != null) { secret.Clear(); return; }
            var input = element as TextBox;
            if (input != null) { input.IsUndoEnabled = false; input.Clear(); return; }
            foreach (object child in LogicalTreeHelper.GetChildren(element))
            { var dependency = child as DependencyObject; if (dependency != null) ClearText(dependency); }
        }
        private async Task Recovery(VaultSession recovering = null)
        {
            if (recovering == null) recovering = session;
            if (recovering == null || locked || sealing || transferring) return;
            if (draftTask != null) await draftTask;
            if (session == null || locked || sealing) return;
            if (recovering.HasPendingMerge && !await BringInChanges(recovering)) return;
            var d = new SaveFileDialog { Title = "Save complete encrypted recovery database", Filter = "KeePass database|*.kdbx", FileName = System.IO.Path.GetFileNameWithoutExtension(recovering.PathName) + "-recovery-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".kdbx", AddExtension = true, DefaultExt = ".kdbx", OverwritePrompt = true };
            if (d.ShowDialog(this) != true) return;
            if (session == null || locked || sealing) return;
            if (File.Exists(d.FileName)) { Toast("Choose a new filename. Recovery copies never overwrite another database.", true); return; }
            sealing = true; workspace.IsEnabled = false;
            try
            {
                if (saveTask != null) await saveTask;
                if (draftTask != null) await draftTask;
                await recovering.RecoveryCopyAsync(d.FileName);
                foreach (var other in LoadedSessions.Where(s => s != recovering && s.Dirty)) { var checkpoint = await other.CheckpointAsync(); other.AcceptDraft(checkpoint); }
                selectedPath = d.FileName; pathBox.Text = selectedPath;
                DisposeSession();
                unlockTitle.Text = "Recovery copy saved"; unlockHint.Text = "Your complete database is saved. Unlock this copy to continue.";
                unlock.Visibility = Visibility.Visible; saveBlocked = false; recoveryButton.Visibility = Visibility.Collapsed;
                Toast("Complete encrypted recovery copy saved.", false);
            }
            catch { Toast("Recovery copy could not be saved. Your working data is still in memory.", true); }
            finally { sealing = false; UpdateInteraction(); SetStatus(); if (closeAfterSeal) { closeAfterSeal = false; if (session == null) QueueClose(); else { var closeRequest = Dispatcher.BeginInvoke(new Action(() => Close())); } } }
        }
        private void OnKey(object sender, KeyEventArgs e)
        {
            Activity();
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 && e.Key == Key.S) { e.Handled = true; if (!locked) { var unused = SaveNow(true); } }
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 && e.Key == Key.L) { e.Handled = true; var unused = Seal(false); }
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 && e.Key == Key.Z && !locked) { e.Handled = true; Undo(); }
            if (e.Key == Key.Escape && veil.Visibility == Visibility.Visible && !protectionFailure) { FinishReview(MergeChoice.Cancel); CompleteChoice(0); CloseModal(); }
        }
        private void Toast(string message, bool error)
        {
            toastSave.Visibility = Visibility.Collapsed;
            toast.Background = Brush(error ? "#EFF4FF" : "#396BD7"); toast.BorderBrush = Brush(error ? "#7198E8" : "#2858B8"); toastText.Foreground = error ? Brush("#244A91") : Brushes.White; toastText.Text = message; toast.Visibility = Visibility.Visible;
            System.Windows.Automation.AutomationProperties.SetLiveSetting(toast, System.Windows.Automation.AutomationLiveSetting.Polite);
            toastTimer.Stop(); toastTimer.Interval = TimeSpan.FromSeconds(error ? 11 : 4); toastTimer.Start();
        }
        public void ReportUnexpectedError() { Toast("That action could not finish. Your database has not been discarded. Save or retry before closing.", true); }

        private Border Dialog(string title, StackPanel body)
        {
            veil.Children.Clear(); veil.Visibility = Visibility.Visible; UpdateInteraction(); KeyboardNavigation.SetTabNavigation(veil, KeyboardNavigationMode.Cycle);
            var card = new Border { Width = 410, MaxHeight = Math.Max(300, Height - 160), Padding = new Thickness(27), Background = Brushes.White, CornerRadius = new CornerRadius(12), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(20, 90, 20, 20) };
            var content = new StackPanel(); var heading = Text(title, 19, "#25334B"); heading.Margin = new Thickness(0, 0, 0, 19); content.Children.Add(heading); content.Children.Add(body); card.Child = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }; veil.Children.Add(card); EnsureTooltips(veil); return card;
        }
        private void PromptName(string title, string initial, Action<string> accept)
        {
            var body = new StackPanel(); var input = new TextBox { Text = initial == "Group name" ? "" : initial, MaxLength = 200 }; body.Children.Add(input);
            Action submit = () => { string value = input.Text.Trim(); if (value.Length == 0) { input.Focus(); return; } CloseModal(); accept(value); };
            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 17, 0, 0) };
            row.Children.Add(Button("Cancel", CloseModal, "Quiet")); var confirm = Button("Save", submit, "Primary"); confirm.Margin = new Thickness(8, 0, 0, 0); row.Children.Add(confirm); body.Children.Add(row); Dialog(title, body);
            input.KeyDown += (s, e) => { if (e.Key == Key.Enter) { e.Handled = true; submit(); } }; input.Focus(); input.SelectAll();
        }
        private void ShowSettings()
        {
            if (locked) return;
            var body = new StackPanel(); body.Children.Add(Text("ENCRYPTED DRAFT CHECKPOINT (SECONDS)", 10, "#8390A3"));
            var save = new ComboBox { ItemsSource = new[] { 15, 30 }, SelectedItem = saveSeconds, Margin = new Thickness(0, 7, 0, 18) }; body.Children.Add(save);
            UiVisual.Help(save, "How often unpublished edits are protected in a private encrypted checkpoint. This does not save the shared database.");
            var saveOptions=new Style(typeof(ComboBoxItem),(Style)Application.Current.FindResource(typeof(ComboBoxItem)));saveOptions.Setters.Add(new Setter(FrameworkElement.ToolTipProperty,new System.Windows.Data.Binding("Content"){RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.Self),StringFormat="Protect unpublished edits every {0} seconds."}));save.ItemContainerStyle=saveOptions;
            body.Children.Add(Text("LOCK AFTER INACTIVITY (MINUTES)", 10, "#8390A3"));
            var idle = new ComboBox { ItemsSource = new[] { 1, 2, 5, 10, 15 }, SelectedItem = lockMinutes, Margin = new Thickness(0, 7, 0, 21) }; body.Children.Add(idle);
            UiVisual.Help(idle, "Minutes without activity before databases are safely locked and released.");
            var idleOptions=new Style(typeof(ComboBoxItem),(Style)Application.Current.FindResource(typeof(ComboBoxItem)));idleOptions.Setters.Add(new Setter(FrameworkElement.ToolTipProperty,new System.Windows.Data.Binding("Content"){RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.Self),StringFormat="Lock after {0} minutes without activity."}));idle.ItemContainerStyle=idleOptions;
            var done = Button("Done", () => { saveSeconds = (int)save.SelectedItem; lockMinutes = (int)idle.SelectedItem; WriteSettings(); CloseModal(); Activity(); SetStatus(); }, "Primary"); body.Children.Add(done); Dialog("Settings", body);
        }
        private void ReadSettings()
        {
            try { var values = File.ReadAllLines(settingsPath); int a, b; if (values.Length > 1 && Int32.TryParse(values[0], out a) && Int32.TryParse(values[1], out b)) { if (a == 15 || a == 30) saveSeconds = a; if (new[] { 1, 2, 5, 10, 15 }.Contains(b)) lockMinutes = b; } } catch { }
        }
        private void WriteSettings()
        {
            try { File.WriteAllLines(settingsPath, new[] { saveSeconds.ToString(), lockMinutes.ToString() }); } catch { Toast("Settings apply for this session; the app folder is not writable.", true); }
        }

    }
}
