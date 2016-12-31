using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAspect.Aspects
{
    /// <summary>
    /// Aspect for intercepting method invocation
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Assembly)]
    public abstract class MethodInterceptionAspect : AspectBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MethodInterceptionAspect"/> class.
        /// </summary>
        public MethodInterceptionAspect() : base(WeaveTargetType.Methods)
        {
        }

        /// <summary>
        /// Capture method invocation
        /// </summary>
        /// <param name="context">MethodContext</param>
        internal override void OnEntry(MethodContext context)
        {
            context.Returns = null;
        }

        internal override void OnExit(MethodContext context)
        {
            OnInvoke(context);
        }

        /// <summary>
        /// Capture method Invocation
        /// </summary>
        /// <param name="context">MethodContext</param>
        public abstract void OnInvoke(MethodContext context);

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
