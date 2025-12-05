using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace InjectorApp
{
    public static class DllInjector
    {
        /** Using PInvoking **/

        [DllImport("kernel32.dll")]
        static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress,
            uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, char[] lpBuffer, int nSize, out UIntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll")]
        static extern IntPtr CreateRemoteThread(IntPtr hProcess,
            IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, out IntPtr lpThreadId);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        // Privileges
        const int PROCESS_ALL_ACCESS = 0x1F0FFF;

        // Used for memory allocation
        const uint MEM_COMMIT = 0x1000;
        const uint MEM_RESERVE = 0x2000;
        const uint PAGE_READWRITE = 0x04;

        /// <summary>
        /// This method Inject a specific DLL inside of an already started process
        /// </summary>
        /// <param name="procId">the process id you want to be injected</param>
        /// <param name="dllPath">the path where the dll is stored</param>
        /// <returns>true in case of success, false otherwise</returns>
        public static bool TryToInject(int procId, string dllPath) 
        {
            IntPtr procHandle = OpenProcess(PROCESS_ALL_ACCESS, false, procId);
            return TryToInject(procHandle, procId, dllPath);
        }

        /// <summary>
        /// This method Inject a specific DLL inside of the given process
        /// </summary>
        /// <param name="proc">the process you want to be injected</param>
        /// <param name="dllPath">the path where the dll is stored</param>
        /// <returns>true in case of success, false otherwise</returns>
        public static bool TryToInject(Process proc, string dllPath)
        {
            return TryToInject(proc.Handle, proc.Id, dllPath);
        }

        private static bool TryToInject(IntPtr procHandle, int procId, string dllPath)
        {
            if (procHandle == IntPtr.Zero) {
                return logAndError($"No procHandle available for procId: {procId}");
            }

            Console.WriteLine($"[ STARTING INJECTION ] - Trying to inject {dllPath} into PID {procId}.");

            // Allocate the memory for the path of the DLL
            IntPtr allocMemAddress = VirtualAllocEx(procHandle, IntPtr.Zero, (uint)((dllPath.Length + 1) * Marshal.SizeOf(typeof(char))), MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
            if (allocMemAddress == IntPtr.Zero) {
                return logAndError("Not able to allocate memory to store Dll path.");
            }
            Console.WriteLine(String.Format("[ INJECTOR ] - Memory allocated in 0x{0:X}", allocMemAddress));

            // Writing the DLL path inside the allocated memory
            UIntPtr writtenBytes = UIntPtr.Zero;
            bool writeResult = WriteProcessMemory(procHandle, allocMemAddress, dllPath.ToCharArray(), dllPath.Length, out writtenBytes);
            if (!writeResult)
            {
                return logAndError("Error while writing dll path to allocated memory.\nBytes written: {writtenBytes}");
            }
            Console.WriteLine($"[ INJECTOR ] - Write success: {writeResult}, bytes written: {writtenBytes}");

            // Loading LoadLibraryA from kernel32.dll
            IntPtr loadLibraryAddr = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryA");
            if (loadLibraryAddr == IntPtr.Zero)
            {
                return logAndError("Error during LoadingLibraryA.");
            }

            // creating a thread that will call LoadLibraryA with allocMemAddress as argument
            IntPtr hThread = CreateRemoteThread(procHandle, IntPtr.Zero, 0, loadLibraryAddr, allocMemAddress, 0, out _);
            if (hThread == IntPtr.Zero)
            {
                CloseHandle(procHandle);
                return logAndError("Error creating remote thread.");
            }

            WaitForSingleObject(hThread, 5000);
            Console.WriteLine($"[ SUCCESSFUL INJECTION ] - Dll injected successfully.");

            // Finally closing thread and procHandle
            CloseHandle(hThread);
            CloseHandle(procHandle);
            return true;
        }
        private static bool logAndError(string msg)
        {
            Console.WriteLine($"[ FAILED INJECTION ] - {msg}");
            return false;
        }
    }


}
