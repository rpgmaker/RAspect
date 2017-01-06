using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RAspect.Patterns.Exception;

namespace RAspect.Patterns.Threading
{
    /// <summary>
    /// Attribute when applied on a type, ensure that the instance of this can only be accessed by the thread that created the instance. When a different thread accesses instance of this type, a <see cref="ThreadMismatchException" /> exception is thrown.
    /// </summary>
    public class ThreadAffinityAttribute : AspectBase
    {
        /// <summary>
        /// Gets weave block type
        /// </summary>
        internal override WeaveBlockType BlockType
        {
            get
            {
                return WeaveBlockType.Wrapping;
            }
        }
    }
}
