using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using KeePassLib;
using KeePassLib.Security;
using KeePassLib.Serialization;

namespace VaultOrganizer
{
    // A cross-file move is deliberately destination-first. There is no claim of a
    // distributed atomic transaction: an interruption may leave two complete copies.
    internal sealed class CardTransfer : IDisposable
    {
        internal readonly VaultSession Source, Destination;
        internal object Original, Imported;
        internal readonly string OriginalId, ImportedId;
        private readonly PwDatabase sourceBefore, destinationBefore;
        private byte[] verifiedDestinationHash;
        private readonly long sourceRevision;
        private readonly Dictionary<string, PwUuid> iconMap = new Dictionary<string, PwUuid>();
        private static readonly Regex UuidReference = new Regex(@"(\{REF:[^{}]*@I:)([0-9a-f]{32})(\})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        internal CardTransfer(VaultSession source, VaultSession destination, object original, PwGroup target, string before)
        {
            if (source == destination || source.Dirty || destination.Dirty || source.HasPendingMerge || destination.HasPendingMerge) throw new InvalidOperationException("Save and reconcile both databases before transferring.");
            if (original == source.Database.RootGroup || VaultSession.ParentOf(original) == null) throw new InvalidOperationException("The database root cannot be transferred.");
            if (!VaultSession.Ordered(VaultSession.ParentOf(original)).Any(x => Object.ReferenceEquals(x, original))) throw new InvalidOperationException("The source card is no longer in its parent.");
            if (!source.Database.RootGroup.GetGroups(true).Concat(new[] { source.Database.RootGroup }).Any(g => g == VaultSession.ParentOf(original))) throw new InvalidOperationException("The source card is stale.");
            if (!destination.Database.RootGroup.GetGroups(true).Concat(new[] { destination.Database.RootGroup }).Any(g => g == target)) throw new InvalidOperationException("The destination is stale.");
            Source = source; Destination = destination; Original = original; OriginalId = VaultSession.ItemId(original);
            sourceRevision = source.Revision;
            sourceBefore = VaultSession.Snapshot(source.Database); destinationBefore = VaultSession.Snapshot(destination.Database);
            try
            {
                var group = original as PwGroup;
                Imported = group != null ? (object)group.CloneDeep() : ((PwEntry)original).CloneDeep();
                var map = new Dictionary<string, PwUuid>(StringComparer.OrdinalIgnoreCase);
                var groups = group == null ? new List<PwGroup>() : new[] { (PwGroup)Imported }.Concat(((PwGroup)Imported).GetGroups(true)).ToList();
                var entries = group == null ? new[] { (PwEntry)Imported }.ToList() : ((PwGroup)Imported).GetEntries(true).ToList();
                foreach (var g in groups) map[g.Uuid.ToHexString()] = new PwUuid(true);
                foreach (var e in entries) map[e.Uuid.ToHexString()] = new PwUuid(true);
                foreach (var g in groups)
                {
                    string order = g.CustomData.Get(VaultSession.OrderKey);
                    if (order != null) g.CustomData.Set(VaultSession.OrderKey, String.Join(",", order.Split(',').Select(id => map.ContainsKey(id) ? map[id].ToHexString() : id)));
                    g.Uuid = map[g.Uuid.ToHexString()]; g.PreviousParentGroup = PwUuid.Zero;
                    if (map.ContainsKey(g.LastTopVisibleEntry.ToHexString())) g.LastTopVisibleEntry = map[g.LastTopVisibleEntry.ToHexString()];
                    g.CustomIconUuid = CopyIcon(g.CustomIconUuid);
                }
                foreach (var e in entries)
                {
                    e.SetUuid(map[e.Uuid.ToHexString()], true); e.PreviousParentGroup = PwUuid.Zero;
                    Rewrite(e, map); foreach (var history in e.History) Rewrite(history, map);
                }
                var importedGroup = Imported as PwGroup;
                if (importedGroup != null) target.AddGroup(importedGroup, true, true); else target.AddEntry((PwEntry)Imported, true, true);
                ImportedId = VaultSession.ItemId(Imported);
                destination.Changed(); destination.Place(Imported, target, before); VaultSession.ValidateTree(destination.Database);
            }
            catch { Restore(destination, destinationBefore); throw; }
        }
        private PwUuid CopyIcon(PwUuid id)
        {
            if (id.Equals(PwUuid.Zero)) return id;
            PwUuid known; if (iconMap.TryGetValue(id.ToHexString(), out known)) return known;
            var icon = Source.Database.CustomIcons.FirstOrDefault(i => i.Uuid.Equals(id));
            if (icon == null) return PwUuid.Zero;
            var copy = new PwCustomIcon(new PwUuid(true), (byte[])icon.ImageDataPng.Clone()) { Name = icon.Name, LastModificationTime = icon.LastModificationTime };
            Destination.Database.CustomIcons.Add(copy); iconMap[id.ToHexString()] = copy.Uuid; return copy.Uuid;
        }
        private void Rewrite(PwEntry entry, Dictionary<string, PwUuid> map)
        {
            foreach (var pair in entry.Strings.ToArray())
            {
                string value = pair.Value.ReadString();
                string rewritten = UuidReference.Replace(value, m => map.ContainsKey(m.Groups[2].Value) ? m.Groups[1].Value + map[m.Groups[2].Value].ToHexString() + m.Groups[3].Value : m.Value);
                if (rewritten != value) entry.Strings.Set(pair.Key, new ProtectedString(pair.Value.IsProtected, rewritten));
            }
            entry.CustomIconUuid = CopyIcon(entry.CustomIconUuid);
        }
        internal static bool HasBoundaryReferences(PwDatabase database, object item)
        {
            var group = item as PwGroup;
            if (group != null && new[] { group }.Concat(group.GetGroups(true)).Any(g => g.Uuid.Equals(database.RecycleBinUuid) || g.Uuid.Equals(database.EntryTemplatesGroup))) return true;
            var moved = group == null ? new[] { (PwEntry)item }.ToList() : group.GetEntries(true).ToList();
            var ids = new HashSet<string>(moved.Select(e => e.Uuid.ToHexString()), StringComparer.OrdinalIgnoreCase);
            foreach (var entry in database.RootGroup.GetEntries(true))
                foreach (var version in new[] { entry }.Concat(entry.History))
                    foreach (var pair in version.Strings)
                        foreach (Match match in UuidReference.Matches(pair.Value.ReadString()))
                            if (ids.Contains(entry.Uuid.ToHexString()) != ids.Contains(match.Groups[2].Value)) return true;
            return false;
        }
        // Called only after destination publication and complete authenticated readback.
        internal async Task VerifyDestinationAsync()
        {
            string path = Destination.PathName; byte[] expected = VaultSession.HashFile(path);
            var readback = new PwDatabase();
            try
            {
                await Task.Run(() => readback.Open(IOConnectionInfo.FromPath(path), Destination.Database.MasterKey, null));
                if (!expected.SequenceEqual(VaultSession.HashFile(path))) throw new IOException("The destination changed during verification.");
                object card = Find(readback, ImportedId);
                if (card == null || !EqualCard(Imported, card)) throw new IOException("The transferred card was changed or removed before verification. The source was kept.");
                foreach (var id in iconMap.Values)
                {
                    var expectedIcon = Destination.Database.CustomIcons.First(i => i.Uuid.Equals(id));
                    var actualIcon = readback.CustomIcons.FirstOrDefault(i => i.Uuid.Equals(id));
                    if (actualIcon == null || actualIcon.Name != expectedIcon.Name || !actualIcon.ImageDataPng.SequenceEqual(expectedIcon.ImageDataPng)) throw new IOException("A transferred custom icon did not verify. The source was kept.");
                }
                verifiedDestinationHash = expected;
            }
            finally { readback.Close(); }
        }
        private static object Find(PwDatabase db, string id)
        { return db.RootGroup.GetEntries(true).Cast<object>().Concat(db.RootGroup.GetGroups(true).Cast<object>()).FirstOrDefault(x => VaultSession.ItemId(x) == id); }
        private static bool EqualCard(object left, object right)
        {
            var a = left as PwEntry; var b = right as PwEntry;
            if (VaultSession.ParentOf(left) == null || VaultSession.ParentOf(right) == null || !VaultSession.ParentOf(left).Uuid.Equals(VaultSession.ParentOf(right).Uuid)) return false;
            if (a != null || b != null)
                return a != null && b != null && a.EqualsEntry(b, PwCompareOptions.IgnoreParentGroup, MemProtCmpMode.Full);
            var ga = (PwGroup)left; var gb = right as PwGroup;
            if (gb == null || !ga.EqualsGroup(gb, PwCompareOptions.IgnoreParentGroup, MemProtCmpMode.Full)) return false;
            var ca = VaultSession.Ordered(ga); var cb = VaultSession.Ordered(gb);
            return ca.Count == cb.Count && ca.Select(VaultSession.ItemId).SequenceEqual(cb.Select(VaultSession.ItemId)) && ca.Zip(cb, (x, y) => EqualCard(x, y)).All(x => x);
        }
        internal void RemoveSource()
        {
            if (Source.Revision != sourceRevision) throw new IOException("The source was edited after preparing the copy. Start the transfer again.");
            if (verifiedDestinationHash == null || !verifiedDestinationHash.SequenceEqual(VaultSession.HashFile(Destination.PathName))) throw new IOException("Verify the saved destination before removing the source.");
            if (HasBoundaryReferences(Source.Database, Original)) throw new IOException("UUID field references cross the selected card's boundary. Use Copy so existing links remain valid.");
            var parent = VaultSession.ParentOf(Original);
            var group = Original as PwGroup;
            var ids = group == null ? new[] { ((PwEntry)Original).Uuid } : new[] { group.Uuid }.Concat(group.GetGroups(true).Select(g => g.Uuid)).Concat(group.GetEntries(true).Select(e => e.Uuid));
            foreach (var id in ids) Source.Database.DeletedObjects.Add(new PwDeletedObject(id, DateTime.UtcNow));
            if (group != null) parent.Groups.Remove(group); else parent.Entries.Remove((PwEntry)Original);
            VaultSession.WriteOrder(parent, VaultSession.Ordered(parent).Select(VaultSession.ItemId)); parent.Touch(true, false); Source.Changed();
        }
        private static void Restore(VaultSession session, PwDatabase snapshot)
        { session.Database.Close(); session.Database = VaultSession.Snapshot(snapshot); session.Changed(); }
        internal void RestoreSource() { Restore(Source, sourceBefore); }
        internal void RestoreDestination() { Restore(Destination, destinationBefore); }
        public void Dispose() { sourceBefore.Close(); destinationBefore.Close(); Original = Imported = null; }
    }

