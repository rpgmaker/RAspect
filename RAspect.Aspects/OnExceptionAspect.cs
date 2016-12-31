using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAspect.Aspects
{
    /// <summary>
    /// Aspect for intercepting exception
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Assembly)]
    public abstract class OnExceptionAspect : AspectBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OnExceptionAspect"/> class.
        /// </summary>
        public OnExceptionAspect() : base(WeaveTargetType.Methods | WeaveTargetType.Properties)
        {
        }

        /// <summary>
        /// Capture exception
        /// </summary>
        /// <param name="context">MethodContext</param>
        /// <param name="ex">Exception</param>
        internal override void OnException(MethodContext context, Exception ex)
        {
            context.Exception = ex;
            OnException(context);
        }

        /// <summary>
        /// Capture exception
        /// </summary>
        /// <param name="context">MethodContext</param>
        public abstract void OnException(MethodContext context);

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
