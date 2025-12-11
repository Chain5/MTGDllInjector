using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ManagedLibrary.Inspectors
{
    public class MtgoProcessInspector : IProcessInspector
    {
        public string GetProcessInfo()
        {
            try
            {
                Process proc = Process.GetCurrentProcess();
                string info = string.Format("PID:{0}|Name:{1}|Memory:{2}MB|Threads:{3}",
                    proc.Id,
                    proc.ProcessName,
                    proc.WorkingSet64 / 1024 / 1024,
                    proc.Threads.Count);
                return info;
            }
            catch (Exception ex)
            {
                return $"ERROR: {ex.Message}";
            }
        }

        public string GetWindowsInfo()
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                Process proc = Process.GetCurrentProcess();

                sb.AppendFormat("Current Process: {0} (PID: {1}) - ", proc.ProcessName, proc.Id);
                if (!string.IsNullOrEmpty(proc.MainWindowTitle))
                    sb.AppendFormat("Main Window: {0}", proc.MainWindowTitle);

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"ERROR: {ex.Message}";
            }
        }

        public string GetSessionId()
        {
            try
            {
                Type type = FindTypeByName("CardsetReleaseManager");
                if (type == null) return "ERROR: CardsetReleaseManager Type not found";

                object session = GetStaticValue(type, "s_session");
                if (session == null) return "ERROR: Session is null";

                object sessionId = GetPropertyValue(session, "SessionId");
                return sessionId != null ? sessionId.ToString() : "NULL";
            }
            catch (Exception ex)
            {
                return $"ERROR: {ex.Message}";
            }
        }

        public string GetUsername()
        {
            try
            {
                string username = ResolveUsername();
                return username ?? "NOT_LOGGED_IN";
            }
            catch
            {
                return "ERROR_GETTING_USERNAME";
            }
        }

        #region Reflection helpers

        private string ResolveUsername()
        {
            // Try session-based
            object session = FindSession();
            if (session != null)
            {
                object loggedInUser = GetPropertyValue(session, "LoggedInUser");
                if (loggedInUser != null)
                {
                    object name = GetPropertyValue(loggedInUser, "Name") ?? GetPropertyValue(loggedInUser, "ScreenName");
                    if (name != null) return name.ToString();
                }
            }

            // Try user manager
            object userManager = FindStaticInstanceByName("UserManager");
            if (userManager != null)
            {
                object currentUser = GetPropertyValue(userManager, "CurrentUser") ?? GetPropertyValue(userManager, "LocalUser");
                if (currentUser != null)
                {
                    object screenName = GetPropertyValue(currentUser, "ScreenName") ?? GetPropertyValue(currentUser, "Name");
                    if (screenName != null) return screenName.ToString();
                }
            }

            return null;
        }

        private object FindSession()
        {
            Type type = FindTypeByName("CardsetReleaseManager");
            if (type != null)
            {
                object session = GetStaticValue(type, "s_session");
                if (session != null) return session;
            }

            object sessionManager = FindStaticInstanceByName("SessionManager");
            if (sessionManager != null)
            {
                object session = GetPropertyValue(sessionManager, "Session")
                               ?? GetPropertyValue(sessionManager, "CurrentSession")
                               ?? GetPropertyValue(sessionManager, "ClientSession");
                if (session != null) return session;
            }

            return null;
        }

        private Type FindTypeByName(string simpleName)
        {
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    Type t = asm.GetTypes().FirstOrDefault(x => x.Name.EndsWith(simpleName, StringComparison.OrdinalIgnoreCase));
                    if (t != null) return t;
                }
                catch
                {
                    // ignore reflection load exceptions for dynamic assemblies
                }
            }
            return null;
        }

        private object GetStaticValue(Type type, string memberName)
        {
            FieldInfo field = type.GetField(memberName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null) return field.GetValue(null);

            PropertyInfo prop = type.GetProperty(memberName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null) return prop.GetValue(null, null);

            return null;
        }

        private object GetPropertyValue(object instance, string propName)
        {
            if (instance == null) return null;
            PropertyInfo prop = instance.GetType().GetProperty(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop == null) return null;
            return prop.GetValue(instance, null);
        }

        private object FindStaticInstanceByName(string simpleName)
        {
            Type type = FindTypeByName(simpleName);
            if (type == null) return null;

            object instance = GetStaticValue(type, "Instance")
                            ?? GetStaticValue(type, "Current")
                            ?? GetStaticValue(type, "s_instance")
                            ?? GetStaticValue(type, "m_instance");
            return instance;
        }

        #endregion
    }
}
