using System;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using System.Threading;

namespace ProxyApp;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        bool createdNew;

        using (Mutex mutex = new Mutex(true, "ProxyHelper_SingleInstance", out createdNew))
        {
            if (!createdNew)
            {
                var current = Process.GetCurrentProcess();
                var running = Process.GetProcessesByName(current.ProcessName)
                                     .FirstOrDefault(p => p.Id != current.Id);

                if (running != null)
                {
                    NativeMethods.ShowWindow(running.MainWindowHandle, 9);
                    NativeMethods.SetForegroundWindow(running.MainWindowHandle);
                }

                return;
            }

            ApplicationConfiguration.Initialize();

            bool startInTray = args.Any(x =>
                string.Equals(x, "--tray", StringComparison.OrdinalIgnoreCase));

            Application.Run(new Form1(startInTray));
        }
    }
}
