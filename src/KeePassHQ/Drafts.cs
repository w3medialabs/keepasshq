using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using KeePassLib;
using KeePassLib.Keys;
using KeePassLib.Serialization;

namespace VaultOrganizer
{
    internal sealed class DraftResult { public long Revision; public DateTime When; }
    internal sealed partial class VaultSession
    {
        public string DraftPath;
        private string draftPrefix, selectedDraft;
        private byte[] baselineCiphertext;
        private string draftNonce = Guid.NewGuid().ToString("N");
        private const string RoleKey = "VaultOrganizer.PrivateDraft.Role", NonceKey = "VaultOrganizer.PrivateDraft.Nonce";
        private const string ComponentsKey = "VaultOrganizer.PrivateDraft.Components";
        private const string BasicComponents = "working.kdbx,baseline.kdbx", PendingComponents = BasicComponents + ",pending-file.kdbx,pending-base.kdbx";
#if DIAGNOSTICS
        internal static Action BeforeDraftPublish;
        internal static Action<string> AfterDraftWrite;
        internal static string IsolatedDraftRoot;
#endif
        public long DraftRevision = -1;
        public DateTime LastDraftUtc = DateTime.MinValue;
        public bool DraftExists { get { return RecoveryCandidate() != null; } }
        public bool DraftIsPreviousCheckpoint { get { string path = RecoveryCandidate(); return path != null && path.EndsWith(".vdo-draft.previous", StringComparison.OrdinalIgnoreCase); } }
        public bool HasPreviousCheckpoint { get { string path = RecoveryCandidate(); return path != null && !DraftIsPreviousCheckpoint && File.Exists(path + ".previous"); } }
        public void ConfigureDraftRoot(string root)
        {
#if DIAGNOSTICS
            root = root ?? IsolatedDraftRoot;
            if (root == null) throw new InvalidOperationException("Diagnostics require an isolated draft root.");
#endif
            root = root ?? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VaultOrganizer", "Drafts");
            string token;
            using (var sha = SHA256.Create()) token = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(PathName.ToUpperInvariant()))).Replace("-", "");
            draftPrefix = System.IO.Path.Combine(root, token.Substring(0, 32));
            DraftPath = draftPrefix + "." + Guid.NewGuid().ToString("N") + ".vdo-draft";
            selectedDraft = null;
            draftNonce = Guid.NewGuid().ToString("N"); InvalidateDraftCache();
        }
        private void InvalidateDraftCache() { if (baselineCiphertext != null) Array.Clear(baselineCiphertext, 0, baselineCiphertext.Length); baselineCiphertext = null; }
        private static byte[] EncryptBytes(PwDatabase db)
        { using (var stream = new MemoryStream()) { new KdbxFile(db).Save(new LeaveOpenStream(stream), null, KdbxFormat.Default, null); return stream.ToArray(); } }
        private static byte[] Digest(Stream stream) { using (var hash = SHA256.Create()) return hash.ComputeHash(stream); }
        private string RecoveryCandidate()
        {
            if (File.Exists(DraftPath)) return DraftPath;
            if (selectedDraft != null && File.Exists(selectedDraft)) return selectedDraft;
            string root = System.IO.Path.GetDirectoryName(draftPrefix);
            if (!Directory.Exists(root)) return null;
            selectedDraft = Directory.GetFiles(root, System.IO.Path.GetFileName(draftPrefix) + ".*.vdo-draft*")
                .Where(path => path.EndsWith(".vdo-draft", StringComparison.OrdinalIgnoreCase) ||
                    (path.EndsWith(".vdo-draft.previous", StringComparison.OrdinalIgnoreCase) && !File.Exists(path.Substring(0, path.Length - ".previous".Length))))
                .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            return selectedDraft;
        }
        private static void ZipDatabase(ZipArchive archive, string name, PwDatabase db)
        { using (var stream = archive.CreateEntry(name, CompressionLevel.NoCompression).Open()) new KdbxFile(db).Save(new LeaveOpenStream(stream), null, KdbxFormat.Default, null); }
        private static PwDatabase ReadDatabase(ZipArchive archive, string name, CompositeKey key)
        {
            var entry = archive.GetEntry(name); if (entry == null) throw new InvalidDataException("Incomplete encrypted draft.");
            var db = new PwDatabase(); db.MasterKey = key;
            try { using (var stream = entry.Open()) new KdbxFile(db).Load(stream, KdbxFormat.Default, null); ValidateTree(db); return db; }
            catch { db.Close(); throw; }
        }
        public Task<DraftResult> CheckpointAsync()
        {
            ValidateTree(Database); var current = Snapshot(Database); var original = baselineCiphertext == null ? Snapshot(baseline) : null; long revision = Revision;
            current.CustomData.Set(RoleKey, "working"); current.CustomData.Set(NonceKey, draftNonce);
            current.CustomData.Set(ComponentsKey, pending == null ? BasicComponents : PendingComponents);
            if (original != null) { original.CustomData.Set(RoleKey, "baseline"); original.CustomData.Set(NonceKey, draftNonce); }
            PendingMergeData keepPending = pending == null ? null : pending.Clone();
            if (keepPending != null) { keepPending.File.CustomData.Set(RoleKey, "pending-file"); keepPending.File.CustomData.Set(NonceKey, draftNonce); keepPending.Baseline.CustomData.Set(RoleKey, "pending-base"); keepPending.Baseline.CustomData.Set(NonceKey, draftNonce); }
            string destination = DraftPath;
            return Task.Run(() =>
            {
                string temp = null;
                try
                {
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination));
                    if (baselineCiphertext == null)
                    {
                        byte[] blob = EncryptBytes(original);
                        var verify = new PwDatabase(); verify.MasterKey = original.MasterKey;
                        try { using (var memory = new MemoryStream(blob, false)) new KdbxFile(verify).Load(memory, KdbxFormat.Default, null); VerifyContent(original, verify); }
                        finally { verify.Close(); }
                        baselineCiphertext = blob;
                    }
                    temp = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(destination), ".draft-" + Guid.NewGuid().ToString("N") + ".tmp");
                    using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
                    {
                        using (var archive = new ZipArchive(file, ZipArchiveMode.Create, true))
                        { ZipDatabase(archive, "working.kdbx", current); using (var stream = archive.CreateEntry("baseline.kdbx", CompressionLevel.NoCompression).Open()) stream.Write(baselineCiphertext, 0, baselineCiphertext.Length); if (keepPending != null) { ZipDatabase(archive,"pending-file.kdbx",keepPending.File); ZipDatabase(archive,"pending-base.kdbx",keepPending.Baseline); } }
                        file.Flush(true);
                    }
#if DIAGNOSTICS
                    if (AfterDraftWrite != null) AfterDraftWrite(temp);
#endif
                    using (var file = File.OpenRead(temp)) using (var archive = new ZipArchive(file, ZipArchiveMode.Read))
                    {
                        var a = ReadDatabase(archive, "working.kdbx", current.MasterKey); try { VerifyContent(current, a); } finally { a.Close(); }
                        if (keepPending != null)
                        {
                            var f = ReadDatabase(archive,"pending-file.kdbx",current.MasterKey); try { VerifyContent(keepPending.File, f); } finally { f.Close(); }
                            var b = ReadDatabase(archive,"pending-base.kdbx",current.MasterKey); try { VerifyContent(keepPending.Baseline, b); } finally { b.Close(); }
                        }
                        using (var stream = archive.GetEntry("baseline.kdbx").Open()) using (var memory = new MemoryStream(baselineCiphertext, false))
                            if (!Digest(stream).SequenceEqual(Digest(memory))) throw new InvalidDataException("Draft baseline readback failed.");
                    }
#if DIAGNOSTICS
                    if (BeforeDraftPublish != null) BeforeDraftPublish();
#endif
                    if (File.Exists(destination)) File.Replace(temp, destination, destination + ".previous", true); else File.Move(temp, destination);
                    temp = null; return new DraftResult { Revision = revision, When = DateTime.UtcNow };
                }
                finally { if (temp != null && File.Exists(temp)) { try { File.Delete(temp); } catch { } } current.Close(); if (original != null) original.Close(); if (keepPending != null) keepPending.Dispose(); }
            });
        }
        public void AcceptDraft(DraftResult result) { DraftRevision = result.Revision; LastDraftUtc = result.When; }
        public void RemoveDraft()
        {
            foreach (string file in new[] { DraftPath, selectedDraft }.Where(p => p != null).Distinct())
                foreach (string path in new[] { file, file + ".previous" }) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
            selectedDraft = null;
        }
        public void ReleaseFileGate() { if (gate != null) { gate.Dispose(); gate = null; } }
        public Task<MergePlan> ResumeDraftAsync(bool previousCheckpoint = false)
        {
            var plan = new MergePlan { External = Snapshot(Database), FileHash = (byte[])expectedHash.Clone(), Revision = Revision };
            string source = RecoveryCandidate(); CompositeKey key = Database.MasterKey;
            if (previousCheckpoint) source = source == null || DraftIsPreviousCheckpoint ? null : source + ".previous";
            return Task.Run(() =>
            {
                try
                {
                    if (source == null) throw new FileNotFoundException("No recoverable draft exists.");
                    using (var file = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var archive = new ZipArchive(file, ZipArchiveMode.Read))
                    {
                        plan.Local = ReadDatabase(archive, "working.kdbx", key); plan.Baseline = ReadDatabase(archive, "baseline.kdbx", key);
                        bool hasPending = archive.GetEntry("pending-file.kdbx") != null || archive.GetEntry("pending-base.kdbx") != null;
                        string components = hasPending ? PendingComponents : BasicComponents;
                        string declared = plan.Local.CustomData.Get(ComponentsKey);
                        plan.LegacyRecovery = declared == null;
                        // Older drafts have no manifest. Keep their format readable; new drafts
                        // bind the complete component inventory inside authenticated ciphertext.
                        if ((declared != null && declared != components) || !archive.Entries.Select(entry => entry.FullName).OrderBy(name => name, StringComparer.Ordinal).SequenceEqual(components.Split(',').OrderBy(name => name, StringComparer.Ordinal)))
                            throw new InvalidDataException("Encrypted draft component inventory is incomplete or ambiguous.");
                        plan.Local.CustomData.Remove(ComponentsKey);
                        if (hasPending)
                        {
                            plan.RecoveredPending = new PendingMergeData();
                            plan.RecoveredPending.File = ReadDatabase(archive,"pending-file.kdbx",key); plan.RecoveredPending.Baseline = ReadDatabase(archive,"pending-base.kdbx",key);
                            var f=plan.RecoveredPending.File;var b=plan.RecoveredPending.Baseline;
                            if(f.CustomData.Get(RoleKey)!="pending-file"||b.CustomData.Get(RoleKey)!="pending-base"||f.CustomData.Get(NonceKey)!=plan.Local.CustomData.Get(NonceKey)||b.CustomData.Get(NonceKey)!=plan.Local.CustomData.Get(NonceKey)||!f.RootGroup.Uuid.Equals(plan.Local.RootGroup.Uuid)||!b.RootGroup.Uuid.Equals(plan.Local.RootGroup.Uuid))throw new InvalidDataException("Pending merge draft binding is invalid.");
                            foreach(var db in new[]{f,b}){db.CustomData.Remove(RoleKey);db.CustomData.Remove(NonceKey);}
                        }
                    }
                    if (plan.Local.CustomData.Get(RoleKey) != "working" || plan.Baseline.CustomData.Get(RoleKey) != "baseline" || String.IsNullOrEmpty(plan.Local.CustomData.Get(NonceKey)) || plan.Local.CustomData.Get(NonceKey) != plan.Baseline.CustomData.Get(NonceKey)) throw new InvalidDataException("Draft pairing or roles are invalid.");
                    foreach (var db in new[] { plan.Local, plan.Baseline }) { db.CustomData.Remove(RoleKey); db.CustomData.Remove(NonceKey); }
                    if (!plan.Local.RootGroup.Uuid.Equals(plan.External.RootGroup.Uuid) || !plan.Baseline.RootGroup.Uuid.Equals(plan.External.RootGroup.Uuid)) throw new InvalidDataException("Draft belongs to another database.");
                    plan.Analyze();
                    // An explicitly selected fallback is a separate recovered branch. Keep the
                    // damaged primary for inspection instead of overwriting/removing it later.
                    if (previousCheckpoint && source == DraftPath + ".previous") DraftPath = draftPrefix + "." + Guid.NewGuid().ToString("N") + ".vdo-draft";
                    selectedDraft = source;
                    return plan;
                }
                catch { plan.Dispose(); throw; }
            });
        }
    }
}
