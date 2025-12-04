using Common;

namespace InjectorApp
{
    public static class InputEvaluator
    {
        private const string EXIT = "exit";
        private const string HELP = "help";
        private const string COMMAND_LIST = $"\nPlease enter one of the following command:\n" +
            $"- {Commands.Echo} -> get a message back\n" +
            $"- {Commands.ProcessInfo} -> get some information about the process\n" +
            $"- {Commands.GetWindows} -> get the window main nane\n" +
            $"- {Commands.GetSessionInfo} -> get the session id.\n" +
            $"- {EXIT} -> to stop the program (or just CTRL+C).\n";
         

        public static void evaluate(IpcClient ipcClient)
        {
            Console.WriteLine("\nPlease enter a command.");
            Console.WriteLine("To check the command list, type 'help'");
            while (true)
            {
                Console.Write("Send Command: ");
                var input = Console.ReadLine();
                if (input == null)
                {
                    Console.WriteLine("Not valid input");
                }
                else
                {
                    string command = input.ToLower();
                    if (command == EXIT)
                    {
                        ipcClient.Dispose();
                        return;
                    }
                    else if (command == HELP) 
                    {
                        Console.WriteLine(COMMAND_LIST);
                    }
                    else
                    {
                        ipcClient.Send(input);
                        Console.WriteLine(); // just formatting
                    }
                }
            }
        }
    }
}
