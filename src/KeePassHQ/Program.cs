using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;

namespace VaultOrganizer
{
    internal static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            AppContext.SetSwitch("Switch.System.IO.UseLegacyPathHandling", false);
            AppContext.SetSwitch("Switch.System.IO.BlockLongPaths", false);
            AppDomain.CurrentDomain.AssemblyResolve += (sender, e) =>
            {
                if (new AssemblyName(e.Name).Name != "KeePass") return null;
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("KeePass.exe"))
                {
                    if (s == null) return null;
                    var bytes = new byte[s.Length]; int read = 0;
                    while (read < bytes.Length) { int n = s.Read(bytes, read, bytes.Length - read); if (n == 0) throw new EndOfStreamException(); read += n; }
                    return Assembly.Load(bytes);
                }
            };
            return Start(args);
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int Start(string[] args)
        {
            // The managed KeePass implementation keeps this a single-file portable app.
            KeePassLib.Native.NativeLib.AllowNative = false;
            try
            {
                var app = new Application();
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Theme.xaml"))
                    app.Resources.MergedDictionaries.Add((ResourceDictionary)System.Windows.Markup.XamlReader.Load(stream));
#if DIAGNOSTICS
                if (args.Length >= 2 && args[0] == "--self-test") return Tests.Run(args[1]);
#endif
                var window = new OrganizerWindow();
#if DIAGNOSTICS
                if (args.Length >= 2 && args[0] == "--render-demo")
                {
                    try { window.RenderDemo(args[1], args.Length > 2 ? args[2] : "cards"); return 0; }
                    catch (Exception ex) { File.WriteAllText(args[1] + ".error.txt", ex.ToString()); return 2; }
                }
#endif
                app.DispatcherUnhandledException += (sender, e) => { e.Handled = true; window.ReportUnexpectedError(); };
                app.Run(window); return 0;
            }
            catch (Exception)
            {
                // No vault contents, exception dumps, or plaintext diagnostics are written.
                if (args.Length >= 2) return 2;
                MessageBox.Show("KeePassHQ could not start. This app requires Windows with .NET Framework 4.8 or later.", "KeePassHQ");
                return 1;
            }
        }
    }
}
