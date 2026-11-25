using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace VaultOrganizer
{
    internal static class PasswordGenerator
    {
        private static int Pick(RandomNumberGenerator rng, int count)
        {
            var bytes = new byte[4]; uint value, bound = UInt32.MaxValue - (UInt32.MaxValue % (uint)count);
            do { rng.GetBytes(bytes); value = BitConverter.ToUInt32(bytes, 0); } while (value >= bound);
            Array.Clear(bytes, 0, bytes.Length); return (int)(value % (uint)count);
        }
        public static string Generate(int length, bool upper, bool lower, bool digits, bool symbols)
        {
            if (length < 8 || length > 128) throw new ArgumentException("Choose a length from 8 to 128.");
            var sets = new List<string>();
            if (upper) sets.Add("ABCDEFGHIJKLMNOPQRSTUVWXYZ"); if (lower) sets.Add("abcdefghijklmnopqrstuvwxyz");
            if (digits) sets.Add("0123456789"); if (symbols) sets.Add("!@#$%^&*()-_=+[]{};:,.?/~");
            if (sets.Count == 0) throw new ArgumentException("Select at least one character type.");
            string all = String.Concat(sets); var result = new char[length];
            using (var rng = RandomNumberGenerator.Create())
            {
                int i = 0; foreach (string set in sets) result[i++] = set[Pick(rng, set.Length)];
                while (i < length) result[i++] = all[Pick(rng, all.Length)];
                for (i = length - 1; i > 0; --i) { int j = Pick(rng, i + 1); char c = result[i]; result[i] = result[j]; result[j] = c; }
            }
            string password = new string(result); Array.Clear(result, 0, result.Length); return password;
        }
    }
}
