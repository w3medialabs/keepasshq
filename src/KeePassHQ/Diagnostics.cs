#if DIAGNOSTICS
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using KeePassLib;
using KeePassLib.Security;

namespace VaultOrganizer
{
    internal sealed class IsolatedClipboard : IClipboardService
    {
        public string Value; public bool Block;
        public string Read() { if (Block) throw new IOException("Synthetic clipboard busy"); return Value; }
        public void Write(string value) { if (Block) throw new IOException("Synthetic clipboard busy"); Value = value; }
        public void Clear() { if (Block) throw new IOException("Synthetic clipboard busy"); Value = null; }
    }
    internal sealed partial class OrganizerWindow
    {
        private bool testClose;
        private int queuedCloses;
        private void LoadSynthetic(string path, string draftRoot)
        {
            session = VaultSession.Open(path, VaultSession.MakeKey("demo", true, null, false)); session.ConfigureDraftRoot(draftRoot);
            selectedPath = path; locked = false; opening = sealing = saveBlocked = protectionFailure = allowClose = false;
            closeTask = null; closeAfterSeal = closeAfterOpen = lockAfterOpen = editingSuspended = false;
            unlock.Visibility = Visibility.Collapsed; BuildPanes(); fileLabel.Text = "Demo.kdbx"; Activity();
        }
        private TextBox FieldBox(BrowserPane pane, PwEntry entry, string field)
        { return Descendants<TextBox>(pane.Columns).First(t => { var b = t.Tag as FieldBinding; return b != null && b.Entry == entry && b.Field == field; }); }
        private Button ModalButton(string name) { return Descendants<Button>(veil).Single(b => b.Content as string == name); }
        private static void Press(Button button) { button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent)); }
        private async Task WaitFor(Func<bool> predicate)
        { for (int i = 0; i < 3000 && !predicate(); ++i) await Task.Delay(10); if (!predicate()) throw new Exception("Synthetic UI operation did not reach its expected state."); }
        internal async Task TestWorkflow(string directory, Action<bool, string> check)
        {
            heartbeat.Stop(); testClose = true; var fake = new IsolatedClipboard(); clipboard = fake;
            string path = Tests.CreateFixture(directory, false), draftRoot = System.IO.Path.Combine(directory, "local-drafts"); byte[] originalHash = VaultSession.HashFile(path);
            LoadSynthetic(path, draftRoot);
            check(panes.Count == 2 && panes[0].Root == panes[1].Root && panes[0].Root == session.Database.RootGroup, "Both browser instances share the exact database root");
            check(panes[1].Layout.Visibility == Visibility.Collapsed, "Viewer defaults to one pane"); long revision = session.Revision;
            SwitchMode(true); check(panes[1].Layout.Visibility == Visibility.Visible && session.Revision == revision, "Mode switch only changes layout");
            SwitchMode(false);check(panes[1].Columns.Children.Count==0,"Viewer releases hidden browser controls rather than rendering them");SwitchMode(true);check(panes[1].Columns.Children.Count>0&&session.Revision==revision,"Organizer rebuilds hidden browser without changing the database");
            PwGroup imports = session.Database.RootGroup.Groups.First(g => g.Name == "Imported notes"); PwEntry entry = imports.Entries.First(); string id = entry.Uuid.ToHexString();
            Navigate(panes[0], imports, 0); check(panes[1].Path.Count == 1, "Independent pane navigation"); Navigate(panes[1], imports, 0);
            foreach (var pane in panes) pane.Expanded = id; RenderAll();
            var a = FieldBox(panes[0], entry, PwDefs.UserNameField); var b = FieldBox(panes[1], entry, PwDefs.UserNameField);
            string before = a.Text; a.Text = "mirrored@example.test"; check(b.Text == a.Text && VaultSession.Read(entry, PwDefs.UserNameField) == a.Text, "Live edit immediately mirrors into the other browser");
            b.Text = "reverse@example.test"; check(a.Text == b.Text, "Mirroring works in both directions"); Undo(); check(VaultSession.Read(entry, PwDefs.UserNameField) == before, "Coalesced field undo updates shared model");
            foreach (var pane in panes) pane.Expanded = id; RenderAll();
            var passwordBox = FieldBox(panes[0], entry, PwDefs.PasswordField); string oldPassword = passwordBox.Text;
            ShowGenerator(passwordBox); check(Descendants<Button>(veil).Count() == 3, "Generator has exactly Cancel, Copy and Use buttons"); Press(ModalButton("Cancel")); check(passwordBox.Text == oldPassword && veil.Visibility == Visibility.Collapsed, "Generator Cancel leaves field unchanged");
            PromptName("New group","Group name",name=>{}); check(!workspace.IsEnabled,"Modal blocks background keyboard/navigation edits"); Press(ModalButton("Cancel"));check(workspace.IsEnabled,"Closing a group modal restores browser interaction");
            ShowGenerator(passwordBox); Press(ModalButton("Copy")); string copied = fake.Value;
            check(copied != null && copied.Length == 24 && veil.Visibility == Visibility.Collapsed && passwordBox.Text == oldPassword, "Generator Copy closes and copies without editing database");
            ShowGenerator(passwordBox); Press(ModalButton("Use"));
            check(passwordBox.Text.Length == 24 && passwordBox.Text != oldPassword && fake.Value == copied && veil.Visibility == Visibility.Collapsed, "Generator Use fills caller, closes, and does not copy");
            check(FieldBox(panes[1], entry, PwDefs.PasswordField).Text == passwordBox.Text, "Generated password mirrors into both panes");
            fake.Value = "Copied by another app"; clipboardAtTicks = Stopwatch.GetTimestamp() - 40 * Stopwatch.Frequency; ExpireClipboard(); check(fake.Value == "Copied by another app" && clipboardHash == null, "Clipboard cleanup never erases another app's replacement");
            fake.Write("synthetic-owned"); OwnClipboard("synthetic-owned"); fake.Block = true; ClearOwnedClipboard(true); check(clipboardHash != null, "Clipboard busy failure retains only ownership digest for retry"); fake.Block = false; ExpireClipboard(); check(fake.Value == null && clipboardHash == null, "Clipboard clearing retries after access returns");
            dragging = entry; var data = new DataObject("VaultOrganizer.Card", VaultSession.ItemId(entry)); check(ReadDrag(data) == entry, "Drag payload uses an in-process UUID, not serialized secrets"); dragging = null; check(ReadDrag(data) == null, "Foreign/inactive drag payload ignored");
            var root = session.Database.RootGroup; string next = VaultSession.ItemId(VaultSession.Ordered(root).First()); PlaceCard(entry, root, next); check(entry.ParentGroup == root && VaultSession.Ordered(root).First() == entry, "Cross-browser insertion moves to requested root gap");
            PlaceCard(entry, imports, null); check(entry.ParentGroup == imports, "Move back into nested group works"); Undo(); check(entry.ParentGroup == root, "Reparent undo restores prior position");
            Border gap = ReceiveGap(panes[0], root, next); check(gap.AllowDrop && gap.Height == 7, "Insertion gap is a real drop target");
            dirtyTicks = Stopwatch.GetTimestamp() - 70 * Stopwatch.Frequency; lastNudgeTicks = 0; NudgeSave(); check(toast.Visibility == Visibility.Visible && toastSave.Visibility == Visibility.Visible && toast.VerticalAlignment == VerticalAlignment.Top, "One-minute unsaved reminder appears top-center with Save action");
            dirtyTicks = Stopwatch.GetTimestamp() - 240 * Stopwatch.Frequency; lastNudgeTicks = lastStrongTicks = 0; NudgeSave(); check(veil.Visibility == Visibility.Visible && Descendants<Button>(veil).Any(x => x.Content as string == "Save database"), "Long-uncommitted work triggers stronger save prompt"); CloseModal();
            session.Set(entry, PwDefs.NotesField, "UI draft-only secret marker"); lastDraftTicks = 0; Activity(); await Tick();
            check(session.DraftRevision == session.Revision && session.DraftExists && originalHash.SequenceEqual(VaultSession.HashFile(path)), "Timer protects draft without publishing edits");
            string draftPath = session.DraftPath; lastActivityTicks = Stopwatch.GetTimestamp() - 360 * Stopwatch.Frequency; await Tick();
            check(locked && session == null && panes.Count == 0 && workspace.Children.Count == 0 && undo.Count == 0, "Timeout clears both views, working model, and undo history");
            var closing = new CancelEventArgs(); OnClosing(this, closing); check(!closing.Cancel, "Already locked window closes directly, without authentication or recursive Close");
            using (var other = VaultSession.Open(path, VaultSession.MakeKey("demo", true, null, false))) check(true, "Timeout releases the session gate for another opener");
            LoadSynthetic(path, draftRoot); opening=true;check(!await SaveNow(true) && session.DraftExists,"Save shortcuts during recovery cannot delete unpublished drafts");opening=false;
            using (var plan = await session.ResumeDraftAsync()) { check(plan.Conflicts.Count == 0, "Draft can be resumed after timeout"); session.ApplyMerge(plan); } BuildPanes();
            check(session.Database.RootGroup.GetEntries(true).Any(x => VaultSession.Read(x, PwDefs.NotesField) == "UI draft-only secret marker"), "Draft preserves final field edit");
            entry = session.Database.RootGroup.GetEntries(true).First(x => x.Uuid.ToHexString() == id); session.Set(entry, PwDefs.UserNameField, "local-before-merge@example.test");
            Tests.WriteOutside(path, db => { var e = db.RootGroup.GetEntries(true).First(x => x.Uuid.ToHexString() == id); e.Strings.Set("External info", new ProtectedString(false, "Independent external information")); e.Touch(true, false); });
            check(await SaveNow(true), "Explicit save brings in compatible external changes");
            entry = session.Database.RootGroup.GetEntries(true).First(x => x.Uuid.ToHexString() == id);
            check(!session.Dirty && !session.DraftExists && VaultSession.Read(entry, PwDefs.UserNameField) == "local-before-merge@example.test" && VaultSession.Read(entry, "External info") == "Independent external information", "Published database contains all reconciled edits and clean draft is removed");
            session.Set(entry, PwDefs.PasswordField, "local-review"); Tests.WriteOutside(path, db => { var e = db.RootGroup.GetEntries(true).First(x => x.Uuid.ToHexString() == id); e.Strings.Set(PwDefs.PasswordField, new ProtectedString(true, "file-review")); e.Touch(true, false); });
            Task<bool> conflictSave = SaveNow(true); await WaitFor(() => mergeChoice != null || conflictSave.IsCompleted); check(mergeChoice != null, "Competing password changes require review"); FinishReview(MergeChoice.Both); check(await conflictSave, "Keep-both conflict review completes explicit save");
            check(session.Database.RootGroup.GetEntries(true).Any(x => VaultSession.Read(x, PwDefs.PasswordField) == "file-review" && VaultSession.Read(x,PwDefs.TitleField).EndsWith("(file copy)")), "Complete competing entry preserved");
            entry=session.Database.RootGroup.GetEntries(true).First(x=>x.Uuid.ToHexString()==id);session.Set(entry,PwDefs.PasswordField,"late-local-review");
            VaultSession.BeforeSaveReplace=()=>{VaultSession.BeforeSaveReplace=null;Tests.WriteOutside(path,db=>{var e=db.RootGroup.GetEntries(true).First(x=>x.Uuid.ToHexString()==id);e.Strings.Set(PwDefs.PasswordField,new ProtectedString(true,"late-file-review"));e.Touch(true,false);});};
            Task<bool> lateSave=SaveNow(true);await WaitFor(()=>mergeChoice!=null||lateSave.IsCompleted);check(mergeChoice!=null&&session.HasPendingMerge,"Last-instant conflicting write is held for review, not silently discarded");
            await Seal(false);check(!await lateSave&&session==null&&locked,"Timeout protects a pending competing version and releases the file");
            LoadSynthetic(path,draftRoot);using(var recovered=await session.ResumeDraftAsync())session.ApplyMerge(recovered);BuildPanes();
            check(session.HasPendingMerge,"Competing file and original baseline survive encrypted draft restart");
            Task<bool> resumedSave=SaveNow(true);await WaitFor(()=>mergeChoice!=null||resumedSave.IsCompleted);check(mergeChoice!=null,"Recovered pending conflict still requires review");FinishReview(MergeChoice.Both);check(await resumedSave,"Recovered conflict can be resolved and published");
            check(session.Database.RootGroup.GetEntries(true).Any(x=>VaultSession.Read(x,PwDefs.PasswordField)=="late-file-review"),"Recovered late-writer version remains present after publishing");
            session.Set(session.Database.RootGroup.GetEntries(true).First(), PwDefs.NotesField, "Keep on close without publish"); byte[] published = VaultSession.HashFile(path);
            closing = new CancelEventArgs(); OnClosing(this, closing); await WaitFor(() => modalChoice != null); Task<bool> pendingClose = closeTask; check(closing.Cancel && queuedCloses == 0, "Dirty close is deferred outside Closing event"); CompleteChoice(1); await pendingClose;
            check(session == null && locked && queuedCloses == 1 && published.SequenceEqual(VaultSession.HashFile(path)), "Keep-draft close releases database without login or publishing unfinished work");
            LoadSynthetic(path, draftRoot); session.RemoveDraft(); session.Set(session.Database.RootGroup.GetEntries(true).First(), PwDefs.NotesField, "Cannot checkpoint");
            string blocker = System.IO.Path.Combine(directory, "not-a-directory"); File.WriteAllText(blocker,"synthetic blocker"); session.ConfigureDraftRoot(blocker);
            await Seal(false); check(session != null && protectionFailure && !locked && veil.Visibility == Visibility.Visible, "Failed checkpoint is clearly lock-incomplete, not falsely locked");
            check(Descendants<Button>(veil).Any(x=>x.Content as string=="Recovery copy"),"Checkpoint failure offers another recovery location rather than forcing discard");
            using (var other = VaultSession.Open(path,VaultSession.MakeKey("demo",true,null,false))) check(true,"Even incomplete lock releases original file gate");
            Press(ModalButton("Discard and lock")); check(session == null && locked, "Explicit discard exits failure state without authentication");
            LoadSynthetic(path,draftRoot);session.RemoveDraft();session.Set(session.Database.RootGroup.GetEntries(true).First(),PwDefs.NotesField,"Close while protecting");
            using(var entered=new ManualResetEventSlim(false))using(var release=new ManualResetEventSlim(false))
            {
                VaultSession.BeforeDraftPublish=()=>{entered.Set();if(!release.Wait(5000))throw new IOException("Synthetic checkpoint wait expired");};
                try
                {
                    int priorCloses=queuedCloses;Task lockTask=Seal(false);await WaitFor(()=>entered.IsSet);closing=new CancelEventArgs();OnClosing(this,closing);check(closing.Cancel&&closeAfterSeal,"Closing during timeout queues exit after protection");release.Set();await lockTask;
                    check(session==null&&locked&&queuedCloses==priorCloses+1&&modalChoice==null,"Window exits after in-flight checkpoint without a second prompt or login");
                }
                finally{release.Set();VaultSession.BeforeDraftPublish=null;}
            }
            StopTimers(); SystemEvents.SessionSwitch -= OnSystemSession;
        }
        internal void RenderDemo(string destination, string mode)
        {
            rendering = true; heartbeat.Stop(); clipboard = new IsolatedClipboard();
            int width = mode == "compact" ? 980 : 1280, height = mode == "compact" ? 640 : 820; Width = width; Height = height;
            VaultSession.IsolatedDraftRoot = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "render-drafts-" + Guid.NewGuid().ToString("N"));
            if (mode != "unlock" && mode != "create")
            {
                string dir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"render-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
                string path = Tests.CreateFixture(dir,false); LoadSynthetic(path,System.IO.Path.Combine(dir,"drafts"));
                SwitchMode(mode != "viewer"); var root = session.Database.RootGroup; var imports=root.Groups.First(g=>g.Name=="Imported notes"); var target=session.NewGroup(root,"Personal");
                var e=session.NewEntry(target);session.Set(e,PwDefs.TitleField,"Personal mailbox");session.Set(e,PwDefs.UserNameField,"alex@example.test");session.Set(e,PwDefs.PasswordField,"fictional-mail-123");session.Set(e,PwDefs.UrlField,"https://mail.example.test");session.Set(e,PwDefs.NotesField,"Separated from the original note.\nPersonal account.");
                panes[0].Path.Add(imports);panes[0].Expanded=imports.Entries.First().Uuid.ToHexString();panes[1].Path.Add(target);panes[1].Expanded=e.Uuid.ToHexString();RenderAll();
                saveStatus.Text="Unsaved changes · encrypted draft protected · Ctrl+S to publish";
                if(mode=="generator")ShowGenerator(FieldBox(panes[0],imports.Entries.First(),PwDefs.PasswordField));
                if(mode=="search")
                {
                    panes[0].Search.Text="email";panes[0].SearchExpanded=imports.Entries.First().Uuid.ToHexString();RenderPane(panes[0]);
                    panes[1].Search.Text="subscription";panes[1].SearchExpanded=root.GetEntries(true).First(entry=>VaultSession.Read(entry,PwDefs.TitleField)=="Reading subscription").Uuid.ToHexString();RenderPane(panes[1]);
                }
                if(mode=="conflict")
                {
                    var mine=imports.Entries.First();var file=mine.CloneDeep();
                    file.Strings.Set(PwDefs.UserNameField,new ProtectedString(false,"other-identity@example.test"));file.Strings.Set(PwDefs.PasswordField,new ProtectedString(true,"fictional-file-password"));
                    var conflict=new MergeConflict { Title=VaultSession.Read(mine,PwDefs.TitleField),MineEntry=mine,FileEntry=file,Credentials=true };
                    conflict.Fields.AddRange(new[]{PwDefs.UserNameField,PwDefs.PasswordField,PwDefs.UrlField});ReviewConflict(conflict);
                }
                if(mode=="recovery") { var decision = AskPreviousCheckpoint(); }
                if(mode=="settings"||mode=="dropdown")ShowSettings();
                if(mode=="open")DatabaseDialog(panes[1],false);
                if(mode=="toast")Toast("Changes saved. Both databases are up to date.",false);
                if(mode=="reminder") { dirtyTicks=Stopwatch.GetTimestamp()-240*Stopwatch.Frequency;lastNudgeTicks=lastStrongTicks=0;NudgeSave();SetStatus(); }
                if(mode=="gap"){var gap=panes[1].Columns.Children.OfType<Grid>().Last();var receive=Descendants<Border>(gap).First(b=>b.AllowDrop&&b.Height==7);receive.Height=45;receive.Background=Brush("#DDF2E5");receive.BorderBrush=Brush("#8DBDA0");receive.BorderThickness=new Thickness(1);}
                if(mode=="dual")
                {
                    string otherDir=System.IO.Path.Combine(dir,"Second vault");Directory.CreateDirectory(otherDir);string otherPath=Tests.CreateFixture(otherDir,true);
                    otherSession=VaultSession.Open(otherPath,VaultSession.MakeKey("demo",true,null,false));otherSession.ConfigureDraftRoot(System.IO.Path.Combine(otherDir,"drafts"));
                    BuildPanes();SwitchMode(true);panes[0].Path.Add(imports);panes[0].Expanded=imports.Entries.First().Uuid.ToHexString();
                    var otherGroup=otherSession.Database.RootGroup.Groups.First(g=>g.Name=="Imported notes");panes[1].Path.Add(otherGroup);panes[1].Expanded=otherGroup.Entries.First().Uuid.ToHexString();RenderAll();UpdateFileLabel();
                }
            }
            if(mode=="create")DatabaseDialog(null,true);
            var content=(FrameworkElement)Content;content.Width=width;content.Height=height;content.Measure(new Size(width,height));content.Arrange(new Rect(0,0,width,height));content.UpdateLayout();
            var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(content);
            if(mode=="dropdown")
            {
                var combo=Descendants<ComboBox>(veil).First();combo.ApplyTemplate();var popup=(System.Windows.Controls.Primitives.Popup)combo.Template.FindName("PART_Popup",combo);
                var options=(FrameworkElement)popup.Child;options.Measure(new Size(combo.ActualWidth,Double.PositiveInfinity));options.Arrange(new Rect(new Point(),options.DesiredSize));options.UpdateLayout();
                int optionWidth=(int)Math.Ceiling(options.ActualWidth),optionHeight=(int)Math.Ceiling(options.ActualHeight);
                var optionBitmap=new RenderTargetBitmap(optionWidth,optionHeight,96,96,PixelFormats.Pbgra32);optionBitmap.Render(options);
                Point at=combo.TranslatePoint(new Point(0,combo.ActualHeight),content);var overlay=new DrawingVisual();using(var drawing=overlay.RenderOpen())drawing.DrawImage(optionBitmap,new Rect(at.X,at.Y,optionWidth,optionHeight));bitmap.Render(overlay);
            }
            if(mode=="tooltip")
            {
                var previewTip=new ToolTip { Content="Save · publish pending edits in all loaded databases · Ctrl+S",Style=(Style)Application.Current.FindResource(typeof(ToolTip)),Visibility=Visibility.Visible };
                previewTip.Measure(new Size(300,Double.PositiveInfinity));previewTip.Arrange(new Rect(new Point(),previewTip.DesiredSize));previewTip.UpdateLayout();
                int tipWidth=(int)Math.Ceiling(previewTip.ActualWidth),tipHeight=(int)Math.Ceiling(previewTip.ActualHeight);
                var tipBitmap=new RenderTargetBitmap(tipWidth,tipHeight,96,96,PixelFormats.Pbgra32);tipBitmap.Render(previewTip);
                var overlay=new DrawingVisual();using(var drawing=overlay.RenderOpen())drawing.DrawImage(tipBitmap,new Rect(width-tipWidth-100,70,tipWidth,tipHeight));bitmap.Render(overlay);
            }
            using(var stream=new FileStream(destination,FileMode.Create)){var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));encoder.Save(stream);}
            foreach(var working in LoadedSessions)working.Dispose();session=otherSession=null;StopTimers();SystemEvents.SessionSwitch-=OnSystemSession;
        }
    }
}
#endif
