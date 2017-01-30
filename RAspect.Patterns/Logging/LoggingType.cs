using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAspect.Patterns.Logging
{
    /// <summary>
    /// Logging Types
    /// </summary>
    public enum LoggingType
    {
        Trace = 1,
        Debug = 2,
        Warning = 4,
        Information = 8,
        Error = 16
    }
}
