using System.IO;
using System.Windows;
namespace VaultOrganizer
{
    internal static class ClipboardPayload
    {
        internal static void Protect(IDataObject data)
        {
            // Registered Windows formats: raw DWORD zero opts out of both services.
            data.SetData("ExcludeClipboardContentFromMonitorProcessing", new MemoryStream(new byte[4], false), false);
            data.SetData("CanIncludeInClipboardHistory", new MemoryStream(new byte[4], false), false);
            data.SetData("CanUploadToCloudClipboard", new MemoryStream(new byte[4], false), false);
        }
        internal static DataObject Text(string text)
        {
            var data = new DataObject(); Protect(data); data.SetData(DataFormats.UnicodeText, text); return data;
        }
    }
    internal interface IClipboardService { string Read(); void Write(string text); void Clear(); }
    internal sealed class SystemClipboardService : IClipboardService
    {
        public string Read() { return Clipboard.ContainsText() ? Clipboard.GetText() : null; }
        public void Write(string text) { Clipboard.SetDataObject(ClipboardPayload.Text(text), true); }
        public void Clear() { Clipboard.Clear(); }
    }
}
