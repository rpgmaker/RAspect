using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAspect.Patterns.Exception
{
    /// <summary>
    /// Exception thrown when a thread attempts to access an object that is affined to another thread
    /// </summary>
    public class ThreadMismatchException : ApplicationException
    {
    }
}
