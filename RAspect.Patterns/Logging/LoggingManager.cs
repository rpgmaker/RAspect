using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAspect.Patterns.Logging
{
    /// <summary>
    /// Manager class for configuration of logging backend
    /// </summary>
    public class LoggingManager
    {
        /// <summary>
        /// Current logging back end
        /// </summary>
        private static LoggingBackend backend;

        /// <summary>
        /// Default backend
        /// </summary>
        private static LoggingBackend Default = new ConsoleLoggingBackend();
        
        /// <summary>
        /// Configure backend to use for logging
        /// </summary>
        /// <param name="backend">Backend</param>
        public static void Configure(LoggingBackend backend)
        {
            LoggingManager.backend = backend;
        }
        
        /// <summary>
        /// Gets current logging backedn
        /// </summary>
        public static LoggingBackend Backend
        {
            get
            {
                return backend ?? Default;
            }
        }
    }
}
