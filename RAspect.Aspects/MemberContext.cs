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
    public class MemberContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MemberContext"/> class.
        /// </summary>
        public MemberContext()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MemberContext"/> class.
        /// </summary>
        /// <param name="instance">Instance</param>
        /// <param name="locationName">Location Name</param>
        /// <param name="value">Value</param>
        public MemberContext(object instance, string locationName, object value)
        {
            Instance = instance;
            LocationName = locationName;
            Value = value;
        }

        /// <summary>
        /// Gets or sets index for member that are properties with indexer
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Gets or sets value of field/property
        /// </summary>
        public object Value { get; set; }

        /// <summary>
        /// Gets or sets instance of class for the field/property
        /// </summary>
        public object Instance { get; set; }

        /// <summary>
        /// Gets or sets member information of field/property
        /// </summary>
        public MemberInfo Location { get; set; }

        /// <summary>
        /// Gets or sets name of field/property
        /// </summary>
        public string LocationName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether flag to indicate continuation of code execution
        /// </summary>
        public bool Continue { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether member is a property or field
        /// </summary>
        public bool IsProperty { get; internal set; } = false;
    }
}
