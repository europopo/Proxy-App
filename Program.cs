using System;
using System.Linq;
using System.Windows.Forms;

namespace ProxyApp;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        bool startInTray = args.Any(x => string.Equals(x, "--tray", StringComparison.OrdinalIgnoreCase));
        Application.Run(new Form1(startInTray));
    }
}
