using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;
using KeePassLib;

namespace VaultOrganizer
{
    internal sealed class FieldBinding { public PwEntry Entry; public string Field; }
    internal sealed partial class OrganizerWindow
    {
        private Button viewerButton, organizerButton, toastSave;
        private bool organizerMode, fieldGuard, protectionFailure, closeAfterOpen, lockAfterOpen, closeAfterSeal, editingSuspended;
        private long lastFieldEditTicks, dirtyTicks, lastNudgeTicks, lastStrongTicks, lastDraftTicks;
        private long clipboardAtTicks;
        private int completedActions;
        private Task<bool> draftTask, closeTask;
        private TaskCompletionSource<int> modalChoice;
        private Border activeGap;
        private byte[] clipboardHash;
        private IClipboardService clipboard = new SystemClipboardService();

        private void SwitchMode(bool organize)
        {
            organizerMode = organize;
            workspace.ColumnDefinitions[1].Width = organize ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
            if (panes.Count == 2)
            {
                panes[1].Layout.Visibility = organize ? Visibility.Visible : Visibility.Collapsed;
                if (organize && panes[1].Columns.Children.Count == 0) RenderPane(panes[1]);
                if (!organize) { panes[1].Columns.Children.Clear(); panes[1].Editors.Clear(); panes[1].EntryHeaders.Clear(); }
            }
            if (viewerButton != null) { viewerButton.Background = organize ? Brushes.Transparent : Brushes.White; organizerButton.Background = organize ? Brushes.White : Brushes.Transparent; viewerButton.Foreground = Brush(organize ? "#7D8A9E" : "#396BD7"); organizerButton.Foreground = Brush(organize ? "#396BD7" : "#7D8A9E"); viewerButton.BorderBrush = organize ? Brushes.Transparent : Brush("#D6E3F6"); organizerButton.BorderBrush = organize ? Brush("#D6E3F6") : Brushes.Transparent; }
            Activity();
        }
        private void UpdateInteraction()
        {
            bool enabled = !locked && !editingSuspended && !transferring && !changingDatabase && saveTask == null && !opening && veil.Visibility != Visibility.Visible;
            workspace.IsEnabled = enabled;
            if (shell.Children.Count > 0) ((UIElement)shell.Children[0]).IsEnabled = enabled;
            if (toastSave != null) toastSave.IsEnabled = enabled;
        }
        private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
        {
            var own = root as T; if (own != null) yield return own;
            foreach (object child in LogicalTreeHelper.GetChildren(root))
            { var dependency = child as DependencyObject; if (dependency != null) foreach (T result in Descendants<T>(dependency)) yield return result; }
        }
        private void SyncField(TextBox source, PwEntry entry, string field)
        {
            fieldGuard = true;
            try
            {
                foreach (var pane in panes) foreach (TextBox box in pane.Editors)
                {
                    var binding = box.Tag as FieldBinding;
                    if (box == source || binding == null || binding.Entry != entry || binding.Field != field || box.Text == source.Text) continue;
                    int start = box.SelectionStart, length = box.SelectionLength; box.Text = source.Text;
                    box.Select(Math.Min(start, box.Text.Length), Math.Min(length, Math.Max(0, box.Text.Length - start)));
                }
            }
            finally { fieldGuard = false; }
        }
        private void RefreshNames(PwEntry entry)
        {
            string id = entry.Uuid.ToHexString(), value = VaultSession.Read(entry, PwDefs.TitleField);
            foreach (var pane in panes)
            {
                if (pane.Session != SessionFor(entry)) continue;
                Button button; if (!pane.EntryHeaders.TryGetValue(id, out button)) continue;
                var grid = button.Content as Grid; if (grid != null) { var name = (TextBlock)grid.Children[1]; name.Text = String.IsNullOrWhiteSpace(value) ? "Untitled" : value; name.ToolTip = value; }
                string display=String.IsNullOrWhiteSpace(value)?"Untitled":value;
                UiVisual.Help(button,(ExpandedEntry(pane)==id?"Collapse entry: ":"Expand entry: ")+display+". Drag this header to move or reorder the card.");
                System.Windows.Automation.AutomationProperties.SetName(button,"Entry: "+display);
            }
        }
        private Border ReceiveGap(BrowserPane pane, PwGroup parent, string before)
        {
            var gap = new Border { Height = 7, Background = Brushes.Transparent, CornerRadius = new CornerRadius(5), AllowDrop = true };
            gap.DragOver += (sender, args) =>
            {
                args.Handled = true; object item = ReadDrag(args.Data);
                if (item == null || session == null || locked || !workspace.IsEnabled || !CanDrop(item, parent, before) || before == VaultSession.ItemId(item)) { args.Effects = DragDropEffects.None; return; }
                args.Effects = DragDropEffects.Move; Activity(); hoverTimer.Stop(); hoverGroup = null;
                if (activeGap != gap) { CloseGap(); activeGap = gap; gap.Background = Brush("#DDF2E5"); gap.BorderBrush = Brush("#8DBDA0"); gap.BorderThickness = new Thickness(1); AnimateGap(gap, 45); }
                ScrollDuringDrag(pane, gap, args);
            };
            gap.DragLeave += (sender, args) => { Point p = args.GetPosition(gap); if (p.X < 0 || p.Y < 0 || p.X > gap.ActualWidth || p.Y > gap.ActualHeight) { if (activeGap == gap) CloseGap(); } };
            gap.Drop += (sender, args) =>
            {
                args.Handled = true; object item = ReadDrag(args.Data); CloseGap();
                if (item == null || session == null || locked || !workspace.IsEnabled || !CanDrop(item, parent, before)) return;
                PlaceCard(item, parent, before); args.Effects = DragDropEffects.Move;
            };
            return gap;
        }
        private static void AnimateGap(Border gap, double height)
        {
            var animation = new DoubleAnimation(height, TimeSpan.FromMilliseconds(120)) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
            gap.BeginAnimation(FrameworkElement.HeightProperty, animation);
        }
        private void CloseGap()
        { if (activeGap == null) return; activeGap.Background = Brushes.Transparent; activeGap.BorderThickness = new Thickness(0); AnimateGap(activeGap, 7); activeGap = null; }
        private void ScrollDuringDrag(BrowserPane pane, DependencyObject source, DragEventArgs args)
        {
            Point p = args.GetPosition(pane.Scroll);
            if (p.X > pane.Scroll.ActualWidth - 28) pane.Scroll.ScrollToHorizontalOffset(pane.Scroll.HorizontalOffset + 14);
            if (p.X < 28) pane.Scroll.ScrollToHorizontalOffset(Math.Max(0, pane.Scroll.HorizontalOffset - 14));
            for (DependencyObject current = source; current != null; current = LogicalTreeHelper.GetParent(current))
            {
                var scroll = current as ScrollViewer;
                if (scroll == null || scroll.VerticalScrollBarVisibility != ScrollBarVisibility.Auto) continue;
                Point inside = args.GetPosition(scroll);
                if (inside.Y < 34) scroll.ScrollToVerticalOffset(Math.Max(0, scroll.VerticalOffset - 14));
                if (inside.Y > scroll.ActualHeight - 34) scroll.ScrollToVerticalOffset(scroll.VerticalOffset + 14);
                break;
            }
        }
        private void ShowGenerator(TextBox target)
        {
            var body = new StackPanel(); var rules = new WrapPanel();
            var upper = new CheckBox { Content = "A–Z", IsChecked = true, Margin = new Thickness(0, 0, 17, 0) };
            var lower = new CheckBox { Content = "a–z", IsChecked = true, Margin = new Thickness(0, 0, 17, 0) };
            var digits = new CheckBox { Content = "0–9", IsChecked = true, Margin = new Thickness(0, 0, 17, 0) };
            var symbols = new CheckBox { Content = "Symbols", IsChecked = true };
            foreach (var option in new[] { upper, lower, digits, symbols }) rules.Children.Add(option);
            UiVisual.Help(upper, "Include uppercase letters."); UiVisual.Help(lower, "Include lowercase letters."); UiVisual.Help(digits, "Include digits."); UiVisual.Help(symbols, "Include symbols.");
            body.Children.Add(Text("LENGTH", 10, "#7D8A9E"));
            var length = new TextBox { Text = "24", Margin = new Thickness(0, 7, 0, 17), MaxLength = 3 }; body.Children.Add(length); body.Children.Add(rules);
            UiVisual.Help(length, "Generated password length, from 8 to 128 characters.");
            var output = new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 18, 0, 7), FontFamily = new FontFamily("Consolas"), FontSize = 16, MinHeight = 55 };
            body.Children.Add(output);
            UiVisual.Help(output, "Generated password. Use fills the calling field; Copy writes to the clipboard.");
            var buttons = new Grid { Margin = new Thickness(0, 20, 0, 0) }; buttons.ColumnDefinitions.Add(new ColumnDefinition()); buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var cancel = Button("Cancel", CloseModal, "Quiet"); cancel.HorizontalAlignment = HorizontalAlignment.Left; buttons.Children.Add(cancel);
            var right = new StackPanel { Orientation = Orientation.Horizontal }; Grid.SetColumn(right, 1); buttons.Children.Add(right);
            var copy = Button("Copy", () => { try { clipboard.Write(output.Text); OwnClipboard(output.Text); CloseModal(); Toast("Password copied. Clipboard clears after 30 seconds or on lock.", false); } catch { Toast("Clipboard is busy. Try again.", true); } }, null);
            var use = Button("Use", () => { string value = output.Text; CloseModal(); pendingField = null; target.Text = value; target.Focus(); }, "Primary"); use.Margin = new Thickness(8, 0, 0, 0); right.Children.Add(copy); right.Children.Add(use); body.Children.Add(buttons);
            Action generate = () =>
            {
                try { int size; if (!Int32.TryParse(length.Text, out size)) throw new ArgumentException(); output.Text = PasswordGenerator.Generate(size, upper.IsChecked == true, lower.IsChecked == true, digits.IsChecked == true, symbols.IsChecked == true); copy.IsEnabled = use.IsEnabled = true; }
                catch { output.Text = "Choose 8–128 characters and at least one type."; copy.IsEnabled = use.IsEnabled = false; }
            };
            length.TextChanged += (sender, args) => generate();
            foreach (var option in new[] { upper, lower, digits, symbols }) { option.Checked += (sender, args) => generate(); option.Unchecked += (sender, args) => generate(); }
            var modal = Dialog("Generate password", body); modal.Width = 470; modal.VerticalAlignment = VerticalAlignment.Center; modal.Margin = new Thickness(0); generate();
        }
        private void CloseModal()
        { ClearText(veil); veil.Children.Clear(); veil.Visibility = Visibility.Collapsed; UpdateInteraction(); }
        private static byte[] ClipboardDigest(string text) { using (var hash = SHA256.Create()) return hash.ComputeHash(Encoding.UTF8.GetBytes(text ?? "")); }
        private void TrackTextCopy(object sender, DataObjectCopyingEventArgs args)
        {
            if (args.IsDragDrop || !args.DataObject.GetDataPresent(DataFormats.UnicodeText)) return;
            try
            {
                string text = args.DataObject.GetData(DataFormats.UnicodeText) as string;
                if (String.IsNullOrEmpty(text)) return;
                ClipboardPayload.Protect(args.DataObject); OwnClipboard(text);
            }
            catch { args.CancelCommand(); Toast("The clipboard copy could not be protected. Try again.", true); }
        }
        private void OwnClipboard(string text) { if (String.IsNullOrEmpty(text)) return; clipboardHash = ClipboardDigest(text); clipboardAtTicks = Stopwatch.GetTimestamp(); }
        private void ExpireClipboard()
        {
            if (clipboardHash != null && Elapsed(clipboardAtTicks) >= 30) ClearOwnedClipboard(false);
        }
        private void ClearOwnedClipboard(bool immediate)
        {
            if (clipboardHash == null) return;
            try { if (ClipboardDigest(clipboard.Read()).SequenceEqual(clipboardHash)) clipboard.Clear(); Array.Clear(clipboardHash, 0, clipboardHash.Length); clipboardHash = null; }
            catch { if (immediate) clipboardAtTicks = 0; } // Retry after OS unlock; retain only a digest.
        }
        private void RecordAction()
        { ++completedActions; if (dirtyTicks == 0) dirtyTicks = Stopwatch.GetTimestamp(); }
        private void NudgeSave()
        {
            if (session == null || !AnyDirty || saveTask != null || veil.Visibility == Visibility.Visible || Elapsed(lastNudgeTicks) < 60) return;
            if (dirtyTicks == 0) dirtyTicks = Stopwatch.GetTimestamp();
            double age = Elapsed(dirtyTicks);
            if (age < 60 && completedActions < 10) return;
            lastNudgeTicks = Stopwatch.GetTimestamp();
            if ((age >= 180 || completedActions >= 30) && Elapsed(lastStrongTicks) >= 180)
            {
                lastStrongTicks = Stopwatch.GetTimestamp(); var body = new StackPanel();
                bool protectedEdits = LoadedSessions.Where(s => s.Dirty).All(s => s.DraftRevision == s.Revision);
                var note = Text(protectedEdits ? "Your edits are protected in a local draft, but other users cannot see them until you save the database." : "Your latest edits are not yet protected in an encrypted draft. Save the database now, or keep this window open until a checkpoint succeeds.", 13, "#64748C"); note.TextWrapping = TextWrapping.Wrap; body.Children.Add(note);
                var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
                row.Children.Add(Button("Keep editing", () => { CloseModal(); Activity(); }, "Quiet")); var save = Button("Save database", async () => { CloseModal(); await SaveNow(true); }, "Primary"); save.Margin = new Thickness(8, 0, 0, 0); row.Children.Add(save); body.Children.Add(row); Dialog("Save your progress", body);
            }
            else { Toast("Unsaved database changes. Save now so other users can receive them.", false); toastSave.Visibility = Visibility.Visible; toastTimer.Stop(); }
        }
        private async Task<bool> ProtectDraft()
        {
            if (draftTask != null) return await draftTask;
            if (session == null || !AnyDirty) return true;
            if (saveTask != null) await saveTask;
            draftTask = WriteDraft(); bool result = await draftTask; draftTask = null; SetStatus(); return result;
        }
        private async Task<bool> WriteDraft()
        {
            try { foreach (var working in LoadedSessions) if (working.Dirty) { var result = await working.CheckpointAsync(); working.AcceptDraft(result); } lastDraftTicks = Stopwatch.GetTimestamp(); return true; }
            catch { Toast("Draft checkpoint failed. Save the database now, or keep this window open until recovery is possible.", true); return false; }
        }
        private Task<int> Ask(string title, string message, string left, string middle, string right)
        {
            FinishReview(MergeChoice.Cancel); CompleteChoice(0);
            var completion = new TaskCompletionSource<int>(); modalChoice = completion;
            var body = new StackPanel(); var text = Text(message, 13, "#64748C"); text.TextWrapping = TextWrapping.Wrap; body.Children.Add(text);
            var row = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
            row.Children.Add(Button(left, () => CompleteChoice(0), "Quiet")); var b = Button(middle, () => CompleteChoice(1), null); b.Margin = new Thickness(7, 0, 0, 0); row.Children.Add(b); var c = Button(right, () => CompleteChoice(2), "Primary"); c.Margin = new Thickness(7, 0, 0, 0); row.Children.Add(c); body.Children.Add(row); Dialog(title, body).Width = 510; return completion.Task;
        }
        private void CompleteChoice(int choice)
        { if (modalChoice == null) return; var completion = modalChoice; modalChoice = null; CloseModal(); completion.TrySetResult(choice); }
        private async Task OfferDraft()
        {
            int choice = await Ask("Unpublished work found", session.DraftIsPreviousCheckpoint ? "Only a previous encrypted checkpoint was found. It can be older than your latest edits. Resume and review it, or discard it explicitly." : "An encrypted local draft exists for this database. Resume it and reconcile any external edits, or discard it explicitly.", "Cancel", "Discard draft", "Resume draft");
            if (choice == 0) { DisposeSession(); ShowLocked("Database closed. The draft was kept."); return; }
            if (choice == 1) { session.RemoveDraft(); return; }
            MergePlan plan = null; editingSuspended = true; UpdateInteraction();
            try
            {
                plan = await ReadDraftForReview(session);
                if (plan == null) { DisposeSession(); ShowLocked("Database closed. The draft was kept."); return; }
                foreach (var conflict in plan.Conflicts) { conflict.Choice = await ReviewConflict(conflict); if (conflict.Choice == MergeChoice.Cancel) { DisposeSession(); ShowLocked("Draft kept. Unlock to resume it."); return; } }
                session.ApplyMerge(plan); BuildPanes(); RecordAction(); Toast("Draft resumed. Save the database when these changes are ready to share.", false);
            }
            catch { Toast("Draft could not be opened with this key, or could not be safely merged. It has been kept unchanged.", true); DisposeSession(); ShowLocked("Draft retained. No data was overwritten."); }
            finally { if (plan != null) plan.Dispose(); editingSuspended = false; UpdateInteraction(); }
        }
        private async Task<MergePlan> ReadDraftForReview(VaultSession working)
        {
            bool previous = false;
            while (true)
            {
                MergePlan plan = null;
                try
                {
                    plan = await working.ResumeDraftAsync(previous);
                    if (!plan.LegacyRecovery) return plan;
                    int legacyChoice = await Ask("Review older draft format", "This older draft authenticates its encrypted contents, but cannot prove that optional pending-review files were not removed. Resume only a trusted local checkpoint, then review its entries before saving. Its next checkpoint uses the stronger format.", "Cancel opening", "Keep draft", "Resume and review");
                    if (legacyChoice == 2) return plan;
                    plan.Dispose(); return null;
                }
                catch
                {
                    if (plan != null) plan.Dispose();
                    if (previous || !working.HasPreviousCheckpoint) throw;
                }
                int choice = await AskPreviousCheckpoint();
                if (choice == 0) return null;
                previous = choice == 2;
            }
        }
        private Task<int> AskPreviousCheckpoint()
        { return Ask("Latest draft unavailable", "The latest draft could not be authenticated or read. It has been kept unchanged. A previous checkpoint is available, but can omit newer edits. Resume it only if you want to review that older work.", "Cancel opening", "Retry latest", "Resume previous"); }
        private void ShowLocked(string message)
        { unlockTitle.Text = "Database closed and locked"; unlockHint.Text = message; unlock.Visibility = Visibility.Visible; unlockButton.IsEnabled = true; password.Clear(); }
        private void DisposeSession()
        {
            locked = true; CloseGap(); Keyboard.ClearFocus(); FocusManager.SetFocusedElement(this, null); CloseModal();
            ClearViewsAndClipboard(); undo.Clear(); completedActions = 0; dirtyTicks = lastDraftTicks = 0; saveBlocked = false; protectionFailure = false;
            foreach (var working in LoadedSessions) working.Dispose(); otherSession = null; session = null; password.Clear(); fileLabel.Text = ""; fileLabel.ToolTip = null; SetStatus();
        }
        private async Task Seal(bool close)
        {
            if (changingDatabase) { lockAfterChange = true; closeAfterChange |= close; FinishReview(MergeChoice.Cancel); return; }
            if (transferring) { lockAfterTransfer = true; closeAfterTransfer |= close; FinishReview(MergeChoice.Cancel); return; }
            if (opening) { lockAfterOpen = true; closeAfterOpen |= close; FinishReview(MergeChoice.Cancel); CompleteChoice(0); CloseModal(); password.Clear(); return; }
            if (sealing) return;
            if (session == null) { FinishReview(MergeChoice.Cancel); CompleteChoice(0); CloseModal(); password.Clear(); ClearOwnedClipboard(true); if (close) QueueClose(); return; }
            sealing = true; protectionFailure = false; locked = true;
            FinishReview(MergeChoice.Cancel); CompleteChoice(0); CloseModal();
            unlockTitle.Text = "Protecting your work…"; unlockHint.Text = "Writing an encrypted draft before releasing the database."; unlock.Visibility = Visibility.Visible; unlockButton.IsEnabled = false;
            fieldGuard = true; try { ClearViewsAndClipboard(); } finally { fieldGuard = false; }
            try
            {
                if (saveTask != null) await saveTask;
                if (draftTask != null) await draftTask;
                bool protectedWork = !AnyDirty || await ProtectDraft();
                if (protectedWork) { bool draft = AnyDirty; DisposeSession(); ShowLocked(draft ? "Unpublished changes are protected in an encrypted draft. The database file is released." : "All changes were saved. The database file is released."); if (close || closeAfterSeal) { closeAfterSeal = false; QueueClose(); } }
                else { locked = false; protectionFailure = true; foreach (var working in LoadedSessions) working.ReleaseFileGate(); unlockTitle.Text = "Lock incomplete"; unlockHint.Text = "Work could not be saved safely. Choose Retry or explicitly discard it; no password is required to close."; ShowProtectionFailure(close || closeAfterSeal); closeAfterSeal = false; }
            }
            finally { sealing = false; if (session == null) unlockButton.IsEnabled = true; SetStatus(); }
        }
        private void ShowProtectionFailure(bool close)
        {
            var body = new StackPanel(); var note = Text("The draft could not be written. Your edits are still in memory. The database file is released. Retry protection, or explicitly discard these unsaved edits.", 13, "#64748C"); note.TextWrapping = TextWrapping.Wrap; body.Children.Add(note);
            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
            row.Children.Add(Button(close ? "Discard and close" : "Discard and lock", () => { CloseModal(); DisposeSession(); ShowLocked("Unsaved changes were discarded."); if (close) QueueClose(); }, "Quiet"));
            var recovery = Button("Recovery copy", async () => { CloseModal(); protectionFailure = false; await Recovery(); if (session != null) { protectionFailure = true; ShowProtectionFailure(close); } else if (close) QueueClose(); }, null); recovery.Margin = new Thickness(8, 0, 0, 0); row.Children.Add(recovery);
            var retry = Button("Retry", async () => { CloseModal(); await Seal(close); }, "Primary"); retry.Margin = new Thickness(8, 0, 0, 0); row.Children.Add(retry); body.Children.Add(row); Dialog("Work is not protected", body).Width = 510;
        }
        private void OnClosing(object sender, CancelEventArgs args)
        {
            if (allowClose || (session == null && !opening && saveTask == null && draftTask == null)) { ClearText(unlock); CloseModal(); ClearOwnedClipboard(true); StopTimers(); return; }
            args.Cancel = true;
            if (changingDatabase) { lockAfterChange = closeAfterChange = true; FinishReview(MergeChoice.Cancel); return; }
            if (opening) { closeAfterOpen = true; FinishReview(MergeChoice.Cancel); CompleteChoice(0); return; }
            if (transferring) { lockAfterTransfer = closeAfterTransfer = true; return; }
            if (sealing) { closeAfterSeal = true; return; }
            if (protectionFailure) { ShowProtectionFailure(true); return; }
            if (closeTask == null) closeTask = CloseAndReset();
        }
        private async Task<bool> CloseAndReset() { try { return await CloseWorkflow(); } finally { closeTask = null; } }
        private async Task<bool> CloseWorkflow()
        {
            await Task.Yield(); // Never call Window.Close synchronously inside its Closing event.
            FinishReview(MergeChoice.Cancel);
            if (saveTask != null) await saveTask;
            if (session == null) { QueueClose(); return true; }
            if (AnyDirty)
            {
                int choice = await Ask("Close with unpublished changes?", "Save publishes your changes. Keeping an encrypted draft closes safely without updating the shared database.", "Cancel", "Keep draft and close", "Save and close");
                if (choice == 0) return false;
                if (choice == 2 && !await SaveNow(true)) return false;
            }
            await Seal(true); return session == null;
        }
        private void QueueClose()
        {
            allowClose = true;
#if DIAGNOSTICS
            if (testClose) { ++queuedCloses; return; }
#endif
            Dispatcher.BeginInvoke(new Action(() => Close()), DispatcherPriority.Normal);
        }
        private void StopTimers() { heartbeat.Stop(); toastTimer.Stop(); hoverTimer.Stop(); }
        private void OnSystemSession(object sender, SessionSwitchEventArgs args)
        { if (args.Reason == SessionSwitchReason.SessionLock) Dispatcher.BeginInvoke(new Action(async () => { password.Clear(); if (opening) { lockAfterOpen = true; FinishReview(MergeChoice.Cancel); CompleteChoice(0); } else await Seal(false); })); }
    }
}
