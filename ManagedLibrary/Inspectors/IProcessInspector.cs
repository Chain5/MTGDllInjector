using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ManagedLibrary.Inspectors
{
    internal interface IProcessInspector
    {
        string GetProcessInfo();
        string GetWindowsInfo();
        string GetSessionId();
        string GetUsername();
    }
}
