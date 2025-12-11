using Common;
using System.Text;

namespace InjectorApp.IPC
{
    public static class InputEvaluator
    {
        private const string ExitCommand = "exit";
        private const string HelpCommand = "help";
        private static readonly string CommandList = BuildCommandList();
         
        public static void Evaluate()
        {
            Console.WriteLine("\nPlease enter a command.");
            Console.WriteLine("To check the command list, type 'help'");

            while (true)
            {
                Console.Write("Send Command: ");
                string? input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input))
                {
                    Console.WriteLine("Invalid input.");
                    continue;
                }

                string command = input.Trim().ToLowerInvariant();

                switch (command) 
                {
                    case ExitCommand:
                        IpcClient.Instance.Send(Commands.Shutdown);
                        IpcClient.Instance.Dispose();
                        return;
                    case HelpCommand:
                        Console.WriteLine(CommandList);
                        break;
                    default:
                        IpcClient.Instance.Send(command);
                        Console.WriteLine(); // spacing
                        break;
                }
            }
        }

        private static string BuildCommandList()
        {
            var sb = new StringBuilder();
            sb.AppendLine("\nPlease enter one of the following commands:");
            sb.AppendLine($"- {Commands.Echo} -> get a message back");
            sb.AppendLine($"- {Commands.ProcessInfo} -> get some information about the process");
            sb.AppendLine($"- {Commands.GetWindows} -> get the window main name");
            sb.AppendLine($"- {Commands.GetSessionInfo} -> get the session id");
            sb.AppendLine($"- {Commands.GetUsername} -> get the username (use it when you are logged in)");
            sb.AppendLine($"- {ExitCommand} -> close the pipe connection and stop the program");

            return sb.ToString();
        }
    }
}
