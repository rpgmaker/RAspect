using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAspect.Patterns.Exception
{
    /// <summary>
    /// Exception thrown when two thread simultaneously attempt to access a method decorated with the ThreadUnsafeAttribute attribute
    /// </summary>
    public class ConcurrentAccessException : ApplicationException
    {
    }
}
