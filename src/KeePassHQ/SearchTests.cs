#if DIAGNOSTICS
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using KeePassLib;
using KeePassLib.Security;

namespace VaultOrganizer
{
    internal sealed partial class OrganizerWindow
    {
        internal async Task TestBrowserSearch(string directory, Action<bool, string> check)
        {
            heartbeat.Stop(); testClose = true; clipboard = new IsolatedClipboard(); Directory.CreateDirectory(directory);
            string path = Tests.CreateFixture(directory, false);
            Tests.WriteOutside(path, db => db.RootGroup.GetEntries(true).First().History.First().Strings.Set(PwDefs.TitleField, new ProtectedString(false, "history-only-search-marker")));
            LoadSynthetic(path, Path.Combine(directory, "drafts")); SwitchMode(true);
            var left = panes[0]; var right = panes[1]; var imports = session.Database.RootGroup.Groups.First(group => group.Name == "Imported notes");
            var rich = imports.Entries.First(); string id = rich.Uuid.ToHexString();
            left.Path.Add(imports); left.Expanded = id; RenderPane(left); var originalPath = left.Path.ToArray();
            long revision = session.Revision; byte[] original = VaultSession.HashFile(path);
            left.Search.Text = "EMAIL ALEX";
            check(left.SearchActive && left.EntryHeaders.Count == 1 && left.EntryHeaders.ContainsKey(id), "Browser search matches all literal terms across fields without case sensitivity");
            check(!right.SearchActive && right.Search.Text.Length == 0 && right.Path.Count == 1, "Searching one browser leaves the other browser's query and navigation independent");
            left.Search.Text = "fictional-original-123";
            check(left.EntryHeaders.Count == 0, "Password values are not searched");
            left.Search.Text = "fictional recovery data";
            check(left.EntryHeaders.Count == 0, "Hidden custom values are not searched");
            left.Search.Text = "history-only-search-marker";
            check(left.EntryHeaders.Count == 0, "Historical entry values are not searched");
            left.Search.Text = "Garden shop";
            check(left.EntryHeaders.Count == 1 && left.EntryHeaders.ContainsKey(imports.Entries.Last().Uuid.ToHexString()), "Notes are searchable without opening each entry");
            var subscriptions = session.Database.RootGroup.Groups.First(group => group.Name == "Internet").Groups.First(); var reader = subscriptions.Entries.First();
            left.Search.Text = "Subscriptions";
            check(left.EntryHeaders.Count == 1 && left.EntryHeaders.ContainsKey(reader.Uuid.ToHexString()), "Search reaches nested entries by group path outside the current browser path");
            right.Search.Text = "Email accounts";
            check(right.EntryHeaders.ContainsKey(id) && !right.EntryHeaders.ContainsKey(reader.Uuid.ToHexString()) && left.EntryHeaders.ContainsKey(reader.Uuid.ToHexString()), "Two browsers can show different searches in the same database");
            Press(Descendants<Button>(left.Layout).First(button => Convert.ToString(button.ToolTip).StartsWith("Clear search", StringComparison.Ordinal)));
            check(!left.SearchActive && left.Path.SequenceEqual(originalPath) && left.Expanded == id && right.SearchActive, "Clearing search restores that browser's original path and expanded card");
            left.Search.Text = "Email"; left.Search.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, new SyntheticInputSource(), 0, Key.Escape) { RoutedEvent = Keyboard.KeyDownEvent });
            check(left.Search.Text.Length == 0 && !left.SearchActive && left.Path.SequenceEqual(originalPath), "Escape clears the focused search and restores folder browsing");
            left.Search.Text = "   \t "; check(!left.SearchActive && left.Path.SequenceEqual(originalPath), "Whitespace-only queries keep ordinary browsing");
            left.Search.Text = "[no-such-.*]"; check(left.EntryHeaders.Count == 0, "Search treats expression syntax as literal text");
            left.Search.Text = "Subscriptions";
            var location = Descendants<Button>(left.Columns).First(button => Convert.ToString(button.ToolTip).StartsWith("Browse this entry's group:", StringComparison.Ordinal)); Press(location);
            check(!left.SearchActive && left.Path.Last() == subscriptions && left.Expanded == reader.Uuid.ToHexString(), "A result's group link returns to its actual folder and expands the entry");
            check(session.Revision == revision && original.SequenceEqual(VaultSession.HashFile(path)), "Searching, clearing and locating results do not change the working database or file");
            var internet = subscriptions.ParentGroup; internet.EnableSearching = false; session.Changed(); left.Search.Text = "reader";
            check(left.EntryHeaders.Count == 0, "Search honors inherited disabled-group search settings");
            subscriptions.EnableSearching = true; session.Changed(); left.Search.Text = "subscription";
            check(left.EntryHeaders.ContainsKey(reader.Uuid.ToHexString()), "An explicit enabled child search setting overrides its inherited parent setting");
            var recycle = session.Database.RootGroup.FindGroup(session.Database.RecycleBinUuid, true); var deleted = session.NewEntry(recycle); session.Set(deleted, PwDefs.TitleField, "recycled-search-marker");
            left.Search.Text = "recycled-search-marker"; check(left.EntryHeaders.Count == 0, "Enabled recycle-bin contents are excluded from search results");
            left.Search.Text = "Email accounts"; right.Search.Text = "Email accounts"; Press(left.EntryHeaders[id]); Press(right.EntryHeaders[id]);
            var name = FieldBox(left, rich, PwDefs.TitleField); name.Text = "Renamed search result";
            check(FieldBox(right, rich, PwDefs.TitleField).Text == name.Text && left.EntryHeaders.ContainsKey(id) && right.EntryHeaders.ContainsKey(id), "Search results retain live mirrored editing without removing a card mid-edit");
            PressEnter(left.Search); check(left.EntryHeaders.Count == 0, "Enter refreshes results after editing a matching value");
            left.Search.Text = "Renamed"; right.Search.Text = "reader"; var oldLeft = left.Search; var oldRight = right.Search; BuildPanes();
            check(panes[0].Search.Text == "Renamed" && panes[1].Search.Text == "reader" && panes[0].Path.Last().Uuid.Equals(subscriptions.Uuid) && panes[0].Expanded == reader.Uuid.ToHexString() && oldLeft.Text.Length == 0 && oldRight.Text.Length == 0, "Browser rebuilds preserve same-database queries and browsing context and clear detached query controls");
            SwitchMode(false); SwitchMode(true);
            check(panes[0].Search.Text == "Renamed" && panes[1].Search.Text == "reader" && panes[1].EntryHeaders.ContainsKey(reader.Uuid.ToHexString()), "Viewer/Organizer switching retains independent searches and rebuilds hidden results");
            check(await SaveNow(true) && panes[0].Search.Text == "Renamed", "Saving edited search results preserves the query and uses normal publication");
            string otherRoot = Path.Combine(directory, "second"); Directory.CreateDirectory(otherRoot); string otherPath = Tests.CreateFixture(otherRoot, false);
            otherSession = VaultSession.Open(otherPath, VaultSession.MakeKey("demo", true, null, false)); otherSession.ConfigureDraftRoot(Path.Combine(otherRoot, "drafts")); BuildPanes();
            check(panes[0].Search.Text == "Renamed" && panes[1].Search.Text.Length == 0, "Changing a browser's database resets only that browser's query");
            panes[1].Search.Text = "Email accounts";
            check(panes[1].EntryHeaders.Count == 1 && !panes[1].EntryHeaders.ContainsKey(id) && panes[0].EntryHeaders.ContainsKey(id), "Each query stays within its browser's separately keyed database session");
            check(await ClosePane(panes[1]) && panes[0].Search.Text == "Renamed", "Closing another database retains the remaining browser's query");
            oldLeft = panes[0].Search; oldRight = panes[1].Search; oldRight.Text = "fictional-private-query"; await Seal(false);
            check(session == null && oldLeft.Text.Length == 0 && oldRight.Text.Length == 0 && !oldLeft.IsUndoEnabled && !oldRight.IsUndoEnabled, "Lock clears both transient search queries and their undo buffers");
            StopTimers(); Microsoft.Win32.SystemEvents.SessionSwitch -= OnSystemSession;
        }
    }
}
#endif
