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
    public sealed class LogAttribute : AspectBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LogAttribute"/> class.
        /// </summary>
        public LogAttribute()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LogAttribute"/> class.
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
            var attr = context.Attributes.Where(x => x is LogAttribute).FirstOrDefault() as LogAttribute;
            var logType = attr != null ? attr.Type : LoggingType.Debug;

            Backend.Log(context, logType, "Entering {0}({1})", null, context.Method.Name, string.Join(",", 
                context.Arguments.Select(x => string.Format("{0} = {1}", x.Name, x.Value))));
        }

        /// <summary>
        /// Method that will be called prior to exiting of weaved methods
        /// </summary>
        /// <param name="context">MethodContext</param>
        internal override void OnExit(MethodContext context)
        {
            var attr = context.Attributes.Where(x => x is LogAttribute).FirstOrDefault() as LogAttribute;
            var logType = attr != null ? attr.Type : LoggingType.Debug;

            Backend.Log(context, logType, "Exiting {0}({1})", null, context.Method.Name, string.Join(",",
                context.Arguments.Select(x => string.Format("{0} = {1}", x.Name, x.Value))));
        }

        /// <summary>
        /// Method that will be called prior to success of weaved methods
        /// </summary>
        /// <param name="context">MethodContext</param>
        internal override void OnSuccess(MethodContext context)
        {
            var attr = context.Attributes.Where(x => x is LogAttribute).FirstOrDefault() as LogAttribute;
            var logType = attr != null ? attr.Type : LoggingType.Debug;

            Backend.Log(context, logType, "Completed {0}({1}) -> {2}", null, context.Method.Name, string.Join(",",
                context.Arguments.Select(x => string.Format("{0} = {1}", x.Name, x.Value))), context.Returns);
        }

        /// <summary>
        /// Method that will be called upon exception
        /// </summary>
        /// <param name="context">MethodContext</param>
        /// <param name="ex">Exception that occurred while executing weaved method</param>
        internal override void OnException(MethodContext context, System.Exception ex)
        {
            var attr = context.Attributes.Where(x => x is LogAttribute).FirstOrDefault() as LogAttribute;
            var logType = attr != null ? attr.Type : LoggingType.Debug;

            Backend.Log(context, logType, "Error {0}({1})", ex, context.Method.Name, string.Join(",",
                context.Arguments.Select(x => string.Format("{0} = {1}", x.Name, x.Value))));
        }

        /// <summary>
        /// Gets or sets logging type
        /// </summary>
        public LoggingType Type { get; set; }

        /// <summary>
        /// Gets backend
        /// </summary>
        public LoggingBackend Backend
        {
            get
            {
                return LoggingManager.Backend;
            }
        }

        /// <summary>
        /// Gets weave block type
        /// </summary>
        internal override WeaveBlockType BlockType
        {
            get
            {
                return WeaveBlockType.Inline;
            }
        }
    }
}
