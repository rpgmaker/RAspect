using log4net;
using log4net.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAspect.Patterns.Logging.Log4Net
{
    /// <summary>
    /// Log4Net implementation for logging backend
    /// </summary>
    public class Log4NetBackend : LoggingBackend
    {
        /// <summary>
        /// Static constructor for <see cref="Log4NetBackend"/>
        /// </summary>
        static Log4NetBackend()
        {
            XmlConfigurator.Configure();
        }

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
            var log = LogManager.GetLogger(context.Method.DeclaringType);
            var formatMessage = string.Format(message, args);

            switch (logType)
            {
                case LoggingType.Debug:
                    log.Debug(formatMessage, ex);
                    break;
                case LoggingType.Information:
                    log.Info(formatMessage, ex); ;
                    break;
                case LoggingType.Warning:
                    log.Warn(formatMessage, ex); ;
                    break;
                case LoggingType.Error:
                    log.Error(formatMessage, ex); ;
                    break;
            }
        }
    }
}
