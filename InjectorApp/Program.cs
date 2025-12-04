using Common;
using System.Diagnostics;

namespace InjectorApp
{
    internal class Program
    {
        static void Main(string[] args)
        {
            bool injectionOutcome = false;
            Console.WriteLine("InjectorApp starting...");

            if (args.Length == 2) // mode: processId + dllPath
            {
                int processId = int.Parse(args[0]);
                string dllPath = args[1];
                injectionOutcome = DllInjector.TryToInject(processId, dllPath);
            }
            else if (args.Length == 3 && args[0] == "-appref") // mode: -appref pathToAppref + dllPath
            {
                string apprefPath = args[1];
                string dllPath = args[2];

                var proc = ProcessStarter.StartTargetProcess(apprefPath);
                if (proc == null)
                {
                    Console.WriteLine("Could not start target process. Exiting.");
                    return;
                }
                injectionOutcome = DllInjector.TryToInject(proc, dllPath);
            }
            else // wrong usage
            {
                Console.WriteLine("Usage:");
                Console.WriteLine("   DllInjector.exe <processId> <dllPath>");
                Console.WriteLine("   DllInjector.exe -exe <pathToExe> <dllPath>");
                return;
            }

            if (injectionOutcome) {
                Console.WriteLine("InjectionSuccessful!");
            }
            else {
                Console.WriteLine($"Error injecting .dll file into process.");
                return;
            }

            // Starting IPC comunication
            var ipc = new IpcClient(PipeConfig.PIPE_NAME);
            // Starting user input evaluator
            InputEvaluator.evaluate(ipc);

            Console.WriteLine("Exiting...");
        }
    }
}
