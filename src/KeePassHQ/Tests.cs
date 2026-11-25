#if DIAGNOSTICS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Threading;
using KeePassLib;
using KeePassLib.Keys;
using KeePassLib.Security;
using KeePassLib.Serialization;
using KeePassLib.Cryptography.Cipher;
using KeePassLib.Cryptography.KeyDerivation;

namespace VaultOrganizer
{
    internal static partial class Tests
    {
        private static int checks;
        private static void Check(bool value,string description){if(!value)throw new Exception(description);++checks;}
        public static string CreateFixture(string directory, bool argon)
        {
            string path = System.IO.Path.Combine(directory, "Demo.kdbx");
            var db = new PwDatabase(); db.New(IOConnectionInfo.FromPath(path), VaultSession.MakeKey("demo", true, null, false));
            db.Name = "Fictional sample vault"; db.Description = "Synthetic entries only. Password: demo";
            db.DefaultUserName = "alex@example.test"; db.HistoryMaxItems = 20;
            if (argon)
            {
                var kdf = new Argon2Kdf(Argon2Type.ID); db.KdfParameters = kdf.GetDefaultParameters();
                db.KdfParameters.SetUInt64("I", 2); db.KdfParameters.SetUInt64("M", 8 * 1024 * 1024); db.KdfParameters.SetUInt32("P", 2);
                db.DataCipherUuid = new ChaCha20Engine().CipherUuid;
            }
            else db.KdfParameters.SetUInt64("R", 10000);
            var imports = new PwGroup(true, true, "Imported notes", PwIcon.Folder);
            db.RootGroup.AddGroup(imports, true);
            var entry = new PwEntry(true, true);
            entry.Strings.Set(PwDefs.TitleField, new ProtectedString(false, "Email accounts"));
            entry.Strings.Set(PwDefs.UserNameField, new ProtectedString(false, "alex@example.test"));
            entry.Strings.Set(PwDefs.PasswordField, new ProtectedString(true, "fictional-original-123"));
            entry.Strings.Set(PwDefs.UrlField, new ProtectedString(false, "https://mail.example.test"));
            entry.Strings.Set(PwDefs.NotesField, new ProtectedString(false, "Personal email\nalex@example.test\nfictional-mail-123\n\nOld work email\nalex@work.example.test\nfictional-work-456\n\nSplit these into separate entries when ready."));
            entry.Strings.Set("Extra recovery info", new ProtectedString(true, "fictional recovery data"));
            entry.Binaries.Set("sample.txt", new ProtectedBinary(true, Encoding.UTF8.GetBytes("An attachment that must survive.")));
            entry.Tags.Add("sample"); entry.Expires = true; entry.ExpiryTime = DateTime.UtcNow.AddYears(1);
            entry.OverrideUrl = "cmd://fictional-reference"; entry.CustomData.Set("test.extra", "preserve");
            entry.AutoType.DefaultSequence = "{USERNAME}{TAB}{PASSWORD}{ENTER}";
            imports.AddEntry(entry, true); entry.CreateBackup(db);
            var shopping = new PwEntry(true, true); shopping.Strings.Set(PwDefs.TitleField, new ProtectedString(false, "Shopping notes"));
            shopping.Strings.Set(PwDefs.NotesField, new ProtectedString(false, "Garden shop\ngardener@example.test / fictional-garden-789")); imports.AddEntry(shopping, true);
            var internet = new PwGroup(true, true, "Internet", PwIcon.Folder); db.RootGroup.AddGroup(internet, true);
            var nested = new PwGroup(true, true, "Subscriptions", PwIcon.Folder); internet.AddGroup(nested, true);
            var sample = new PwEntry(true, true); sample.Strings.Set(PwDefs.TitleField, new ProtectedString(false, "Reading subscription")); sample.Strings.Set(PwDefs.UserNameField, new ProtectedString(false, "reader@example.test")); nested.AddEntry(sample, true);
            var devices = new PwGroup(true, true, "Devices", PwIcon.Folder); db.RootGroup.AddGroup(devices, true);
            // Existing groups named Old and New must never be mistaken for organizer containers.
            var namedOld = new PwGroup(true, true, "Old", PwIcon.Folder); devices.AddGroup(namedOld, true);
            var recycle = new PwGroup(true, true, "Recycle Bin", PwIcon.TrashBin); db.RootGroup.AddGroup(recycle, true); db.RecycleBinUuid = recycle.Uuid; db.RecycleBinEnabled = true;
            db.CustomData.Set("unrelated.app", "retained"); db.PublicCustomData.SetString("unrelated.public", "retained");
            db.DeletedObjects.Add(new PwDeletedObject(new PwUuid(true), DateTime.UtcNow.AddDays(-1)));
            byte[] png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a/EcAAAAASUVORK5CYII=");
            var icon = new PwCustomIcon(new PwUuid(true), png); icon.Name = "Sample icon"; icon.LastModificationTime = DateTime.UtcNow; db.CustomIcons.Add(icon); entry.CustomIconUuid = icon.Uuid;
            VaultSession.WriteEncrypted(db, path); db.Close(); return path;
        }
        private static PwEntry Rich(PwDatabase db) { return db.RootGroup.GetEntries(true).First(e => VaultSession.Read(e, PwDefs.TitleField) == "Email accounts"); }
        private static void Save(VaultSession session) { SaveResult result = session.SaveAsync().GetAwaiter().GetResult(); session.AcceptSave(result); }
        internal static void WriteOutside(string path, Action<PwDatabase> edit)
        {
            var db = new PwDatabase(); db.Open(IOConnectionInfo.FromPath(path), VaultSession.MakeKey("demo", true, null, false), null);
            string temp = path + ".outside-write";
            try { edit(db); VaultSession.WriteEncrypted(db, temp); File.Replace(temp, path, null); }
            finally { db.Close(); if (File.Exists(temp)) File.Delete(temp); }
        }
        private static void ExternalSet(PwEntry entry, string field, string value)
        { entry.CreateBackup(null); entry.Strings.Set(field, new ProtectedString(field == PwDefs.PasswordField, value)); entry.Touch(true, false); }
        private static void Preserved(PwDatabase db, PwEntry rich, string originalId, PwUuid cipher, PwUuid kdf)
        {
            Check(rich.Uuid.ToHexString() == originalId, "Entry UUID remains stable");
            Check(rich.Strings.ReadSafe("Extra recovery info") == "fictional recovery data", "Hidden custom fields survive");
            Check(rich.Strings.Get("Extra recovery info").IsProtected, "Custom protection survives");
            Check(Encoding.UTF8.GetString(rich.Binaries.Get("sample.txt").ReadData()) == "An attachment that must survive.", "Attachments survive");
            Check(rich.History.UCount >= 1, "History survives"); Check(rich.Tags.Contains("sample"), "Tags survive");
            Check(rich.Expires && rich.ExpiryTime > DateTime.UtcNow, "Expiry survives");
            Check(rich.AutoType.DefaultSequence == "{USERNAME}{TAB}{PASSWORD}{ENTER}", "Auto-type survives");
            Check(rich.OverrideUrl == "cmd://fictional-reference", "Override URL survives");
            Check(rich.CustomData.Get("test.extra") == "preserve", "Entry custom metadata survives");
            Check(db.CustomData.Get("unrelated.app") == "retained", "Database custom metadata survives");
            Check(db.PublicCustomData.GetString("unrelated.public") == "retained", "Public custom metadata survives");
            Check(db.CustomIcons.Count == 1 && db.CustomIcons[0].Name == "Sample icon", "Custom icon and metadata survive");
            Check(db.DeletedObjects.UCount == 1, "Deleted-object metadata survives");
            Check(db.DataCipherUuid.Equals(cipher) && db.KdfParameters.KdfUuid.Equals(kdf), "Cipher and KDF are retained");
            Check(db.RootGroup.FindGroup(db.RecycleBinUuid, true) != null, "Recycle-bin reference survives");
        }
        private static VaultSession OpenTest(string path, string directory)
        { var s = VaultSession.Open(path, VaultSession.MakeKey("demo", true, null, false)); s.ConfigureDraftRoot(System.IO.Path.Combine(directory, "drafts")); return s; }
        private static string Fixture(string run, string name, bool argon)
        { string directory = System.IO.Path.Combine(run, name); Directory.CreateDirectory(directory); return CreateFixture(directory, argon); }
        private static void ModelChecks(string run)
        {
            foreach (bool argon in new[] { false, true })
            {
                string path = Fixture(run, argon ? "argon" : "aes", argon), directory = System.IO.Path.GetDirectoryName(path); byte[] original = VaultSession.HashFile(path);
                string id; string[] expectedOrder; PwUuid cipher, kdf;
                using (var session = OpenTest(path, directory))
                {
                    Check(!session.Dirty && session.Database.RootGroup.Groups.UCount == 4, "Opening is structurally read-only; no automatic Old/New");
                    Check(original.SequenceEqual(VaultSession.HashFile(path)), "Opening does not write the file");
                    var rich = Rich(session.Database); id = rich.Uuid.ToHexString(); cipher = session.Database.DataCipherUuid; kdf = session.Database.KdfParameters.KdfUuid;
                    Preserved(session.Database, rich, id, cipher, kdf);
                    var root = session.Database.RootGroup; var target = session.NewGroup(root, "Personal"); session.Place(rich, target, null);
                    Check(rich.ParentGroup == target && session.Database.RootGroup.GetEntries(true).Count() == 3, "Move retains identity and does not copy records");
                    Check(!session.CanPlace(target, target, null) && !session.CanPlace(root, target, null), "Self/root cycles rejected");
                    var nested = session.NewGroup(target, "Nested"); Check(!session.CanPlace(target, nested, null), "Ancestor-to-descendant move rejected");
                    var split = session.NewEntry(target); session.Set(split, PwDefs.TitleField, "Split mailbox"); session.Set(split, PwDefs.PasswordField, "new-split-example");
                    session.Place(split, target, rich.Uuid.ToHexString());
                    var ordered = VaultSession.Ordered(target); expectedOrder = ordered.Select(VaultSession.ItemId).ToArray();
                    Check(ordered.Contains(nested) && ordered.IndexOf(split) + 1 == ordered.IndexOf(rich), "Mixed group/entry order preserves insertion anchor");
                    long revision = session.Revision; Check(!session.Place(split, target, split.Uuid.ToHexString()) && session.Revision == revision, "Drop onto itself is a no-op");
                    Check(VaultSession.Read(rich, PwDefs.NotesField).Contains("Old work email"), "Splitting does not remove source notes");
                    var save = session.SaveAsync(); session.Set(split, PwDefs.NotesField, "Edited while encrypting"); session.AcceptSave(save.GetAwaiter().GetResult());
                    Check(session.Dirty, "Edits during save remain pending"); Save(session); Check(!session.Dirty, "Latest edit commits on next save");
                    Check(original.SequenceEqual(VaultSession.HashFile(Directory.GetFiles(path + ".versions", "session-*.kdbx").Single())), "First encrypted backup equals original bytes");
                    bool gated = false; try { using (var other = OpenTest(path, directory)) { } } catch (IOException) { gated = true; } Check(gated, "Own-app second writer blocked while unlocked");
                    Preserved(session.Database, rich, id, cipher, kdf);
                }
                using (var reopened = OpenTest(path, directory))
                {
                    var rich = reopened.Database.RootGroup.GetEntries(true).First(e => e.Uuid.ToHexString() == id); Preserved(reopened.Database, rich, id, cipher, kdf);
                    Check(VaultSession.Ordered(rich.ParentGroup).Select(VaultSession.ItemId).SequenceEqual(expectedOrder), "Mixed ordering survives encryption and reopening");
                    Check(reopened.Database.RootGroup.GetEntries(true).Any(e => VaultSession.Read(e, PwDefs.NotesField) == "Edited while encrypting"), "In-flight edits survive reopen");
                }
                bool wrong = false; try { using (var bad = VaultSession.Open(path, VaultSession.MakeKey("wrong", true, null, false))) { } } catch { wrong = true; } Check(wrong, "Wrong key rejected and gate released");
            }
        }
        private static void MergeChecks(string run)
        {
            string path = Fixture(run, "merge-fields", false), directory = System.IO.Path.GetDirectoryName(path);
            using (var s = OpenTest(path, directory))
            {
                var rich = Rich(s.Database); string id = rich.Uuid.ToHexString(); s.Set(rich, PwDefs.UserNameField, "local@example.test");
                var personal = s.NewGroup(s.Database.RootGroup, "Personal"); s.Place(rich, personal, null);
                WriteOutside(path, db => { var e = Rich(db); ExternalSet(e, PwDefs.NotesField, "File note"); e.Binaries.Set("external.txt", new ProtectedBinary(true, Encoding.UTF8.GetBytes("External attachment"))); var added = new PwEntry(true, true); added.Strings.Set(PwDefs.TitleField,new ProtectedString(false,"External addition")); db.RootGroup.AddEntry(added,true); db.DataCipherUuid = new ChaCha20Engine().CipherUuid; db.KdfParameters.SetUInt64("R", 12000); });
                bool detected = false; try { Save(s); } catch (ExternalChangeException) { s.SaveFailed(); detected = true; } Check(detected, "Hash validator detects changed file");
                using (var plan = s.PrepareMergeAsync().GetAwaiter().GetResult()) { Check(plan.Conflicts.Count == 0, "Different fields and independent additions merge without conflict"); s.ApplyMerge(plan); }
                rich = s.Database.RootGroup.GetEntries(true).First(e => e.Uuid.ToHexString() == id);
                Check(VaultSession.Read(rich, PwDefs.UserNameField) == "local@example.test" && VaultSession.Read(rich, PwDefs.NotesField) == "File note" && rich.ParentGroup.Name == "Personal", "Merge retains local move and both field edits");
                Check(rich.Binaries.Get("external.txt") != null && s.Database.DataCipherUuid.Equals(new ChaCha20Engine().CipherUuid), "External attachments and encryption settings survive merge"); Save(s);
            }
            foreach (MergeChoice choice in new[] { MergeChoice.Mine, MergeChoice.File, MergeChoice.Both })
            {
                path = Fixture(run, "conflict-" + choice, false); directory = System.IO.Path.GetDirectoryName(path);
                using (var s = OpenTest(path, directory))
                {
                    var rich = Rich(s.Database); string id = rich.Uuid.ToHexString(); s.Set(rich, PwDefs.PasswordField, "app-password"); s.Set(rich, PwDefs.UrlField, "https://local.example.test");
                    WriteOutside(path, db => { ExternalSet(Rich(db), PwDefs.PasswordField, "file-password"); ExternalSet(Rich(db), PwDefs.NotesField, "Independent file note"); });
                    using (var plan = s.PrepareMergeAsync().GetAwaiter().GetResult()) { Check(plan.Conflicts.Count == 1 && plan.Conflicts[0].Fields.Single() == PwDefs.PasswordField, "Only competing password requires review"); plan.Conflicts[0].Choice = choice; s.ApplyMerge(plan); }
                    rich = s.Database.RootGroup.GetEntries(true).First(e => e.Uuid.ToHexString() == id);
                    Check(VaultSession.Read(rich, PwDefs.PasswordField) == (choice == MergeChoice.File ? "file-password" : "app-password"), "Conflict choice honored");
                    Check(VaultSession.Read(rich, PwDefs.NotesField) == "Independent file note" && VaultSession.Read(rich, PwDefs.UrlField) == "https://local.example.test", "Conflict resolution preserves independent fields");
                    if (choice == MergeChoice.Both) { var copy = s.Database.RootGroup.GetEntries(true).Single(e => VaultSession.Read(e,PwDefs.TitleField).EndsWith("(file copy)")); Check(copy.Uuid.ToHexString() != id && copy.Binaries.Get("sample.txt") != null && VaultSession.Read(copy,PwDefs.PasswordField) == "file-password", "Keep-both retains complete file entry with a fresh identity"); }
                    Save(s);
                }
            }
            foreach (bool keep in new[] { false, true })
            {
                path = Fixture(run, "delete-review-" + keep, false); directory = System.IO.Path.GetDirectoryName(path);
                using (var s = OpenTest(path,directory))
                {
                    var rich=Rich(s.Database); string id=rich.Uuid.ToHexString(); s.Set(rich,PwDefs.NotesField,"Local changed note");
                    WriteOutside(path,db=>{var e=Rich(db);e.ParentGroup.Entries.Remove(e);db.DeletedObjects.Add(new PwDeletedObject(e.Uuid,DateTime.UtcNow.AddSeconds(2)));});
                    using(var plan=s.PrepareMergeAsync().GetAwaiter().GetResult()){Check(plan.Conflicts.Count==1 && plan.Conflicts[0].Presence,"Deletion versus edit requires review");plan.Conflicts[0].Choice=keep?MergeChoice.Mine:MergeChoice.File;s.ApplyMerge(plan);}
                    Check(s.Database.RootGroup.GetEntries(true).Any(e=>e.Uuid.ToHexString()==id)==keep,"Reviewed deletion choice honored"); Save(s);
                }
            }
            path=Fixture(run,"order-merge",false); directory=System.IO.Path.GetDirectoryName(path);
            using(var s=OpenTest(path,directory))
            {
                var root=s.Database.RootGroup; var initial=VaultSession.Ordered(root); s.Place(initial[3],root,VaultSession.ItemId(initial[0]));
                WriteOutside(path,db=>ExternalSet(Rich(db),PwDefs.UserNameField,"file-reorder@example.test"));
                using(var plan=s.PrepareMergeAsync().GetAwaiter().GetResult()){Check(plan.Conflicts.Count==0,"Reorder plus unrelated field edit is compatible");s.ApplyMerge(plan);}
                Check(VaultSession.ItemId(VaultSession.Ordered(s.Database.RootGroup).First())==VaultSession.ItemId(initial[3]),"Local root reorder survives external merge");Save(s);
            }
        }
        private static void DraftChecks(string run)
        {
            foreach(bool argon in new[]{false,true})
            {
                string path=Fixture(run,"draft-"+argon,argon), directory=System.IO.Path.GetDirectoryName(path); byte[] original=VaultSession.HashFile(path); string id,draftPath;
                using(var s=OpenTest(path,directory))
                {
                    var e=Rich(s.Database);id=e.Uuid.ToHexString();s.Set(e,PwDefs.NotesField,"private-draft-only-marker-9528");draftPath=s.DraftPath;
                    s.AcceptDraft(s.CheckpointAsync().GetAwaiter().GetResult());Check(s.DraftRevision==s.Revision && File.Exists(draftPath),"Encrypted draft checkpoint verified");
                    byte[] baseFirst=ZipPayload(draftPath,"baseline.kdbx");s.Set(e,PwDefs.PasswordField,"new-draft-password");s.AcceptDraft(s.CheckpointAsync().GetAwaiter().GetResult());
                    Check(baseFirst.SequenceEqual(ZipPayload(draftPath,"baseline.kdbx")),"Validated encrypted baseline is reused across checkpoints");
                    byte[] validBundle=File.ReadAllBytes(draftPath),working=ZipPayload(draftPath,"working.kdbx"),originalBase=ZipPayload(draftPath,"baseline.kdbx");
                    using(var file=new FileStream(draftPath,FileMode.Create))using(var zip=new System.IO.Compression.ZipArchive(file,System.IO.Compression.ZipArchiveMode.Create))
                    {using(var stream=zip.CreateEntry("working.kdbx").Open())stream.Write(originalBase,0,originalBase.Length);using(var stream=zip.CreateEntry("baseline.kdbx").Open())stream.Write(working,0,working.Length);}
                    bool roleSwap=false;try{using(var altered=s.ResumeDraftAsync().GetAwaiter().GetResult()) {}}catch(InvalidDataException){roleSwap=true;}
                    File.WriteAllBytes(draftPath,validBundle);Check(roleSwap,"Authenticated draft roles reject swapped working/baseline payloads");
                    Check(original.SequenceEqual(VaultSession.HashFile(path)),"Checkpoint does not publish to shared database");
                    Check(!Encoding.UTF8.GetString(File.ReadAllBytes(draftPath)).Contains("private-draft-only-marker-9528"),"Draft bundle has no plaintext note value");
                }
                WriteOutside(path,db=>ExternalSet(Rich(db),PwDefs.UserNameField,"outside-after-lock@example.test"));
                using(var s=OpenTest(path,directory))
                {
                    using(var plan=s.ResumeDraftAsync().GetAwaiter().GetResult()){Check(plan.Conflicts.Count==0,"Draft baseline reconciles changes made while closed");s.ApplyMerge(plan);}
                    var e=s.Database.RootGroup.GetEntries(true).First(x=>x.Uuid.ToHexString()==id);Check(VaultSession.Read(e,PwDefs.NotesField)=="private-draft-only-marker-9528" && VaultSession.Read(e,PwDefs.UserNameField)=="outside-after-lock@example.test","Recovered draft preserves both unpublished and remote work");
                    Save(s);s.RemoveDraft();Check(!s.DraftExists,"Committed draft can be removed");
                }
            }
            string concurrentPath=Fixture(run,"independent-drafts",false),concurrentDir=System.IO.Path.GetDirectoryName(concurrentPath);
            using(var first=OpenTest(concurrentPath,concurrentDir))
            {
                first.Set(Rich(first.Database),PwDefs.NotesField,"First session unpublished");first.AcceptDraft(first.CheckpointAsync().GetAwaiter().GetResult());first.ReleaseFileGate();
                using(var second=OpenTest(concurrentPath,concurrentDir))
                {
                    second.Set(Rich(second.Database),PwDefs.NotesField,"Second session unpublished");second.AcceptDraft(second.CheckpointAsync().GetAwaiter().GetResult());
                    first.AcceptDraft(first.CheckpointAsync().GetAwaiter().GetResult());
                    Check(first.DraftPath!=second.DraftPath && File.Exists(first.DraftPath)&&File.Exists(second.DraftPath),"Separate sessions never overwrite each other's draft branch");
                    using(var one=first.ResumeDraftAsync().GetAwaiter().GetResult())using(var two=second.ResumeDraftAsync().GetAwaiter().GetResult())
                        Check(VaultSession.Read(Rich(one.Local),PwDefs.NotesField)=="First session unpublished" && VaultSession.Read(Rich(two.Local),PwDefs.NotesField)=="Second session unpublished","Both independent unpublished branches remain recoverable");
                }
            }
        }
        private static byte[] ZipPayload(string path,string name)
        {using(var file=File.OpenRead(path))using(var zip=new System.IO.Compression.ZipArchive(file,System.IO.Compression.ZipArchiveMode.Read))using(var source=zip.GetEntry(name).Open())using(var result=new MemoryStream()){source.CopyTo(result);return result.ToArray();}}
        private static void SecurityChecks(string run)
        {
            string keys=System.IO.Path.Combine(run,"composite-keys");Directory.CreateDirectory(keys);string keyFile=System.IO.Path.Combine(keys,"synthetic.key");File.WriteAllBytes(keyFile,Enumerable.Range(1,32).Select(i=>(byte)i).ToArray());
            foreach(bool password in new[]{false,true})
            {
                string filename=System.IO.Path.Combine(keys,password?"password-key.kdbx":"key-only.kdbx");var db=new PwDatabase();db.New(IOConnectionInfo.FromPath(filename),VaultSession.MakeKey("demo",password,keyFile,false));db.KdfParameters.SetUInt64("R",10000);VaultSession.WriteEncrypted(db,filename);db.Close();
                using(var s=VaultSession.Open(filename,VaultSession.MakeKey("demo",password,keyFile,false)))
                {s.ConfigureDraftRoot(System.IO.Path.Combine(keys,"drafts"));var added=s.NewEntry(s.Database.RootGroup);s.Set(added,PwDefs.NotesField,"Composite-key draft");s.AcceptDraft(s.CheckpointAsync().GetAwaiter().GetResult());}
                using(var s=VaultSession.Open(filename,VaultSession.MakeKey("demo",password,keyFile,false)))
                {s.ConfigureDraftRoot(System.IO.Path.Combine(keys,"drafts"));using(var plan=s.ResumeDraftAsync().GetAwaiter().GetResult())s.ApplyMerge(plan);Check(s.Database.RootGroup.GetEntries(true).Any(e=>VaultSession.Read(e,PwDefs.NotesField)=="Composite-key draft"),"Composite-key draft restores with the required factors");Save(s);s.RemoveDraft();}
                VaultSession.VerifyEncrypted(filename,VaultSession.MakeKey("demo",password,keyFile,false));Check(true,"Composite-key saved database verifies");
                bool wrongFactors=false;try{using(var s=VaultSession.Open(filename,VaultSession.MakeKey("demo",true,null,false))) {}}catch{wrongFactors=true;}Check(wrongFactors,"Missing key file cannot unlock the saved database");
            }
            for(int mask=1;mask<16;++mask)
            {
                bool upper=(mask&1)!=0,lower=(mask&2)!=0,digits=(mask&4)!=0,symbols=(mask&8)!=0;
                string value=PasswordGenerator.Generate(24,upper,lower,digits,symbols);
                Check(value.Length==24 && (!upper||value.Any(c=>c>='A'&&c<='Z')) && (!lower||value.Any(c=>c>='a'&&c<='z')) && (!digits||value.Any(c=>c>='0'&&c<='9')) && (!symbols||value.Any(c=>!Char.IsLetterOrDigit(c))),"Generator includes each selected category");
                Check((upper||!value.Any(c=>c>='A'&&c<='Z'))&&(lower||!value.Any(c=>c>='a'&&c<='z'))&&(digits||!value.Any(Char.IsDigit))&&(symbols||value.All(Char.IsLetterOrDigit)),"Generator excludes disabled categories");
            }
            foreach(int n in new[]{0,7,129}){bool rejected=false;try{PasswordGenerator.Generate(n,true,true,true,true);}catch(ArgumentException){rejected=true;}Check(rejected,"Invalid password length rejected");}
            bool noRules=false;try{PasswordGenerator.Generate(24,false,false,false,false);}catch(ArgumentException){noRules=true;}Check(noRules,"Empty generator rule set rejected");
            Check(PasswordGenerator.Generate(8,true,true,true,true).Length==8 && PasswordGenerator.Generate(128,true,true,true,true).Length==128,"Generator length boundaries");
            string path=Fixture(run,"negative-io",false),directory=System.IO.Path.GetDirectoryName(path);
            using(var s=OpenTest(path,directory))
            {
                var e=Rich(s.Database);var duplicate=e.CloneDeep();e.ParentGroup.AddEntry(duplicate,true);bool invalid=false;
                try{VaultSession.ValidateTree(s.Database);}catch(InvalidDataException){invalid=true;}e.ParentGroup.Entries.Remove(duplicate);Check(invalid,"Structural validator rejects duplicate UUIDs");
                string recovery=System.IO.Path.Combine(directory,"competing.kdbx");byte[] sentinel=Encoding.UTF8.GetBytes("Other process owns this file");
                VaultSession.BeforeRecoveryPublish=destination=>File.WriteAllBytes(destination,sentinel);
                bool collision=false;try{s.RecoveryCopyAsync(recovery).GetAwaiter().GetResult();}catch(IOException){collision=true;}finally{VaultSession.BeforeRecoveryPublish=null;}
                Check(collision && sentinel.SequenceEqual(File.ReadAllBytes(recovery)),"Recovery destination race never deletes another writer's file");
                byte[] before=VaultSession.HashFile(path);s.Set(e,PwDefs.NotesField,"Must remain unpublished");VaultSession.AfterSaveWrite=temp=>{using(var stream=new FileStream(temp,FileMode.Open,FileAccess.Write)){stream.SetLength(18);}};
                bool rejected=false;try{Save(s);}catch{rejected=true;s.SaveFailed();}finally{VaultSession.AfterSaveWrite=null;}
                Check(rejected && before.SequenceEqual(VaultSession.HashFile(path)),"Readback validator rejects a damaged candidate before replacement");
                VaultSession.BeforeSaveReplace=()=>{VaultSession.BeforeSaveReplace=null;WriteOutside(path,db=>ExternalSet(Rich(db),PwDefs.UserNameField,"last-instant-writer@example.test"));};
                SaveResult raced;
                try{raced=s.SaveAsync().GetAwaiter().GetResult();}finally{VaultSession.BeforeSaveReplace=null;}
                Check(raced.ConcurrentPath!=null&&File.Exists(raced.ConcurrentPath),"Post-replacement preimage validator preserves a last-instant external write");
                s.AcceptSave(raced);
                using(var plan=s.PrepareMergeAsync(raced.ConcurrentPath,raced.ConcurrentBase).GetAwaiter().GetResult())
                {Check(plan.Conflicts.Count==0,"Last-instant independent edit is mergeable");s.HoldPending(plan.External,plan.Baseline);plan.Revision=s.Revision;plan.CompletesPending=true;s.ApplyMerge(plan);}
                raced.ConcurrentBase.Close();Save(s);
                Check(VaultSession.Read(Rich(s.Database),PwDefs.UserNameField)=="last-instant-writer@example.test"&&VaultSession.Read(Rich(s.Database),PwDefs.NotesField)=="Must remain unpublished","Late writer and local edits both survive reconciliation");
            }
        }
        public static int Run(string baseDirectory)
        {
            Directory.CreateDirectory(baseDirectory);string run=System.IO.Path.Combine(baseDirectory,"synthetic-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(run);var log=new List<string>();
            VaultSession.IsolatedDraftRoot = System.IO.Path.Combine(run, "default-drafts");
            try
            {
                Check(String.IsNullOrEmpty(typeof(PwDatabase).Assembly.Location),"Library loaded from embedded resource");
                Check(typeof(PwDatabase).Assembly.GetName().Version>=new Version(2,60,0,0),"Library includes upstream account-key safety fix");
                ModelChecks(run);log.Add("PASS read-only opening, moves, mixed reordering, encryption, preservation, backups, concurrent edits, and gate release");
                MergeChecks(run);log.Add("PASS three-way merge, competing field choices, deletion review, and order retention");
                DraftChecks(run);log.Add("PASS authenticated draft checkpoints, no shared-file publication, restart recovery, and external changes");
                SecurityChecks(run);log.Add("PASS CSPRNG rules/bounds, structure validation, recovery destination races, and damaged-write rejection");
                AuditChecks(run);log.Add("PASS previous-checkpoint discovery, explicit fallback, damaged-draft preservation, and unreadable-preimage retention across sessions");
                var window=new OrganizerWindow();System.Threading.SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
                var uiDir=System.IO.Path.Combine(run,"ui");Directory.CreateDirectory(uiDir);var workflow=window.TestWorkflow(uiDir,Check);var frame=new DispatcherFrame();var dispatcher=Dispatcher.CurrentDispatcher;
                workflow.ContinueWith(task=>dispatcher.BeginInvoke(new Action(()=>frame.Continue=false)));if(!workflow.IsCompleted)Dispatcher.PushFrame(frame);workflow.GetAwaiter().GetResult();
                log.Add("PASS matching browsers, mirrored edits, mode switching, generator actions, animated gap wiring, reminders, timeout, closing, and failed-checkpoint choices");
                window=new OrganizerWindow();workflow=window.TestMultipleDatabases(System.IO.Path.Combine(run,"multi"),Check);frame=new DispatcherFrame();
                workflow.ContinueWith(task=>dispatcher.BeginInvoke(new Action(()=>frame.Continue=false)));if(!workflow.IsCompleted)Dispatcher.PushFrame(frame);workflow.GetAwaiter().GetResult();
                log.Add("PASS database creation, no-overwrite races, dual-session isolation, independent drafts, destination-first transfers, metadata preservation, and failed-transfer source retention");
                window=new OrganizerWindow();workflow=window.TestAuditWorkflow(System.IO.Path.Combine(run,"audit-ui"),Check);frame=new DispatcherFrame();
                workflow.ContinueWith(task=>dispatcher.BeginInvoke(new Action(()=>frame.Continue=false)));if(!workflow.IsCompleted)Dispatcher.PushFrame(frame);workflow.GetAwaiter().GetResult();
                log.Add("PASS empty-session locking clears dialog secrets and save reminders reflect checkpoint state");
                window=new OrganizerWindow();workflow=window.TestPasswordReveal(System.IO.Path.Combine(run,"reveal-ui"),Check);frame=new DispatcherFrame();
                workflow.ContinueWith(task=>dispatcher.BeginInvoke(new Action(()=>frame.Continue=false)));if(!workflow.IsCompleted)Dispatcher.PushFrame(frame);workflow.GetAwaiter().GetResult();
                log.Add("PASS explicit password reveal, factor synchronization, hide/close/lock clearing and isolated clipboard boundary");
                window=new OrganizerWindow();workflow=window.TestUiCompleteness(System.IO.Path.Combine(run,"ui-completeness"),Check);frame=new DispatcherFrame();
                workflow.ContinueWith(task=>dispatcher.BeginInvoke(new Action(()=>frame.Continue=false)));if(!workflow.IsCompleted)Dispatcher.PushFrame(frame);workflow.GetAwaiter().GetResult();
                log.Add("PASS revealed-field keyboard submission, empty-session exit cleanup and styled-control behavior");
                log.Add("PASS "+checks+" assertions; fictional data and an isolated clipboard only");File.WriteAllLines(System.IO.Path.Combine(baseDirectory,"test-results.txt"),log);return 0;
            }
            catch(Exception ex){log.Add("FAIL "+ex.ToString());File.WriteAllLines(System.IO.Path.Combine(baseDirectory,"test-results.txt"),log);return 1;}
        }
    }
}
#endif
