using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAspect
{
    /// <summary>
    /// Wrapper class for invoking internal functionalities of aspect implementation
    /// </summary>
    public class AspectWrapper
    {
        /// <summary>
        /// Aspect instance
        /// </summary>
        private AspectBase aspect;

        /// <summary>
        /// Initializes a new instance of the <see cref="AspectWrapper"/> class.
        /// </summary>
        /// <param name="aspect">Aspect Instance</param>
        public AspectWrapper(AspectBase aspect)
        {
            this.aspect = aspect;
        }

        /// <summary>
        /// Method that will be called prior to execute of weaved methods
        /// </summary>
        /// <param name="context">MethodContext</param>
        public void OnEntry(MethodContext context)
        {
            aspect.OnEntry(context);
        }

        /// <summary>
        /// Method that will be called prior to exiting of weaved methods
        /// </summary>
        /// <param name="context">MethodContext</param>
        public void OnExit(MethodContext context)
        {
            aspect.OnExit(context);
        }

        /// <summary>
        /// Method that will be called after success of weaved methods
        /// </summary>
        /// <param name="context">MethodContext</param>
        public void OnSuccess(MethodContext context)
        {
            aspect.OnSuccess(context);
        }

        /// <summary>
        /// Method that will be called upon exception
        /// </summary>
        /// <param name="context">MethodContext</param>
        /// <param name="ex">Exception that occurred while executing weaved method</param>
        public void OnException(MethodContext context, Exception ex)
        {
            aspect.OnException(context, ex);
        }
    }
}
