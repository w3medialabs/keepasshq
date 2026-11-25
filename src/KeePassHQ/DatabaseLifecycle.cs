using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using KeePassLib;
using KeePassLib.Keys;
using KeePassLib.Serialization;
using KeePassLib.Cryptography.KeyDerivation;
using Microsoft.Win32;

namespace VaultOrganizer
{
    internal sealed partial class VaultSession
    {
        public static VaultSession Create(string path, CompositeKey key)
        {
            path = System.IO.Path.GetFullPath(path);
            if (!String.Equals(System.IO.Path.GetExtension(path), ".kdbx", StringComparison.OrdinalIgnoreCase)) throw new IOException("Choose a .kdbx filename.");
            if (File.Exists(path)) throw new IOException("Creating a database never overwrites an existing file.");
            string temp = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path), ".hq-create-" + Guid.NewGuid().ToString("N") + ".tmp");
            var db = new PwDatabase();
            try
            {
                db.New(IOConnectionInfo.FromPath(path), key);
                db.Name = System.IO.Path.GetFileNameWithoutExtension(path); db.RootGroup.Name = db.Name;
                // Explicit production defaults; diagnostic fixtures use cheaper parameters separately.
                var kdf = new Argon2Kdf(Argon2Type.ID);
                db.KdfParameters = kdf.GetDefaultParameters();
                db.KdfParameters.SetUInt64("M", 64UL * 1024 * 1024);
                db.KdfParameters.SetUInt64("I", 3); db.KdfParameters.SetUInt32("P", 2);
                WriteEncrypted(db, temp); VerifyEncrypted(temp, key, db);
#if DIAGNOSTICS
                if (BeforeCreatePublish != null) BeforeCreatePublish(path);
#endif
                File.Move(temp, path); temp = null; // Non-overwriting publication even if a file appeared during encryption.
                return Open(path, key);
            }
            finally { db.Close(); if (temp != null && File.Exists(temp)) { try { File.Delete(temp); } catch { } } }
        }
#if DIAGNOSTICS
        internal static Action<string> BeforeCreatePublish;