    internal sealed partial class OrganizerWindow
    {
        private async Task CrossDatabaseDrop(VaultSession source, VaultSession dest, object item, PwGroup target, string before)
        {
            if (transferring || saveTask != null || draftTask != null || opening || sealing) return;
            string id = VaultSession.ItemId(item), targetId = target.Uuid.ToHexString();
            bool boundary = CardTransfer.HasBoundaryReferences(source.Database, item);
            var decision = Ask("Transfer between databases", "Save & move publishes pending edits in both databases, saves and verifies the destination, then removes the source. Copy keeps the source. An interruption can leave a duplicate." + (boundary ? " This selection contains a special system group or UUID links crossing its boundary; only Copy is available." : "") + "\n\nFrom: " + source.PathName + "\nTo: " + dest.PathName, "Cancel", "Save & copy", "Save & move");
            if (boundary) Descendants<System.Windows.Controls.Button>(veil).First(b => b.Content as string == "Save & move").IsEnabled = false;
            int choice = await decision;
            if (choice == 0 || (choice == 2 && boundary)) return;
            transferring = true; editingSuspended = true; UpdateInteraction(); CardTransfer transfer = null; bool destinationSaved = false, sourceRemoved = false;
            Toast("Saving and verifying the transfer…", false); toastTimer.Stop(); SetStatus();
            try
            {
                if (!await SaveAllWork()) return;
                item = source.Database.RootGroup.GetEntries(true).Cast<object>().Concat(source.Database.RootGroup.GetGroups(true).Cast<object>()).FirstOrDefault(x => VaultSession.ItemId(x) == id);
                target = dest.Database.RootGroup.GetGroups(true).Concat(new[] { dest.Database.RootGroup }).FirstOrDefault(g => g.Uuid.ToHexString() == targetId);
                if (item == null || target == null) throw new IOException("The card or destination changed during reconciliation. Try the drag again.");
                transfer = new CardTransfer(source, dest, item, target, before);
                if (!await SaveWork(dest)) { Toast("Transfer stopped. The source is unchanged; the destination copy remains available for review. Save or discard it explicitly before retrying.", true); return; }
                destinationSaved = true; await transfer.VerifyDestinationAsync();
                if (choice == 2)
                {
                    transfer.RemoveSource(); sourceRemoved = true;
                    // No automatic merge after deleting: a competing source writer must be reviewed
                    // with the original card retained, rather than propagating a blind deletion.
                    SaveResult result = null;
                    try
                    {
                        result = await source.SaveAsync(); source.AcceptSave(result);
                        if (result.ConcurrentPath != null)
                        {
                            var external = new PwDatabase();
                            try { external.Open(IOConnectionInfo.FromPath(result.ConcurrentPath), source.Database.MasterKey, null); source.HoldPending(external, result.ConcurrentBase); }
                            finally { external.Close(); }
                            throw new IOException("A concurrent source version was preserved. Review it before publishing again.");
                        }
                    }
                    finally { if (result != null && result.ConcurrentBase != null) result.ConcurrentBase.Close(); }
                }
                foreach (var working in LoadedSessions) if (!working.Dirty) working.RemoveDraft();
                undo.Clear(); Toast(choice == 2 ? "Card moved. Both databases saved." : "Card copied. Destination saved; source kept.", false);
            }
            catch (Exception ex)
            {
                source.SaveFailed(); dest.SaveFailed();
                if (sourceRemoved && transfer != null) transfer.RestoreSource();
                Toast(destinationSaved ? "Destination copy is saved. Source removal was not completed safely; the original is retained in memory. Review both databases before retrying." : "Transfer stopped. The source was kept. " + (ex is IOException ? ex.Message : "Review both databases before retrying."), true);
                saveBlocked = true; recoveryButton.Visibility = Visibility.Visible;
                if (AnyDirty) { var protection = ProtectDraft(); }
            }
            finally
            {
                if (transfer != null) transfer.Dispose(); transferring = editingSuspended = false; undo.Clear(); BuildPanes(); UpdateFileLabel(); UpdateInteraction(); SetStatus();
                if (lockAfterTransfer) { bool close = closeAfterTransfer; lockAfterTransfer = closeAfterTransfer = false; var queued = Dispatcher.BeginInvoke(new Action(async () => { await Seal(close); })); }
            }
        }
    }
}
