using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using KeePassLib;
using KeePassLib.Collections;
using KeePassLib.Keys;
using KeePassLib.Security;
using KeePassLib.Serialization;
using KeePassLib.Cryptography.KeyDerivation;

namespace VaultOrganizer
{
    internal sealed class SaveResult
    {
        public long Revision;
        public byte[] Hash;
        public PwDatabase Baseline;
        public string ConcurrentPath;
        public PwDatabase ConcurrentBase;
    }

    internal sealed partial class VaultSession : IDisposable
    {
        public PwDatabase Database;
        public string PathName;
        public long Revision;
        public long SavedRevision;
        public DateTime LastSavedUtc;
        private byte[] expectedHash;
        private FileStream gate;
        private string firstBackup;
        private bool saving;
        private PwDatabase baseline;
        private bool replacementBlocked;
#if DIAGNOSTICS
        internal static Action<string> BeforeRecoveryPublish;
        internal static Action<string> AfterSaveWrite;
        internal static Action BeforeSaveReplace;
#endif
        public bool Dirty { get { return Revision != SavedRevision; } }

        public static CompositeKey MakeKey(string password, bool usePassword, string keyFile, bool windowsAccount)
        {
            var key = new CompositeKey();
            if (usePassword) key.AddUserKey(new KcpPassword(password ?? ""));
            if (!String.IsNullOrWhiteSpace(keyFile)) key.AddUserKey(new KcpKeyFile(System.IO.Path.GetFullPath(keyFile)));
            if (windowsAccount) key.AddUserKey(new KcpUserAccount());
            if (key.UserKeyCount == 0) throw new InvalidOperationException("Select a password, key file, or Windows account key.");
            return key;
        }

