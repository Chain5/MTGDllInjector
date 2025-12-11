using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace InjectorApp.Injector
{
    public static class DllInjector
    {
        #region Native Definitions

        [Flags]
        private enum ProcessAccessFlags : int
        {
            All = 0x1F0FFF
        }

        [Flags]
        private enum AllocationType : uint
        {
            Commit = 0x1000,
            Reserve = 0x2000
        }

        private enum MemoryProtection : uint
        {
            ReadWrite = 0x04
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(ProcessAccessFlags dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, AllocationType flAllocationType, MemoryProtection flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int nSize, out UIntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll")]
        private static extern IntPtr CreateRemoteThread( IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, out IntPtr lpThreadId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        #endregion

        #region Public API

        /// <summary>
        /// Inject a specific DLL inside of an already started process (using the id of the process)
        /// </summary>
        /// <param name="processId">the id of the process you want to be injected</param>
        /// <param name="dllPath">the path where the dll is stored</param>
        /// <returns>true in case of success, false otherwise</returns>
        public static bool TryToInject(int processId, string dllPath)
        {
            IntPtr processHandle = OpenProcess(ProcessAccessFlags.All, false, processId);
            return TryToInject(processHandle, processId, dllPath);
        }

        /// <summary>
        /// Inject a specific DLL inside of the given process
        /// </summary>
        /// <param name="process">the process you want to be injected</param>
        /// <param name="dllPath">the path where the dll is stored</param>
        /// <returns>true in case of success, false otherwise</returns>
        public static bool TryToInject(Process process, string dllPath)
        {
            return TryToInject(process.Handle, process.Id, dllPath);
        }

        #endregion

        #region Implementation

        private static bool TryToInject(IntPtr processHandle, int processId, string dllPath)
        {
            if (processHandle == IntPtr.Zero)
                return Fail($"Unable to open handle for PID {processId}. Error code: {Marshal.GetLastWin32Error()}");

            if (!File.Exists(dllPath))
                return Fail($"DLL file not found at path: {dllPath}");

            Console.WriteLine($"[INJECTOR] Injecting \"{dllPath}\" into PID {processId}...");

            // Allocate remote memory for string
            IntPtr remoteAddress = AllocateRemoteString(processHandle, dllPath);
            if (remoteAddress == IntPtr.Zero)
            {
                CloseHandle(processHandle);
                return Fail("Failed to allocate remote memory for DLL path.");
            }

            Console.WriteLine($"[INJECTOR] Allocated memory at: 0x{remoteAddress.ToInt64():X}");

            // Resolve LoadLibraryA address
            IntPtr kernel32 = GetModuleHandle("kernel32.dll");
            IntPtr loadLibraryAddr = GetProcAddress(kernel32, "LoadLibraryA");

            if (loadLibraryAddr == IntPtr.Zero)
            {
                CloseHandle(processHandle);
                return Fail($"Failed to resolve LoadLibraryA. Error code: {Marshal.GetLastWin32Error()}");
            }

            // Create remote thread
            IntPtr threadHandle = CreateRemoteThread(processHandle, IntPtr.Zero, 0, loadLibraryAddr, remoteAddress, 0, out _);

            if (threadHandle == IntPtr.Zero)
            {
                CloseHandle(processHandle);
                return Fail($"Failed to create remote thread. Error code: {Marshal.GetLastWin32Error()}");
            }

            WaitForSingleObject(threadHandle, 5000);

            Console.WriteLine("[INJECTOR] DLL injection successful.");

            CloseHandle(threadHandle);
            CloseHandle(processHandle);
            return true;
        }

        private static IntPtr AllocateRemoteString(IntPtr processHandle, string text)
        {
            byte[] buffer = System.Text.Encoding.ASCII.GetBytes(text + '\0');

            IntPtr alloc = VirtualAllocEx(
                processHandle,
                IntPtr.Zero,
                (uint)buffer.Length,
                AllocationType.Commit | AllocationType.Reserve,
                MemoryProtection.ReadWrite);

            if (alloc == IntPtr.Zero)
                return IntPtr.Zero;

            bool result = WriteProcessMemory(processHandle, alloc, buffer, buffer.Length, out _);

            return result ? alloc : IntPtr.Zero;
        }

        private static bool Fail(string message)
        {
            Console.WriteLine($"[FAILED INJECTION] {message}");
            return false;
        }

        #endregion
    }
}
