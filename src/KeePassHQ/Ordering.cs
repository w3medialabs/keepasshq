using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KeePassLib;

namespace VaultOrganizer
{
    internal sealed partial class VaultSession
    {
        public const string OrderKey = "VaultOrganizer.CardOrder";
        public static string ItemId(object item) { var g = item as PwGroup; return g != null ? g.Uuid.ToHexString() : ((PwEntry)item).Uuid.ToHexString(); }
        public static PwGroup ParentOf(object item) { var g = item as PwGroup; return g != null ? g.ParentGroup : ((PwEntry)item).ParentGroup; }
        public static List<object> Ordered(PwGroup group)
        {
            var raw = group.Groups.Cast<object>().Concat(group.Entries.Cast<object>()).ToList();
            var byId = raw.ToDictionary(ItemId); var result = new List<object>();
            string order = group.CustomData.Get(OrderKey) ?? "";
            foreach (string id in order.Split(',')) { object item; if (byId.TryGetValue(id, out item)) { result.Add(item); byId.Remove(id); } }
            foreach (object item in raw) if (byId.Remove(ItemId(item))) result.Add(item);
            return result;
        }
        public static void WriteOrder(PwGroup group, IEnumerable<string> preferred)
        {
            var raw = group.Groups.Cast<object>().Concat(group.Entries.Cast<object>()).ToList();
            var map = raw.ToDictionary(ItemId); var ordered = new List<object>();
            foreach (string id in preferred) { object item; if (map.TryGetValue(id, out item)) { ordered.Add(item); map.Remove(id); } }
            foreach (object item in raw) if (map.Remove(ItemId(item))) ordered.Add(item);
            group.Groups.Clear(); group.Entries.Clear();
            foreach (object item in ordered) { var g = item as PwGroup; if (g != null) group.AddGroup(g, true); else group.AddEntry((PwEntry)item, true); }
            group.CustomData.Set(OrderKey, String.Join(",", ordered.Select(ItemId)));
        }
        public bool CanPlace(object item, PwGroup target, string before)
        {
            if (item == null || target == null || item == Database.RootGroup) return false;
            if (!Belongs(target)) return false;
            PwGroup parent = ParentOf(item);
            if (parent == null || !Belongs(parent)) return false;
            var group = item as PwGroup;
            if (group != null ? parent.Groups.IndexOf(group) < 0 : parent.Entries.IndexOf((PwEntry)item) < 0) return false;
            for (PwGroup g = target; g != null; g = g.ParentGroup) if (g == group) return false;
            if (before != null && !target.Groups.Any(g => g.Uuid.ToHexString() == before) && !target.Entries.Any(e => e.Uuid.ToHexString() == before)) return false;
            return true;
        }
        private bool Belongs(PwGroup group)
        { int depth = 0; for (PwGroup g = group; g != null && depth++ < 65536; g = g.ParentGroup) if (g == Database.RootGroup) return true; return false; }
        public bool Place(object item, PwGroup target, string before)
        {
            if (!CanPlace(item, target, before)) throw new InvalidOperationException("This move would create an invalid group structure.");
            PwGroup prior = ParentOf(item); var fromOrder = Ordered(prior).Select(ItemId).ToList(); var toOrder = Ordered(target).Select(ItemId).ToList(); string id = ItemId(item);
            if (before == id) return false;
            toOrder.Remove(id); int index = before == null ? toOrder.Count : toOrder.IndexOf(before); toOrder.Insert(index, id);
            if (prior == target && fromOrder.SequenceEqual(toOrder)) return false;
            if (prior != target)
            {
                var g = item as PwGroup;
                if (g != null) { prior.Groups.Remove(g); g.PreviousParentGroup = prior.Uuid; target.AddGroup(g, true, true); g.Touch(true, false); }
                else { var e = (PwEntry)item; prior.Entries.Remove(e); e.PreviousParentGroup = prior.Uuid; target.AddEntry(e, true, true); e.Touch(true, false); }
                fromOrder.Remove(id); WriteOrder(prior, fromOrder); prior.Touch(true, false);
            }
            WriteOrder(target, toOrder); target.Touch(true, false); Changed(); return true;
        }
        public static void ValidateTree(PwDatabase db)
        {
            if (db == null || db.RootGroup == null || db.RootGroup.ParentGroup != null) throw new InvalidDataException("Invalid database root.");
            var ids = new HashSet<string>(); var references = new HashSet<PwGroup>(); var stack = new Stack<PwGroup>(); stack.Push(db.RootGroup);
            while (stack.Count > 0)
            {
                PwGroup group = stack.Pop();
                if (!references.Add(group) || !ids.Add(group.Uuid.ToHexString())) throw new InvalidDataException("Duplicate or cyclic groups; no file was written.");
                foreach (PwGroup child in group.Groups) { if (child.ParentGroup != group) throw new InvalidDataException("Inconsistent group parent."); stack.Push(child); }
                foreach (PwEntry entry in group.Entries) if (entry.ParentGroup != group || !ids.Add(entry.Uuid.ToHexString())) throw new InvalidDataException("Duplicate entry or inconsistent parent; no file was written.");
            }
        }
    }
}
