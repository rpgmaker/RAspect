using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAspect.Patterns.Exception
{
    /// <summary>
    /// Exception thrown when threading validation was not satified
    /// </summary>
    public class ThreadingValidationException : ApplicationException
    {
        public ThreadingValidationException(string message) : base(message)
        {
        }
    }
}
