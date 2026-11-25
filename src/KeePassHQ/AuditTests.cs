#if DIAGNOSTICS
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using KeePassLib;

namespace VaultOrganizer
{
    internal static partial class Tests
    {
        private static void AuditChecks(string run)
        {
            string path = Fixture(run, "previous-draft", false), directory = Path.GetDirectoryName(path);
            string draft;
            using (var working = OpenTest(path, directory))
            {
                draft = working.DraftPath;
                working.Set(Rich(working.Database), PwDefs.NotesField, "Earlier fictional checkpoint");
                working.AcceptDraft(working.CheckpointAsync().GetAwaiter().GetResult());
                working.Set(Rich(working.Database), PwDefs.NotesField, "Latest fictional checkpoint");
                working.AcceptDraft(working.CheckpointAsync().GetAwaiter().GetResult());
            }
            byte[] latest = File.ReadAllBytes(draft);
            File.Move(draft, draft + ".retained");
            using (var working = OpenTest(path, directory))
            {
                Check(working.DraftExists, "A previous checkpoint remains discoverable when its primary bundle is missing");
                using (var plan = working.ResumeDraftAsync().GetAwaiter().GetResult()) working.ApplyMerge(plan);
                Check(VaultSession.Read(Rich(working.Database), PwDefs.NotesField) == "Earlier fictional checkpoint", "Backup-only recovery restores its authenticated earlier working state");
                Save(working); working.RemoveDraft();
                Check(!working.DraftExists && latest.SequenceEqual(File.ReadAllBytes(draft + ".retained")), "Saving recovered work removes its own branch and preserves unrelated retained material");
            }
            path = Fixture(run, "damaged-draft", false); directory = Path.GetDirectoryName(path);
            using (var working = OpenTest(path, directory))
            {
                draft = working.DraftPath;
                working.Set(Rich(working.Database), PwDefs.NotesField, "Earlier authenticated work");
                working.AcceptDraft(working.CheckpointAsync().GetAwaiter().GetResult());
                working.Set(Rich(working.Database), PwDefs.NotesField, "Latest work before damage");
                working.AcceptDraft(working.CheckpointAsync().GetAwaiter().GetResult());
            }
            File.WriteAllText(draft, "Fictional damaged checkpoint"); byte[] damaged = File.ReadAllBytes(draft);
            using (var working = OpenTest(path, directory))
            {
                Check(working.DraftExists && working.HasPreviousCheckpoint && !working.DraftIsPreviousCheckpoint, "A damaged primary exposes an explicit previous-checkpoint option");
                bool rejected = false;
                try { using (var plan = working.ResumeDraftAsync().GetAwaiter().GetResult()) { } } catch (InvalidDataException) { rejected = true; }
                Check(rejected, "Damage never silently selects older work");
                using (var plan = working.ResumeDraftAsync(true).GetAwaiter().GetResult()) working.ApplyMerge(plan);
                Check(VaultSession.Read(Rich(working.Database), PwDefs.NotesField) == "Earlier authenticated work", "Explicit fallback authenticates and restores the earlier checkpoint");
                Save(working); working.RemoveDraft();
                Check(damaged.SequenceEqual(File.ReadAllBytes(draft)), "Successful fallback preserves the damaged primary for separate review");
            }
            path = Fixture(run, "unreadable-preimage", false); directory = Path.GetDirectoryName(path);
            string preimage = null; byte[] kept = null;
            using (var working = OpenTest(path, directory))
            {
                working.Set(Rich(working.Database), PwDefs.NotesField, "Local fictional save");
                FileStream held = null;
                VaultSession.BeforeSaveReplace = () => { VaultSession.BeforeSaveReplace = null; WriteOutside(path, db => ExternalSet(Rich(db), PwDefs.UserNameField, "late-unreadable@example.test")); };
                VaultSession.AfterSaveReplace = previous => { preimage = previous; held = new FileStream(previous, FileMode.Open, FileAccess.Read, FileShare.None); };
                bool stopped = false;
                try { Save(working); } catch (IOException) { stopped = true; working.SaveFailed(); }
                finally { VaultSession.BeforeSaveReplace = null; VaultSession.AfterSaveReplace = null; if (held != null) held.Dispose(); }
                Check(stopped, "A post-replacement preimage read failure stops publication completion");
                kept = File.ReadAllBytes(preimage);
                var priorDatabase = new PwDatabase();
                try
                {
                    priorDatabase.Open(KeePassLib.Serialization.IOConnectionInfo.FromPath(preimage), working.Database.MasterKey, null);
                    Check(VaultSession.Read(Rich(priorDatabase), PwDefs.UserNameField) == "late-unreadable@example.test", "The unreadable preimage retains the late writer's complete encrypted database");
                }
                finally { priorDatabase.Close(); }
                bool blocked = false;
                try { Save(working); } catch (IOException ex) { blocked = ex.Message.Contains("recovery copy"); working.SaveFailed(); }
                Check(blocked && kept.SequenceEqual(File.ReadAllBytes(preimage)), "Retry cannot roll over an unverified competing version");
                string recovery = Path.Combine(directory, "fictional-recovery.kdbx");
                working.RecoveryCopyAsync(recovery).GetAwaiter().GetResult();
                Check(File.Exists(recovery) && kept.SequenceEqual(File.ReadAllBytes(preimage)), "Recovery saves local work without replacing the preserved late writer");
            }
            using (var reopened = OpenTest(path, directory))
            {
                reopened.Set(Rich(reopened.Database), PwDefs.NotesField, "Fictional later session"); Save(reopened);
                Check(kept.SequenceEqual(File.ReadAllBytes(preimage)), "Later sessions cannot overwrite a uniquely retained unverified preimage");
            }
        }
    }

