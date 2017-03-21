using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace RAspect.Aspects
{
    /// <summary>
    /// Member Context of fields/properties
    /// </summary>
    public class EventContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EventContext"/> class.
        /// </summary>
        public EventContext()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MemberContext"/> class.
        /// </summary>
        /// <param name="instance">Instance</param>
        /// <param name="handler">Handler</param>
        /// <param name="proceed">Proceed Flag</param>
        /// <param name="arguments">Arguments</param>
        public EventContext(object instance, object handler, bool @proceed, object[] arguments)
        {
            Instance = instance;
            Handler = handler;
            Proceed = @proceed;
            Arguments = arguments;
        }
        
        /// <summary>
        /// Gets or sets handler for event
        /// </summary>
        public object Handler { get; set; }

        /// <summary>
        /// Gets or sets instance of event
        /// </summary>
        public object Instance { get; set; }

        /// <summary>
        /// Gets or sets event information of event
        /// </summary>
        public EventInfo Event { get; set; }

        /// <summary>
        /// Gets or sets value of event
        /// </summary>
        public object Value { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether flag to indicate continuation of code execution
        /// </summary>
        public bool Proceed { get; set; } = true;

        /// <summary>
        /// Gets or sets argument for event handler when invoked
        /// </summary>
        public object[] Arguments { get; set; }
    }
}
