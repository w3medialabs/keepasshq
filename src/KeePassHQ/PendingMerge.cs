using System;
using System.Threading.Tasks;
using KeePassLib;

namespace VaultOrganizer
{
    internal sealed class PendingMergeData : IDisposable
    {
        public PwDatabase File, Baseline;
        public PendingMergeData Clone() { return new PendingMergeData { File = VaultSession.Snapshot(File), Baseline = VaultSession.Snapshot(Baseline) }; }
        public void Dispose() { if (File != null) File.Close(); if (Baseline != null) Baseline.Close(); File = Baseline = null; }
    }
    internal sealed partial class VaultSession
    {
        private PendingMergeData pending;
        public bool HasPendingMerge { get { return pending != null; } }
        public void HoldPending(PwDatabase file, PwDatabase original)
        {
            if (!file.RootGroup.Uuid.Equals(Database.RootGroup.Uuid) || !original.RootGroup.Uuid.Equals(Database.RootGroup.Uuid)) throw new InvalidOperationException("Competing database identity differs.");
            var keep = new PendingMergeData { File = Snapshot(file), Baseline = Snapshot(original) };
            ClearPending(); pending = keep; Changed();
        }
        private void ClearPending() { if (pending != null) pending.Dispose(); pending = null; }
        public Task<MergePlan> PreparePendingAsync()
        {
            var plan = new MergePlan { Local = Snapshot(Database), External = Snapshot(pending.File), Baseline = Snapshot(pending.Baseline), DiskBaseline = Snapshot(baseline), FileHash = (byte[])expectedHash.Clone(), Revision = Revision, CompletesPending = true };
            try { plan.Analyze(); return Task.FromResult(plan); } catch { plan.Dispose(); throw; }
        }
    }
}
