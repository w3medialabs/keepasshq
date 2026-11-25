using System.Windows;
namespace VaultOrganizer
{
    internal interface IClipboardService { string Read(); void Write(string text); void Clear(); }
    internal sealed class SystemClipboardService : IClipboardService
    {
        public string Read() { return Clipboard.ContainsText() ? Clipboard.GetText() : null; }
        public void Write(string text) { Clipboard.SetText(text); }
        public void Clear() { Clipboard.Clear(); }
    }
}
