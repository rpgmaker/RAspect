using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RAspect.Patterns.Exception;
using System.Reflection;
using System.Reflection.Emit;

namespace RAspect.Patterns.Threading
{
    /// <summary>
    /// Attribute when applied on a type, ensure that the instance of this can only be accessed by the thread that created the instance. When a different thread accesses instance of this type, a <see cref="ThreadMismatchException" /> exception is thrown.
    /// </summary>
    public class ThreadAffinityAttribute : AspectBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ThreadAffinityAttribute"/> class.
        /// </summary>
        public ThreadAffinityAttribute()
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
        /// <param name="typeBuilder">Type Builder</param>
        /// <param name="method">Method</param>
        /// <param name="parameter">Parameter</param>
        /// <param name="il">ILGenerator</param>
        internal void BeginAspectBlock(TypeBuilder typeBuilder, MethodBase method, ParameterInfo parameter, ILGenerator il)
        {
        }
    }
}
