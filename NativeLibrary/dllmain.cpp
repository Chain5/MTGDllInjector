#include <windows.h>
#include <stdio.h>

#using <System.dll>
#using <System.Windows.Forms.dll>

using namespace System;
using namespace System::Threading;
using namespace System::IO;
using namespace System::Windows::Forms;
using namespace System::Reflection;

// Logging function for debugging
void LogMessage(const char* message) {
    try {
        FILE* logFile = nullptr;
        fopen_s(&logFile, "C:\\Temp\\injection_log.txt", "a");
        if (logFile) {
            SYSTEMTIME st;
            GetLocalTime(&st);
            fprintf(logFile, "[%02d:%02d:%02d] %s\n", st.wHour, st.wMinute, st.wSecond, message);
            fclose(logFile);
        }
    }
    catch (...) {
        // Ingnore logging errors
    }
}

// Handler to resolve missing assembly
Assembly^ ResolveAssemblyHandler(Object^ sender, ResolveEventArgs^ args) {
    try {
        String^ assemblyName = args->Name->Split(',')[0];

        // Getting directory where NativeLibrary.dll is stored
        String^ dllPath = Assembly::GetExecutingAssembly()->Location;
        String^ dllDir = Path::GetDirectoryName(dllPath);

        // Building ManagedLibrary.dll path
        String^ managedDllPath = Path::Combine(dllDir, assemblyName + ".dll");

        char logMsg[512];
        sprintf_s(logMsg, "Looking for assembly at: %s",
            (char*)(void*)Runtime::InteropServices::Marshal::StringToHGlobalAnsi(managedDllPath));
        LogMessage(logMsg);

        if (File::Exists(managedDllPath)) {
            LogMessage("Assembly found! Loading...");
            Assembly^ assembly = Assembly::LoadFrom(managedDllPath);
            LogMessage("Assembly loaded successfully!");
            return assembly;
        }
        else {
            LogMessage("Assembly NOT found at expected location");
        }
    }
    catch (Exception^ ex) {
        String^ errorMsg = String::Format("Error in AssemblyResolve: {0}", ex->Message);
        File::WriteAllText("C:\\Temp\\resolve_error.txt", errorMsg);
        LogMessage("ERROR in AssemblyResolve handler");
    }

    return nullptr;
}

// Creating ad hoc thread to execute managed code
DWORD WINAPI ManagedEntryPoint(LPVOID lpParam) {
    try {
        LogMessage("=== MANAGED THREAD STARTED ===");

        // Register resolver BEFORE loading managed assembly
        LogMessage("Registering AssemblyResolve handler...");
        AppDomain::CurrentDomain->AssemblyResolve +=
            gcnew ResolveEventHandler(ResolveAssemblyHandler);
        LogMessage("AssemblyResolve handler registered");

        // Waiting for process
        Sleep(500);

        // Loading ManagedLibrary.dll manually
        LogMessage("Loading ManagedLibrary.dll...");

        String^ nativeDllPath = Assembly::GetExecutingAssembly()->Location;
        String^ nativeDllDir = Path::GetDirectoryName(nativeDllPath);
        String^ managedDllPath = Path::Combine(nativeDllDir, "ManagedLibrary.dll");

        char pathLog[512];
        sprintf_s(pathLog, "Native DLL directory: %s",
            (char*)(void*)Runtime::InteropServices::Marshal::StringToHGlobalAnsi(nativeDllDir));
        LogMessage(pathLog);

        sprintf_s(pathLog, "Looking for ManagedLibrary.dll at: %s",
            (char*)(void*)Runtime::InteropServices::Marshal::StringToHGlobalAnsi(managedDllPath));
        LogMessage(pathLog);

        if (!File::Exists(managedDllPath)) {
            LogMessage("FATAL ERROR: ManagedLibrary.dll not found!");
            MessageBox::Show(
                "ManagedLibrary.dll not found!\n\nExpected at: " + managedDllPath,
                "Injection Error",
                MessageBoxButtons::OK,
                MessageBoxIcon::Error
            );
            return 1;
        }

        LogMessage("ManagedLibrary.dll found, loading assembly...");
        Assembly^ managedAssembly = Assembly::LoadFrom(managedDllPath);
        LogMessage("Assembly loaded successfully!");

        // Getting entryType
        Type^ entryType = managedAssembly->GetType("ManagedLibrary.Entry");
        if (entryType == nullptr) {
            LogMessage("ERROR: Could not find ManagedLibrary.Entry type");
            return 1;
        }
        LogMessage("Entry type found");

        // Getting 'Initialize' method
        auto initializeMethod = entryType->GetMethod("Initialize",
            BindingFlags::Public | BindingFlags::Static);

        if (initializeMethod == nullptr) {
            LogMessage("ERROR: Could not find Initialize method");
            return 1;
        }
        LogMessage("Initialize method found");

        // Calling method
        LogMessage("Calling Entry::Initialize()...");
        initializeMethod->Invoke(nullptr, nullptr);

        LogMessage("Managed code executed successfully!");
        LogMessage("=== MANAGED THREAD COMPLETED ===");

        return 0;
    }
    catch (Exception^ ex) {
        try {
            String^ errorMsg = String::Format(
                "MANAGED EXCEPTION:\nType: {0}\nMessage: {1}\nStackTrace:\n{2}",
                ex->GetType()->Name,
                ex->Message,
                ex->StackTrace
            );

            MessageBox::Show(errorMsg, "Injection Error",
                MessageBoxButtons::OK, MessageBoxIcon::Error);

            File::WriteAllText("C:\\Temp\\injection_error.txt", errorMsg);

            LogMessage("CRITICAL ERROR in managed code - check injection_error.txt");
        }
        catch (...) {
            LogMessage("CRITICAL ERROR: Could not even write error file");
        }

        return 1;
    }
    catch (...) {
        LogMessage("CRITICAL ERROR: Unhandled native exception in managed thread");
        return 2;
    }
}

// Native Entry point
BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved) {
    switch (ul_reason_for_call) {
    case DLL_PROCESS_ATTACH:
    {
        LogMessage("========================================");
        LogMessage("DLL_PROCESS_ATTACH - Injection detected!");
        LogMessage("========================================");

        // Disabling notifies for next threads
        DisableThreadLibraryCalls(hModule);

        char modulePathBuffer[MAX_PATH];
        GetModuleFileNameA(hModule, modulePathBuffer, MAX_PATH);
        char logMsg[512];
        sprintf_s(logMsg, "DLL loaded from: %s", modulePathBuffer);
        LogMessage(logMsg);

        LogMessage("Creating managed thread...");

        HANDLE hThread = CreateThread(
            nullptr,
            0,
            ManagedEntryPoint,
            nullptr,
            0,
            nullptr
        );

        if (hThread != nullptr) {
            LogMessage("SUCCESS: Managed thread created successfully");
            CloseHandle(hThread);
        }
        else {
            DWORD error = GetLastError();
            char errorMsg[256];
            sprintf_s(errorMsg, "FATAL ERROR: CreateThread failed with error code: %lu", error);
            LogMessage(errorMsg);
        }

        break;
    }

    case DLL_PROCESS_DETACH:
        LogMessage("========================================");
        LogMessage("DLL_PROCESS_DETACH - DLL is being unloaded");
        LogMessage("========================================");
        break;
    }

    return TRUE;
}