using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAspect.Patterns.Logging.NLog
{
    /// <summary>
    /// NLog implementation for logging backend
    /// </summary>
    public class NLogBackend : LoggingBackend
    {
        /// <summary>
        /// Log message using the given parameters
        /// </summary>
        /// <param name="context">Context</param>
        /// <param name="logType">Logging Type</param>
        /// <param name="message">Message</param>
        /// <param name="ex">Exception</param>
        /// <param name="args">Arguments</param>
        public override void Log(MethodContext context, LoggingType logType, string message, System.Exception ex, params object[] args)
        {
            var log = LogManager.GetLogger(context.Method.DeclaringType.FullName);

            switch (logType)
            {
                case LoggingType.Debug:
                    log.Debug(ex, message, args);
                    break;
                case LoggingType.Information:
                    log.Info(ex, message, args);
                    break;
                case LoggingType.Warning:
                    log.Warn(ex, message, args);
                    break;
                case LoggingType.Error:
                    log.Error(ex, message, args);
                    break;
                case LoggingType.Trace:
                    log.Trace(ex, message, args);
                    break;
            }
        }
    }
}
