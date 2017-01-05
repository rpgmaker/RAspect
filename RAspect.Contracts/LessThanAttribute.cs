using RAspect.Aspects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAspect.Contracts
{
    /// <summary>
    /// Attribute that throws <see cref="ArgumentOutOfRangeException"/> for target it is applied to when value is greater than a given value
    /// </summary>
    public class LessThanAttribute : RangeAttribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LessThanAttribute"/> class.
        /// </summary>
        /// <param name="value">Value</param>
        public LessThanAttribute(double value) : base(value < 0 ? double.MinValue : 0, value)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LessThanAttribute"/> class.
        /// </summary>
        /// <param name="value">Value</param>
        public LessThanAttribute(long value) : base(value < 0 ? long.MinValue : 0, value)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LessThanAttribute"/> class.
        /// </summary>
        /// <param name="value">Value</param>
        public LessThanAttribute(ulong value) : base(value < 0 ? ulong.MinValue : 0, value)
        {
        }
    }
}
