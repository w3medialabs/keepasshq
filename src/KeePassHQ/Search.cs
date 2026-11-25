using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KeePassLib;

namespace VaultOrganizer
{
    internal sealed partial class OrganizerWindow
    {
        private FrameworkElement SearchBar(BrowserPane pane, string query)
        {
            var frame = new Border { Margin = new Thickness(10, 0, 10, 10), CornerRadius = new CornerRadius(6), BorderBrush = Brush("#DDE4ED"), BorderThickness = new Thickness(1), Background = Brushes.White };
            var row = new Grid(); row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) }); row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); frame.Child = row;
            row.Children.Add(UiVisual.Icon("search", "#8795AA", 14));
            var input = new TextBox { Text = query ?? "", MinHeight = 34, FontSize = 12, Padding = new Thickness(3, 7, 3, 7), BorderThickness = new Thickness(0), Background = Brushes.Transparent, MaxLength = 200, IsUndoEnabled = false };
            pane.Search = input; pane.SearchActive = !String.IsNullOrWhiteSpace(input.Text);
            var editor = new Grid(); editor.Children.Add(input); Grid.SetColumn(editor, 1); row.Children.Add(editor);
            var hint = Text("Search this database…", 12, "#8996A9"); hint.Margin = new Thickness(3, 0, 0, 0); hint.IsHitTestVisible = false; editor.Children.Add(hint);
            var clear = IconButton("close", "Clear search · restore this browser's folders", () => { input.Clear(); input.Focus(); }, 12);
            clear.Width = clear.MinWidth = 26; clear.Height = clear.MinHeight = 28; clear.Padding = new Thickness(6); clear.Margin = new Thickness(3); Grid.SetColumn(clear, 2); row.Children.Add(clear);
            UiVisual.Help(input, "Search names, usernames, URLs, notes and group paths. All words must match. Password fields and hidden fields are excluded. Enter refreshes; Escape clears.");
            System.Windows.Automation.AutomationProperties.SetName(input, "Search this browser's database");
            Action updateHint = () => { bool empty = input.Text.Length == 0; hint.Visibility = empty ? Visibility.Visible : Visibility.Collapsed; clear.Visibility = empty ? Visibility.Collapsed : Visibility.Visible; };
            input.TextChanged += (sender, args) =>
            {
                updateHint(); if (pane.SuppressSearch || pane.Scroll == null || !panes.Contains(pane)) return;
                bool active = !String.IsNullOrWhiteSpace(input.Text);
                bool wasActive = pane.SearchActive;
                if (active && !pane.SearchActive) pane.BrowseOffset = pane.Scroll.HorizontalOffset;
                pane.SearchActive = active; pane.SearchExpanded = null; pendingField = null;
                RenderPane(pane); if (active) pane.Scroll.ScrollToHorizontalOffset(0); else if (wasActive) pane.Scroll.ScrollToHorizontalOffset(pane.BrowseOffset); Activity();
            };
            input.KeyDown += (sender, args) =>
            {
                if (args.Key == Key.Escape) { input.Clear(); args.Handled = true; }
                else if (args.Key == Key.Enter) { RenderPane(pane); args.Handled = true; }
            };
            input.GotKeyboardFocus += (sender, args) => frame.BorderBrush = Brush("#7198ED");
            input.LostKeyboardFocus += (sender, args) => frame.BorderBrush = Brush("#DDE4ED");
            updateHint(); return frame;
        }
        private static void ClearSearchInput(BrowserPane pane)
        {
            pane.SuppressSearch = true;
            try { if (pane.Search != null) pane.Search.Clear(); pane.SearchExpanded = null; pane.SearchActive = false; }
            finally { pane.SuppressSearch = false; }
        }
        private static string ExpandedEntry(BrowserPane pane) { return pane.SearchActive ? pane.SearchExpanded : pane.Expanded; }
        private static string GroupPath(PwGroup group)
        {
            var names = new List<string>(); for (var current = group; current != null; current = current.ParentGroup) names.Add(current.Name);
            names.Reverse(); return String.Join(" / ", names);
        }
        private static List<PwEntry> SearchEntries(BrowserPane pane)
        {
            string[] terms = pane.Search.Text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            var result = new List<PwEntry>();
            foreach (var entry in pane.Root.GetEntries(true))
            {
                if (!entry.ParentGroup.GetSearchingEnabledInherited()) continue;
                bool recycled = false;
                for (var group = entry.ParentGroup; group != null; group = group.ParentGroup)
                    if (pane.Session.Database.RecycleBinEnabled && group.Uuid.Equals(pane.Session.Database.RecycleBinUuid)) { recycled = true; break; }
                if (recycled) continue;
                var values = new[] { VaultSession.Read(entry, PwDefs.TitleField), VaultSession.Read(entry, PwDefs.UserNameField), VaultSession.Read(entry, PwDefs.UrlField), VaultSession.Read(entry, PwDefs.NotesField), GroupPath(entry.ParentGroup) };
                if (terms.All(term => values.Any(value => value.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0))) result.Add(entry);
            }
            return result;
        }
        private void RenderSearch(BrowserPane pane, int recordCount)
        {
            var matches = SearchEntries(pane);
            pane.Count.Text = matches.Count + (matches.Count == 1 ? " match" : " matches") + " · " + recordCount + (recordCount == 1 ? " record" : " records");
            var column = new Grid { Width = 280, Margin = new Thickness(0, 0, 8, 0) };
            column.RowDefinitions.Add(new RowDefinition { Height = new GridLength(36) }); column.RowDefinitions.Add(new RowDefinition());
            var frame = new Border { BorderBrush = Brush("#E1E7F0"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(7), IsHitTestVisible = false }; Grid.SetRowSpan(frame, 2); column.Children.Add(frame);
            var caption = Text("SEARCH RESULTS", 10, "#8390A3"); caption.FontWeight = FontWeights.SemiBold; caption.HorizontalAlignment = HorizontalAlignment.Center; column.Children.Add(caption);
            var cards = new StackPanel { Margin = new Thickness(7, 0, 7, 8) };
            foreach (var entry in matches)
            {
                var found = entry; string path = GroupPath(entry.ParentGroup);
                var location = Button("", () => BrowseSearchResult(pane, found), "Quiet"); location.MinHeight = 20; location.Padding = new Thickness(3, 2, 3, 2); location.Margin = new Thickness(0, 5, 0, 1); location.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                var label = Text(path, 10, "#7D8A9E"); label.TextTrimming = TextTrimming.CharacterEllipsis; location.Content = label; UiVisual.Help(location, "Browse this entry's group: " + path); cards.Children.Add(location);
                cards.Children.Add(EntryCard(pane, entry));
            }
            if (matches.Count == 0)
            {
                var empty = Text("No matching entries.\nTry another word or clear search.", 12, "#8996A9"); empty.TextAlignment = TextAlignment.Center; empty.TextWrapping = TextWrapping.Wrap; empty.Margin = new Thickness(10, 30, 10, 0); cards.Children.Add(empty);
            }
            var scroll = new ScrollViewer { Content = cards, Margin = new Thickness(1), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Style = (Style)Application.Current.FindResource(typeof(ScrollViewer)) };
            Grid.SetRow(scroll, 1); column.Children.Add(scroll); pane.Columns.Children.Add(column); EnsureTooltips(pane.Layout);
        }
        private void BrowseSearchResult(BrowserPane pane, PwEntry entry)
        {
            if (SessionFor(entry) != pane.Session) return;
            var path = new List<PwGroup>(); for (var group = entry.ParentGroup; group != null; group = group.ParentGroup) path.Add(group); path.Reverse();
            pane.Path.Clear(); pane.Path.AddRange(path); pane.Expanded = entry.Uuid.ToHexString(); pane.Search.Clear();
            pane.Scroll.Dispatcher.BeginInvoke(new Action(() => pane.Scroll.ScrollToRightEnd()), System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }
}
