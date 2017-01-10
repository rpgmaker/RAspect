using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RAspect.Patterns.Exception;
using System.Reflection.Emit;
using System.Reflection;

namespace RAspect.Patterns.Threading
{
    /// <summary>
    /// Attribute when applied to a type, ensures that only one thread executes in methods of this type. When more than one thread accesses methods of this type, a <see cref="ConcurrentAccessException"/> exception is thrown
    /// </summary>
    public class ThreadUnSafeAttribute : AspectBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ThreadUnSafeAttribute"/> class.
        /// </summary>
        public ThreadUnSafeAttribute()
        {
            OnBeginAspectBlock = BeginAspectBlock;
        }

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

        /// <summary>
        /// Aspect code to inject at the beginning of weaved method
        /// </summary>
        /// <param name="method">Method</param>
        /// <param name="parameter">Parameter</param>
        /// <param name="il">ILGenerator</param>
        internal void BeginAspectBlock(MethodBase method, ParameterInfo parameter, ILGenerator il)
        {
        }
    }
}
