using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace RAspect.Aspects
{
    /// <summary>
    /// Aspect for intercepting entry/exit/exception
    /// </summary>
    public abstract class OnMethodBoundaryAspect : AspectBase
    {
        /// <summary>
        /// Capture on enter method execution
        /// </summary>
        /// <param name="context">MethodContext</param>
        [EntryPointAttribute(EntryPointType.Enter)]
        protected virtual void OnEnter(MethodContext context) {}

        /// <summary>
        /// Capture on leave method
        /// </summary>
        /// <param name="context">MethodContext</param>
        [EntryPointAttribute(EntryPointType.Exit)]
        protected virtual void OnLeave(MethodContext context) { }

        /// <summary>
        /// Capture on completion of method execution
        /// </summary>
        /// <param name="context">MethodContext</param>
        [EntryPointAttribute(EntryPointType.Success)]
        protected virtual void OnComplete(MethodContext context) { }

        /// <summary>
        /// Capture on error of method execution
        /// </summary>
        /// <param name="context">MethodContext</param>
        [EntryPointAttribute(EntryPointType.Error)]
        protected virtual void OnError(MethodContext context) { }

        /// <summary>
        /// Capture on entry method
        /// </summary>
        /// <param name="context">MethodContext</param>
        internal override void OnEntry(MethodContext context)
        {
            OnEnter(context);
        }

        /// <summary>
        /// Capture on success method
        /// </summary>
        /// <param name="context">MethodContext</param>
        internal override void OnSuccess(MethodContext context)
        {
            OnComplete(context);
        }

        /// <summary>
        /// Capture on exception method
        /// </summary>
        /// <param name="context">MethodContext</param>
        internal override void OnException(MethodContext context, Exception exception)
        {
            context.Exception = exception;
            OnError(context);
        }

        /// <summary>
        /// Capture on exit method
        /// </summary>
        /// <param name="context">MethodContext</param>
        internal override void OnExit(MethodContext context)
        {
            OnLeave(context);
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
    }
}
