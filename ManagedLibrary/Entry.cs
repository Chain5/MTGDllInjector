using Common;
using ManagedLibrary.Inspectors;
using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;

namespace ManagedLibrary
{
    public class Entry
    {
        private static Thread ipcThread;
        private static volatile bool isRunning;
        private static IProcessInspector inspector;

        /// <summary>
        /// Initialize an IPC connection (namedPipe).
        /// </summary>
        public static void Initialize()
        {
            inspector = new MtgoProcessInspector();
            StartIPCListener();
        }

        private static void StartIPCListener()
        {
            if (isRunning) return;

            isRunning = true;
            ipcThread = new Thread(IPCListenerThread)
            {
                IsBackground = true,
                Name = "IPC Listener Thread"
            };
            ipcThread.Start();
        }

        private static void IPCListenerThread()
        {
            while (isRunning)
            {
                NamedPipeServerStream pipeServer = null;
                try
                {
                    pipeServer = new NamedPipeServerStream(PipeConfig.PIPE_NAME, PipeDirection.InOut, 1, PipeTransmissionMode.Byte);
                    pipeServer.WaitForConnection();
                    Console.WriteLine("[DLL] Client connected.");

                    StreamReader reader = null;
                    StreamWriter writer = null;

                    try
                    {
                        reader = new StreamReader(pipeServer);
                        writer = new StreamWriter(pipeServer);
                        writer.AutoFlush = true;

                        while (pipeServer.IsConnected && isRunning)
                        {
                            string clientMessage = null;
                            try
                            {
                                clientMessage = reader.ReadLine();
                            }
                            catch (IOException)
                            {
                                Console.WriteLine("[DLL] Connection lost while reading.");
                                break;
                            }

                            if (clientMessage == null) break;

                            Console.WriteLine("[DLL] Message received: " + clientMessage);
                            string response = ProcessCommand(clientMessage);
                            try
                            {
                                writer.WriteLine(response);
                            }
                            catch (IOException)
                            {
                                Console.WriteLine("[DLL] Connection lost while writing.");
                                break;
                            }
                        }
                    }
                    finally
                    {
                        if (writer != null) writer.Close();
                        if (reader != null) reader.Close();
                    }

                    Console.WriteLine("[DLL] Client disconnected.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[DLL] Error: " + ex.Message);
                }
                finally
                {
                    if (pipeServer != null)
                    {
                        try
                        {
                            if (pipeServer.IsConnected) pipeServer.Disconnect();
                        }
                        catch { /* ignore */ }

                        pipeServer.Close();
                        pipeServer.Dispose();
                    }
                }
            }
        }

        private static string ProcessCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return "ERROR: Empty command";

            string lcCommand = command.ToLowerInvariant();
            try
            {
                switch (lcCommand) 
                {
                    case Commands.Echo:
                        return "OK: echo received";
                    case Commands.ProcessInfo:
                        return "OK: " + inspector.GetProcessInfo();
                    case Commands.GetWindows:
                        return "OK: " + inspector.GetWindowsInfo();
                    case Commands.GetSessionInfo:
                        return "OK: " + inspector.GetSessionId();
                    case Commands.GetUsername:
                        return "OK: " + inspector.GetUsername();
                    case Commands.Shutdown:
                        isRunning = false;
                        return "OK: shutting down";
                    default:
                        return string.Format("ERROR: Unknown command '{0}'", command);
                }
            }
            catch (Exception ex)
            {
                string err = "ERROR: " + ex.Message;
                Console.WriteLine(err);
                return err;
            }
        }
    }
}