        public static VaultSession Open(string path, CompositeKey key)
        {
            path = System.IO.Path.GetFullPath(path);
            if (!String.Equals(System.IO.Path.GetExtension(path), ".kdbx", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Choose a KeePass .kdbx database.");
            var session = new VaultSession();
            try
            {
                session.PathName = path;
                session.ConfigureDraftRoot(null);
                session.gate = new FileStream(path + ".organizer-lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
                byte[] before = HashFile(path);
                session.Database = new PwDatabase();
                session.Database.Open(IOConnectionInfo.FromPath(path), key, null);
                session.expectedHash = HashFile(path);
                if (!before.SequenceEqual(session.expectedHash)) throw new IOException("The database changed while it was opening. Please reopen it.");
                ValidateTree(session.Database);
                session.baseline = Snapshot(session.Database);
                session.LastSavedUtc = DateTime.UtcNow;
                return session;
            }
            catch { session.Dispose(); throw; }
        }

        public void Changed() { ++Revision; Database.Modified = true; }

        public static string Read(PwEntry e, string field) { return e.Strings.ReadSafe(field); }

        public void Set(PwEntry e, string field, string text)
        {
            bool protect = field == PwDefs.PasswordField;
            ProtectedString prior = e.Strings.Get(field);
            if (prior != null) protect = prior.IsProtected;
            e.Strings.Set(field, new ProtectedString(protect, text ?? ""));
            e.Touch(true, false); Changed();
        }

        public PwEntry NewEntry(PwGroup parent)
        {
            var e = new PwEntry(true, true);
            e.Strings.Set(PwDefs.TitleField, new ProtectedString(false, "Untitled"));
            parent.AddEntry(e, true, true); parent.Touch(true, false); Changed(); return e;
        }

        public PwGroup NewGroup(PwGroup parent, string name)
        {
            var g = new PwGroup(true, true, name, PwIcon.Folder);
            parent.AddGroup(g, true, true); parent.Touch(true, false); Changed(); return g;
        }

        public bool CanMove(object item, PwGroup target)
        {
            if (target == null) return false;
            var group = item as PwGroup;
            var entry = item as PwEntry;
            if (group == null && entry == null) return false;
            if (group == Database.RootGroup) return false;
            if (entry != null) return entry.ParentGroup != target;
            for (PwGroup g = target; g != null; g = g.ParentGroup) if (g == group) return false;
            return group.ParentGroup != target;
        }

        public void Move(object item, PwGroup target)
        {
            if (!CanMove(item, target)) throw new InvalidOperationException("Choose another group; a group cannot be moved inside itself.");
            var group = item as PwGroup;
            if (group != null)
            {
                PwGroup from = group.ParentGroup;
                from.Groups.Remove(group); group.PreviousParentGroup = from.Uuid;
                target.AddGroup(group, true, true); group.Touch(true, false); from.Touch(true, false);
            }
            else
            {
                var entry = (PwEntry)item; PwGroup from = entry.ParentGroup;
                from.Entries.Remove(entry); entry.PreviousParentGroup = from.Uuid;
                target.AddEntry(entry, true, true); entry.Touch(true, false); from.Touch(true, false);
            }
            target.Touch(true, false); Changed();
        }

        internal static PwDatabase Snapshot(PwDatabase source)
        {
            var result = new PwDatabase();
            // All scalar metadata, cipher selections, UUID references, and settings are retained.
            foreach (PropertyInfo p in typeof(PwDatabase).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!p.CanWrite || !p.CanRead || p.GetIndexParameters().Length != 0) continue;
                if (new[] { "RootGroup", "MasterKey", "KdfParameters", "MemoryProtection", "CustomData", "PublicCustomData" }.Contains(p.Name)) continue;
                p.SetValue(result, p.GetValue(source, null), null);
            }
            result.RootGroup = source.RootGroup.CloneDeep();
            result.MasterKey = source.MasterKey;
            result.KdfParameters = KdfParameters.DeserializeExt(KdfParameters.SerializeExt(source.KdfParameters));
            result.MemoryProtection = source.MemoryProtection.CloneDeep();
            typeof(PwDatabase).GetProperty("CustomData").SetValue(result, source.CustomData.CloneDeep(), null);
            VariantDictionary.Deserialize(VariantDictionary.Serialize(source.PublicCustomData)).CopyTo(result.PublicCustomData);
            foreach (PwDeletedObject d in source.DeletedObjects) result.DeletedObjects.Add(d.CloneDeep());
            foreach (PwCustomIcon icon in source.CustomIcons)
            {
                var c = new PwCustomIcon(icon.Uuid, (byte[])icon.ImageDataPng.Clone());
                c.Name = icon.Name; c.LastModificationTime = icon.LastModificationTime;
                result.CustomIcons.Add(c);
            }
            return result;
        }

        public Task<SaveResult> SaveAsync()
        {
            if (HasPendingMerge) throw new InvalidOperationException("Resolve the preserved competing version before publishing.");
            if (replacementBlocked) throw new IOException("A competing version is preserved in the previous-version backup. It could not be secured for automatic merge. Save a recovery copy and resolve the competing files before publishing again.");
            if (saving) throw new InvalidOperationException("A save is already running.");
            saving = true;
            PwDatabase snapshot;
            try { ValidateTree(Database); snapshot = Snapshot(Database); }
            catch { saving = false; throw; }
            long revision = Revision;
            return Task.Run(() =>
            {
                string temp = null;
                try
                {
                    using (var fileLock = new FileLock(IOConnectionInfo.FromPath(PathName)))
                    {
                        if (!HashFile(PathName).SequenceEqual(expectedHash))
                            throw new ExternalChangeException();
                        string dir = System.IO.Path.GetDirectoryName(PathName);
                        string backupDir = System.IO.Path.Combine(dir, System.IO.Path.GetFileName(PathName) + ".versions");
                        Directory.CreateDirectory(backupDir);
                        if (firstBackup == null)
                        {
                            string proposed = System.IO.Path.Combine(backupDir, "session-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".kdbx");
                            File.Copy(PathName, proposed, false);
                            if (!HashFile(proposed).SequenceEqual(expectedHash)) throw new ExternalChangeException();
                            firstBackup = proposed;
                        }
                        temp = System.IO.Path.Combine(dir, ".vdo-save-" + Guid.NewGuid().ToString("N") + ".tmp");
                        WriteEncrypted(snapshot, temp);
#if DIAGNOSTICS
                        if (AfterSaveWrite != null) AfterSaveWrite(temp);
#endif
                        // Full authenticated readback before replacing the last good database.
                        VerifyEncrypted(temp, snapshot.MasterKey);
                        if (!HashFile(PathName).SequenceEqual(expectedHash))
                            throw new ExternalChangeException();
                        byte[] hash = HashFile(temp);
#if DIAGNOSTICS
                        if (BeforeSaveReplace != null) BeforeSaveReplace();
#endif
                        string previousPath = System.IO.Path.Combine(backupDir, "previous.kdbx");
                        File.Replace(temp, PathName, previousPath, true);
                        temp = null;
                        var result = new SaveResult { Revision = revision, Hash = hash, Baseline = snapshot };
                        if (!HashFile(previousPath).SequenceEqual(expectedHash))
                        {
                            string preserved = System.IO.Path.Combine(backupDir, "concurrent-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".kdbx");
                            try { File.Move(previousPath, preserved); }
                            catch { replacementBlocked = true; throw new IOException("A competing version is preserved in previous.kdbx, but could not be secured for automatic merge. No further publishing is permitted in this session; save a recovery copy."); }
                            result.ConcurrentPath = preserved; result.ConcurrentBase = Snapshot(baseline);
                        }
                        snapshot = null; return result;
                    }
                }
                finally
                {
                    if (temp != null && File.Exists(temp)) { try { File.Delete(temp); } catch { } }
                    if (snapshot != null) snapshot.Close();
                }
            });
        }

        public void AcceptSave(SaveResult result)
        {
            InvalidateDraftCache();
            expectedHash = result.Hash; SavedRevision = result.Revision;
            if (baseline != null) baseline.Close(); baseline = result.Baseline;
            LastSavedUtc = DateTime.UtcNow; Database.Modified = Dirty; saving = false;
        }
        public void SaveFailed() { saving = false; }
        public void BlockUnresolvedPublishing() { replacementBlocked = true; }

        public Task<MergePlan> PrepareMergeAsync(string candidatePath = null, PwDatabase candidateBase = null)
        {
            var plan = new MergePlan { Local = Snapshot(Database), Baseline = Snapshot(candidateBase ?? baseline), Revision = Revision };
            string source = candidatePath ?? PathName;
            if (candidatePath != null) { plan.DiskBaseline = Snapshot(baseline); plan.FileHash = (byte[])expectedHash.Clone(); }
            return Task.Run(() =>
            {
                try
                {
                    byte[] before = HashFile(source);
                    plan.External = new PwDatabase(); plan.External.Open(IOConnectionInfo.FromPath(source), Database.MasterKey, null);
                    byte[] after = HashFile(source);
                    if (candidatePath == null) plan.FileHash = after;
                    if (!before.SequenceEqual(after)) throw new IOException("The file kept changing while it was read. Try Save again when the other app finishes.");
                    if (!plan.External.RootGroup.Uuid.Equals(plan.Local.RootGroup.Uuid)) throw new IOException("The file was replaced by a different database. Save a recovery copy of your edits before reopening it.");
                    plan.Analyze(); return plan;
                }
                catch { plan.Dispose(); throw; }
            });
        }
        public void ApplyMerge(MergePlan plan)
        {
            if (Revision != plan.Revision) throw new InvalidOperationException("The working copy changed during merge review. Retry Save.");
            PwDatabase merged = plan.Build();
            InvalidateDraftCache();
            if (baseline != null) baseline.Close(); baseline = Snapshot(plan.DiskBaseline ?? plan.External);
            Database.Close(); Database = merged; expectedHash = plan.FileHash;
            if (plan.CompletesPending) ClearPending();
            if (plan.RecoveredPending != null) HoldPending(plan.RecoveredPending.File, plan.RecoveredPending.Baseline);
            Changed();
        }

        public Task RecoveryCopyAsync(string path)
        {
            string dest = System.IO.Path.GetFullPath(path);
            if (String.Equals(dest, PathName, StringComparison.OrdinalIgnoreCase) || File.Exists(dest))
                throw new IOException("Choose a new filename for the recovery copy.");
            PwDatabase snapshot = Snapshot(Database);
            return Task.Run(() =>
            {
                string temp = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(dest), ".recovery-" + Guid.NewGuid().ToString("N") + ".tmp");
                try {
                    WriteEncrypted(snapshot, temp); VerifyEncrypted(temp, snapshot.MasterKey);
#if DIAGNOSTICS
                    if (BeforeRecoveryPublish != null) BeforeRecoveryPublish(dest);
#endif
                    File.Move(temp, dest); temp = null;
                }
                finally { if (temp != null && File.Exists(temp)) { try { File.Delete(temp); } catch { } } snapshot.Close(); }
            });
        }

        internal static void WriteEncrypted(PwDatabase db, string path)
        {
            ValidateTree(db);
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { new KdbxFile(db).Save(new LeaveOpenStream(stream), null, KdbxFormat.Default, null); stream.Flush(true); }
        }
        internal static void VerifyEncrypted(string path, CompositeKey key)
        {
            var verify = new PwDatabase();
            try { verify.Open(IOConnectionInfo.FromPath(path), key, null); }
            finally { verify.Close(); }
        }
        internal static byte[] HashFile(string path)
        {
            using (var hash = SHA256.Create())
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) return hash.ComputeHash(stream);
        }
        public void Dispose()
        {
            ClearPending();
            InvalidateDraftCache();
            if (Database != null) { Database.Close(); Database = null; }
            if (baseline != null) { baseline.Close(); baseline = null; }
            if (gate != null) { gate.Dispose(); gate = null; }
            if (expectedHash != null) { Array.Clear(expectedHash, 0, expectedHash.Length); expectedHash = null; }
        }
    }

    // KeePass owns/disposes its supplied stream. Keep the actual file open for Flush(true).
    internal sealed class LeaveOpenStream : Stream
    {
        private readonly Stream inner;
        public LeaveOpenStream(Stream stream) { inner = stream; }
        public override bool CanRead { get { return inner.CanRead; } }
        public override bool CanSeek { get { return inner.CanSeek; } }
        public override bool CanWrite { get { return inner.CanWrite; } }
        public override long Length { get { return inner.Length; } }
        public override long Position { get { return inner.Position; } set { inner.Position = value; } }
        public override void Flush() { inner.Flush(); }
        public override int Read(byte[] buffer, int offset, int count) { return inner.Read(buffer, offset, count); }
        public override long Seek(long offset, SeekOrigin origin) { return inner.Seek(offset, origin); }
        public override void SetLength(long length) { inner.SetLength(length); }
        public override void Write(byte[] buffer, int offset, int count) { inner.Write(buffer, offset, count); }
        protected override void Dispose(bool disposing) { if (disposing) inner.Flush(); }
    }
}
