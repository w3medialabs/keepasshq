#if DIAGNOSTICS
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using KeePassLib;

namespace VaultOrganizer
{
    internal sealed class HelpClipboardProbe : IClipboardService
    {
        internal string Value; internal int Reads, Writes; internal bool FailWrite;
        public string Read() { ++Reads; return Value; }
        public void Write(string text) { if (FailWrite) throw new IOException("Fictional busy clipboard"); ++Writes; Value = text; }
        public void Clear() { Value = null; }
    }
    internal sealed partial class OrganizerWindow
    {
        private void HelpKey(UIElement target, Key key)
        { target.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, new SyntheticInputSource(), 0, key) { RoutedEvent = Keyboard.PreviewKeyDownEvent }); }
        private Button HelpButton(string label)
        { return Descendants<Button>((DependencyObject)helpWindow.Content).Single(button => button.Content as string == label); }
        internal async Task TestOfflineHelp(string directory, Action<bool, string> check)
        {
            heartbeat.Stop(); testClose = true; var probe = new HelpClipboardProbe { Value = "Fictional prior clipboard" }; clipboard = probe;
            password.Password = "fictional-main-help-factor";
            Press(Descendants<Button>(unlock).Single(button => button.Content as string == "Help & documentation · F1"));
            var guide = helpWindow;
            check(guide != null && !guide.IsVisible && session == null && locked, "Offline Help opens from the locked screen without a native window or database");
            check(password.Password == "fictional-main-help-factor" && probe.Writes == 0 && probe.Reads == 0 && probe.Value == "Fictional prior clipboard", "Opening Help leaves key inputs and clipboard untouched");
            check(helpTopics.Any(topic => topic.Title == "Searching") && helpTopics.Any(topic => topic.Title == "Recovery and conflicts") && helpTopics.Any(topic => topic.Title == "Report a problem") && helpTopics.All(topic => topic.Paragraphs.Count > 0), "Embedded documentation supplies populated workflow, recovery and reporting topics");
            HelpKey(this, Key.F1); check(helpWindow == guide, "Repeated F1 reuses the existing Help window");
            guide.Close(); check(helpWindow == null && password.Password == "fictional-main-help-factor", "Closing Help leaves the application and unlock factor intact");
            DatabaseDialog(null, true); var secret = Descendants<PasswordBox>(veil).First(); secret.Password = "fictional-modal-help-factor"; object modal = veil.Children[0];
            HelpKey(this, Key.F1);
            check(helpWindow != null && veil.Children[0] == modal && secret.Password == "fictional-modal-help-factor", "F1 does not replace or clear a key-factor dialog");
            helpWindow.Close(); CloseModal();
            Task<int> choice = Ask("Fictional pending choice", "Fictional data only", "Cancel", "Keep", "Use"); modal = veil.Children[0]; HelpKey(this, Key.F1);
            check(!choice.IsCompleted && modalChoice != null && veil.Children[0] == modal, "Opening Help preserves an unresolved application choice");
            HelpKey(helpWindow, Key.Escape);
            check(helpWindow == null && !choice.IsCompleted && veil.Children[0] == modal, "Escape in Help closes only Help and preserves the underlying decision");
            CompleteChoice(0); await choice;
            Task<MergeChoice> conflict = ReviewConflict(new MergeConflict { Title = "Fictional pending conflict" }); modal = veil.Children[0]; HelpKey(this, Key.F1);
            check(!conflict.IsCompleted && mergeChoice != null && veil.Children[0] == modal, "Help does not cancel or replace pending conflict review");
            helpWindow.Close(); FinishReview(MergeChoice.Cancel); await conflict; password.Clear();
            Directory.CreateDirectory(directory); string path = Tests.CreateFixture(directory, false); LoadSynthetic(path, Path.Combine(directory, "drafts"));
            var entry = session.Database.RootGroup.GetEntries(true).First(); session.Set(entry, PwDefs.TitleField, "private-entry-help-token"); session.Set(entry, PwDefs.NotesField, "private-note-help-token");
            panes[0].Search.Text = "private-query-help-token"; password.Password = "private-factor-help-token"; keyBox.Text = Path.Combine(directory, "fictional-unread-key.key");
            long revision = session.Revision; byte[] original = VaultSession.HashFile(path); ShowHelp(); guide = helpWindow;
            Press(HelpButton("Report a problem")); long idle = lastActivityTicks;
            Press(HelpButton("Copy report template")); string report = probe.Value;
            check(report.Contains("KeePassHQ version: " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version) && report.Contains("Steps using a disposable fictional database"), "Manual report template includes the application version and blank fictional-reproduction prompts");
            check(!new[] { path, "Demo.kdbx", "private-entry-help-token", "private-note-help-token", "private-query-help-token", "private-factor-help-token", keyBox.Text }.Any(report.Contains), "Report preparation excludes loaded database, entry, query, key-factor and path data");
            check(probe.Reads == 0 && lastActivityTicks == idle, "Report copying does not inspect the clipboard or extend vault inactivity");
            Press(HelpButton("Copy issue link")); var issue = new Uri(probe.Value);
            check(issue.Scheme == "https" && issue.Host == "github.com" && issue.AbsolutePath == "/w3medialabs/keepasshq/issues/new" && issue.Query == "?template=bug_report.md", "Issue link is fixed and contains no state-derived body or URL parameters");
            Press(HelpButton("Copy private security link")); var security = new Uri(probe.Value);
            check(security.AbsoluteUri == SecurityLink && security.Query.Length == 0, "Security reporting has a distinct fixed private-channel link");
            string retained = probe.Value; probe.FailWrite = true; Press(HelpButton("Copy report template")); probe.FailWrite = false;
            check(probe.Value == retained && helpStatus.Text.Contains("Clipboard busy"), "A busy clipboard reports failure without substituting or sending report data");
            check(session.Revision == revision && original.SequenceEqual(VaultSession.HashFile(path)) && probe.Reads == 0, "Help and reporting do not change working data, publish the database or read clipboard content");
            var reader = Descendants<RichTextBox>((DependencyObject)guide.Content).Single();
            check(reader.IsReadOnly && !reader.IsUndoEnabled && !new TextRange(reader.Document.ContentStart, reader.Document.ContentEnd).Text.Contains("private-entry-help-token"), "Help text is read-only embedded documentation, not a view of the live database");
            Press(HelpButton("Copy report template")); clipboardAtTicks = System.Diagnostics.Stopwatch.GetTimestamp() - 40 * System.Diagnostics.Stopwatch.Frequency; ExpireClipboard();
            check(probe.Value == null && clipboardHash == null, "Copied report templates use normal app-owned clipboard timeout cleanup");
            await Seal(false);
            check(session == null && helpWindow == guide && ReportTemplate() == report && password.Password.Length == 0, "The vault can protect and lock while static Help stays open with an unchanged state-free report template");
            guide.Close(); StopTimers(); Microsoft.Win32.SystemEvents.SessionSwitch -= OnSystemSession;
        }
    }
}
#endif