    internal sealed partial class OrganizerWindow
    {
        internal async Task TestAuditWorkflow(string directory, Action<bool, string> check)
        {
            heartbeat.Stop(); clipboard = new IsolatedClipboard(); testClose = true;
            DatabaseDialog(null, true);
            var secrets = Descendants<PasswordBox>(veil).ToArray();
            foreach (var secret in secrets) secret.Password = "fictional-dialog-only-key";
            await Seal(false);
            check(veil.Visibility == Visibility.Collapsed && secrets.All(secret => secret.Password.Length == 0), "Lock clears creation-dialog key factors even without a loaded database");
            opening = true; password.Password = "fictional-opening-key"; await Seal(false);
            check(lockAfterOpen && password.Password.Length == 0, "A lock requested during decryption is queued and clears the open-screen password");
            opening = lockAfterOpen = false;
            foreach (bool transfer in new[] { false, true })
            {
                changingDatabase = !transfer; transferring = transfer;
                Task<MergeChoice> review = ReviewConflict(new MergeConflict { Title = "Fictional pending merge" });
                await Seal(false);
                check(review.IsCompleted && await review == MergeChoice.Cancel && (transfer ? lockAfterTransfer : lockAfterChange), "Lock cancels a pending conflict decision and queues closure through the active workflow");
                changingDatabase = transferring = lockAfterTransfer = lockAfterChange = false;
            }
            Directory.CreateDirectory(directory);
            string path = Tests.CreateFixture(directory, false), drafts = Path.Combine(directory, "drafts");
            LoadSynthetic(path, drafts);
            session.Set(session.Database.RootGroup.GetEntries(true).First(), PwDefs.NotesField, "Uncheckpointed fictional edit");
            dirtyTicks = System.Diagnostics.Stopwatch.GetTimestamp() - 240 * System.Diagnostics.Stopwatch.Frequency;
            lastNudgeTicks = lastStrongTicks = 0; NudgeSave();
            check(Descendants<TextBlock>(veil).Any(text => text.Text.Contains("not yet protected")), "Save reminders do not claim uncheckpointed edits are protected");
            CloseModal(); check(await ProtectDraft(), "Fictional latest edit checkpoints in its isolated root");
            lastNudgeTicks = lastStrongTicks = 0; NudgeSave();
            check(Descendants<TextBlock>(veil).Any(text => text.Text.Contains("edits are protected")), "Save reminders recognize a verified current checkpoint");
            CloseModal();
            session.Set(session.Database.RootGroup.GetEntries(true).First(), PwDefs.NotesField, "Later fictional edit");
            check(await ProtectDraft(), "A second checkpoint creates a previous recovery version");
            string damagedDraft = session.DraftPath; File.WriteAllText(damagedDraft, "Fictional unreadable draft");
            Task<MergePlan> fallback = ReadDraftForReview(session);
            await WaitFor(() => modalChoice != null || fallback.IsCompleted);
            check(modalChoice != null && !fallback.IsCompleted && Descendants<Button>(veil).Any(button => button.Content as string == "Resume previous"), "A damaged draft asks before selecting an older checkpoint");
            CompleteChoice(2);
            using (var plan = await fallback)
                check(plan != null && plan.Local.RootGroup.GetEntries(true).Any(entry => VaultSession.Read(entry, PwDefs.NotesField) == "Uncheckpointed fictional edit"), "Explicit UI fallback returns the authenticated previous working model");
            check(File.ReadAllText(damagedDraft) == "Fictional unreadable draft", "UI fallback does not overwrite damaged evidence");
            session.RemoveDraft(); DisposeSession();
            StopTimers(); SystemEvents.SessionSwitch -= OnSystemSession;
        }
    }
}
#endif
