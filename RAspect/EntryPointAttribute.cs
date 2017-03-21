using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAspect
{
    /// <summary>
    /// Attribute to mark entry point category for a given weaving method
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = true)]
    internal class EntryPointAttribute : Attribute
    {
        /// <summary>
        /// Initializes static members of the <see cref="EntryPointAttribute"/> class.
        /// </summary>
        public EntryPointAttribute()
        {

        }

        /// <summary>
        /// Initializes static members of the <see cref="EntryPointAttribute"/> class.
        /// </summary>
        /// <param name="type"></param>
        public EntryPointAttribute(EntryPointType type)
        {
            Type = type;
        }

        public EntryPointType Type { get; set; }
    }
}
