using Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

namespace ManagedLibrary
{
    public class Entry
    {
        private static Thread ipcThread;
        private static bool isRunning = false;

        /// <summary>
        /// Initialize an IPC connection (namedPipe).
        /// </summary>
        public static void Initialize()
        {
            try { StartIPCListener(); }
            catch (Exception ex) { Console.WriteLine($"error {ex.Message}"); }
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
            NamedPipeServerStream pipeServer = null;
            try
            {
                RunPipeServer(pipeServer);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"error {ex.Message}");
            }
            finally
            {
                try
                {
                    if (pipeServer != null)
                    {
                        if (pipeServer.IsConnected)
                            pipeServer.Disconnect();
                        pipeServer.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"error {ex.Message}");
                }
            }
        }

        private static void RunPipeServer(NamedPipeServerStream pipeServer)
        {
            while (isRunning)
            {
                pipeServer = new NamedPipeServerStream(PipeConfig.PIPE_NAME, PipeDirection.InOut, 1, PipeTransmissionMode.Byte);

                try
                {
                    pipeServer.WaitForConnection();
                    Console.WriteLine("[DLL] Client connected.");

                    using (StreamReader reader = new StreamReader(pipeServer))
                    using (StreamWriter writer = new StreamWriter(pipeServer))
                    {
                        writer.AutoFlush = true;

                        // Loop to handle messages on the same connections
                        while (pipeServer.IsConnected && isRunning)
                        {
                            try
                            {
                                string clientMessage = reader.ReadLine();
                                // if ReadLine return null, then the client is disconnected
                                if (clientMessage == null)
                                    break;

                                Console.WriteLine("[DLL] Message received: " + clientMessage);
                                string response = ProcessCommand(clientMessage);
                                writer.WriteLine(response);
                            }
                            catch (IOException)
                            {
                                Console.WriteLine("[DLL] Connection lost.");
                                break;
                            }
                        }
                    }

                    Console.WriteLine("[DLL] Client disconnected.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[DLL] Error: " + ex.Message);
                }
                finally
                {
                    pipeServer?.Dispose();
                }
            }
        }

