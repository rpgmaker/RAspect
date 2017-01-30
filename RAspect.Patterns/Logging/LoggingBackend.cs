using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAspect.Patterns.Logging
{
    /// <summary>
    /// Base class for all logging backend implementation
    /// </summary>
    public abstract class LoggingBackend
    {
        /// <summary>
        /// Gets or sets logging format
        /// </summary>
        public string Format { get; set; }

        /// <summary>
        /// Log message using the given parameters
        /// </summary>
        /// <param name="logType">Logging Type</param>
        /// <param name="message">Message</param>
        /// <param name="ex">Exception</param>
        /// <param name="args">Arguments</param>
        public abstract void Log(LoggingType logType, string message, System.Exception ex, params object[] args);
    }
}
