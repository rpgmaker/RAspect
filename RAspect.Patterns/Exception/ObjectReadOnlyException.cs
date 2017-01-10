using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAspect.Patterns.Exception
{
    /// <summary>
    /// Exception thrown by objects that have Freezeable or Immutable threading model attribute when an attemp is made to modify the object after it been made read-only
    /// </summary>
    public class ObjectReadOnlyException : ApplicationException
    {

    }
}
