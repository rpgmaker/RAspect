using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAspect.Patterns.Logging
{
    /// <summary>
    /// Trace implementation of logging backend
    /// </summary>
    public class TraceLoggingBackend : LoggingBackend
    {
        /// <summary>
        /// Log message using the given parameters
        /// </summary>
        /// <param name="logType">Logging Type</param>
        /// <param name="message">Message</param>
        /// <param name="ex">Exception</param>
        /// <param name="args">Arguments</param>
        public override void Log(LoggingType logType, string message, System.Exception ex, params object[] args)
        {
            if (ex == null)
            {
                Trace.WriteLine(string.Format(
                    "{0}:{1}", logType, string.IsNullOrWhiteSpace(message) ? string.Empty : string.Format(message, args)));
            }
            else
            {
                Trace.WriteLine(
                    string.Format("{0}:{1}:{2}", logType, string.IsNullOrWhiteSpace(message) ? string.Empty : string.Format(message, args),
                    ex));
            }
        }
    }
}
