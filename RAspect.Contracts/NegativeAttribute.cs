using RAspect.Aspects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAspect.Contracts
{
    /// <summary>
    /// Attribute that throws <see cref="ArgumentOutOfRangeException"/> for target it is applied to when value is greater than or equal zero
    /// </summary>
    public sealed class NegativeAttribute : LessThanAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NegativeAttribute"/> class.
        /// </summary>
        public NegativeAttribute() : base(-1)
        {
        }
    }
}
