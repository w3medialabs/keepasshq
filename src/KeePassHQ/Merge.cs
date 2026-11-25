using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KeePassLib;
using KeePassLib.Collections;
using KeePassLib.Security;

namespace VaultOrganizer
{
    internal sealed class ExternalChangeException : IOException
    { public ExternalChangeException() : base("The database changed outside this app.") { } }
    internal enum MergeChoice { Cancel, Mine, File, Both }
    internal sealed class MergeConflict
    {
        public string Id, Title;
        public bool IsGroup, Presence, Credentials;
        public PwEntry MineEntry, FileEntry;
        public PwGroup MineGroup, FileGroup;
        public readonly List<string> Fields = new List<string>();
        public MergeChoice Choice;
        public bool CanKeepBoth { get { return !IsGroup && MineEntry != null && FileEntry != null; } }
    }

    internal sealed class MergePlan : IDisposable
    {
        private static readonly string[] CredentialFields = { PwDefs.UserNameField, PwDefs.PasswordField, PwDefs.UrlField };
        public PwDatabase Local, External, Baseline, DiskBaseline;
        public byte[] FileHash;
        public long Revision;
        public bool CompletesPending, LegacyRecovery;
        public PendingMergeData RecoveredPending;
        public readonly List<MergeConflict> Conflicts = new List<MergeConflict>();
        private readonly HashSet<string> covered = new HashSet<string>();
        private static string Id(PwGroup g) { return g == null ? "" : g.Uuid.ToHexString(); }
        private static string Id(PwEntry e) { return e == null ? "" : e.Uuid.ToHexString(); }
        private static string Parent(PwGroup g) { return g == null ? "" : Id(g.ParentGroup); }
        private static string Parent(PwEntry e) { return e == null ? "" : Id(e.ParentGroup); }
        internal static Dictionary<string, PwEntry> Entries(PwDatabase db) { return db.RootGroup.GetEntries(true).ToDictionary(e => Id(e)); }
        internal static Dictionary<string, PwGroup> Groups(PwDatabase db)
        {
            var result = new Dictionary<string, PwGroup>(); Action<PwGroup> walk = null;
            walk = g => { result.Add(Id(g), g); foreach (var child in g.Groups) walk(child); }; walk(db.RootGroup); return result;
        }
        private static HashSet<string> Deleted(PwDatabase db) { return new HashSet<string>(db.DeletedObjects.Select(d => d.Uuid.ToHexString())); }
        private static string Order(PwGroup group) { return String.Join(",", VaultSession.Ordered(group).Select(VaultSession.ItemId)); }
        private static bool Reordered(string original, string changed)
        {
            var a = original.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries); var b = changed.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var aa = new HashSet<string>(a); var bb = new HashSet<string>(b);
            return !a.Where(bb.Contains).SequenceEqual(b.Where(aa.Contains));
        }
        private static string ChooseOrder(PwGroup original, PwGroup mine, PwGroup file, bool mineWins)
        {
            string before = original == null ? "" : Order(original), local = Order(mine), remote = Order(file);
            bool useMine = Reordered(before, local) && (!Reordered(before, remote) || mineWins);
            var primary = (useMine ? local : remote).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            var secondary = (useMine ? remote : local).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < secondary.Length; ++i)
            {
                string id = secondary[i]; if (primary.Contains(id)) continue;
                int insertion = primary.Count;
                for (int j = i + 1; j < secondary.Length; ++j) { int next = primary.IndexOf(secondary[j]); if (next >= 0) { insertion = next; break; } }
                primary.Insert(insertion, id);
            }
            return String.Join(",", primary);
        }
        private static bool Same(ProtectedString a, ProtectedString b)
        { if (a == null || b == null) return a == b; return a.ReadString() == b.ReadString(); }
        private static bool SameEntry(PwEntry a, PwEntry b)
        {
            return a != null && b != null && a.EqualsEntry(b, PwCompareOptions.IgnoreTimes | PwCompareOptions.IgnoreHistory | PwCompareOptions.IgnoreParentGroup, MemProtCmpMode.None) && Parent(a) == Parent(b);
        }
        private bool IsCovered(PwGroup group)
        { for (PwGroup p = group; p != null; p = p.ParentGroup) if (covered.Contains(Id(p))) return true; return false; }

        public void Analyze()
        {
            var bg = Groups(Baseline); var lg = Groups(Local); var rg = Groups(External);
            var ld = Deleted(Local); var rd = Deleted(External);
            foreach (string id in bg.Keys)
            {
                PwGroup mine, file; lg.TryGetValue(id, out mine); rg.TryGetValue(id, out file);
                if ((mine != null && file == null && rd.Contains(id)) || (mine == null && file != null && ld.Contains(id)))
                {
                    var conflict = new MergeConflict { Id = id, Title = mine != null ? mine.Name : file.Name, IsGroup = true, Presence = true, MineGroup = mine, FileGroup = file };
                    conflict.Fields.Add(mine == null ? "Group deleted in app" : "Group deleted in file"); Conflicts.Add(conflict); covered.Add(id);
                }
                else if (mine != null && file != null)
                {
                    var conflict = new MergeConflict { Id = id, Title = mine.Name, IsGroup = true, MineGroup = mine, FileGroup = file };
                    if (mine.Name != bg[id].Name && file.Name != bg[id].Name && mine.Name != file.Name) conflict.Fields.Add("Name");
                    if (Parent(mine) != Parent(bg[id]) && Parent(file) != Parent(bg[id]) && Parent(mine) != Parent(file)) conflict.Fields.Add("Group location");
                    string bo = Order(bg[id]), lo = Order(mine), ro = Order(file);
                    if (Reordered(bo, lo) && Reordered(bo, ro) && Reordered(lo, ro)) conflict.Fields.Add("Card order");
                    if (conflict.Fields.Count > 0) Conflicts.Add(conflict);
                }
            }
            var be = Entries(Baseline); var le = Entries(Local); var re = Entries(External);
            foreach (string id in le.Keys.Union(re.Keys))
            {
                PwEntry mine, file, original; le.TryGetValue(id, out mine); re.TryGetValue(id, out file); be.TryGetValue(id, out original);
                if ((mine != null && IsCovered(mine.ParentGroup)) || (file != null && IsCovered(file.ParentGroup))) continue;
                var conflict = new MergeConflict { Id = id, Title = VaultSession.Read(mine ?? file, PwDefs.TitleField), MineEntry = mine, FileEntry = file };
                if (original != null && ((mine != null && file == null && rd.Contains(id) && !SameEntry(mine, original)) || (mine == null && file != null && ld.Contains(id) && !SameEntry(file, original))))
                { conflict.Presence = true; conflict.Fields.Add(mine == null ? "Entry deleted in app; edited in file" : "Entry edited in app; deleted in file"); }
                else if (mine != null && file != null)
                {
                    // Independent credential edits can produce a username/password/URL
                    // combination that existed in neither version. Review that tuple as one.
                    conflict.Credentials = CredentialFields.Any(field => !Same(mine.Strings.Get(field), original == null ? null : original.Strings.Get(field)))
                        && CredentialFields.Any(field => !Same(file.Strings.Get(field), original == null ? null : original.Strings.Get(field)))
                        && CredentialFields.Any(field => !Same(mine.Strings.Get(field), file.Strings.Get(field)));
                    foreach (string field in mine.Strings.GetKeys().Union(file.Strings.GetKeys()))
                    {
                        ProtectedString a = mine.Strings.Get(field), b = file.Strings.Get(field), before = original == null ? null : original.Strings.Get(field);
                        if (!Same(a, b) && !Same(a, before) && !Same(b, before)) conflict.Fields.Add(field);
                    }
                    if (conflict.Credentials) foreach (string field in CredentialFields) if (!conflict.Fields.Contains(field)) conflict.Fields.Add(field);
                    if (original != null && Parent(mine) != Parent(original) && Parent(file) != Parent(original) && Parent(mine) != Parent(file)) conflict.Fields.Add("Group location");
                }
                if (conflict.Fields.Count > 0) Conflicts.Add(conflict);
            }
        }

        private void ResolvePresence(PwDatabase mine, PwDatabase file, MergeConflict conflict)
        {
            bool keepMine = conflict.Choice == MergeChoice.Mine;
            PwDatabase winner = keepMine ? mine : file, loser = keepMine ? file : mine;
            if (conflict.IsGroup)
            {
                PwGroup chosen, other; Groups(winner).TryGetValue(conflict.Id, out chosen); Groups(loser).TryGetValue(conflict.Id, out other);
                if (chosen == null)
                { if (other != null) other.ParentGroup.Groups.Remove(other); return; }
                var ids = new HashSet<string>(); Action<PwGroup> collect = null;
                collect = g => { ids.Add(Id(g)); foreach (var e in g.Entries) ids.Add(Id(e)); foreach (var child in g.Groups) collect(child); }; collect(chosen);
                foreach (var deleted in loser.DeletedObjects.ToList()) if (ids.Contains(deleted.Uuid.ToHexString())) loser.DeletedObjects.Remove(deleted);
            }
            else
            {
                PwEntry chosen, other; Entries(winner).TryGetValue(conflict.Id, out chosen); Entries(loser).TryGetValue(conflict.Id, out other);
                if (chosen == null) { if (other != null) other.ParentGroup.Entries.Remove(other); return; }
                foreach (var deleted in loser.DeletedObjects.ToList()) if (deleted.Uuid.ToHexString() == conflict.Id) loser.DeletedObjects.Remove(deleted);
            }
        }

        public PwDatabase Build()
        {
            if (Conflicts.Any(c => c.Choice == MergeChoice.Cancel)) throw new InvalidOperationException("Resolve the listed conflicts first.");
            PwDatabase combined = VaultSession.Snapshot(Local), incoming = VaultSession.Snapshot(External);
            try
            {
                foreach (var conflict in Conflicts.Where(c => c.Presence)) ResolvePresence(combined, incoming, conflict);
                // KeePass merges object identities, deletions, histories, icons, and metadata.
                combined.MergeIn(incoming, PwMergeMethod.Synchronize, null);
                var bg = Groups(Baseline); var lg = Groups(Local); var rg = Groups(External); var cg = Groups(combined);
                foreach (string id in lg.Keys.Intersect(rg.Keys))
                {
                    PwGroup mine = lg[id], file = rg[id], before, target;
                    if (id == Id(combined.RootGroup) || !cg.TryGetValue(id, out target)) continue;
                    bg.TryGetValue(id, out before);
                    var conflict = Conflicts.FirstOrDefault(c => c.IsGroup && c.Id == id);
                    bool mineWins = conflict == null || conflict.Choice != MergeChoice.File;
                    string selectedName = before == null ? (mineWins ? mine.Name : file.Name) : (mine.Name == before.Name ? file.Name : (file.Name == before.Name || mine.Name == file.Name || mineWins ? mine.Name : file.Name));
                    string parent = before == null ? Parent(file) : SelectParent(Parent(mine), Parent(file), Parent(before), mineWins);
                    string selectedOrder = ChooseOrder(before, mine, file, mineWins);
                    target.AssignProperties(file, false, false); target.Name = selectedName;
                    VaultSession.WriteOrder(target, selectedOrder.Split(','));
                    PwGroup parentGroup;
                    if (cg.TryGetValue(parent, out parentGroup) && target.ParentGroup != parentGroup)
                    {
                        for (PwGroup p = parentGroup; p != null; p = p.ParentGroup) if (p == target) throw new IOException("The combined group moves form a cycle. No file was overwritten; save a recovery copy to resolve it.");
                        target.ParentGroup.Groups.Remove(target); parentGroup.AddGroup(target, true, true);
                    }
                    if (selectedName != file.Name || parent != Parent(file) || selectedOrder != Order(file)) target.Touch(true, false);
                }
                var be = Entries(Baseline); var le = Entries(Local); var re = Entries(External); var ce = Entries(combined);
                foreach (string id in le.Keys.Intersect(re.Keys))
                {
                    PwEntry mine = le[id], file = re[id], before, target;
                    if (!ce.TryGetValue(id, out target)) continue;
                    be.TryGetValue(id, out before);
                    var conflict = Conflicts.FirstOrDefault(c => !c.IsGroup && c.Id == id);
                    bool mineWins = conflict == null || conflict.Choice != MergeChoice.File;
                    var strings = new ProtectedStringDictionary(); bool differs = false;
                    foreach (string field in mine.Strings.GetKeys().Union(file.Strings.GetKeys()).Union(before == null ? new List<string>() : before.Strings.GetKeys()))
                    {
                        ProtectedString a = mine.Strings.Get(field), b = file.Strings.Get(field), original = before == null ? null : before.Strings.Get(field);
                        ProtectedString chosen = conflict != null && conflict.Credentials && CredentialFields.Contains(field) ? (mineWins ? a : b)
                            : (Same(a, original) ? b : (Same(b, original) || Same(a, b) || mineWins ? a : b));
                        if (chosen != null) strings.Set(field, chosen);
                        if (!Same(chosen, b)) differs = true;
                    }
                    string parent = before == null ? Parent(file) : SelectParent(Parent(mine), Parent(file), Parent(before), mineWins);
                    target.AssignProperties(file, false, false, false); target.Strings = strings;
                    PwGroup parentGroup;
                    if (!cg.TryGetValue(parent, out parentGroup)) parentGroup = combined.RootGroup;
                    if (target.ParentGroup != parentGroup) { target.ParentGroup.Entries.Remove(target); parentGroup.AddEntry(target, true, true); }
                    if (differs || parent != Parent(file)) target.Touch(true, false);
                    if (conflict != null && conflict.Choice == MergeChoice.Both)
                    {
                        var duplicate = file.CloneDeep(); duplicate.SetUuid(new PwUuid(true), true);
                        duplicate.Strings.Set(PwDefs.TitleField, new ProtectedString(false, VaultSession.Read(file, PwDefs.TitleField) + " (file copy)"));
                        duplicate.Touch(true, false); parentGroup.AddEntry(duplicate, true, true);
                    }
                }
                // This editor never changes database settings. Retain the external file's
                // encryption/settings choices, rather than reverting them during synchronization.
                foreach (var property in typeof(PwDatabase).GetProperties())
                {
                    if (property.GetSetMethod() == null || property.GetIndexParameters().Length != 0) continue;
                    Type type = property.PropertyType;
                    if (type.IsValueType || type == typeof(string) || type == typeof(PwUuid)) property.SetValue(combined, property.GetValue(External, null), null);
                }
                combined.KdfParameters = KeePassLib.Cryptography.KeyDerivation.KdfParameters.DeserializeExt(KeePassLib.Cryptography.KeyDerivation.KdfParameters.SerializeExt(External.KdfParameters));
                combined.MemoryProtection = External.MemoryProtection.CloneDeep();
                combined.RootGroup.AssignProperties(External.RootGroup, false, false);
                var rootConflict = Conflicts.FirstOrDefault(c => c.IsGroup && c.Id == Id(Local.RootGroup));
                string rootOrder = ChooseOrder(Baseline.RootGroup, Local.RootGroup, External.RootGroup, rootConflict == null || rootConflict.Choice != MergeChoice.File);
                VaultSession.WriteOrder(combined.RootGroup, rootOrder.Split(','));
                if (rootOrder != Order(External.RootGroup)) combined.RootGroup.Touch(true, false);
                combined.Modified = true;
                VaultSession.ValidateTree(combined);
                return combined;
            }
            catch { combined.Close(); throw; }
            finally { incoming.Close(); }
        }
        private static string SelectParent(string mine, string file, string baseline, bool mineWins)
        { return mine == baseline ? file : (file == baseline || mine == file || mineWins ? mine : file); }
        public void Dispose()
        {
            if (Local != null) Local.Close(); if (External != null) External.Close(); if (Baseline != null) Baseline.Close();
            if (DiskBaseline != null) DiskBaseline.Close();
            if (RecoveredPending != null) RecoveredPending.Dispose(); RecoveredPending = null;
            Local = null; External = null; Baseline = null; DiskBaseline = null; Conflicts.Clear();
        }
    }
}