        private static string ProcessCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return "ERROR: Empty command";
            try
            {
                string cmd = command.ToLower();
                switch (cmd)
                {
                    case Commands.Echo:
                        return HandleEcho();

                    case Commands.ProcessInfo:
                        return HandleProcessInfoCommand();

                    case Commands.GetWindows:
                        return HandleGetWindowsCommand();

                    case Commands.GetSessionInfo:
                        return HandleGetSessionInfoCommand();

                    case Commands.GetUsername:
                        return HandleGetUsernameCommand();

                    default:
                        return $"ERROR: Comando sconosciuto '{cmd}'";
                }
            }
            catch (Exception ex)
            {
                string errorMsg = $"ERROR: {ex.Message}";
                Console.WriteLine(errorMsg);
                return errorMsg;
            }
        }

        /// <summary>
        /// Just a simple echo to check if the server is responsing
        /// </summary>
        /// <returns>OK: echo received</returns>
        private static string HandleEcho()
        {
            return "OK: echo received";
        }

        /// <summary>
        /// Gives some informations about the current process
        /// </summary>
        /// <returns>The processId, processName, memory allocated and number of threads</returns>
        private static string HandleProcessInfoCommand()
        {
            try
            {
                var proc = Process.GetCurrentProcess();
                var info = $"PID:{proc.Id}|Name:{proc.ProcessName}|Memory:{proc.WorkingSet64 / 1024 / 1024}MB|Threads:{proc.Threads.Count}";
                return $"OK: {info}";
            }
            catch (Exception ex)
            {
                return $"ERROR: {ex.Message}";
            }
        }

        /// <summary>
        /// Get some windows information from the current process
        /// </summary>
        /// <returns>Process name, process id and window main title</returns>
        private static string HandleGetWindowsCommand()
        {
            try
            {
                var sb = new StringBuilder();
                var proc = Process.GetCurrentProcess();

                sb.Append($"Current Process: {proc.ProcessName} (PID: {proc.Id}) - ");
                if (!string.IsNullOrEmpty(proc.MainWindowTitle))
                {
                    sb.Append($"Main Window: {proc.MainWindowTitle}");
                }

                return $"OK: {sb}";
            }
            catch (Exception ex)
            {
                return $"ERROR: {ex.Message}";
            }
        }

        /// <summary>
        /// Retrieve the sessionId from the process (this is just a POC)
        /// </summary>
        /// <returns>The id of the session</returns>
        private static string HandleGetSessionInfoCommand()
        {
            try
            {
                var type = FindTypeByName("CardsetReleaseManager");
                if (type == null) return "ERROR: CardsetReleaseManager Type not found";

                // trying to get the session
                var session = GetStaticValue(type, "s_session");
                if (session == null) return "ERROR: Session is null";

                // extracting sessionId
                var sessionId = GetPropertyValue(session, "SessionId")?.ToString();
                return $"OK: sessionId: {sessionId}";
            }
            catch (Exception ex)
            {
                return $"ERROR: {ex.Message}";
            }
        }

        /// <summary>
        /// Get the logged in username
        /// </summary>
        private static string HandleGetUsernameCommand()
        {
            try
            {
                var username = GetUsername();
                return $"OK: {username}";
            }
            catch (Exception ex)
            {
                return $"ERROR: {ex.Message}";
            }
        }

        /** Some utility methods: **/

        /// <summary>
        /// Get the current logged username
        /// </summary>
        private static string GetUsername()
        {
            try
            {
                // Method 1: Using Session
                var session = FindSession();
                if (session != null)
                {
                    var loggedInUser = GetPropertyValue(session, "LoggedInUser");
                    if (loggedInUser != null)
                    {
                        var name = GetPropertyValue(loggedInUser, "Name")
                                ?? GetPropertyValue(loggedInUser, "ScreenName");
                        if (name != null)
                            return name.ToString();
                    }
                }

                // Method 2: Using UserManager
                var userManager = FindStaticInstanceByName("UserManager");
                if (userManager != null)
                {
                    var currentUser = GetPropertyValue(userManager, "CurrentUser")
                                   ?? GetPropertyValue(userManager, "LocalUser");
                    if (currentUser != null)
                    {
                        var screenName = GetPropertyValue(currentUser, "ScreenName")
                                      ?? GetPropertyValue(currentUser, "Name");
                        if (screenName != null)
                            return screenName.ToString();
                    }
                }

                return "NOT_LOGGED_IN";
            }
            catch
            {
                return "ERROR_GETTING_USERNAME";
            }
        }

        /// <summary>
        /// Find the session object (FlsClientSession)
        /// </summary>
        private static object FindSession()
        {
            // Method 1: using CardsetReleaseManager.s_session
            var type = FindTypeByName("CardsetReleaseManager");
            if (type != null)
            {
                var session = GetStaticValue(type, "s_session");
                if (session != null)
                    return session;
            }

            // Method 2: using SessionManager
            var sessionManager = FindStaticInstanceByName("SessionManager");
            if (sessionManager != null)
            {
                var session = GetPropertyValue(sessionManager, "Session")
                           ?? GetPropertyValue(sessionManager, "CurrentSession")
                           ?? GetPropertyValue(sessionManager, "ClientSession");
                if (session != null)
                    return session;
            }

            return null;
        }

        /// <summary>
        /// Find static instance by simple type name (searches for Instance/Current property)
        /// </summary>
        private static object FindStaticInstanceByName(string simpleName)
        {
            var type = FindTypeByName(simpleName);
            if (type == null)
                return null;

            // Search Instance
            var instance = GetStaticValue(type, "Instance");
            if (instance != null)
                return instance;

            // Search Current
            instance = GetStaticValue(type, "Current");
            if (instance != null)
                return instance;

            // Search s_instance (private pattern)
            instance = GetStaticValue(type, "s_instance");
            if (instance != null)
                return instance;

            // Search m_instance (other pattern)
            instance = GetStaticValue(type, "m_instance");
            if (instance != null)
                return instance;

            return null;
        }

        private static Type FindTypeByName(string simpleName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var type = asm.GetTypes().FirstOrDefault(t => t.Name.EndsWith(simpleName, StringComparison.OrdinalIgnoreCase));
                    if (type != null) return type;
                }
                catch { }
            }
            return null;
        }

        private static object GetStaticValue(Type type, string memberName)
        {
            var field = type.GetField(memberName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null) return field.GetValue(null);

            var prop = type.GetProperty(memberName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null) return prop.GetValue(null, null);

            return null;
        }

        private static object GetPropertyValue(object instance, string propName)
        {
            if (instance == null) return null;
            var prop = instance.GetType().GetProperty(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return prop?.GetValue(instance, null);
        }
    }
}
