using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RAspect.Patterns.Logging
{
    /// <summary>
    /// Attribute when applied on method cause tracing before and after execution of this method
    /// </summary>
    public class LogAttribute : AspectBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MethodInterceptionAspect"/> class.
        /// </summary>
        /// <param name="logType">LogType</param>
        public LogAttribute(LoggingType logType = LoggingType.Debug) : base(WeaveTargetType.Methods | WeaveTargetType.Properties)
        {
            Type = logType;
        }

        /// <summary>
        /// Method that will be called prior to execute of weaved methods
        /// </summary>
        /// <param name="context">MethodContext</param>
        internal override void OnEntry(MethodContext context)
        {
            base.OnEntry(context);
        }

        /// <summary>
        /// Method that will be called prior to exiting of weaved methods
        /// </summary>
        /// <param name="context">MethodContext</param>
        internal override void OnExit(MethodContext context)
        {
            base.OnExit(context);
        }

        /// <summary>
        /// Method that will be called prior to success of weaved methods
        /// </summary>
        /// <param name="context">MethodContext</param>
        internal override void OnSuccess(MethodContext context)
        {
            base.OnSuccess(context);
        }

        /// <summary>
        /// Method that will be called upon exception
        /// </summary>
        /// <param name="context">MethodContext</param>
        /// <param name="ex">Exception that occurred while executing weaved method</param>
        internal override void OnException(MethodContext context, System.Exception ex)
        {
            base.OnException(context, ex);
        }

        /// <summary>
        /// Gets or sets logging type
        /// </summary>
        public LoggingType Type { get; set; }

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
