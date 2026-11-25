#if DIAGNOSTICS
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using KeePassLib;
using KeePassLib.Security;
using KeePassLib.Serialization;

namespace VaultOrganizer
{
    internal sealed partial class OrganizerWindow
    {
        internal async Task TestMultipleDatabases(string directory, Action<bool, string> check)
        {
            heartbeat.Stop(); clipboard = new IsolatedClipboard(); testClose = true;
            Directory.CreateDirectory(directory);
            string aDir = System.IO.Path.Combine(directory, "a"), bDir = System.IO.Path.Combine(directory, "b");
            Directory.CreateDirectory(aDir); Directory.CreateDirectory(bDir);
            string aPath = Tests.CreateFixture(aDir, false), bPath = Tests.CreateFixture(bDir, true);
            LoadSynthetic(aPath, System.IO.Path.Combine(aDir, "drafts"));
            otherSession = VaultSession.Open(bPath, VaultSession.MakeKey("demo", true, null, false)); otherSession.ConfigureDraftRoot(System.IO.Path.Combine(bDir, "drafts"));
            BuildPanes(); SwitchMode(true); var aSession = session; var bSession = otherSession;
            check(panes[0].Session != panes[1].Session && panes[0].Root != panes[1].Root, "Distinct database sessions are bound independently to each browser");
            var first = aSession.Database.RootGroup.GetEntries(true).First(); var second = bSession.Database.RootGroup.GetEntries(true).First();
            check(SessionFor(first) == aSession && SessionFor(second) == bSession, "Card ownership resolves by object ancestry rather than UUID");
            panes[1].Path.Add(second.ParentGroup); panes[1].Expanded = second.Uuid.ToHexString(); RenderAll();
            string aUser = VaultSession.Read(first, PwDefs.UserNameField);
            FieldBox(panes[1], second, PwDefs.UserNameField).Text = "different-db@example.test";
            check(bSession.Dirty && !aSession.Dirty && VaultSession.Read(first, PwDefs.UserNameField) == aUser, "Editing the second browser only changes its own database");
            check(await SaveNow(true) && !AnyDirty, "Save publishes both distinct loaded databases with their own keys");
            aSession.Set(first, PwDefs.NotesField, "First database private draft"); bSession.Set(second, PwDefs.NotesField, "Second database private draft");
            byte[] beforeA = VaultSession.HashFile(aPath), beforeB = VaultSession.HashFile(bPath);
            check(await ProtectDraft() && aSession.DraftExists && bSession.DraftExists, "Both loaded databases get independent encrypted checkpoints");
            check(beforeA.SequenceEqual(VaultSession.HashFile(aPath)) && beforeB.SequenceEqual(VaultSession.HashFile(bPath)), "Dual checkpoints never publish to either shared file");
            await Seal(false); check(session == null && otherSession == null && panes.Count == 0, "Timeout releases both database models and gates");
            using (var reopened = VaultSession.Open(aPath, VaultSession.MakeKey("demo", true, null, false))) check(true, "First gate released on lock");
            using (var reopened = VaultSession.Open(bPath, VaultSession.MakeKey("demo", true, null, false))) check(true, "Second gate released on lock");
            LoadSynthetic(aPath, System.IO.Path.Combine(aDir, "drafts")); session.RemoveDraft();
            check(await CloseDatabases() && session == null && unlock.Visibility == Visibility.Visible && !allowClose, "Close database returns to the open screen without exiting the app");
            DatabaseDialog(null, true); check(Descendants<PasswordBox>(veil).Count() == 2 && Descendants<Button>(veil).Any(b => b.Content as string == "Create & open"), "Open screen offers password-confirmed database creation"); CloseModal();
            string createPath = System.IO.Path.Combine(directory, "New.kdbx");
            using (var created = await Task.Run(() => VaultSession.Create(createPath, VaultSession.MakeKey("new-fictional-secret", true, null, false))))
            {
                created.ConfigureDraftRoot(System.IO.Path.Combine(directory,"new-drafts"));
                check(created.Database.RootGroup.GetEntries(true).Count() == 0 && created.Database.RootGroup.Groups.UCount == 0, "Creation starts with an empty database, without organizer container groups");
                check(created.Database.KdfParameters.GetUInt64("M", 0) == 64UL*1024*1024 && created.Database.KdfParameters.GetUInt64("I", 0) == 3, "New database uses production Argon2id memory and pass settings");
                check(created.Database.KdfParameters.GetUInt32("P", 0) == 2, "New database has explicit Argon2id parallelism");
            }
            bool refused = false; byte[] createdHash = VaultSession.HashFile(createPath);
            try { using (var duplicate = VaultSession.Create(createPath,VaultSession.MakeKey("other",true,null,false))) { } } catch (IOException) { refused = true; }
            check(refused && createdHash.SequenceEqual(VaultSession.HashFile(createPath)), "Creation refuses to overwrite an existing database");
            string racePath = System.IO.Path.Combine(directory, "CreateRace.kdbx"); refused = false;
            VaultSession.BeforeCreatePublish = path => File.Copy(createPath,path,false);
            try { using (var raced = await Task.Run(() => VaultSession.Create(racePath,VaultSession.MakeKey("other",true,null,false)))) { } } catch (IOException) { refused = true; }
            finally { VaultSession.BeforeCreatePublish = null; }
            check(refused && VaultSession.HashFile(createPath).SequenceEqual(VaultSession.HashFile(racePath)), "A file created at the last instant is never overwritten or deleted");
            using (var source = VaultSession.Open(aPath, VaultSession.MakeKey("demo",true,null,false)))
            using (var dest = VaultSession.Open(bPath, VaultSession.MakeKey("demo",true,null,false)))
            {
                source.ConfigureDraftRoot(System.IO.Path.Combine(aDir,"transfer-drafts")); dest.ConfigureDraftRoot(System.IO.Path.Combine(bDir,"transfer-drafts"));
                var group = source.Database.RootGroup.Groups.First(g=>g.Name=="Imported notes"); var rich = group.Entries.First();
                var linked = source.NewEntry(group); source.Set(linked,PwDefs.TitleField,"Internal reference"); source.Set(linked,PwDefs.PasswordField,"{REF:P@I:"+rich.Uuid.ToHexString()+"}");
                var result=await source.SaveAsync();source.AcceptSave(result);
                string oldId=rich.Uuid.ToHexString(); byte[] original=VaultSession.HashFile(aPath);
                using (var transfer = new CardTransfer(source,dest,group,dest.Database.RootGroup,null))
                {
                    var imported=(PwGroup)transfer.Imported; var newRich=imported.GetEntries(true).First(e=>VaultSession.Read(e,PwDefs.TitleField)=="Email accounts");
                    check(newRich.Uuid.ToHexString()!=oldId && imported.Uuid!=group.Uuid, "Cross-database copies receive fresh group and entry UUIDs");
                    check(newRich.History.All(h=>h.Uuid.Equals(newRich.Uuid)) && newRich.Binaries.Get("sample.txt")!=null && newRich.Strings.ReadSafe("Extra recovery info")=="fictional recovery data", "Transfer keeps history, attachments, and hidden protected fields");
                    check(imported.GetEntries(true).Any(e=>e.Strings.ReadSafe(PwDefs.PasswordField)=="{REF:P@I:"+newRich.Uuid.ToHexString()+"}"), "Internal UUID field references are remapped to transferred entries");
                    check(dest.Database.CustomIcons.Any(i=>i.Uuid.Equals(newRich.CustomIconUuid)), "Transferred custom icons resolve in the destination");
                    refused=false;try{transfer.RemoveSource();}catch(IOException){refused=true;}
                    check(refused && original.SequenceEqual(VaultSession.HashFile(aPath)) && source.Database.RootGroup.Groups.Contains(group), "Core protocol forbids removing source before verified destination commit");
                    result=await dest.SaveAsync();dest.AcceptSave(result); await transfer.VerifyDestinationAsync();
                    check(source.Database.RootGroup.Groups.Contains(group), "Destination save and verification keep original source intact");
                    source.Set(rich,PwDefs.NotesField,"Edited after destination verification");refused=false;try{transfer.RemoveSource();}catch(IOException){refused=true;}
                    check(refused&&source.Database.RootGroup.Groups.Contains(group),"Edits after copying invalidate source removal instead of discarding a newer value");
                    transfer.RestoreSource(); group=source.Database.RootGroup.Groups.First(g=>g.Name=="Imported notes");
                    // Complete a fresh transfer after the guarded in-memory edit.
                    result=await source.SaveAsync();source.AcceptSave(result);
                }
                using(var transfer=new CardTransfer(source,dest,group,dest.Database.RootGroup,null))
                {
                    result=await dest.SaveAsync();dest.AcceptSave(result);await transfer.VerifyDestinationAsync();
                    transfer.RemoveSource(); result=await source.SaveAsync();source.AcceptSave(result);
                    check(!source.Database.RootGroup.Groups.Contains(group) && source.Database.DeletedObjects.Any(d=>d.Uuid.ToHexString()==oldId), "Successful move removes source and records subtree deletion tombstones");
                }
                VaultSession.ValidateTree(source.Database);VaultSession.ValidateTree(dest.Database);
                var back=dest.Database.RootGroup.GetEntries(true).First(e=>VaultSession.Read(e,PwDefs.TitleField)=="Email accounts");
                using(var transfer=new CardTransfer(dest,source,back,source.Database.RootGroup,null))
                {
                    result=await source.SaveAsync();source.AcceptSave(result);await transfer.VerifyDestinationAsync();
                    Tests.WriteOutside(aPath,db=>{var e=db.RootGroup.GetEntries(true).First(x=>x.Uuid.ToHexString()==transfer.ImportedId);e.Strings.Set(PwDefs.PasswordField,new ProtectedString(true,"external-after-verification"));});
                    refused=false;try{transfer.RemoveSource();}catch(IOException){refused=true;}
                    check(refused && dest.Database.RootGroup.GetEntries(true).Contains(back), "A destination change after verification blocks source removal");
                }
            }
            // End-to-end UI transfer, including deliberate copy and a failed destination write.
            LoadSynthetic(aPath,System.IO.Path.Combine(aDir,"ui-transfer-drafts"));otherSession=VaultSession.Open(bPath,VaultSession.MakeKey("demo",true,null,false));otherSession.ConfigureDraftRoot(System.IO.Path.Combine(bDir,"ui-transfer-drafts"));BuildPanes();SwitchMode(true);
            var card=session.Database.RootGroup.GetEntries(true).First(); string cardId=card.Uuid.ToHexString();
            Task move=CrossDatabaseDrop(session,otherSession,card,otherSession.Database.RootGroup,null); await WaitFor(()=>modalChoice!=null); CompleteChoice(1);await move;
            check(session.Database.RootGroup.GetEntries(true).Any(e=>e.Uuid.ToHexString()==cardId) && otherSession.Database.RootGroup.GetEntries(true).Any(e=>VaultSession.Read(e,PwDefs.PasswordField)==VaultSession.Read(card,PwDefs.PasswordField)), "UI Save & copy retains source and publishes destination");
            move=CrossDatabaseDrop(session,otherSession,card,otherSession.Database.RootGroup,null);await WaitFor(()=>modalChoice!=null);CompleteChoice(2);await move;
            check(!session.Database.RootGroup.GetEntries(true).Any(e=>e.Uuid.ToHexString()==cardId)&&!AnyDirty,"UI Save & move commits destination and then source, clearing dirty state");
            card=session.Database.RootGroup.GetEntries(true).First();cardId=card.Uuid.ToHexString();
            var sourceSession=session;var destSession=otherSession; byte[] sourceHash=VaultSession.HashFile(aPath);int writes=0;
            VaultSession.AfterSaveWrite=path=>{if(++writes==3){sourceHash=VaultSession.HashFile(aPath);File.WriteAllText(path,"synthetic corrupt destination");}};
            try { move=CrossDatabaseDrop(session,otherSession,card,otherSession.Database.RootGroup,null);await WaitFor(()=>modalChoice!=null);CompleteChoice(2);await move; }
            finally{VaultSession.AfterSaveWrite=null;}
            check(sourceHash.SequenceEqual(VaultSession.HashFile(aPath)) && sourceSession.Database.RootGroup.GetEntries(true).Any(e=>e.Uuid.ToHexString()==cardId), "Destination failure during cross-database move never removes source");
            check(await SaveNow(true),"A retained destination copy can be explicitly saved after a failed transfer");
            card=session.Database.RootGroup.GetEntries(true).First(e=>e.Uuid.ToHexString()==cardId);writes=0;int destinationCount=otherSession.Database.RootGroup.GetEntries(true).Count();
            VaultSession.AfterSaveWrite=path=>{if(++writes==4)File.WriteAllText(path,"synthetic corrupt source deletion candidate");};
            try{move=CrossDatabaseDrop(session,otherSession,card,otherSession.Database.RootGroup,null);await WaitFor(()=>modalChoice!=null);CompleteChoice(2);await move;}
            finally{VaultSession.AfterSaveWrite=null;}
            check(session.Database.RootGroup.GetEntries(true).Any(e=>e.Uuid.ToHexString()==cardId)&&otherSession.Database.RootGroup.GetEntries(true).Count()==destinationCount+1,"A failed source deletion save restores its working original while retaining the saved destination copy");
            await Seal(false);check(session==null&&otherSession==null,"Both sessions close safely after a transfer failure");
            string uiCreated=System.IO.Path.Combine(directory,"UI-created.kdbx");
            DatabaseDialog(null,true);var boxes=Descendants<PasswordBox>(veil).ToArray();Descendants<TextBox>(veil).First().Text=uiCreated;
            boxes[0].Password="fictional-ui-new-key";boxes[1].Password="mismatch";Press(ModalButton("Create & open"));
            check(!File.Exists(uiCreated)&&veil.Visibility==Visibility.Visible,"Mismatching confirmation cannot create a database");
            boxes[1].Password=boxes[0].Password;Press(ModalButton("Create & open"));await WaitFor(()=>!opening&&session!=null);
            check(!locked&&panes.Count==2&&panes[0].Session==panes[1].Session&&File.Exists(uiCreated),"Create & open completes the full lifecycle and mirrors the new database by default");
            otherSession=VaultSession.Open(aPath,VaultSession.MakeKey("demo",true,null,false));otherSession.ConfigureDraftRoot(System.IO.Path.Combine(aDir,"different-key-drafts"));BuildPanes();
            var newEntry=session.NewEntry(session.Database.RootGroup);session.Set(newEntry,PwDefs.PasswordField,"new-db-value");var existingEntry=otherSession.Database.RootGroup.GetEntries(true).First();otherSession.Set(existingEntry,PwDefs.UserNameField,"separate-key@example.test");
            check(await SaveNow(true)&&!AnyDirty,"Save supports two databases with different composite master keys");
            var closingSession=otherSession;closingSession.Set(existingEntry,PwDefs.NotesField,"Retained when closing one pane");
            Task<bool> closeOne=ClosePane(panes[1]);await WaitFor(()=>modalChoice!=null);CompleteChoice(1);check(await closeOne,"Close one dirty database offers keep-draft behavior");
            check(otherSession==null&&session.PathName==uiCreated&&panes[0].Session==panes[1].Session&&closingSession.Database==null,"Closing one database mirrors the remaining database in both panes and disposes the closed session");
            using(var released=VaultSession.Open(aPath,VaultSession.MakeKey("demo",true,null,false)))check(true,"Closing one database releases its file gate");
            check(FileIdentity.Same(uiCreated,uiCreated.ToUpperInvariant())&&!FileIdentity.Same(uiCreated,aPath),"Local file identities distinguish separate databases and normalize path aliases");
            await CloseDatabases();check(session==null&&unlock.Visibility==Visibility.Visible,"Closing the final database returns to open screen after dual-database use");
            selectedPath=createPath;pathBox.Text=createPath;password.Password="new-fictional-secret";await Open();
            check(session!=null&&!locked&&workspace.IsEnabled&&password.Password.Length==0,"Normal open screen restores interaction and clears the master password after decryption");await CloseDatabases();
            StopTimers(); Microsoft.Win32.SystemEvents.SessionSwitch-=OnSystemSession;
        }
    }
}
#endif
