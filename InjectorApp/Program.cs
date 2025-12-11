using Common;
using InjectorApp.Injector;
using InjectorApp.IPC;
using InjectorApp.Utils;
using System.Diagnostics;

namespace InjectorApp
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("InjectorApp starting...");

            if (!TryHandleArguments(args, out bool injectionOutcome))
            {
                PrintUsage();
                Environment.Exit(1);
            }

            if (!injectionOutcome)
            {
                Console.WriteLine("[MAIN] - Error injecting DLL into process.");
                Environment.Exit(1);
            }

            Console.WriteLine("[MAIN] - Injection successful!");

            // Start IPC communication
            IpcClient.Instance.Initialize(PipeConfig.PIPE_NAME);

            // Start user input evaluator
            InputEvaluator.Evaluate();

            Console.WriteLine("Exiting...");
        }

        private static bool TryHandleArguments(string[] args, out bool injectionOutcome)
        {
            injectionOutcome = false;

            if (args.Length == 2)
            {
                return HandleProcessIdMode(args, out injectionOutcome);
            }
            else if (args.Length == 3 && args[0].Equals("-appref", StringComparison.OrdinalIgnoreCase))
            {
                return HandleAppRefMode(args, out injectionOutcome);
            }

            return false;
        }

        private static bool HandleProcessIdMode(string[] args, out bool injectionOutcome)
        {
            injectionOutcome = false;

            if (!int.TryParse(args[0], out int processId))
            {
                Console.WriteLine("Invalid process ID.");
                return false;
            }

            string dllPath = args[1];

            injectionOutcome = DllInjector.TryToInject(processId, dllPath);
            return true;
        }

        private static bool HandleAppRefMode(string[] args, out bool injectionOutcome)
        {
            injectionOutcome = false;

            string apprefPath = args[1];
            string dllPath = args[2];

            Process? process = ProcessStarter.StartTargetProcess(apprefPath);

            if (process == null)
            {
                Console.WriteLine("Could not start target process.");
                return false;
            }

            injectionOutcome = DllInjector.TryToInject(process, dllPath);
            return true;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("\tInjectorApp.exe <processId> <dllPath>");
            Console.WriteLine("\tInjectorApp.exe -appref <pathToAppRef> <dllPath>");
            Console.WriteLine();
        }
    }
}
