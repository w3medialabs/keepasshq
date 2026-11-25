#if DIAGNOSTICS
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using KeePassLib;
using KeePassLib.Serialization;

namespace VaultOrganizer
{
    internal static partial class Tests
    {
        private static void FinalAuditChecks(string run)
        {
            foreach (var choice in new[] { MergeChoice.Mine, MergeChoice.File, MergeChoice.Both })
            {
                string credentialPath = Fixture(run, "credential-bundle-" + choice, false);
                using (var working = OpenTest(credentialPath, Path.GetDirectoryName(credentialPath)))
                {
                    var entry = Rich(working.Database); string id = entry.Uuid.ToHexString();
                    working.Set(entry, PwDefs.UserNameField, "local-identity@example.test");
                    WriteOutside(credentialPath, db => { ExternalSet(Rich(db), PwDefs.PasswordField, "external-fictional-password"); ExternalSet(Rich(db), PwDefs.NotesField, "Independent external note"); });
                    using (var plan = working.PrepareMergeAsync().GetAwaiter().GetResult())
                    {
                        Check(plan.Conflicts.Count == 1 && plan.Conflicts[0].Fields.Contains(PwDefs.UserNameField) && plan.Conflicts[0].Fields.Contains(PwDefs.PasswordField), "Concurrent changes to different credential fields require review as a bundle");
                        plan.Conflicts[0].Choice = choice; working.ApplyMerge(plan);
                    }
                    entry = working.Database.RootGroup.GetEntries(true).Single(item => item.Uuid.ToHexString() == id);
                    Check(VaultSession.Read(entry, PwDefs.UserNameField) == (choice == MergeChoice.File ? "alex@example.test" : "local-identity@example.test") && VaultSession.Read(entry, PwDefs.PasswordField) == (choice == MergeChoice.File ? "external-fictional-password" : "fictional-original-123"), "Credential choice retains a username/password pair from one actual version");
                    Check(VaultSession.Read(entry, PwDefs.NotesField) == "Independent external note", "Credential bundle review retains independent note changes");
                    if (choice == MergeChoice.Both)
                        Check(working.Database.RootGroup.GetEntries(true).Any(item => item.Uuid.ToHexString() != id && VaultSession.Read(item, PwDefs.UserNameField) == "alex@example.test" && VaultSession.Read(item, PwDefs.PasswordField) == "external-fictional-password"), "Keep both retains the complete second credential version under a fresh identity");
                    Save(working);
                }
            }
            string path = Fixture(run, "valid-candidate-replay", false), directory = Path.GetDirectoryName(path);
            using (var working = OpenTest(path, directory))
            {
                byte[] original = VaultSession.HashFile(path);
                working.Set(Rich(working.Database), PwDefs.NotesField, "New fictional work must survive");
                VaultSession.AfterSaveWrite = temp => File.Copy(path, temp, true);
                bool rejected = false;
                try { Save(working); } catch (InvalidDataException) { rejected = true; working.SaveFailed(); }
                finally { VaultSession.AfterSaveWrite = null; }
                Check(rejected && working.Dirty && original.SequenceEqual(VaultSession.HashFile(path)), "Authenticated but stale candidate content cannot replace the database or mark new work saved");
                Save(working);
                using (var reopened = new PwDatabaseScope(path, working.Database.MasterKey))
                    Check(VaultSession.Read(Rich(reopened.Database), PwDefs.NotesField) == "New fictional work must survive", "Retry publishes and read-verifies the intended current content");
            }
            path = Fixture(run, "valid-checkpoint-replay", false); directory = Path.GetDirectoryName(path);
            using (var working = OpenTest(path, directory))
            {
                working.Set(Rich(working.Database), PwDefs.NotesField, "Earlier fictional checkpoint");
                working.AcceptDraft(working.CheckpointAsync().GetAwaiter().GetResult()); byte[] earlier = File.ReadAllBytes(working.DraftPath);
                working.Set(Rich(working.Database), PwDefs.NotesField, "Latest fictional checkpoint");
                VaultSession.AfterDraftWrite = temp => File.WriteAllBytes(temp, earlier); bool rejected = false;
                try { working.AcceptDraft(working.CheckpointAsync().GetAwaiter().GetResult()); } catch (InvalidDataException) { rejected = true; }
                finally { VaultSession.AfterDraftWrite = null; }
                Check(rejected && working.DraftRevision != working.Revision && earlier.SequenceEqual(File.ReadAllBytes(working.DraftPath)), "A stale authenticated checkpoint cannot replace the previous valid draft or claim to protect the latest revision");
                working.AcceptDraft(working.CheckpointAsync().GetAwaiter().GetResult());
                using (var plan = working.ResumeDraftAsync().GetAwaiter().GetResult())
                    Check(VaultSession.Read(Rich(plan.Local), PwDefs.NotesField) == "Latest fictional checkpoint", "Checkpoint retry preserves the intended latest working content");
            }
            directory = Path.Combine(run, "standard-protection-policy"); Directory.CreateDirectory(directory); path = Path.Combine(directory, "Policy.kdbx");
            var policy = new PwDatabase();
            try
            {
                policy.New(IOConnectionInfo.FromPath(path), VaultSession.MakeKey("demo", true, null, false));
                policy.KdfParameters = new KeePassLib.Cryptography.KeyDerivation.AesKdf().GetDefaultParameters(); policy.KdfParameters.SetUInt64("R", 10000);
                policy.MemoryProtection.ProtectTitle = true;
                var entry = new PwEntry(true, true); entry.Strings.Set(PwDefs.TitleField, new KeePassLib.Security.ProtectedString(false, "Fictional protected title"));
                entry.Strings.Set(PwDefs.PasswordField, new KeePassLib.Security.ProtectedString(true, "fictional\r\npassword"));
                entry.Strings.Set(PwDefs.NotesField, new KeePassLib.Security.ProtectedString(false, "Fictional Windows lines\r\nSecond line\tUnicode Ω and <XML> & text")); policy.RootGroup.AddEntry(entry, true);
                VaultSession.WriteEncrypted(policy, path);
                using (var reopened = new PwDatabaseScope(path, policy.MasterKey))
                {
                    Check(!reopened.Database.MemoryProtection.ProtectTitle, "KeePass restores its documented source-level default in-memory protection policy on load");
                    VaultSession.VerifyContent(policy, reopened.Database);
                    Check(reopened.Database.RootGroup.Entries.First().Strings.Get(PwDefs.TitleField).IsProtected, "Readback accepts KeePass standard-field policy normalization while preserving its protection");
                    reopened.Database.RootGroup.Entries.First().Strings.Set(PwDefs.PasswordField, new KeePassLib.Security.ProtectedString(true, "fictional\npassword"));
                    bool changedSecret = false; try { VaultSession.VerifyContent(policy, reopened.Database); } catch (InvalidDataException) { changedSecret = true; }
                    Check(changedSecret, "XML newline equivalence never accepts different whitespace in a protected password");
                }
            }
            finally { policy.Close(); }
            path = Fixture(run, "draft-component-omission", false); directory = Path.GetDirectoryName(path);
            using (var working = OpenTest(path, directory))
            using (var file = new PwDatabaseScope(path, working.Database.MasterKey))
            {
                working.Set(Rich(working.Database), PwDefs.NotesField, "Private fictional local edit");
                working.HoldPending(file.Database, file.Database);
                working.AcceptDraft(working.CheckpointAsync().GetAwaiter().GetResult());
                byte[] intact = File.ReadAllBytes(working.DraftPath);
                foreach (bool both in new[] { true, false })
                {
                    File.WriteAllBytes(working.DraftPath, intact);
                    using (var archive = ZipFile.Open(working.DraftPath, ZipArchiveMode.Update))
                    { archive.GetEntry("pending-file.kdbx").Delete(); if (both) archive.GetEntry("pending-base.kdbx").Delete(); }
                    bool rejected = false;
                    try { using (var plan = working.ResumeDraftAsync().GetAwaiter().GetResult()) { } }
                    catch (InvalidDataException) { rejected = true; }
                    Check(rejected, "Removing " + (both ? "both pending components" : "one pending component") + " cannot silently remove required recovery review");
                }
                File.WriteAllBytes(working.DraftPath, intact);
                using (var plan = working.ResumeDraftAsync().GetAwaiter().GetResult())
                    Check(plan.RecoveredPending != null && plan.Local.CustomData.Get("VaultOrganizer.PrivateDraft.Components") == null, "Intact pending components remain recoverable without publishing private manifest metadata");
                byte[] payload = ZipPayload(working.DraftPath, "working.kdbx");
                using (var archive = ZipFile.Open(working.DraftPath, ZipArchiveMode.Update))
                using (var stream = archive.CreateEntry("working.kdbx").Open()) stream.Write(payload, 0, payload.Length);
                bool duplicate = false;
                try { using (var plan = working.ResumeDraftAsync().GetAwaiter().GetResult()) { } }
                catch (InvalidDataException) { duplicate = true; }
                Check(duplicate, "Ambiguous duplicate recovery components are rejected");
                File.WriteAllBytes(working.DraftPath, intact);
                // Reproduce the legacy bundle layout without the new encrypted manifest.
                RewriteDraftWorking(working.DraftPath, working.Database.MasterKey, db => db.CustomData.Remove("VaultOrganizer.PrivateDraft.Components"));
                using (var plan = working.ResumeDraftAsync().GetAwaiter().GetResult())
                    Check(plan.RecoveredPending != null && plan.LegacyRecovery, "Legacy drafts retain their original roles and nonce and are identified for explicit review");
                File.WriteAllBytes(working.DraftPath, intact);
                RewriteDraftWorking(working.DraftPath, working.Database.MasterKey, db => db.CustomData.Set("VaultOrganizer.PrivateDraft.Components", "unknown-future-format"));
                bool unsupported = false;
                try { using (var plan = working.ResumeDraftAsync().GetAwaiter().GetResult()) { } }
                catch (InvalidDataException) { unsupported = true; }
                Check(unsupported, "An unsupported authenticated draft manifest fails closed");
            }
        }
        internal static void RewriteDraftWorking(string path, KeePassLib.Keys.CompositeKey key, Action<PwDatabase> edit)
        {
            byte[] payload = ZipPayload(path, "working.kdbx"); var db = new PwDatabase { MasterKey = key };
            try
            {
                using (var input = new MemoryStream(payload, false)) new KdbxFile(db).Load(input, KdbxFormat.Default, null);
                edit(db);
                using (var output = new MemoryStream())
                {
                    new KdbxFile(db).Save(new LeaveOpenStream(output), null, KdbxFormat.Default, null); payload = output.ToArray();
                }
                using (var archive = ZipFile.Open(path, ZipArchiveMode.Update))
                { archive.GetEntry("working.kdbx").Delete(); using (var stream = archive.CreateEntry("working.kdbx").Open()) stream.Write(payload, 0, payload.Length); }
            }
            finally { db.Close(); }
        }
        private sealed class PwDatabaseScope : IDisposable
        {
            internal readonly PwDatabase Database = new PwDatabase();
            internal PwDatabaseScope(string path, KeePassLib.Keys.CompositeKey key) { try { Database.Open(IOConnectionInfo.FromPath(path), key, null); } catch { Database.Close(); throw; } }
            public void Dispose() { Database.Close(); }
        }
    }

