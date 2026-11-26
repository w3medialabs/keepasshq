using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace VaultOrganizer
{
    internal sealed class HelpTopic
    {
        internal string Title;
        internal readonly List<string> Paragraphs = new List<string>();
    }
    internal sealed partial class OrganizerWindow
    {
        private const string IssueLink = "https://github.com/w3medialabs/keepasshq/issues/new?template=bug_report.md";
        private const string SecurityLink = "https://github.com/w3medialabs/keepasshq/security/advisories/new";
        private Window helpWindow;
        private List<HelpTopic> helpTopics;
        private Action<int> selectHelpTopic;
        private TextBlock helpStatus;

        private static List<HelpTopic> ReadHelpTopics()
        {
            var topics = new List<HelpTopic>(); HelpTopic topic = null; var paragraph = new StringBuilder();
            Action flush = () => { if (topic != null && paragraph.Length > 0) topic.Paragraphs.Add(paragraph.ToString()); paragraph.Clear(); };
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Help.md"))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.StartsWith("## ", StringComparison.Ordinal)) { flush(); topic = new HelpTopic { Title = line.Substring(3).Trim() }; topics.Add(topic); }
                    else if (topic != null)
                    {
                        if (String.IsNullOrWhiteSpace(line)) flush();
                        else if (line.StartsWith("- ", StringComparison.Ordinal)) { flush(); topic.Paragraphs.Add(line); }
                        else { if (paragraph.Length > 0) paragraph.Append(' '); paragraph.Append(line.Trim()); }
                    }
                }
            }
            flush(); if (topics.Count == 0) throw new InvalidDataException("Embedded help is empty."); return topics;
        }
        private static Paragraph HelpParagraph(string value)
        {
            bool bullet = value.StartsWith("- ", StringComparison.Ordinal); if (bullet) value = "• " + value.Substring(2);
            var paragraph = new Paragraph { Margin = new Thickness(bullet ? 12 : 0, 0, 0, 12), LineHeight = 20 };
            int start = 0;
            foreach (Match match in Regex.Matches(value, @"(\*\*[^*]+\*\*|`[^`]+`)"))
            {
                if (match.Index > start) paragraph.Inlines.Add(new Run(value.Substring(start, match.Index - start)));
                if (match.Value.StartsWith("**", StringComparison.Ordinal)) paragraph.Inlines.Add(new Bold(new Run(match.Value.Substring(2, match.Value.Length - 4))));
                else paragraph.Inlines.Add(new Run(match.Value.Substring(1, match.Value.Length - 2)) { FontFamily = new FontFamily("Consolas"), FontSize = 12 });
                start = match.Index + match.Length;
            }
            if (start < value.Length) paragraph.Inlines.Add(new Run(value.Substring(start))); return paragraph;
        }
        private Button HelpAction(string label, string tip, Action action, bool primary = false)
        {
            // Help navigation does not extend the vault's inactivity timer.
            var button = new Button { Content = label, Style = (Style)Application.Current.FindResource(primary ? "Primary" : "Quiet"), MinHeight = 32, Padding = new Thickness(10, 7, 10, 7), FontSize = 12 };
            UiVisual.Help(button, tip); System.Windows.Automation.AutomationProperties.SetName(button, label); button.Click += (sender, args) => action(); return button;
        }
        private static string ReportTemplate()
        {
            // An allowlist of one nonsecret value. Never inspect the session, OS/account,
            // files, exceptions, query controls or clipboard to prepare a report.
            return "KeePassHQ version: " + Assembly.GetExecutingAssembly().GetName().Version + "\n"
                + "Build/commit (optional, enter manually):\nWindows/display scale (enter manually):\n\n"
                + "Problem:\n\nSteps using a disposable fictional database:\n1.\n2.\n\nExpected behavior:\n\nObserved behavior:\n\n"
                + "Before submitting: review every line. GitHub Issues are public. Do not include real vaults, factors, drafts, backups, personal paths, entry data, logs, or screenshots of actual secrets. Use the private security channel for vulnerabilities.\n";
        }
        private void CopyHelpText(string text, string message)
        {
            try { clipboard.Write(text); OwnClipboard(text); if (helpStatus != null) helpStatus.Text = message; }
            catch { if (helpStatus != null) helpStatus.Text = "Clipboard busy. Try again. Nothing was sent to GitHub."; }
        }
        private Window CreateHelpWindow()
        {
            if (helpWindow != null) return helpWindow;
            helpTopics = ReadHelpTopics();
            var window = new Window { Title = "KeePassHQ help", Width = Math.Min(940, SystemParameters.WorkArea.Width * 0.9), Height = Math.Min(760, SystemParameters.WorkArea.Height * 0.9), MinWidth = 740, MinHeight = 520, WindowState = WindowState.Normal, ShowInTaskbar = false, Background = Brushes.White };
            if (IsLoaded) { window.Owner = this; window.WindowStartupLocation = WindowStartupLocation.CenterOwner; } else window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            var root = new Grid { Background = Brushes.White }; root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); root.RowDefinitions.Add(new RowDefinition()); root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); window.Content = root;
            var header = new DockPanel { Margin = new Thickness(20, 14, 20, 14) };
            var close = HelpAction("Close help", "Close only Help and return to the application.", () => window.Close()); DockPanel.SetDock(close, Dock.Right); header.Children.Add(close);
            var title = new StackPanel(); title.Children.Add(Text("KeePassHQ help", 23, "#25334B")); title.Children.Add(Text("Offline guide · version " + Assembly.GetExecutingAssembly().GetName().Version, 11, "#7D8A9E")); header.Children.Add(title); root.Children.Add(header);
            var body = new Grid { Margin = new Thickness(14, 0, 20, 0) }; body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) }); body.ColumnDefinitions.Add(new ColumnDefinition()); Grid.SetRow(body, 1); root.Children.Add(body);
            var navigation = new StackPanel { Margin = new Thickness(0, 5, 12, 0) };
            var navigationScroll = new ScrollViewer { Content = navigation, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled }; body.Children.Add(navigationScroll);
            var page = new Grid { Margin = new Thickness(14, 5, 0, 0) }; page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); page.RowDefinitions.Add(new RowDefinition()); page.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); Grid.SetColumn(page, 1); body.Children.Add(page);
            var heading = Text("", 21, "#25334B"); heading.Margin = new Thickness(0, 0, 0, 14); page.Children.Add(heading);
            var reader = new RichTextBox { IsReadOnly = true, IsUndoEnabled = false, BorderThickness = new Thickness(0), Background = Brushes.White, Padding = new Thickness(0), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            System.Windows.Automation.AutomationProperties.SetName(reader, "Help topic text"); UiVisual.Help(reader, "Read or select this locally bundled documentation. No online content is loaded."); Grid.SetRow(reader, 1); page.Children.Add(reader);
            var reportActions = new WrapPanel { Margin = new Thickness(0, 12, 0, 4) }; Grid.SetRow(reportActions, 2); page.Children.Add(reportActions);
            foreach (var button in new[] {
                HelpAction("Copy report template", "Copy blank prompts and the application version only. Review your text before posting publicly.", () => CopyHelpText(ReportTemplate(), "Template copied. Fill it with fictional steps and review it before posting."), true),
                HelpAction("Copy issue link", "Copy the fixed public GitHub issue-template link. The application sends no report.", () => CopyHelpText(IssueLink, "Public issue link copied. Paste it into your browser and review before submitting.")),
                HelpAction("Copy private security link", "Copy GitHub's private vulnerability-reporting link. Never upload actual vaults or factors.", () => CopyHelpText(SecurityLink, "Private reporting link copied. Do not include actual vaults or key factors.")) })
            { button.Margin = new Thickness(0, 0, 6, 6); reportActions.Children.Add(button); }
            helpStatus = Text("Offline help · no automatic reports, uploads or browser launches", 11, "#7D8A9E"); helpStatus.TextWrapping = TextWrapping.Wrap; helpStatus.Margin = new Thickness(20, 12, 20, 14); Grid.SetRow(helpStatus, 2); root.Children.Add(helpStatus);
            var topicButtons = new List<Button>();
            selectHelpTopic = index =>
            {
                var topic = helpTopics[index]; heading.Text = topic.Title;
                var document = new FlowDocument { FontFamily = new FontFamily("Segoe UI"), FontSize = 13, Foreground = Brush("#46536A"), PagePadding = new Thickness(0) };
                foreach (string paragraph in topic.Paragraphs) document.Blocks.Add(HelpParagraph(paragraph)); reader.Document = document; reader.ScrollToHome();
                reportActions.Visibility = topic.Title == "Report a problem" ? Visibility.Visible : Visibility.Collapsed;
                for (int i = 0; i < topicButtons.Count; ++i) { topicButtons[i].Background = i == index ? Brush("#EFF4FF") : Brushes.Transparent; topicButtons[i].Foreground = Brush(i == index ? "#2858B8" : "#667891"); }
            };
            for (int i = 0; i < helpTopics.Count; ++i)
            {
                int index = i; var button = HelpAction(helpTopics[i].Title, "Read " + helpTopics[i].Title.ToLowerInvariant() + ".", () => selectHelpTopic(index));
                button.HorizontalContentAlignment = HorizontalAlignment.Left; button.Margin = new Thickness(0, 0, 0, 3); topicButtons.Add(button); navigation.Children.Add(button);
            }
            window.PreviewKeyDown += (sender, args) =>
            {
                if (args.Key == Key.Escape) { args.Handled = true; window.Close(); }
                else if (args.Key == Key.F1) args.Handled = true;
                else if (args.Key == Key.L && (Keyboard.Modifiers & ModifierKeys.Control) != 0) { args.Handled = true; var locking = Seal(false); }
            };
            DataObject.AddCopyingHandler(window, TrackTextCopy);
            window.Closed += (sender, args) => { helpWindow = null; helpTopics = null; selectHelpTopic = null; helpStatus = null; };
            helpWindow = window; selectHelpTopic(0); EnsureTooltips(root); return window;
        }
        private void ShowHelp()
        {
            if (allowClose) return;
            try
            {
                var window = CreateHelpWindow();
#if DIAGNOSTICS
                if (rendering || testClose) return; // Render/test local controls without showing a native window.
#endif
                if (!window.IsVisible) window.Show(); else window.Activate();
            }
            catch { Toast("Help could not be loaded. Documentation is also included in the portable package.", true); }
        }
    }
}
