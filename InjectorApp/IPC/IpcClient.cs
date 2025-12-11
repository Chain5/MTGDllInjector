using System.IO.Pipes;

namespace InjectorApp.IPC
{
    public class IpcClient : IDisposable
    {
        private static readonly Lazy<IpcClient> _instance = new(() => new IpcClient());

        private string? _pipeName;
        private NamedPipeClientStream? _pipeClient;
        private StreamReader? _reader;
        private StreamWriter? _writer;

        private bool _initialized;

        /// <summary>
        /// Global singleton instance.
        /// </summary>
        public static IpcClient Instance => _instance.Value;

        /// <summary>
        /// Private constructor for Singleton pattern.
        /// </summary>
        private IpcClient() { }

        /// <summary>
        /// Must be called once before using the client.
        /// </summary>
        public void Initialize(string pipeName)
        {
            if (_initialized)
                return;

            _pipeName = pipeName ?? throw new ArgumentNullException(nameof(pipeName));
            _initialized = true;

            Connect();
        }

        /// <summary>
        /// Send the command to the server and logs the response
        /// </summary>
        /// <param name="command">The Message/Command you want to send</param>
        public void Send(string command) 
        {
            if (!_initialized)
                throw new InvalidOperationException("IpcClient must be initialized before use.");

            if (command == null)
                throw new ArgumentNullException(nameof(command));

            try
            {
                if (_pipeClient == null || !_pipeClient.IsConnected)
                    Connect();

                Console.WriteLine($"[CLIENT] Sending: {command}");
                _writer?.WriteLine(command);

                string? response = _reader?.ReadLine();
                Console.WriteLine($"[SERVER] Response: {response}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[IPC] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Connects to the named pipe.
        /// </summary>
        private void Connect()
        {
            if (_pipeName == null)
                throw new InvalidOperationException("Pipe name not set. Call Initialize() first.");

            if (_pipeClient != null && _pipeClient.IsConnected)
                return;

            Console.WriteLine($"[IPC] Connecting to pipe '{_pipeName}'...");

            _pipeClient = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut);
            _pipeClient.Connect();

            _reader = new StreamReader(_pipeClient);
            _writer = new StreamWriter(_pipeClient) { AutoFlush = true };

            Console.WriteLine("[IPC] Connected");
        }

        /// <summary>
        /// Disconnects cleanly.
        /// </summary>
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
                Console.WriteLine($"[IPC] Error during disconnect: {ex.Message}");
            }
        }

        /// <summary>
        /// Dispose the singleton instance.
        /// </summary>
        public void Dispose()
        {
            Disconnect();
            GC.SuppressFinalize(this);
        }
    }
}