    internal sealed partial class OrganizerWindow
    {
        internal async Task TestFinalAuditWorkflow(string directory, Action<bool, string> check)
        {
            heartbeat.Stop(); testClose = true; var fake = new IsolatedClipboard(); clipboard = fake;
            string sourceRoot = Path.Combine(directory, "source"), destinationRoot = Path.Combine(directory, "destination");
            Directory.CreateDirectory(sourceRoot); Directory.CreateDirectory(destinationRoot);
            string sourcePath = Tests.CreateFixture(sourceRoot, false), destinationPath = Tests.CreateFixture(destinationRoot, false);
            LoadSynthetic(sourcePath, Path.Combine(sourceRoot, "drafts"));
            var entry = session.Database.RootGroup.GetEntries(true).First();
            panes[0].Path.Add(entry.ParentGroup);
            panes[0].Expanded = entry.Uuid.ToHexString(); RenderAll();
            ShowGenerator(FieldBox(panes[0], entry, PwDefs.PasswordField));
            var output = Descendants<TextBox>(veil).Single(box => box.IsReadOnly);
            var data = new DataObject(DataFormats.UnicodeText, "fictional-generator-selection");
            output.RaiseEvent(new DataObjectCopyingEventArgs(data, false) { RoutedEvent = DataObject.CopyingEvent });
            fake.Write("fictional-generator-selection"); ClearOwnedClipboard(true);
            check(fake.Value == null, "Keyboard/context copy from the generator participates in app-owned clipboard cleanup");
            check(data.GetDataPresent("ExcludeClipboardContentFromMonitorProcessing"), "Normal text copies request exclusion from Windows clipboard history and cloud synchronization");
            check(new[] { "CanIncludeInClipboardHistory", "CanUploadToCloudClipboard" }.All(format => ((MemoryStream)data.GetData(format)).ToArray().SequenceEqual(new byte[4])) && data.GetData(DataFormats.UnicodeText) as string == "fictional-generator-selection", "Clipboard opt-out values are serialized zero DWORDs without changing copied text");
            CloseModal();
            var explicitCopy = ClipboardPayload.Text("fictional-explicit-copy");
            check(explicitCopy.GetDataPresent("ExcludeClipboardContentFromMonitorProcessing") && explicitCopy.GetData(DataFormats.UnicodeText) as string == "fictional-explicit-copy", "Explicit clipboard actions build the same protected payload without accessing the system clipboard");
            var conflict = new MergeConflict { Title = "Fictional comparison", MineEntry = entry, FileEntry = entry }; conflict.Fields.Add(PwDefs.PasswordField);
            Task<MergeChoice> review = ReviewConflict(conflict);
            var comparison = Descendants<TextBox>(veil).First(); data = new DataObject(DataFormats.UnicodeText, "fictional-review-copy");
            comparison.RaiseEvent(new DataObjectCopyingEventArgs(data, false) { RoutedEvent = DataObject.CopyingEvent }); fake.Write("fictional-review-copy"); ClearOwnedClipboard(true);
            check(fake.Value == null && data.GetDataPresent("ExcludeClipboardContentFromMonitorProcessing"), "Conflict-review text copying shares clipboard protection and cleanup");
            FinishReview(MergeChoice.Cancel); await review;
            data = new DataObject(DataFormats.UnicodeText, "fictional-drag-only");
            FieldBox(panes[0], entry, PwDefs.PasswordField).RaiseEvent(new DataObjectCopyingEventArgs(data, true) { RoutedEvent = DataObject.CopyingEvent });
            check(clipboardHash == null && !data.GetDataPresent("ExcludeClipboardContentFromMonitorProcessing"), "Text drag data does not claim clipboard ownership");
            session.Set(entry, PwDefs.NotesField, "Fictional legacy draft review"); session.AcceptDraft(await session.CheckpointAsync());
            Tests.RewriteDraftWorking(session.DraftPath, session.Database.MasterKey, db => db.CustomData.Remove("VaultOrganizer.PrivateDraft.Components"));
            Task<MergePlan> legacyReview = ReadDraftForReview(session); await WaitFor(() => modalChoice != null || legacyReview.IsCompleted);
            check(modalChoice != null && !legacyReview.IsCompleted, "Older draft format requires a distinct compatibility review before resuming");
            CompleteChoice(2); using (var legacy = await legacyReview) check(legacy != null && legacy.LegacyRecovery, "Explicit compatibility review can resume a valid legacy checkpoint");
            entry.Strings.Set(PwDefs.TitleField, new KeePassLib.Security.ProtectedString(true, VaultSession.Read(entry, PwDefs.TitleField))); session.Changed();
            otherSession = VaultSession.Open(destinationPath, VaultSession.MakeKey("demo", true, null, false));
            otherSession.ConfigureDraftRoot(Path.Combine(destinationRoot, "drafts")); BuildPanes(); SwitchMode(true);
            string entryId = entry.Uuid.ToHexString(); int replacements = 0;
            VaultSession.BeforeSaveReplace = () =>
            {
                if (++replacements != 4) return;
                VaultSession.BeforeSaveReplace = null;
                Tests.WriteOutside(sourcePath, db => { db.MasterKey = VaultSession.MakeKey("different-fictional-key", true, null, false); db.MasterKeyChanged = DateTime.UtcNow; });
            };
            try
            {
                Task transfer = CrossDatabaseDrop(session, otherSession, entry, otherSession.Database.RootGroup, null);
                await WaitFor(() => modalChoice != null); CompleteChoice(2); await transfer;
            }
            finally { VaultSession.BeforeSaveReplace = null; }
            check(session.PublishingBlocked && session.Database.RootGroup.GetEntries(true).Any(item => item.Uuid.ToHexString() == entryId), "An unreadable late source version blocks further publication and restores the original working card");
            string preserved = Directory.GetFiles(sourcePath + ".versions", "concurrent-*.kdbx").Single();
            var late = new PwDatabase();
            try { late.Open(IOConnectionInfo.FromPath(preserved), VaultSession.MakeKey("different-fictional-key", true, null, false), null); check(late.RootGroup.GetEntries(true).Any(item => item.Uuid.ToHexString() == entryId), "The changed-key late source remains preserved and readable using its own fictional factors"); }
            finally { late.Close(); }
            check(!await SaveNow(true), "Retry cannot bypass unreadable source-version review after a transfer");
            await Seal(false); check(session == null && otherSession == null, "A blocked transfer can still checkpoint local work and lock safely");
            StopTimers(); Microsoft.Win32.SystemEvents.SessionSwitch -= OnSystemSession;
        }
    }
}
#endif
