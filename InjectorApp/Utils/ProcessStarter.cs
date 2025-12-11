using System.Diagnostics;

namespace InjectorApp.Utils
{
    public static class ProcessStarter
    {
        private const int StartupWaitMs = 5000;
        private const int SearchTimeoutMs = 30000;
        private const int PollIntervalMs = 500;

        /// <summary>
        /// Starts the ClickOnce .appref-ms application and waits for the real MTGO process to appear.
        /// </summary>
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

                Process? launcher = Process.Start(psi);

                if (launcher == null)
                {
                    Console.WriteLine("[ERROR] Unable to start ClickOnce launcher.");
                    return null;
                }

                Console.WriteLine("[INFO] Launcher started. Waiting for MTGO to initialize...");
                Thread.Sleep(StartupWaitMs);

                return WaitForMtgoProcess();
            }
            catch (Exception ex)
            {
                Console.WriteLine("[EXCEPTION] Error starting MTGO:");
                Console.WriteLine(ex);
                return null;
            }
        }

        private static Process? WaitForMtgoProcess()
        {
            Console.WriteLine("[INFO] Searching for MTGO process...");

            int elapsed = 0;

            while (elapsed < SearchTimeoutMs)
            {
                Process? proc = Process.GetProcesses()
                    .FirstOrDefault(p =>
                        p.ProcessName.Contains("mtgo", StringComparison.OrdinalIgnoreCase) &&
                        p.MainWindowTitle.Contains("Magic: The Gathering Online", StringComparison.OrdinalIgnoreCase));

                if (proc != null)
                {
                    Console.WriteLine($"[INFO] MTGO process found: PID {proc.Id}");
                    return proc;
                }

                Thread.Sleep(PollIntervalMs);
                elapsed += PollIntervalMs;
            }

            Console.WriteLine("[ERROR] Timeout: MTGO process not found.");
            return null;
        }
    }
}
