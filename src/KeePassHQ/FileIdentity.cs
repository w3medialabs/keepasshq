using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace VaultOrganizer
{
    internal static class FileIdentity
    {
        [StructLayout(LayoutKind.Sequential)] private struct Information
        {
            public uint Attributes; public System.Runtime.InteropServices.ComTypes.FILETIME Creation, Access, Write;
            public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
        }
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetFileInformationByHandle(SafeFileHandle file, out Information information);
        private static string Identity(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                Information value;
                if (!GetFileInformationByHandle(stream.SafeFileHandle, out value)) throw new IOException("Cannot verify the database file identity.");
                return value.Volume.ToString("X8") + value.IndexHigh.ToString("X8") + value.IndexLow.ToString("X8");
            }
        }
        internal static bool Same(string a, string b)
        {
            if (String.Equals(System.IO.Path.GetFullPath(a), System.IO.Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase)) return true;
            return Identity(a) == Identity(b);
        }
    }
}