#endif
    }

    internal sealed partial class OrganizerWindow
    {
        private VaultSession otherSession;
        private bool transferring, lockAfterTransfer, closeAfterTransfer, changingDatabase, lockAfterChange, closeAfterChange;
        private IEnumerable<VaultSession> LoadedSessions { get { return new[] { session, otherSession }.Where(s => s != null).Distinct(); } }
        private bool AnyDirty { get { return LoadedSessions.Any(s => s.Dirty); } }
        private VaultSession SessionFor(object item)
        {
            PwGroup root = item as PwGroup ?? VaultSession.ParentOf(item);
            int depth = 0; while (root != null && root.ParentGroup != null && ++depth < 65536) root = root.ParentGroup;
            return LoadedSessions.FirstOrDefault(s => s.Database != null && s.Database.RootGroup == root);
        }
        private bool CanDrop(object item, PwGroup target, string before)
        {
            if (item == null || target == null || transferring) return false;
            var source = SessionFor(item); var dest = SessionFor(target);
            if (source == null || dest == null || VaultSession.ParentOf(item) == null) return false;
            if (source == dest) return source.CanPlace(item, target, before);
            return before == null || VaultSession.Ordered(target).Any(x => VaultSession.ItemId(x) == before);
        }
        private static Image BrandImage(double size)
        {
            var bitmap = new BitmapImage();
            using (var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("Brand.png"))
            { bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze(); }
            return new Image { Source = bitmap, Width = size, Height = size, Margin = new Thickness(0, 0, 12, 0) };
        }
        private void UpdateFileLabel()
        {
            fileLabel.Text = String.Join("  ↔  ", LoadedSessions.Select(s => System.IO.Path.GetFileName(s.PathName)));
            fileLabel.ToolTip = String.Join("\n", LoadedSessions.Select(s => s.PathName));
        }
        private async Task<bool> SaveAllWork()
        {
            foreach (var working in LoadedSessions.ToArray())
            {
                if (!await SaveWork(working)) return false;
                if (!working.Dirty) working.RemoveDraft();
            }
            return true;
        }
        private void PaneDatabaseMenu(BrowserPane pane)
        {
            if (opening || sealing || transferring || changingDatabase || saveTask != null) return;
            var menu = new ContextMenu();
            Action<string, Action> add = (label, action) => { var item = new MenuItem { Header = label }; UiVisual.Help(item, label.StartsWith("Save recovery") ? "Save working edits to a new encrypted database without overwriting the original, then close loaded databases." : label); item.Click += (s, e) => action(); menu.Items.Add(item); };
            add("Open database…", () => DatabaseDialog(pane, false));
            add("Create database…", () => DatabaseDialog(pane, true));
            if (otherSession != null) add("Use other pane's database", async () => { await ClosePane(pane); });
            add("Close database", async () => { await ClosePane(pane); });
            add("Save recovery copy…", async () => { await Recovery(pane.Session); });
            menu.PlacementTarget = pane.Layout; menu.IsOpen = true;
        }
        private async Task<bool> ClosePane(BrowserPane pane)
        {
            if (opening || sealing || transferring || changingDatabase || saveTask != null || draftTask != null) return false;
            if (otherSession == null) return await CloseDatabases();
            var removing = pane.Session;
            int choice = removing.Dirty ? await Ask("Close " + System.IO.Path.GetFileName(removing.PathName) + "?", "Save publishes this database. Keep draft closes it without publishing your edits.", "Cancel", "Keep draft", "Save & close") : 1;
            if (choice == 0) return false;
            changingDatabase = editingSuspended = true; UpdateInteraction();
            try
            {
                if (choice == 2) { if (!await SaveWork(removing)) return false; if (!removing.Dirty) removing.RemoveDraft(); }
                else if (removing.Dirty) { var draft = await removing.CheckpointAsync(); removing.AcceptDraft(draft); }
                ClearOwnedClipboard(true); ClearPaneControls(); undo.Clear();
                var remaining = LoadedSessions.Single(s => s != removing);
                removing.Dispose(); session = remaining; otherSession = null; selectedPath = remaining.PathName; pathBox.Text = selectedPath;
                BuildPanes(); UpdateFileLabel(); Toast("Database closed and file released.", false); return !lockAfterChange;
            }
            catch { Toast("Database could not be closed safely. Your edits are still available; retry Save or keep a recovery copy.", true); return false; }
            finally { changingDatabase = editingSuspended = false; UpdateInteraction(); if (lockAfterChange) { bool exit = closeAfterChange; lockAfterChange = closeAfterChange = false; var queued = Dispatcher.BeginInvoke(new Action(async () => { await Seal(exit); })); } }
        }
        private void ClearPaneControls()
        { fieldGuard = true; try { foreach (var pane in panes) ClearText(pane.Columns); } finally { fieldGuard = false; } }
        private async Task<bool> CloseDatabases()
        {
            if (opening || sealing || transferring || protectionFailure || veil.Visibility == Visibility.Visible) return false;
            if (saveTask != null) await saveTask;
            if (session == null) return true;
            if (AnyDirty)
            {
                int answer = await Ask("Close loaded databases?", "Save publishes all loaded databases. Keep draft protects unpublished work and returns to the open screen.", "Cancel", "Keep draft & close", "Save & close");
                if (answer == 0) return false;
                if (answer == 2 && !await SaveNow(true)) return false;
            }
            await Seal(false);
            if (session == null) { unlockTitle.Text = "Open your database"; unlockHint.Text = "Open an existing KeePass database or create a new one."; }
            return session == null;
        }
        private void DatabaseDialog(BrowserPane pane, bool create)
        {
            if (opening || sealing || transferring || protectionFailure || saveTask != null) return;
            int side = pane == null ? -1 : panes.IndexOf(pane);
            var body = new StackPanel();
            body.Children.Add(Text(create ? "NEW DATABASE FILE" : "DATABASE FILE", 10, "#7D8A9E"));
            var location = new TextBox { IsReadOnly = true, Text = create ? "Choose a new .kdbx filename" : "Choose a .kdbx database", Foreground = Brush("#7D8A9E") };
            body.Children.Add(FilePickerRow(location, create ? "Choose an unused filename for the new database." : "Browse to the database you want to open in this pane.", () =>
            {
                if (create) { var picker = new SaveFileDialog { Filter = "KeePass database|*.kdbx", AddExtension = true, DefaultExt = ".kdbx", OverwritePrompt = true }; if (picker.ShowDialog(this) == true) location.Text = picker.FileName; }
                else { var picker = new OpenFileDialog { Filter = "KeePass database|*.kdbx", CheckFileExists = true }; if (picker.ShowDialog(this) == true) location.Text = picker.FileName; }
            }));
            var use = new CheckBox { Content = "Use master password", IsChecked = true, Margin = new Thickness(0, 13, 0, 4) }; body.Children.Add(use);
            var secret = new PasswordBox(); var masterInput=SecretField(secret,"Master password");body.Children.Add(masterInput);
            var confirm = new PasswordBox();
            SecretInput confirmInput=null;
            if (create) { var caption = Text("CONFIRM PASSWORD", 10, "#7D8A9E"); caption.Margin = new Thickness(0, 12, 0, 5); body.Children.Add(caption); confirmInput=SecretField(confirm,"Confirm master password");body.Children.Add(confirmInput); }
            use.Unchecked += (s, e) => { secret.Clear(); confirm.Clear(); secret.IsEnabled = confirm.IsEnabled = false; }; use.Checked += (s, e) => { secret.IsEnabled = confirm.IsEnabled = true; };
            var keyFile = new TextBox { IsReadOnly = true };
            var advanced = new Expander { Header = "Existing key file / Windows account", Margin = new Thickness(0, 12, 0, 0) }; var keys = new StackPanel(); advanced.Content = keys;
            keys.Children.Add(FilePickerRow(keyFile, "Choose an existing key file required by this database.", () => { var picker = new OpenFileDialog { Filter = "Key files|*.keyx;*.key|All files|*.*" }; if (picker.ShowDialog(this) == true) keyFile.Text = picker.FileName; }));
            keys.Children.Add(Button("Clear key file", () => keyFile.Clear(), "Quiet"));
            var account = new CheckBox { Content = "Use Windows account key" }; keys.Children.Add(account); body.Children.Add(advanced);
            var hint = Text(create ? "Your new database starts empty. Existing files are never overwritten." : "Each database can use its own master password and key file. Everything stays on this computer.", 12, "#7D8A9E"); hint.TextWrapping = TextWrapping.Wrap; hint.Margin = new Thickness(0, 12, 0, 16); body.Children.Add(hint);
            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            row.Children.Add(Button("Cancel", CloseModal, "Quiet"));
            var accept=Button(create ? "Create & open" : "Open", async () =>
            {
                CompositeKey key = null; string path;
                try
                {
                    path = System.IO.Path.GetFullPath(location.Text);
                    if (create && use.IsChecked == true && (secret.Password.Length == 0 || secret.Password != confirm.Password)) { Toast("Enter a nonempty password and matching confirmation.", true); return; }
                    if (create && File.Exists(path)) { Toast("Choose a new filename. Existing databases are never overwritten.", true); return; }
                    var existing = create ? null : LoadedSessions.FirstOrDefault(s => FileIdentity.Same(s.PathName, path));
                    if (existing != null) { CloseModal(); Toast("That database is already loaded. Both panes can browse it after closing the other database.", false); return; }
                    key = VaultSession.MakeKey(secret.Password, use.IsChecked == true, keyFile.Text, account.IsChecked == true);
                }
                catch { Toast("Choose a valid filename and at least one supported key factor.", true); return; }
                CloseModal();
                if (otherSession != null && side >= 0) { if (!await ClosePane(panes[side])) return; }
                opening = true; UpdateInteraction(); VaultSession loaded = null;
                try
                {
                    loaded = await Task.Run(() => create ? VaultSession.Create(path, key) : VaultSession.Open(path, key));
                    if (lockAfterOpen || closeAfterOpen) { loaded.Dispose(); loaded = null; return; }
                    if (session == null) { session = loaded; selectedPath = path; pathBox.Text = path; }
                    else if (side == 0) { otherSession = session; session = loaded; selectedPath = path; }
                    else otherSession = loaded;
                    loaded = null; locked = false; unlock.Visibility = Visibility.Collapsed; Activity(); BuildPanes(); UpdateFileLabel();
                    var added = side == 0 || otherSession == null ? session : otherSession;
                    if (added.DraftExists) await OfferPaneDraft(added);
                    if (LoadedSessions.Any(s => String.Equals(s.PathName, path, StringComparison.OrdinalIgnoreCase))) Toast(create ? "Database created and opened." : "Database opened.", false);
                }
                catch (Exception ex) { Toast(OpenError(ex), true); }
                finally
                {
                    key = null; if (loaded != null) loaded.Dispose(); opening = false; UpdateInteraction(); SetStatus();
                    if (closeAfterOpen) { closeAfterOpen = false; var queued = Dispatcher.BeginInvoke(new Action(async () => { await Seal(true); })); }
                    else if (lockAfterOpen) { lockAfterOpen = false; var queued = Dispatcher.BeginInvoke(new Action(async () => { await Seal(false); })); }
                }
            }, "Primary");row.Children.Add(accept);
            Action submit=()=>{if(!opening&&!sealing&&veil.Visibility==Visibility.Visible&&accept.IsEnabled)accept.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));};
            masterInput.Submitted+=submit;if(confirmInput!=null)confirmInput.Submitted+=submit;
            body.Children.Add(row); Dialog(create ? "Create database" : "Open database", body).Width = 520;
        }
        private async Task OfferPaneDraft(VaultSession working)
        {
            int choice = await Ask("Unpublished work found", System.IO.Path.GetFileName(working.PathName) + (working.DraftIsPreviousCheckpoint ? " has only a previous encrypted checkpoint, which can omit newer edits." : " has an encrypted local draft.") + " Resume and reconcile it, discard it explicitly, or cancel opening this database.", "Cancel opening", "Discard draft", "Resume draft");
            if (choice == 0) { DetachUnchanged(working); return; }
            if (choice == 1) { working.RemoveDraft(); return; }
            MergePlan plan = null; editingSuspended = true; UpdateInteraction();
            try
            {
                plan = await ReadDraftForReview(working);
                if (plan == null) { DetachUnchanged(working); return; }
                foreach (var conflict in plan.Conflicts) { conflict.Choice = await ReviewConflict(conflict); if (conflict.Choice == MergeChoice.Cancel) { DetachUnchanged(working); return; } }
                working.ApplyMerge(plan); undo.Clear(); BuildPanes(); RecordAction();
            }
            catch { DetachUnchanged(working); Toast("Draft could not be safely resumed. It was kept unchanged; this database was closed.", true); }
            finally { if (plan != null) plan.Dispose(); editingSuspended = false; UpdateInteraction(); }
        }
        private void DetachUnchanged(VaultSession working)
        {
            if (otherSession == null) { DisposeSession(); ShowLocked("Draft kept. Unlock to resume it."); return; }
            ClearPaneControls(); undo.Clear(); var remaining = LoadedSessions.Single(s => s != working);
            working.Dispose(); session = remaining; otherSession = null; selectedPath = remaining.PathName; pathBox.Text = selectedPath; BuildPanes(); UpdateFileLabel();
        }
    }
}
