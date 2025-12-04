using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InjectorApp
{
    public static class ProcessStarter
    {
        public static Process? StartTargetProcess(string apprefPath)
        {
            try
            {
                if (!File.Exists(apprefPath))
                {
                    Console.WriteLine($"[ERROR] File not found: {apprefPath}");
                    return null;
                }

                Console.WriteLine("[INFO] Starting via .appref-ms (ClickOnce)...");
                var psi = new ProcessStartInfo
                {
                    FileName = apprefPath,
                    UseShellExecute = true
                };

                var launcher = Process.Start(psi);

                if (launcher == null)
                {
                    Console.WriteLine("[ERROR] Unable to start launcher ClickOnce.");
                    return null;
                }

                Console.WriteLine("[INFO] Launcher started. Waiting for the real MTGO process...");

                // Waiting 5 seconds to let the process start
                Thread.Sleep(5000);

                Process? mtgoProcess = null;

                for (int i = 0; i < 60; i++)
                {
                    mtgoProcess = Process.GetProcesses()
                        .FirstOrDefault(p =>
                            (p.ProcessName.ToLower().Contains("mtgo") &&
                            p.MainWindowTitle.ToLower().Contains("magic: the gathering online")));

                    if (mtgoProcess != null)
                    {
                        Console.WriteLine($"[INFO] Process MTGO found: PID {mtgoProcess.Id}");
                        return mtgoProcess;
                    }

                    Thread.Sleep(500);
                }

                Console.WriteLine("[ERROR] Timeout: unable to find MTGO proces.");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[EXCEPTION] Error starting MTGO:");
                Console.WriteLine(ex.ToString());
                return null;
            }
        }
    }
}
