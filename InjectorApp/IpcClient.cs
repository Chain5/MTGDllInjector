using System.IO.Pipes;

namespace InjectorApp
{
    public class IpcClient : IDisposable
    {
        private readonly string _pipeName;
        private static NamedPipeClientStream _pipeClient;
        private StreamReader _reader;
        private StreamWriter _writer;

        /// <summary>
        /// Creates a new NamedPipeClient for the given pipe name.
        /// </summary>
        /// <param name="pipeName">The name of the pipe you want to connect</param>
        public IpcClient(string pipeName)
        {
            _pipeName = pipeName;
            Connect();
        }

        /// <summary>
        /// Send the command to the server and logs the response
        /// </summary>
        /// <param name="cmd">The Message/Command you want to send</param>
        public void Send(string cmd) {
            try
            {
                if (!_pipeClient.IsConnected)
                    Connect();

                Console.WriteLine($"[CLIENT] Sending: {cmd}");
                _writer.WriteLine(cmd);

                string response = _reader.ReadLine();
                Console.WriteLine($"[SERVER] Response: {response}");
            }
            catch (Exception ex)
            {
                Console.WriteLine("[IPC] Error: " + ex.Message);
                return;
            }
        }

        private void Connect()
        {
            if (_pipeClient != null && _pipeClient.IsConnected)
                return;

            _pipeClient = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut);
            Console.WriteLine($"[IPC] Connecting to pipe {_pipeName}...");
            _pipeClient.Connect();

            _reader = new StreamReader(_pipeClient);
            _writer = new StreamWriter(_pipeClient) { AutoFlush = true };

            Console.WriteLine("[IPC] Connected");
        }

        public void Dispose()
        {
            Disconnect();
        }

        private void Disconnect()
        {
            try
            {
                _pipeClient?.Close();
                _pipeClient?.Dispose();
                Console.WriteLine("[IPC] Disconnected");
            }
            catch (Exception ex)
            {
                Console.WriteLine("[IPC] Error during disconnect: " + ex.Message);
            }
        }
    }
}
