using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAspect
{
    /// <summary>
    /// Enumeration for determing entrypoint category/type
    /// </summary>
    internal enum EntryPointType
    {
        Enter = 1,
        Exit = 2,
        Success = 4,
        Error = 8
    }
}
